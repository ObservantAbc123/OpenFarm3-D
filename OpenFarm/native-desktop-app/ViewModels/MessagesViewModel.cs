using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DatabaseAccess;
using DatabaseAccess.Models;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;
using DbMessage = DatabaseAccess.Models.Message;

namespace native_desktop_app.ViewModels;

/// <summary>
///     Lightweight row model for the inbox grid.
///     Each item represents one sender and shows only their most recent email.
/// </summary>
public class ConversationSummary
{
    /// <summary>
    ///     Patron name to show in the grid
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    ///     Patron's email address. This is also the key we group on.
    /// </summary>
    public required string EmailAddress { get; init; }

    /// <summary>
    ///     The PR / print-job label the email is about (e.g. "PR-42").
    ///     If we can't resolve it from the email's JobId, we show "—".
    /// </summary>
    public string ActivePrLabel { get; init; } = "—";

    /// <summary>
    ///     UTC timestamp of the newest email for this sender.
    /// </summary>
    public DateTime MostRecentEmailUtc { get; init; }

    /// <summary>
    ///     If we were able to resolve the sender to a known user (via job → user),
    ///     this will carry their user id. Otherwise null.
    /// </summary>
    public long? UserId { get; init; }
    /// <summary>Thread identifier for this conversation.</summary>
    public long ThreadId { get; init; }

    /// <summary>
    ///     Operator readable "how long ago" text based on UTC time.
    ///     We keep it simple: show minutes; if it's been more than an hour, show "X hr Y min".
    ///     This is what the timer in the VM forces to re-evaluate.
    /// </summary>
    public string UnresolvedForUtc
    {
        get
        {
            var ts = DateTime.UtcNow - MostRecentEmailUtc;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours} hr {ts.Minutes} min";
            return $"{(int)Math.Round(ts.TotalMinutes)} min";
        }
    }
    
    /// <summary>
    ///     Number of active jobs for this user.
    /// </summary>
    public int ActiveJobs { get; init; }
}

/// <summary>
///     Detailed message model for the "Open Log" dialog.
///     This is per-email (not per-sender).
/// </summary>
public class ConversationEntry
{
    /// <summary>Primary key in email_communications.</summary>
    public required long EmailId { get; init; }

    /// <summary>Subject line stored on the email record.</summary>
    public required string Subject { get; init; }

    /// <summary>Plain-text body of the email.</summary>
    public required string Content { get; init; }

    /// <summary>
    ///     When the email was received, stored in UTC.
    /// </summary>
    public required DateTime ReceivedAtUtc { get; init; }

    /// <summary>The from-address of the message.</summary>
    public required string FromEmailAddress { get; init; }
}

/// <summary>
///     ViewModel that backs the Messages view.
///     Responsibilities:
///     <list type="bullet">
///         <item>Load unseen + seen emails from the DB.</item>
///         <item>Group them by sender so we only show one row per person.</item>
///         <item>Try to resolve that email to a job → user so we can show a nicer name and PR label.</item>
///         <item>Open a dialog to show the full conversation for that sender.</item>
///         <item>Allow marking the latest message as “closed”.</item>
///         <item>Allow replying right from the dialog.</item>
///         <item>Periodically force UI to refresh the “time since” text.</item>
///     </list>
/// </summary>
public class MessagesViewModel : ViewModelBase
{
    // Data access gateway for repositories used by this view model.
    private readonly DatabaseAccessHelper _db;
    // RabbitMQ helper for publishing operator replies
    private readonly IRmqHelper _rmq;
    // Timer to force bindings to re-evaluate computed durations without reloading data.
    private readonly Timer _tick;

    /// <summary>
    ///     Backing store for the inbox rows.
    /// </summary>
    private ObservableCollection<ConversationSummary> _rows = new();

