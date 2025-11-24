// Purpose
//   ViewModel for the staff review queue. Loads jobs awaiting operator review
//   (status == "systemApproved"), allows approving/rejecting, previews a
//   thumbnail for the selected job, and supports downloading G-code bytes via
//   a local API.
//
// Notes
//   • ThumbnailImage is disposed/replaced safely on selection changes.
//   • G-code is written to the user's Desktop as "PrintJob_{Id}.gcode".
// -----------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using DatabaseAccess;
using DatabaseAccess.Models;
using RabbitMQHelper;

// for Win32Exception

namespace native_desktop_app.ViewModels;

/// <summary>
///     ViewModel backing the Staff Review page: queues jobs needing operator decisions,
///     supports approve/reject actions, selection with thumbnail preview, and a command
///     to download the job's G-code from a backing API.
/// </summary>
public class StaffReviewViewModel : ViewModelBase
{
    // HTTP client for fetching G-code bytes from a local API endpoint.
    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("http://localhost:5001/") };

    // Backing field for the selected job.
    private PrintJob? _selectedJob;

    // Backing store for the currently displayed thumbnail image.
    private Bitmap? _thumbnailImage;

    // Store all 8 images for the current job (as base64 strings to avoid memory issues)
    private string?[] _thumbnailImages = new string?[8];

    // Current image index (0-7)
    private int _currentImageIndex = 0;

    // View names for the 8 images
    private static readonly string[] ViewNames = { "NORTH_WEST", "WEST", "SOUTH_WEST", "SOUTH", "SOUTH_EAST", "EAST", "NORTH_EAST", "NORTH" };

    /// <summary>
    ///     Initializes a new instance of <see cref="StaffReviewViewModel" />, validating the
    ///     database connection, wiring commands, and triggering the initial load.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the <c>DATABASE_CONNECTION_STRING</c> environment variable is not set.
    /// </exception>
    public StaffReviewViewModel(DatabaseAccessHelper databaseAccessHelper, IRmqHelper rmqHelper) : base(
        databaseAccessHelper, rmqHelper)
    {
        _ = LoadJobsAsync();

        // Wire commands to async handlers.
        MarkOperatorApprovedCommand = new RelayCommand<PrintJob>(async job => await MarkApprovedAsync(job));
        MarkOperatorNotApprovedCommand = new RelayCommand<PrintJob>(async job => await MarkNotApprovedAsync(job));
        SelectJobCommand = new RelayCommand<PrintJob>(async job => await SelectJobAsync(job));
        PreviousImageCommand = new RelayCommand(() => NavigateToPreviousImage(), () => CanNavigateToPrevious);
        NextImageCommand = new RelayCommand(() => NavigateToNextImage(), () => CanNavigateToNext);
    }

    /// <summary>
    ///     Jobs requiring staff review (i.e., those with status <c>systemApproved</c>).
    /// </summary>
    public ObservableCollection<PrintJob> NeedToBeReviewedJobs { get; set; } = new();

    /// <summary>
    ///     Command that marks a job as operator-approved (status becomes <c>operatorApproved</c>).
    /// </summary>
    public ICommand MarkOperatorApprovedCommand { get; }

    /// <summary>
    ///     Command that marks a job as not approved (status becomes <c>rejected</c>).
    /// </summary>
    public ICommand MarkOperatorNotApprovedCommand { get; }

    /// <summary>
    ///     Command invoked when the user selects a job; updates <see cref="SelectedJob" />
    ///     and loads a thumbnail preview if available.
    /// </summary>
    public ICommand SelectJobCommand { get; }

    /// <summary>
    ///     Command to navigate to the previous image.
    /// </summary>
    public RelayCommand PreviousImageCommand { get; }

    /// <summary>
    ///     Command to navigate to the next image.
    /// </summary>
    public RelayCommand NextImageCommand { get; }

    /// <summary>
    ///     Currently selected job in the review list; triggers UI binding updates.
    /// </summary>
    public PrintJob? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value)) _ = LoadSelectedThumbnailAsync(value);
        }
    }

    /// <summary>
    ///     Thumbnail image for the selected job (if available). Set privately within the VM
    ///     to ensure disposal/replacement is coordinated.
    /// </summary>
    public Bitmap? ThumbnailImage
    {
        get => _thumbnailImage;
        private set => SetProperty(ref _thumbnailImage, value);
    }

    /// <summary>
    ///     Current image index (1-based for display, 0-7 internally).
    /// </summary>
    public int CurrentImageIndex => _currentImageIndex + 1;

    /// <summary>
    ///     Total number of available images.
    /// </summary>
    public int TotalImageCount => _thumbnailImages.Count(img => img != null);

    /// <summary>
    ///     Display text showing current image position (e.g., "1/8").
    /// </summary>
    public string ImageCounterText
    {
        get
        {
            var total = TotalImageCount;
            if (total == 0) return "No images";
            return $"{CurrentImageIndex}/{total}";
        }
    }

    /// <summary>
    ///     Whether navigation to the previous image is possible.
    ///     Returns true if there are any images available (wrapping is enabled).
    /// </summary>
    public bool CanNavigateToPrevious => TotalImageCount > 0;

    /// <summary>
    ///     Whether navigation to the next image is possible.
    ///     Returns true if there are any images available (wrapping is enabled).
    /// </summary>
    public bool CanNavigateToNext => TotalImageCount > 0;

    /// <summary>
    ///     Command that downloads G-code for the <see cref="SelectedJob" /> (if any) from the
    ///     configured API and will download wherever your gcode is set up to open at.
    ///     NOTE: We may need to add to the README or setup that they need to set their default
    ///     gcode slicer to automatically open.
    /// </summary>
    public IAsyncRelayCommand DownloadGcodeCommand => new AsyncRelayCommand(async () =>
    {
        if (SelectedJob == null)
            return;

        var bytes = await DownloadGcodeFromApiAsync(SelectedJob.Id);
        if (bytes == null) return;

        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"PrintJob_{SelectedJob.Id}.gcode");

        await File.WriteAllBytesAsync(filePath, bytes);

        OpenWithDefaultApp(filePath);
    });


    /// <summary>
    ///     Fetches all jobs and populates <see cref="NeedToBeReviewedJobs" /> with those
    ///     that are in the <c>systemApproved</c> state (awaiting operator review). Also
    ///     hydrates <see cref="PrintJob.User" /> for display.
    /// </summary>
    private async Task LoadJobsAsync()
    {
        var jobs = await _databaseAccessHelper.PrintJobs.GetPrintJobsAsync();
        NeedToBeReviewedJobs.Clear();

        foreach (var job in jobs)
            if (job.JobStatus.Equals("systemApproved"))
            {
                job.User = await _databaseAccessHelper.Users.GetUserAsync(job.UserId!.Value);
                NeedToBeReviewedJobs.Add(job);
            }

        if (NeedToBeReviewedJobs.Count > 0) SelectedJob = NeedToBeReviewedJobs[0];
    }

    /// <summary>
    ///     Handles selection of a job: updates <see cref="SelectedJob" /> and attempts to
    ///     load its thumbnails from the file server.
    /// </summary>
    private async Task SelectJobAsync(PrintJob job)
    {
        SelectedJob = job;
    }

    /// <summary>
    ///     Marks the specified job as operator-approved and removes it from the review list.
    /// </summary>
    private async Task MarkApprovedAsync(PrintJob job)
    {
        await _databaseAccessHelper.PrintJobs.UpdatePrintJobStatusAsync(job.Id, "operatorApproved");
        NeedToBeReviewedJobs.Remove(job);
        ClearThumbnails();
        NeedToBeReviewedJobs.Remove(job);
        await SelectNextAfterRemovalAsync(job);
    }

    /// <summary>
    ///     Marks the specified job as rejected and removes it from the review list.
    /// </summary>
    private async Task MarkNotApprovedAsync(PrintJob job)
    {
        await _databaseAccessHelper.PrintJobs.UpdatePrintJobStatusAsync(job.Id, "rejected");
        NeedToBeReviewedJobs.Remove(job);
        ClearThumbnails();
        NeedToBeReviewedJobs.Remove(job);
        await SelectNextAfterRemovalAsync(job);
    }

    /// <summary>
    ///     Calls the local API to fetch raw G-code bytes for the specified job id.
    /// </summary>
    /// <param name="printJobId">The job identifier.</param>
    /// <returns>The response body bytes, or <c>null</c> on failure.</returns>
    private async Task<byte[]?> DownloadGcodeFromApiAsync(long printJobId)
    {
        var response = await _httpClient.GetAsync($"api/gcode/{printJobId}/bytes");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Failed to download gcode for job {printJobId}: {response.StatusCode}");
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>
    ///     Converts a base64-encoded image string into an Avalonia <see cref="Bitmap" />.
    /// </summary>
    /// <param name="base64">The base64 string, or null/whitespace.</param>
    /// <returns>A decoded <see cref="Bitmap" /> or <c>null</c> if the input is invalid.</returns>
    private static Bitmap? BitmapFromBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     After removing a job from the list, select the next sensible job:
    ///     same index if possible, otherwise the previous one. Clears selection if none left.
    /// </summary>
    private async Task SelectNextAfterRemovalAsync(PrintJob removedJob)
    {
        var oldIndex = NeedToBeReviewedJobs.IndexOf(removedJob);
        if (oldIndex < 0) oldIndex = 0;

        if (NeedToBeReviewedJobs.Count == 0)
        {
            ClearThumbnails();
            SelectedJob = null;
            return;
        }

        var nextIndex = Math.Min(oldIndex, NeedToBeReviewedJobs.Count - 1);
        SelectedJob = NeedToBeReviewedJobs[nextIndex]; // setter loads thumbnail
    }

    private async Task LoadSelectedThumbnailAsync(PrintJob? job)
    {
        // Clear previous images first
        ClearThumbnails();

        if (job is null) return;

        // Load all 8 images from the file server
        await LoadAllThumbnailsFromFileServerAsync(job.Id);
        
        // Display the first available image
        UpdateDisplayedImage();
    }

    /// <summary>
    ///     Loads all 8 thumbnail images from the file server for the specified print job.
    /// </summary>
    private async Task LoadAllThumbnailsFromFileServerAsync(long printJobId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/images/{printJobId}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Failed to fetch thumbnails for job {printJobId}: {response.StatusCode}");
                Array.Clear(_thumbnailImages, 0, _thumbnailImages.Length);
                return;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(jsonContent);
            
            if (jsonDoc.RootElement.TryGetProperty("images", out var imagesArray))
            {
                int index = 0;
                foreach (var imageElement in imagesArray.EnumerateArray())
                {
                    if (index >= 8) break;
                    
                    if (imageElement.ValueKind == JsonValueKind.String)
                    {
                        _thumbnailImages[index] = imageElement.GetString();
                    }
                    else
                    {
                        _thumbnailImages[index] = null;
                    }
                    index++;
                }
            }

            // Reset to first image
            _currentImageIndex = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching thumbnails from file server for job {printJobId}: {ex.Message}");
            Array.Clear(_thumbnailImages, 0, _thumbnailImages.Length);
        }
    }

    /// <summary>
    ///     Updates the displayed image based on the current index.
    /// </summary>
    private void UpdateDisplayedImage()
    {
        _thumbnailImage?.Dispose();
        ThumbnailImage = null;

        // Find the first available image if current index is null
        if (_thumbnailImages[_currentImageIndex] == null)
        {
            for (int i = 0; i < 8; i++)
            {
                if (_thumbnailImages[i] != null)
                {
                    _currentImageIndex = i;
                    break;
                }
            }
        }

        var thumbnailBase64 = _thumbnailImages[_currentImageIndex];
        if (!string.IsNullOrWhiteSpace(thumbnailBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(thumbnailBase64);
                using var ms = new MemoryStream(bytes);
                ThumbnailImage = new Bitmap(ms);
            }
            catch
            {
                ThumbnailImage = null;
            }
        }

        // Update navigation button states
        OnPropertyChanged(nameof(CanNavigateToPrevious));
        OnPropertyChanged(nameof(CanNavigateToNext));
        OnPropertyChanged(nameof(CurrentImageIndex));
        OnPropertyChanged(nameof(TotalImageCount));
        OnPropertyChanged(nameof(ImageCounterText));
        
        // Update command states
        PreviousImageCommand.NotifyCanExecuteChanged();
        NextImageCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Navigates to the previous image, wrapping around to the last image if at the beginning.
    /// </summary>
    private void NavigateToPreviousImage()
    {
        if (TotalImageCount == 0) return;

        // First, try to find previous available image from current position
        for (int i = _currentImageIndex - 1; i >= 0; i--)
        {
            if (_thumbnailImages[i] != null)
            {
                _currentImageIndex = i;
                UpdateDisplayedImage();
                return;
            }
        }

        // If no previous image found, wrap around to the last available image
        for (int i = 7; i > _currentImageIndex; i--)
        {
            if (_thumbnailImages[i] != null)
            {
                _currentImageIndex = i;
                UpdateDisplayedImage();
                return;
            }
        }
    }

    /// <summary>
    ///     Navigates to the next image, wrapping around to the first image if at the end.
    /// </summary>
    private void NavigateToNextImage()
    {
        if (TotalImageCount == 0) return;

        // First, try to find next available image from current position
        for (int i = _currentImageIndex + 1; i < 8; i++)
        {
            if (_thumbnailImages[i] != null)
            {
                _currentImageIndex = i;
                UpdateDisplayedImage();
                return;
            }
        }

        // If no next image found, wrap around to the first available image
        for (int i = 0; i < _currentImageIndex; i++)
        {
            if (_thumbnailImages[i] != null)
            {
                _currentImageIndex = i;
                UpdateDisplayedImage();
                return;
            }
        }
    }

    /// <summary>
    ///     Clears all thumbnail images and disposes resources.
    /// </summary>
    private void ClearThumbnails()
    {
        _thumbnailImage?.Dispose();
        ThumbnailImage = null;
        Array.Clear(_thumbnailImages, 0, _thumbnailImages.Length);
        _currentImageIndex = 0;
        OnPropertyChanged(nameof(CurrentImageIndex));
        OnPropertyChanged(nameof(TotalImageCount));
        OnPropertyChanged(nameof(ImageCounterText));
        OnPropertyChanged(nameof(CanNavigateToPrevious));
        OnPropertyChanged(nameof(CanNavigateToNext));
    }


    /// <summary>
    ///     Opens GCode in default app for users machine
    /// </summary>
    private static void OpenWithDefaultApp(string filePath)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(filePath)!
            };
            Process.Start(psi);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1155 /* no association */)
        {
            Console.WriteLine("No app is associated with .gcode files.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open gcode via default app: {ex.Message}");
        }
    }
}