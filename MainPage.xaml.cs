using Microsoft.Maui.ApplicationModel;

namespace VideoClipper
{
    public partial class MainPage : ContentPage
    {
        private readonly Services.VideoService _videoService;
        private string _selectedVideoPath;

        public MainPage()
        {
            InitializeComponent();
            _videoService = new Services.VideoService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CheckAndRequestStoragePermissions();
        }

        private async Task CheckAndRequestStoragePermissions()
        {
            PermissionStatus readStatus;
            PermissionStatus writeStatus;

#if ANDROID
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                // Android 13+
                readStatus = await Permissions.CheckStatusAsync<Permissions.Media>();
                if (readStatus != PermissionStatus.Granted)
                {
                    readStatus = await Permissions.RequestAsync<Permissions.Media>();
                }
                writeStatus = readStatus; // No specific write permission needed on API 33+ for public media
            }
            else
            {
                readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                if (readStatus != PermissionStatus.Granted)
                {
                    readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
                }

                writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (writeStatus != PermissionStatus.Granted)
                {
                    writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
            }
#else
            readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            if (readStatus != PermissionStatus.Granted)
            {
                readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
            }

            writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (writeStatus != PermissionStatus.Granted)
            {
                writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }
#endif

            if (readStatus != PermissionStatus.Granted || (writeStatus != PermissionStatus.Granted && Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Tiramisu))
            {
                await this.DisplayAlertAsync("Permission Denied", "Storage permissions are required to select and process videos.", "OK");
            }
        }

        private async void OnSelectVideoClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a Video",
                    FileTypes = FilePickerFileType.Videos
                });

                if (result == null) return;

                LoadingIndicator.IsRunning = true;
                StatusLabel.Text = "Loading media info...";
                SelectedVideoLabel.Text = result.FileName;

                // Handle Android Content URIs to make them readable by FFmpeg
                _selectedVideoPath = await EnsureLocalFileAsync(result);

                var mediaInfo = await _videoService.GetMediaInformationAsync(_selectedVideoPath);
                MediaInfoLabel.Text = mediaInfo;
                StatusLabel.Text = "Video selected and ready to process!";
            }
            catch (Exception ex)
            {
                await this.DisplayAlertAsync("Error", ex.Message, "OK");
                StatusLabel.Text = "Failed to load video.";
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        private async void OnProcessClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedVideoPath) || !File.Exists(_selectedVideoPath))
            {
                await this.DisplayAlertAsync("No Video", "Please select a video file first.", "OK");
                return;
            }

            try
            {
                // UI Feedback
                LoadingIndicator.IsRunning = true;
                StatusLabel.Text = "Processing...";

                // 2. Define Inputs from Text Field
                string timestampsText = TimestampsEntry.Text;
                var timestamps = new List<double>();
                if (!string.IsNullOrWhiteSpace(timestampsText))
                {
                    var parts = timestampsText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (double.TryParse(part.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ts))
                        {
                            timestamps.Add(ts);
                        }
                    }
                }

                if (timestamps.Count == 0)
                {
                    await this.DisplayAlertAsync("Invalid Input", "Please enter valid comma-separated timestamps.", "OK");
                    return;
                }

                // 4. Process Video
                string finalPath = await _videoService.ProcessHighlightsAsync(_selectedVideoPath, timestamps);

                // Save locally to AppDataDirectory to keep it persistent on the device
                string persistentPath = Path.Combine(FileSystem.AppDataDirectory, $"merged_video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
                File.Copy(finalPath, persistentPath, true);

                StatusLabel.Text = $"Done! Saved locally to:\n{persistentPath}";

                // 5. Open Result Automatically
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    Title = "View Merged Video",
                    File = new ReadOnlyFile(persistentPath)
                });
            }
            catch (Exception ex)
            {
                await this.DisplayAlertAsync("Error", ex.Message, "OK");
                StatusLabel.Text = "Failed.";
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
            }
        }

        private async Task<string> EnsureLocalFileAsync(FileResult fileResult)
        {
#if ANDROID
            // On Android 10+ (API 29+), scoped storage prevents native C libraries
            // like FFmpeg from reading raw file paths even with READ_EXTERNAL_STORAGE.
            // We must copy the stream into the app's cache directory.
            string tempPath = Path.Combine(FileSystem.CacheDirectory, fileResult.FileName);
            
            if (File.Exists(tempPath)) 
            {
                File.Delete(tempPath);
            }

            using (var stream = await fileResult.OpenReadAsync())
            using (var newStream = File.Create(tempPath))
            {
                await stream.CopyToAsync(newStream);
            }
            return tempPath;
#else
            if (!fileResult.FullPath.StartsWith("content://"))
                return fileResult.FullPath;

            string tempPath = Path.Combine(FileSystem.CacheDirectory, fileResult.FileName);
            using (var stream = await fileResult.OpenReadAsync())
            using (var newStream = File.Create(tempPath))
            {
                await stream.CopyToAsync(newStream);
            }
            return tempPath;
#endif
        }
    }
}
