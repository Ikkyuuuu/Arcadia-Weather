using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcadiaWeather.Core;

public enum Scene { Day = 1, Cloudy = 2, Night = 3, Evening = 4, Rain = 5 }
public enum DayPhase { Day, Evening, Night }

public sealed record SunTimes(DateOnly Date, DateTimeOffset Sunrise, DateTimeOffset Sunset);
public sealed record WeatherSnapshot(
    double Latitude, double Longitude, string TimeZoneId,
    DateTimeOffset FetchedAt, DateTimeOffset ObservedAt,
    int WeatherCode, double CloudCover, double Rain, double Showers,
    double Temperature, List<SunTimes> Sun)
{
    public double? FeelsLike { get; init; }
    public double? Humidity { get; init; }
}
public sealed record City(string Name, string Country, string? Admin1, double Latitude, double Longitude, string TimeZoneId)
{
    public override string ToString() => string.Join(", ", new[] { Name, Admin1, Country }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
}
public sealed record SceneDecision(Scene Scene, DayPhase Phase, string Reason, bool WeatherIsFresh);

public sealed class AppSettings
{
    public string LocationName { get; set; } = "Bangkok, Thailand";
    public double Latitude { get; set; } = 13.7563;
    public double Longitude { get; set; } = 100.5018;
    public string TimeZoneId { get; set; } = "Asia/Bangkok";
    [JsonIgnore] public string MediaDirectory => BundledMedia.GetDirectory(AppContext.BaseDirectory);
    public string MonitorDevice { get; set; } = "";
    public int Volume { get; set; } = 25;
    public bool Muted { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool PauseOnFullScreen { get; set; } = true;
    public string AppTheme { get; set; } = "system";
    public string TemperatureUnit { get; set; } = "C";
    public string WallpaperFit { get; set; } = "fill";
    public int CloudEnterPercent { get; set; } = 70;
    public int CloudExitPercent { get; set; } = 55;
    public int EveningMinutesBeforeSunset { get; set; } = 60;
    public int NightMinutesAfterSunset { get; set; } = 30;
    public int FallbackDayHour { get; set; } = 6;
    public int FallbackEveningHour { get; set; } = 17;
    public int FallbackNightHour { get; set; } = 19;
    [JsonIgnore] public static TimeSpan PollInterval => TimeSpan.FromMinutes(5);
    [JsonIgnore] public static TimeSpan WeatherMaxAge => TimeSpan.FromHours(1);
    public string VideoPath(Scene scene) => BundledMedia.GetVideoPath(AppContext.BaseDirectory, scene);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(LocationName)) throw new ArgumentException("Choose a location name.");
        if (!double.IsFinite(Latitude) || Latitude is < -90 or > 90 || !double.IsFinite(Longitude) || Longitude is < -180 or > 180)
            throw new ArgumentException("Enter valid latitude and longitude coordinates.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        if (CloudExitPercent < 0 || CloudEnterPercent > 100 || CloudExitPercent >= CloudEnterPercent)
            throw new ArgumentException("Cloud clearing must be lower than the cloudy threshold (0–100%).");
        if (EveningMinutesBeforeSunset is < 0 or > 240 || NightMinutesAfterSunset is < 0 or > 180)
            throw new ArgumentException("Evening must begin 0–240 minutes before sunset; night 0–180 minutes after.");
        if (Volume is < 0 or > 100) throw new ArgumentException("Volume must be between 0 and 100.");
        if (AppTheme is not ("system" or "light" or "dark")) throw new ArgumentException("Choose a valid app theme.");
        if (TemperatureUnit is not ("C" or "F")) throw new ArgumentException("Choose Celsius or Fahrenheit.");
        if (WallpaperFit is not ("fill" or "fit")) throw new ArgumentException("Choose Fill or Fit.");
        if (FallbackDayHour < 0 || FallbackDayHour >= FallbackEveningHour || FallbackEveningHour >= FallbackNightHour || FallbackNightHour > 23)
            throw new ArgumentException("Offline schedule must have day before evening before night (0–23).");
    }

    public AppSettings Copy() => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;
}

public static class SceneInfo
{
    public static string Title(Scene scene) => scene switch
    {
        Scene.Day => "A quiet morning", Scene.Cloudy => "Clouds over the bay", Scene.Night => "After the light",
        Scene.Evening => "The golden hour", Scene.Rain => "Before the storm", _ => "Arcadia Bay"
    };
    public static string ShortName(Scene scene) => scene switch
    { Scene.Day => "Daylight", Scene.Cloudy => "Cloudy", Scene.Night => "Night", Scene.Evening => "Evening", Scene.Rain => "Rain & storm", _ => "Unknown" };
    public static string WeatherDescription(int code) => code switch
    {
        0 => "Clear sky", 1 => "Mainly clear", 2 => "Partly cloudy", 3 => "Overcast",
        45 or 48 => "Fog", 51 or 53 or 55 => "Drizzle", 56 or 57 => "Freezing drizzle",
        61 or 63 or 65 => "Rain", 66 or 67 => "Freezing rain", 80 or 81 or 82 => "Rain showers",
        71 or 73 or 75 or 77 or 85 or 86 => "Snow", 95 or 96 or 97 or 99 => "Thunderstorm", _ => "Unknown conditions"
    };
}
