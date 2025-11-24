using System.Reflection;
using DatabaseAccess;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQHelper;
using RabbitMQHelper.MessageTypes;

namespace DiscordBot.Services;

public class DiscordBotService(
    ILogger<DiscordBotService> logger,
    DatabaseAccessHelper databaseAccess,
    DiscordSocketClient discordClient,
    CommandService commandService,
    IRmqHelper rabbitMqHelper,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    private const string NotificationRoleName = "BotNotifications";
    private readonly Dictionary<string, ulong> _channels = new();
    private readonly DatabaseAccessHelper _databaseAccess = databaseAccess;
    private readonly string? _discordChannel = Environment.GetEnvironmentVariable("DISCORD_CHANNEL");
    private readonly IRmqHelper _rabbitMQHelper = rabbitMqHelper;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private IRole? _notificationRole;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DiscordService started.");

        if (_discordChannel == null)
            throw new ArgumentException("DISCORD_CHANNEL environment variable is not set.");

        discordClient.Log += ContainerLogger;
        commandService.Log += ContainerLogger;
        discordClient.MessageReceived += HandleCommandAsync;

        discordClient.Ready += async () =>
        {
            foreach (var guild in discordClient.Guilds)
            {
                foreach (var channel in guild.Channels) _channels[channel.Name] = channel.Id;

                // Ensure notification role exists
                _notificationRole = guild.Roles.FirstOrDefault(r => r.Name == NotificationRoleName);
                if (_notificationRole == null)
                {
                    _notificationRole = await guild.CreateRoleAsync(NotificationRoleName,
                        GuildPermissions.None,
                        Color.Blue,
                        isMentionable: true,
                        isHoisted: false);
                    logger.LogInformation("Created {RoleName} role in guild {GuildName}", NotificationRoleName,
                        guild.Name);
                }
            }
        };


        await _rabbitMQHelper.Connect();
        _rabbitMQHelper.AddListener(QueueNames.DiscordPrintFinished, (RabbitMQHelper.MessageTypes.Message message) => NotifyPrintCompleted(message).GetAwaiter().GetResult());
        _rabbitMQHelper.AddListener(QueueNames.DiscordJobValidated, (RabbitMQHelper.MessageTypes.Message message) => NotifyJobVerified(message).GetAwaiter().GetResult());

        var modules = await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
        logger.LogInformation("Loaded {ModuleCount} modules", modules.Count());
        foreach (var module in modules)
            logger.LogInformation("Module: {ModuleName}, Commands: {Commands}",
                module.Name, string.Join(", ", module.Commands.Select(c => c.Name)));

        await discordClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
        await discordClient.StartAsync();
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleCommandAsync(SocketMessage messageParam)
    {
        if (messageParam is not SocketUserMessage message) return;
        if (message.Author.IsBot) return;

        var pos = 0;
        var hasPrefix = message.HasCharPrefix('!', ref pos);
        var hasMention = message.HasMentionPrefix(discordClient.CurrentUser, ref pos);

        if (!hasPrefix && !hasMention) return;

        logger.LogInformation(
            "Received command from {AuthorUsername}: {MessageContent} (Position: {Position}, HasPrefix: {HasPrefix}, HasMention: {HasMention})",
            message.Author.Username, message.Content, pos, hasPrefix, hasMention);

        var context = new SocketCommandContext(discordClient, message);
        var result = await commandService.ExecuteAsync(context, pos, _serviceProvider);

        if (!result.IsSuccess)
            logger.LogError("Command execution failed: {Error} for command: {Command}", result.ErrorReason,
                message.Content);
    }

    private Task ContainerLogger(LogMessage message)
    {
        logger.LogInformation(message.ToString());
        return Task.CompletedTask;
    }

    private async Task<bool> NotifyPrintCompleted(RabbitMQHelper.MessageTypes.Message message)
    {
        logger.LogInformation("Print completed on printer {JobId} completed - sending Discord notification",
            message.JobId);

        if (!_channels.TryGetValue(_discordChannel!, out var channelId))
        {
            logger.LogWarning("Discord channel not found");
            return false;
        }

        var channel = discordClient.GetChannel(channelId) as IMessageChannel;
        if (channel == null)
        {
            logger.LogWarning("Could not get Discord channel with ID {ChannelId}", channelId);
            return false;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Print Completed")
            .WithDescription($"Print on printer #{message.JobId} has been successfully completed.")
            .WithColor(Color.Green)
            .AddField("Status", "Awaiting Print to be Cleared", true)
            .Build();

        var guild = channel is SocketGuildChannel guildChannel ? guildChannel.Guild : null;
        var role = guild?.Roles.FirstOrDefault(r => r.Name == NotificationRoleName);
        var mention = role?.Mention;

        await channel.SendMessageAsync(mention, embed: embed);
        return true;
    }

    private async Task<bool> NotifyJobVerified(RabbitMQHelper.MessageTypes.Message message)
    {
        logger.LogInformation("Print job {JobId} verified - sending Discord notification", message.JobId);

        if (!_channels.TryGetValue(_discordChannel!, out var channelId))
        {
            logger.LogWarning("Discord channel not found");
            return false;
        }

        var channel = discordClient.GetChannel(channelId) as IMessageChannel;
        if (channel == null)
        {
            logger.LogWarning("Could not get Discord channel with ID {ChannelId}", channelId);
            return false;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Print Job Verified")
            .WithDescription($"Print job #{message.JobId} has been verified and received by the system.")
            .WithColor(Color.Blue)
            .AddField("Status", "Awaiting Operator Approval", true)
            .Build();

        var guild = channel is SocketGuildChannel guildChannel ? guildChannel.Guild : null;
        var role = guild?.Roles.FirstOrDefault(r => r.Name == NotificationRoleName);
        var mention = role?.Mention;

        await channel.SendMessageAsync(mention, embed: embed);
        return true;
    }

    public class PrintJobModule : ModuleBase<SocketCommandContext>
    {
        private readonly DatabaseAccessHelper _databaseAccess;

        public PrintJobModule(DatabaseAccessHelper databaseAccess)
        {
            _databaseAccess = databaseAccess;
        }

        [Command("jobs")]
        [Summary("View print jobs by status")]
        public async Task ViewJobsCommand([Remainder] string status = "received")
        {
            try
            {
                var jobs = status.ToLower() switch
                {
                    "received" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("received"),
                    "systemapproved" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("systemApproved"),
                    "operatorapproved" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("operatorApproved"),
                    "printing" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("printing"),
                    "completed" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("completed"),
                    "cancelled" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("cancelled"),
                    "rejected" => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync("rejected"),
                    _ => await _databaseAccess.PrintJobs.GetPrintJobsByStatusAsync(status)
                };

                if (jobs.Count == 0)
                {
                    await ReplyAsync($"No print jobs found with status: {status}");
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle($"Print Jobs - {char.ToUpper(status[0]) + status.Substring(1)}")
                    .WithColor(GetStatusColor(status))
                    .WithFooter($"Total: {jobs.Count} job(s)");

                foreach (var job in jobs)
                    embed.AddField(
                        $"Job #{job.Id}",
                        $"User: {job.UserId ?? 0} | Created: {job.CreatedAt:yyyy-MM-dd HH:mm}");

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"Error retrieving print jobs: {ex.Message}");
            }
        }

        [Command("help")]
        [Summary("View available commands and their usage")]
        public async Task HelpCommand()
        {
            var embed = new EmbedBuilder()
                .WithTitle("OpenFarm Discord Bot Commands")
                .WithColor(Color.Blue)
                .WithDescription("Available commands for managing print jobs")
                .AddField("!jobs [status]", "View print jobs filtered by status:\n" +
                                            "• received - Jobs received by the system\n" +
                                            "• systemApproved - System verified jobs\n" +
                                            "• operatorApproved - Operator approved jobs\n" +
                                            "• printing - Currently printing\n" +
                                            "• completed - Successfully completed\n" +
                                            "• cancelled - Cancelled jobs\n" +
                                            "• rejected - Rejected jobs")
                .AddField("!unpaid", "View operator-approved jobs that haven't been paid for yet")
                .AddField("!subscribe", "Subscribe to receive ping notifications for print jobs")
                .AddField("!unsubscribe", "Unsubscribe from ping notifications")
                .AddField("!help", "Display this help message")
                .Build();

            await ReplyAsync(embed: embed);
        }

        [Command("unpaid")]
        [Summary("View operator-approved jobs that are unpaid")]
        public async Task ViewUnpaidJobsCommand()
        {
            try
            {
                var unpaidJobs = await _databaseAccess.PrintJobs.GetUnpaidPrintJobsAsync();

                var operatorApprovedUnpaid = unpaidJobs
                    .Where(job => job.JobStatus == "operatorApproved")
                    .OrderByDescending(job => job.CreatedAt)
                    .ToList();

                if (operatorApprovedUnpaid.Count == 0)
                {
                    var emptyEmbed = new EmbedBuilder()
                        .WithTitle("Unpaid Operator-Approved Jobs")
                        .WithDescription("No operator-approved jobs pending payment")
                        .WithColor(Color.Green)
                        .Build();

                    await ReplyAsync(embed: emptyEmbed);
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("Unpaid Operator-Approved Jobs")
                    .WithColor(Color.Orange)
                    .WithFooter($"Total unpaid jobs: {operatorApprovedUnpaid.Count}");

                foreach (var job in operatorApprovedUnpaid)
                {
                    var cost = job.PrintCost?.ToString("F2") ?? "TBD";
                    embed.AddField(
                        $"Job #{job.Id}",
                        $"User ID: {job.UserId ?? 0}\nCost: ${cost}\nCreated: {job.CreatedAt:yyyy-MM-dd HH:mm}");
                }

                await ReplyAsync(embed: embed.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync($"Error retrieving unpaid jobs: {ex.Message}");
            }
        }

        [Command("subscribe")]
        [Summary("Subscribe to print job notifications")]
        public async Task SubscribeCommand()
        {
            var user = Context.User as SocketGuildUser;
            if (user == null)
            {
                await ReplyAsync("This command must be used in a server.");
                return;
            }

            try
            {
                var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "BotNotifications");
                if (role == null)
                {
                    await ReplyAsync("Notification role not found. Please contact an administrator.");
                    return;
                }

                await user.ModifyAsync(props => { });

                if (user.Roles.Contains(role))
                {
                    await ReplyAsync("You are already subscribed to print job notifications.");
                }
                else
                {
                    await user.AddRoleAsync(role);
                    await ReplyAsync("You have been subscribed to print job notifications.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"Error subscribing: {ex.Message}");
            }
        }

        [Command("unsubscribe")]
        [Summary("Unsubscribe from print job notifications")]
        public async Task UnsubscribeCommand()
        {
            var user = Context.User as SocketGuildUser;
            if (user == null)
            {
                await ReplyAsync("This command must be used in a server.");
                return;
            }

            try
            {
                var role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "BotNotifications");
                if (role == null)
                {
                    await ReplyAsync("Notification role not found. Please contact an administrator.");
                    return;
                }

                await user.ModifyAsync(props => { });

                if (user.Roles.Contains(role))
                {
                    await user.RemoveRoleAsync(role);
                    await ReplyAsync("You have been unsubscribed from print job notifications.");
                }
                else
                {
                    await ReplyAsync("You are not currently subscribed to print job notifications.");
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync($"Error unsubscribing: {ex.Message}");
            }
        }


        private Color GetStatusColor(string status)
        {
            return status.ToLower() switch
            {
                "received" => Color.Blue,
                "systemapproved" => Color.Teal,
                "operatorapproved" => Color.LightOrange,
                "printing" => Color.Gold,
                "completed" => Color.Green,
                "cancelled" => Color.DarkGrey,
                "rejected" => Color.Red,
                _ => Color.Default
            };
        }
    }
}
