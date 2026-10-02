namespace ArcadiaWeather.Core;

public static class BundledMedia
{
    public static string GetDirectory(string appDirectory) => Path.Combine(appDirectory, "Media");
    public static string GetVideoPath(string appDirectory, Scene scene) =>
        Path.Combine(GetDirectory(appDirectory), $"Arcadia Bay Day {(int)scene} Sound AAC.mp4");
}
