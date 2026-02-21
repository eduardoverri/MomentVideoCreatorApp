# Building a Production-Grade Android Video Clipper with .NET MAUI

## 1. Architectural Overview

### The Recommendation: .NET MAUI + FFmpeg
To target Android (and potentially iOS in the future) with high-performance media requirements, **.NET MAUI** is the optimal choice among the provided options (Java, Node, .NET).

*   **Native Performance:** .NET compiles to native binaries (AOT/JIT), providing the raw speed necessary for video manipulation.
*   **FFmpeg Interop:** We utilize **FFmpegKit**, a wrapper for the industry-standard C libraries. This allows us to perform complex video operations without rewriting codec logic.
*   **Single Codebase:** The business logic written here for Android is 100% reusable for iOS.

### The Strategy: Hybrid Transcoding
Video processing is a trade-off between speed and accuracy.
1.  **Cutting (Re-encoding):** We **re-encode** the short (2-second) clips. Cutting video on specific timestamps without re-encoding often fails because the cut point might not align with a Keyframe (I-Frame), causing visual artifacts. Since the clips are short, re-encoding is fast and ensures frame-perfect accuracy.
2.  **Joining (Stream Copy):** Once we have clean, standardized clips, we use **Stream Copy** to merge them. This is instantaneous as it stitches binary data without processing pixels.

---

## 2. Implementation Steps

### Step 1: Dependencies
Add the following NuGet package to your .NET MAUI project:
*   `FFmpegKit.NET` (Use the `https` variant if network streams are needed, otherwise `full` is recommended).

### Step 2: Android Permissions
Modify `Platforms/Android/AndroidManifest.xml` to allow reading/writing video files.

```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
    <uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
    <!-- Required for Android 13+ (API 33) -->
    <uses-permission android:name="android.permission.READ_MEDIA_VIDEO" />
</manifest>
