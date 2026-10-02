using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using LibVLCSharp.Shared;
using ArcadiaWeather.Presentation;

namespace ArcadiaWeather;

/// <summary>Opt-in packaging diagnostic. Never opens a window, starts wallpaper, or requests weather/location.</summary>
internal static class BundleVerification
{
    private sealed record BundledFile(string File, long Bytes, string SHA256);

    internal static async Task<int> RunAsync(string reportPath)
    {
        var watch = Stopwatch.StartNew();
        var checks = new List<object>();
        bool success = true;
        void Check(string name, bool passed, object? detail = null)
        {
            checks.Add(new { Test = name, Passed = passed, Detail = detail });
            success &= passed;
        }
        try
        {
            string root = AppContext.BaseDirectory;
            string launcherDirectory = Path.GetDirectoryName(Environment.ProcessPath!)!;
            Check("Content extracted away from the standalone EXE", !string.Equals(
                Path.TrimEndingDirectorySeparator(root), Path.TrimEndingDirectorySeparator(launcherDirectory), StringComparison.OrdinalIgnoreCase));
            var manifest = JsonSerializer.Deserialize<List<BundledFile>>(File.ReadAllText(Path.Combine(root, "bundled-media.json")))
                ?? throw new InvalidDataException("Missing video manifest.");
            Check("Exactly five videos embedded", manifest.Count == 5 && manifest.Select(f => f.File).Distinct().Count() == 5);
            foreach (var scene in Enum.GetValues<Scene>())
            {
                string path = new AppSettings().VideoPath(scene);
                string relative = "Media/" + Path.GetFileName(path);
                var expected = manifest.Single(f => f.File == relative);
                using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
                Check($"Day {(int)scene} extracted byte-for-byte", stream.Length == expected.Bytes && hash == expected.SHA256, new { Bytes = stream.Length });
            }
            string native = Path.Combine(root, "libvlc", "win-x64");
            foreach (string file in new[] { "libvlc.dll", "libvlccore.dll", "plugins/codec/libavcodec_plugin.dll", "plugins/demux/libmp4_plugin.dll" })
                Check("Bundled player file: " + file, File.Exists(Path.Combine(native, file)));
            Check("Licenses included", File.Exists(Path.Combine(root, "Licenses", "LibVLC-LGPL-2.1.txt")) && File.Exists(Path.Combine(root, "THIRD-PARTY-NOTICES.txt")));
            // Decode the exact native icon resources used by the taskbar, without
            // creating a window or changing the running app or shell settings.
            foreach (int size in new[] { 32, 64 })
            {
                using var icon = WindowIcons.Load(size);
                using var pixels = icon.ToBitmap();
                int bluePixels = 0;
                for (int x = 0; x < pixels.Width; x++)
                    for (int y = 0; y < pixels.Height; y++)
                    {
                        var color = pixels.GetPixel(x, y);
                        if (color.A > 128 && color.G > color.R + 35 && color.B > color.R + 35) bluePixels++;
                    }
                Check($"Butterfly window icon decodes at {size}px", pixels.Width == size && pixels.Height == size && bluePixels > size * size / 5);
            }
            LibVLCSharp.Shared.Core.Initialize(native);
            using var vlc = new LibVLC("--quiet", "--no-video", "--no-audio", "--no-media-library");
            Check("Embedded native player loads", vlc.NativeReference != IntPtr.Zero);
            foreach (var scene in Enum.GetValues<Scene>())
            {
                string preview = Path.Combine(root, "Assets", "Preview", $"day{(int)scene}.mp4");
                Check($"Day {(int)scene} preview excerpt included", File.Exists(preview));
                using var previewMedia = new Media(vlc, preview, FromType.FromPath);
                var previewStatus = await previewMedia.Parse(MediaParseOptions.ParseLocal, 15000);
                Check($"Day {(int)scene} preview excerpt parses", previewStatus == MediaParsedStatus.Done
                    && previewMedia.Duration >= 11000 && previewMedia.Tracks.Any(t => t.TrackType == TrackType.Video));
                using var media = new Media(vlc, new AppSettings().VideoPath(scene), FromType.FromPath);
                var status = await media.Parse(MediaParseOptions.ParseLocal, 15000);
                Check($"Day {(int)scene} parsed by embedded player", status == MediaParsedStatus.Done && media.Duration > 0
                    && media.Tracks.Any(t => t.TrackType == TrackType.Video) && media.Tracks.Any(t => t.TrackType == TrackType.Audio), new { DurationMs = media.Duration });
            }
        }
        catch (Exception ex) { Check("Bundle verification completed", false, ex.ToString()); }
        var result = new { Success = success, ElapsedSeconds = watch.Elapsed.TotalSeconds, ExtractionDirectory = AppContext.BaseDirectory, Checks = checks };
        try
        {
            string fullPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { return 2; }
        return success ? 0 : 1;
    }
}