    /// <summary>
    ///     Creates a new MessagesViewModel.
    ///     Expects the app-level DatabaseAccessHelper (so we don't touch the EF context directly)
    ///     and the RabbitMQ helper in case we later want to publish outgoing mail.
    /// </summary>
    public MessagesViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper)
        : base(databaseAccessHelper, rmqHelper)
    {
        _db = databaseAccessHelper;
        _rmq = rmqHelper;

        // Wire commands to async handlers.
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ShowConversationCommand = new AsyncRelayCommand<ConversationSummary?>(ShowConversationAsync);
        MarkClosedCommand = new AsyncRelayCommand<ConversationSummary?>(MarkConversationResolvedAsync);

        // Initialize RabbitMQ connection
        Task.Run(async () => await _rmq.Connect());

        // Timer: fires every 10s on a background thread.
        // We marshal back to the Avalonia UI thread to raise property-changed.
        _tick = new Timer(10_000);
        _tick.AutoReset = true;
        _tick.Elapsed += (_, __) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                // This makes Rows' items re-run their time string.
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(CountText));
            });
        };
        _tick.Start();

        // First load is fire-and-forget so ctor stays non-async.
        _ = LoadAsync();
    }

    /// <summary>
    ///     Collection of conversations shown in the UI.
    ///     Replaced wholesale after each refresh.
    /// </summary>
    public ObservableCollection<ConversationSummary> Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    /// <summary>
    ///     Little status text for the top bar.
    /// </summary>
    public string CountText => $"Showing {Rows.Count} conversations";

    /// <summary>
    ///     Manual refresh button in the UI.
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    ///     Opens the "log" / conversation dialog for a selected row.
    /// </summary>
    public ICommand ShowConversationCommand { get; }

    /// <summary>
    ///     Marks the latest email for that sender as "closed".
    /// </summary>
    public ICommand MarkClosedCommand { get; }

    /// <summary>
    /// Loads unresolved conversations by taking the most recent message for each sender
    /// from both <c>unseen</c> and <c>seen</c> statuses, then sorts by most recent first.
    /// Updates <see cref="Rows"/> and notifies dependent bindings.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            Console.WriteLine("LoadAsync started.");
            // Get active threads with unresolved messages (unseen or seen)
            var activeThreads = await _db.Thread.GetActiveThreadsAsync();
            Console.WriteLine($"Found {activeThreads.Count} active threads.");

            var threadsWithUnresolvedMessages = new List<(Thread thread, DbMessage latestMessage)>();

            foreach (var thread in activeThreads)
            {
                var threadMessages = await _db.Message.GetMessagesByThreadAsync(thread.Id);
                Console.WriteLine($"Thread {thread.Id} has {threadMessages.Count} messages.");

                var latestUnresolvedMessage = threadMessages
                    .Where(m => m.MessageStatus == "unseen" || m.MessageStatus == "seen")
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault();

                if (latestUnresolvedMessage != null)
                {
                    Console.WriteLine($"Thread {thread.Id} has unresolved message {latestUnresolvedMessage.Id} status {latestUnresolvedMessage.MessageStatus}");
                    threadsWithUnresolvedMessages.Add((thread, latestUnresolvedMessage));
                }
                else
                {
                    Console.WriteLine($"Thread {thread.Id} has NO unresolved messages.");
                }
            }

            var rows = new List<ConversationSummary>();

            foreach (var (thread, message) in threadsWithUnresolvedMessages.OrderByDescending(x => x.latestMessage.CreatedAt))
            {
                var user = await _db.Users.GetUserAsync(thread.UserId);
                var primaryEmail = await _db.Emails.GetUserPrimaryEmailAddressAsync(thread.UserId);

                rows.Add(new ConversationSummary
                {
                    DisplayName = user?.Name ?? primaryEmail ?? "Unknown User",
                    EmailAddress = primaryEmail ?? "",
                    ActiveJobs = thread.JobId.HasValue ? 1 : 0,
                    MostRecentEmailUtc = message.CreatedAt,
                    UserId = thread.UserId,
                    ThreadId = thread.Id,
                    ActivePrLabel = thread.JobId.HasValue ? $"PR-{thread.JobId}" : "—"
                });
            }

            Console.WriteLine($"Added {rows.Count} rows to view.");
            Rows = new ObservableCollection<ConversationSummary>(rows);
            OnPropertyChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LoadAsync: {ex}");
        }
    }

    /// <summary>
    ///     Opens a dialog window that shows the **full** email history
    ///     for the selected sender. Also wires the reply + mark buttons
    ///     that live inside that dialog.
    /// </summary>
    private async Task ShowConversationAsync(ConversationSummary? summary)
    {
        if (summary is null) return;

        var messages = await _db.Message.GetMessagesByThreadAsync(summary.ThreadId);
        var items = messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ConversationEntry
            {
                EmailId = m.Id,
                Subject = m.MessageSubject ?? "",
                Content = m.MessageContent,
                FromEmailAddress = m.FromEmailAddress ?? "System",
                ReceivedAtUtc = m.CreatedAt
            })
            .ToList();

        // Check for AI draft
        var aiDraft = await _db.AiResponses.GetPendingResponseForThreadAsync(summary.ThreadId);

        var dialog = BuildConversationDialog(summary, items, aiDraft?.GeneratedContent);
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        if (lifetime.MainWindow != null)
        {
            await dialog.ShowDialog(lifetime.MainWindow);
        }
    }

    /// <summary>
    ///     Marks the current conversation (all non-closed emails from this sender)
    ///     as resolved. After this, the row should no longer look "unresolved".
    /// </summary>
    private async Task MarkConversationResolvedAsync(ConversationSummary? summary)
    {
        if (summary is null)
            return;

        var messages = await _db.Message.GetMessagesByThreadAsync(summary.ThreadId);
        var latest = messages
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .FirstOrDefault();

        if (latest == null) return;

        await _db.Message.MarkMessageProcessedAsync(latest.Id);
        await _db.Thread.CloseThreadAsync(summary.ThreadId);
        await LoadAsync();
    }

    /// <summary>
    ///     Saves a reply as a new email_communications row so it shows up in the thread.
    ///     Right now this is the simplest possible implementation: we don't actually send mail,
    ///     we just persist it to the DB with the same "from" address.
    /// </summary>
    /// <param name="summary">The conversation summary to display in the header.</param>
    /// <param name="messages">The ordered message entries to render in the body.</param>
    /// <param name="aiDraftContent">Optional AI draft content.</param>
    /// <returns>A configured <see cref="Window"/> ready to be shown.</returns>
    private Window BuildConversationDialog(ConversationSummary summary, IEnumerable<ConversationEntry> messages, string? aiDraftContent)
    {
        var window = new Window
        {
            Title = $"Conversation — {summary.DisplayName}",
            Width = 700,
            Height = 700,
            CornerRadius = new CornerRadius(10),
            Background = Brush.Parse("#282828"),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };

        // --- header (name, email, PR) ---
        var header = new Border
        {
            Background = Brush.Parse("#3C3836"),
            Padding = new Thickness(12),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = summary.DisplayName,
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        FontSize = 18
                    },
                    new TextBlock
                    {
                        Text = $"<{summary.EmailAddress}>",
                        Foreground = Brush.Parse("#EBDBB2")
                    },
                    new TextBlock
                    {
                        Text = $"• {summary.ActivePrLabel}",
                        Foreground = Brush.Parse("#EBDBB2")
                    }
                }
            }
        };

        // --- messages list ---
        var itemsControl = new ItemsControl
        {
            ItemsSource = messages.Select(m =>
                new Border
                {
                    Background = Brush.Parse("#EBDBB2"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 6, 0, 6),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock 
                            { 
                                Text = m.Subject, 
                                FontWeight = FontWeight.Bold,
                                Foreground = Brush.Parse("#282828") 
                            },
                            new TextBlock
                            {
                                Text = m.Content,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 4, 0, 0),
                                Foreground = Brush.Parse("#282828")
                            },
                            new TextBlock
                            {
                                Text = $"{m.ReceivedAtUtc:g}",
                                Opacity = 0.7,
                                FontSize = 12,
                                FontStyle = FontStyle.Italic,
                                Foreground = Brush.Parse("#282828")
                            }
                        }
                    }
                }
            ).ToList()
        };

        var replyBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 100,
            Margin = new Thickness(0, 10, 0, 10),
            Watermark = "Type your reply here..."
        };

        var aiButton = new Button
        {
            Content = "Use AI Draft",
            IsVisible = !string.IsNullOrEmpty(aiDraftContent),
            Margin = new Thickness(0, 0, 10, 0),
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#83a598")),
            Foreground = Brushes.Black
        };

        aiButton.Click += (_, __) =>
        {
            replyBox.Text = aiDraftContent;
        };

        var sendButton = new Button
        {
            Content = "Send Reply",
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#b8bb26")),
            Foreground = Brushes.Black
        };

        sendButton.Click += async (_, __) =>
        {
            if (string.IsNullOrWhiteSpace(replyBox.Text)) return;

            var (result, createdMessage) = await _db.Message.CreateSystemMessageAsync(summary.ThreadId, replyBox.Text, "Reply");

            if (result != TransactionResult.Succeeded || createdMessage == null) return;

            // Publish to RabbitMQ for email sending
            try
            {
                var thread = await _db.Thread.GetThreadByIdAsync(summary.ThreadId);
                if (thread != null)
                {
                    var operatorReply = new OperatorReplyMessage
                    {
                        JobId = thread.JobId ?? 0,
                        CustomerEmail = summary.EmailAddress,
                        Subject = $"Re: Your OpenFarm Inquiry",
                        Body = replyBox.Text,
                        ThreadId = summary.ThreadId,
                        MessageId = createdMessage.Id
                    };
                    await _rmq.QueueMessage(ExchangeNames.OperatorReply, operatorReply);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to publish operator reply to RabbitMQ: {ex.Message}");
            }

            window.Close();
            await LoadAsync();
        };

        var closeBtn = new Button
        {
            Content = "Close",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Width = 100,
            Background = new SolidColorBrush(Avalonia.Media.Color.Parse("#fabd2f")),
            Foreground = Brushes.Black
        };
        closeBtn.Click += (_, __) => window.Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { aiButton, sendButton, closeBtn }
        };

        var footer = new StackPanel
        {
            Margin = new Thickness(12),
            Children = { replyBox, buttonPanel }
        };

        var root = new DockPanel();

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = itemsControl });

        window.Content = root;
        return window;
    }
}
