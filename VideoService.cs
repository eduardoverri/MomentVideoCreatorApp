using FFMpegKit.Droid;
using System.Text;

namespace VideoClipper.Services
{
#nullable enable
    public class VideoService
    {
        /// <summary>
        /// Main workflow: Validates inputs, cuts clips, and merges them.
        /// </summary>
        /// <param name="sourceVideoPath">The absolute path to the source video.</param>
        /// <param name="timestamps">List of timestamps (seconds) to extract.</param>
        /// <returns>Path to the final merged video.</returns>
        public async Task<string> ProcessHighlightsAsync(string sourceVideoPath, List<double> timestamps)
        {
            // 1. Probe the video to get total duration for validation
            double totalDuration = await GetVideoDurationAsync(sourceVideoPath);
            if (totalDuration <= 0) throw new Exception("Invalid video file or unable to read duration.");

            var tempFiles = new List<string>();
            string cacheDir = FileSystem.CacheDirectory;

            try
            {
                int index = 0;

                // 2. Iterate through timestamps to create sub-clips
                foreach (var point in timestamps)
                {
                    // Logic: Start 1s before, End 1s after.
                    // Validation: Ensure Start isn't < 0.
                    double startTime = Math.Max(0, point - 1);

                    // Validation: Ensure End isn't > Video Duration.
                    double endTime = Math.Min(totalDuration, point + 1);
                    
                    // Calculate actual duration of the clip
                    double clipDuration = endTime - startTime;

                    // Skip invalid clips (e.g., if timestamp is way past duration)
                    if (clipDuration <= 0) continue;

                    string tempOutput = Path.Combine(cacheDir, $"clip_{index}.mp4");
                    
                    // 3. FFmpeg Command for Cutting
                    // -ss {startTime}: Seek to start time (fast seek).
                    // -i: Input file.
                    // -t {clipDuration}: Duration to record.
                    // -c:v libx264: Re-encode video to ensure cut accuracy.
                    // -preset ultrafast: Sacrifice compression efficiency for speed.
                    // -c:a aac: Re-encode audio to ensure sync.
                    string cmd = $"-ss {startTime} -i \"{sourceVideoPath}\" -t {clipDuration} -c:v libx264 -preset ultrafast -c:a aac \"{tempOutput}\"";
                    
                    bool success = await ExecuteFFmpegAsync(cmd);
                    if (success)
                    {
                        tempFiles.Add(tempOutput);
                        index++;
                    }
                }

                if (tempFiles.Count == 0) throw new Exception("No valid clips could be generated.");

                // 4. Concatenate the clips
                string finalOutput = Path.Combine(cacheDir, "output_merged.mp4");
                
                // Delete previous output if exists
                if (File.Exists(finalOutput)) File.Delete(finalOutput);

                await JoinClipsAsync(tempFiles, finalOutput);

                return finalOutput;
            }
            finally
            {
                // 5. Cleanup: Delete the temporary clip files
                foreach (var file in tempFiles)
                {
                    if (File.Exists(file)) File.Delete(file);
                }
            }
        }

        public async Task<string> GetMediaInformationAsync(string path)
        {
            // By bypassing GetMediaInformation JSON wrapper (which has a known crash on empty outputs), 
            // we execute ffprobe directly and grab the human readable generic info.
            var session = await Task.Run(() => FFprobeKit.ExecuteWithArguments(new string[] { "-hide_banner", "-i", path }));
            
            if (session == null || FFMpegKit.Droid.ReturnCode.IsCancel(session.ReturnCode))
            {
                return "FFprobe execution cancelled or failed.";
            }

            // Wait briefly to ensure asynchronous logs are fully flushed to the session
            await Task.Delay(100);

            // FFprobe writes input file format information to stderr, meaning it will show up in Output
            string logOutput = session.GetAllLogsAsString(1000);
            string output = string.IsNullOrWhiteSpace(session.Output) ? logOutput : session.Output;

            return string.IsNullOrWhiteSpace(output) ? "No Output" : output;
        }

        /// <summary>
        /// Uses FFprobe to get video duration manually to bypass JSON crash.
        /// </summary>
        private async Task<double> GetVideoDurationAsync(string path)
        {
#if ANDROID
            try
            {
                // Fallback: Using Android's native metadata retriever is bulletproof
                var retriever = new Android.Media.MediaMetadataRetriever();
                retriever.SetDataSource(path);
                string time = retriever.ExtractMetadata(Android.Media.MetadataKey.Duration);
                retriever.Release();
                if (!string.IsNullOrEmpty(time) && double.TryParse(time, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double durationMs))
                {
                    return durationMs / 1000.0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"MediaMetadataRetriever failed: {ex.Message}");
            }
#endif

            // Do not use -v error, as that suppresses the INFO logs where standard output (duration) is written!
            var session = await Task.Run(() => FFprobeKit.ExecuteWithArguments(new string[] 
            {
                "-show_entries", "format=duration", 
                "-of", "default=noprint_wrappers=1:nokey=1", 
                path
            }));
            
            if (session == null) 
            {
                throw new Exception("FFprobe session was null.");
            }

            // Small delay to ensure FFmpeg logs are flushed completely
            await Task.Delay(100);

            string logOutput = session.GetAllLogsAsString(2000);
            string output = string.IsNullOrWhiteSpace(session.Output) ? logOutput : session.Output;

            if (string.IsNullOrWhiteSpace(output))
            {
                throw new Exception($"FFprobe duration check returned empty.\nState: {session.State}, ReturnCode: {session.ReturnCode?.Value}");
            }
            
            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double duration))
            {
                return duration;
            }
            
            throw new Exception($"Could not parse duration value: {output}\nReturnCode: {session.ReturnCode?.Value}");
        }

        /// <summary>
        /// Joins multiple video files using the Concat Demuxer.
        /// </summary>
        private async Task JoinClipsAsync(List<string> inputPaths, string outputPath)
        {
            // FFmpeg concat requires a text file listing all inputs
            string listFile = Path.Combine(FileSystem.CacheDirectory, "concat_list.txt");
            var sb = new StringBuilder();
            
            foreach (var path in inputPaths)
            {
                // Format: file '/path/to/file.mp4'
                sb.AppendLine($"file '{path}'");
            }
            
            await File.WriteAllTextAsync(listFile, sb.ToString());

            // -f concat: Use the concat demuxer
            // -safe 0: Allow unsafe file paths (absolute paths)
            // -c copy: Stream copy (No re-encoding, very fast)
            string cmd = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";
            
            await ExecuteFFmpegAsync(cmd);
        }

        /// <summary>
        /// Helper to execute FFmpeg commands asynchronously.
        /// </summary>
        private Task<bool> ExecuteFFmpegAsync(string command)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            FFmpegKit.ExecuteAsync(command, new FFmpegSessionCompleteCallback(tcs));
            return tcs.Task;
        }

        private class FFmpegSessionCompleteCallback : Java.Lang.Object, IFFmpegSessionCompleteCallback
        {
            private readonly TaskCompletionSource<bool> _tcs;

            public FFmpegSessionCompleteCallback(TaskCompletionSource<bool> tcs)
            {
                _tcs = tcs;
            }

            public void Apply(FFmpegSession? session)
            {
                var returnCode = session?.ReturnCode;
                if (returnCode != null && FFMpegKit.Droid.ReturnCode.IsSuccess(returnCode))
                {
                    _tcs.SetResult(true);
                }
                else
                {
                    Console.WriteLine($"FFmpeg Error: {session?.Output}");
                    _tcs.SetResult(false);
                }
            }
        }
    }
}
