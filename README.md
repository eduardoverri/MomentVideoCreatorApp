# MomentVideoCreatorApp

**MomentVideoCreatorApp** is a cross-platform application built with .NET MAUI that simplifies video editing. Its primary function is to process an input video file along with a list of timestamps, clip the video into specific moments, and seamlessly merge these sub-clips into a final, consolidated output video file.

## Features
- **Video Playback**: Built-in video player interface to preview the source media.
- **Timestamp Processing**: Users can input a list of specific timestamps corresponding to the moments they want to extract.
- **Automated Video Clipping**: The `VideoService` clips the initial video source into smaller sub-clips based on the provided timestamps.
- **Video Merging**: Automatically concatenates the generated sub-clips into a single, cohesive output video.
- **FFmpeg Integration**: Leverages `FFmpegKit` for robust, high-performance multimedia processing (extracting, decoding, filtering, and merging) directly on the device.
- **Storage Permissions Management**: Built-in handling to ensure the application requests and obtains the necessary local storage permissions.
- **Progress Tracking**: Clear UI updates to show processing status, coupled with error handling logs for FFmpeg operations.

## Technology Stack
- **.NET MAUI**: For creating the cross-platform application interface and logic.
- **C# & XAML**: The core programming language and user interface markup.
- **FFmpegKit**: The backbone for processing the video segments asynchronously.

## Core Components
- **`MainPage.xaml` & `MainPage.xaml.cs`**: The main landing page, containing the video file selector, the timestamp input text field, and user feedback visualizations.
- **`VideoService.cs`**: Contains the core logic responsible for parsing timestamps, invoking specific FFmpeg CLI commands, extracting the clips, and concatenating them.
- **`Platforms/Android`**: Stores Android-specific configurations, such as requested permissions in `AndroidManifest.xml` and startup routines in `MainActivity.cs`.

## Getting Started

### Prerequisites
- Visual Studio 2022 (or newer) with the **.NET Multi-platform App UI development** workload installed.
- Dependencies such as `FFmpegKit` installed via NuGet.
- An Android Emulator or physical device for testing.

### Running the App
1. Open up the project in your IDE (e.g., Visual Studio).
2. Restore all NuGet packages if they don't restore automatically.
3. Make sure you select your desired Android Emulator or plugged-in physical device as the run target.
4. Press Run (F5) to build and deploy.

### Usage Example
1. Launch the application.
2. Automatically approve or manually grant storage read/write permissions.
3. Use the interface to select an existing video file from your device.
4. Input your desired target timestamps.
5. Tap the processing button. The app will clip, merge, and output the final custom video directly back into your device's local storage.