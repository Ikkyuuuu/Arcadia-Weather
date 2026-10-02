using ArcadiaWeather.Core;
using System.Globalization;
using System.Net;
using System.Text.Json;

int passed = 0, failed = 0;
void Test(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception ex) { Console.WriteLine($"FAIL {name}: {ex.Message}"); failed++; }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected validation failure"); }
var settings = new AppSettings();
DateTimeOffset At(string time) => DateTimeOffset.Parse($"2026-09-30T{time}:00+07:00", CultureInfo.InvariantCulture);
WeatherSnapshot Weather(string time, int code = 0, double clouds = 10, double rain = 0, double showers = 0) => new(
    settings.Latitude, settings.Longitude, settings.TimeZoneId, At(time), At(time), code, clouds, rain, showers, 29,
    [new(new DateOnly(2026, 9, 30), At("06:00"), At("18:00"))]);
Scene Pick(string time, int code = 0, double cloud = 10) => new SceneRules().Decide(settings, Weather(time, code, cloud), At(time)).Scene;

Test("Poll interval is exactly five minutes", () => Equal(TimeSpan.FromMinutes(5), AppSettings.PollInterval));
Test("Morning and afternoon use Day 1", () => { Equal(Scene.Day, Pick("07:00")); Equal(Scene.Day, Pick("15:00")); });
Test("Sunrise boundary", () => { Equal(Scene.Night, Pick("05:59")); Equal(Scene.Day, Pick("06:00")); });
Test("Evening boundary", () => { Equal(Scene.Day, Pick("16:59")); Equal(Scene.Evening, Pick("17:00")); });
Test("Night begins 30 minutes after sunset", () => { Equal(Scene.Evening, Pick("18:29")); Equal(Scene.Night, Pick("18:30")); });
Test("Clouds override daytime and evening only", () =>
{
    Equal(Scene.Cloudy, Pick("10:00", 3, 80)); Equal(Scene.Cloudy, Pick("17:30", 3, 80)); Equal(Scene.Night, Pick("23:00", 3, 100));
});
foreach (int code in new[] { 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 80, 81, 82, 95, 96, 97, 99 })
    Test($"Wet code {code} overrides every time phase", () =>
    {
        foreach (string time in new[] { "10:00", "17:30", "23:00" }) Equal(Scene.Rain, Pick(time, code, 100));
    });
Test("Measured rain and showers also trigger Day 5", () =>
{
    Equal(Scene.Rain, new SceneRules().Decide(settings, Weather("23:00", rain: 0.1), At("23:00")).Scene);
    Equal(Scene.Rain, new SceneRules().Decide(settings, Weather("10:00", showers: 0.1), At("10:00")).Scene);
});
Test("Snow and fog are not misclassified as rain", () => { Equal(Scene.Night, Pick("23:00", 71)); Equal(Scene.Day, Pick("10:00", 45)); });
Test("Cloud hysteresis prevents flickering", () =>
{
    var rules = new SceneRules();
    foreach (var (cloud, expected) in new[] { (69, Scene.Day), (70, Scene.Cloudy), (65, Scene.Cloudy), (56, Scene.Cloudy), (55, Scene.Day), (60, Scene.Day) })
        Equal(expected, rules.Decide(settings, Weather("10:00", clouds: cloud), At("10:00")).Scene);
});
Test("Rain ending recomputes current phase", () =>
{
    var rules = new SceneRules();
    Equal(Scene.Rain, rules.Decide(settings, Weather("16:30", 65), At("16:30")).Scene);
    Equal(Scene.Evening, rules.Decide(settings, Weather("17:30"), At("17:30")).Scene);
});
Test("Clouds end at night even if previous state was cloudy", () =>
{
    var rules = new SceneRules(); rules.Decide(settings, Weather("17:30", clouds: 90), At("17:30"));
    Equal(Scene.Night, rules.Decide(settings, Weather("18:30", clouds: 90), At("18:30")).Scene);
});
Test("Recent cached rain survives an API failure", () => Equal(Scene.Rain, new SceneRules().Decide(settings, Weather("10:00", 61), At("10:50")).Scene));
Test("Stale weather falls back to time", () => Equal(Scene.Day, new SceneRules().Decide(settings, Weather("10:00", 61), At("11:01")).Scene));
Test("Old observation is stale even if newly downloaded", () =>
{
    var old = Weather("10:00", 61) with { ObservedAt = At("08:00") };
    Equal(Scene.Day, new SceneRules().Decide(settings, old, At("10:00")).Scene);
});
Test("Future clock anomalies do not keep rain forever", () => Equal(Scene.Day, new SceneRules().Decide(settings, Weather("15:00", 61), At("10:00")).Scene));
Test("Cached weather from another location is ignored", () =>
{
    var other = Weather("10:00", 61) with { Latitude = 40 };
    Equal(Scene.Day, new SceneRules().Decide(settings, other, At("10:00")).Scene);
});
Test("Cached astronomy remains usable while weather is stale", () =>
{
    var old = Weather("10:00", 61) with { Sun = [new(new DateOnly(2026, 9, 30), At("06:00"), At("19:00"))] };
    Equal(Scene.Day, new SceneRules().Decide(settings, old, At("17:30")).Scene);
});
Test("Offline fixed schedule and exact boundaries", () =>
{
    var rules = new SceneRules();
    foreach (var (time, scene) in new[] { ("05:59", Scene.Night), ("06:00", Scene.Day), ("16:59", Scene.Day), ("17:00", Scene.Evening), ("18:59", Scene.Evening), ("19:00", Scene.Night) })
        Equal(scene, rules.Decide(settings, null, At(time)).Scene);
});
Test("Location timezone, not computer timezone, controls scenes", () =>
{
    var other = settings.Copy(); other.TimeZoneId = "America/New_York";
    Equal(Scene.Night, new SceneRules().Decide(other, null, At("10:00")).Scene);
});
Test("Next date does not reuse yesterday's sunset", () => Equal(Scene.Evening, new SceneRules().Decide(settings, Weather("10:00"), At("17:30").AddDays(1)).Scene));
Test("Cloud and time preferences validate", () =>
{
    settings.Validate(); var invalid = settings.Copy(); invalid.CloudExitPercent = 90; Throws(invalid.Validate);
    invalid = settings.Copy(); invalid.Latitude = double.NaN; Throws(invalid.Validate);
    invalid = settings.Copy(); invalid.TimeZoneId = "not-a-timezone"; Throws(invalid.Validate);
});
Test("Video paths support Thai folder names", () =>
{
    Equal(@"C:\รูปภาพ\Arcadia\Media\Arcadia Bay Day 5 Sound AAC.mp4", BundledMedia.GetVideoPath(@"C:\รูปภาพ\Arcadia", Scene.Rain));
});
Test("Legacy external video paths cannot override bundled scenes", () =>
{
    var legacy = JsonSerializer.Deserialize<AppSettings>("""{"MediaDirectory":"Z:\\missing-old-folder","LocationName":"My district","Volume":70,"Muted":false}""")!;
    Equal(Path.Combine(AppContext.BaseDirectory, "Media", "Arcadia Bay Day 1 Sound AAC.mp4"), legacy.VideoPath(Scene.Day));
    Equal("My district", legacy.LocationName); Equal(70, legacy.Volume); Equal(false, legacy.Muted);
    Equal(legacy.VideoPath(Scene.Rain), legacy.Copy().VideoPath(Scene.Rain));
    Equal(false, JsonSerializer.Serialize(legacy).Contains("MediaDirectory"));
});

string Sample(object? sunrise = null) => JsonSerializer.Serialize(new
{
    timezone = "Asia/Bangkok",
    current = new { time = At("10:00").ToUnixTimeSeconds(), weather_code = 63, cloud_cover = 87, rain = 1.2, showers = 0.0, temperature_2m = 28.4 },
    daily = new { sunrise = new[] { sunrise ?? At("06:00").ToUnixTimeSeconds() }, sunset = new[] { At("18:00").ToUnixTimeSeconds() } }
});
Test("API parser reads UTC epochs and Bangkok solar date", () =>
{
    using var doc = JsonDocument.Parse(Sample()); var parsed = WeatherClient.Parse(doc.RootElement, settings, At("10:00"));
    Equal(63, parsed.WeatherCode); Equal(At("06:00"), parsed.Sun[0].Sunrise); Equal(new DateOnly(2026, 9, 30), parsed.Sun[0].Date);
});
Test("Missing or null weather fields reject entire sample", () =>
{
    using var doc = JsonDocument.Parse(Sample().Replace("\"cloud_cover\":87", "\"cloud_cover\":null"));
    Throws(() => WeatherClient.Parse(doc.RootElement, settings, At("10:00")));
});
Test("Null sunrise safely uses a fixed schedule", () =>
{
    using var doc = JsonDocument.Parse(Sample().Replace($"\"sunrise\":[{At("06:00").ToUnixTimeSeconds()}]", "\"sunrise\":[null]"));
    var parsed = WeatherClient.Parse(doc.RootElement, settings, At("10:00"));
    Equal(0, parsed.Sun.Count); Equal(DayPhase.Day, SceneRules.GetPhase(settings, parsed, At("10:00")));
});
Test("Weather cache round trips with solar times", () =>
{
    var weather = Weather("10:00"); var copy = JsonSerializer.Deserialize<WeatherSnapshot>(JsonSerializer.Serialize(weather))!;
    Equal(weather.ObservedAt, copy.ObservedAt); Equal(weather.Sun[0], copy.Sun[0]);
});
Test("HTTP errors propagate so the controller can retain cache", () =>
{
    using var client = new HttpClient(new StubHandler(HttpStatusCode.TooManyRequests, "{}"));
    Throws(() => new WeatherClient(client).FetchAsync(settings, CancellationToken.None).GetAwaiter().GetResult());
});
Test("Geocoder returns empty results safely", () =>
{
    using var client = new HttpClient(new StubHandler(HttpStatusCode.OK, "{}"));
    Equal(0, new WeatherClient(client).SearchAsync("NoCity", CancellationToken.None).GetAwaiter().GetResult().Count);
});
DeviceLocation Fix() => new(13.751234, 100.512345, 25, DateTimeOffset.UtcNow, "GPS / satellite");
Test("Device coordinates are returned only after permission", () =>
{
    var platform = new StubLocationPlatform(LocationAccess.Allowed, Fix());
    var result = new DeviceLocationReader(platform).ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
    Equal(13.751234, result.Latitude); Equal(100.512345, result.Longitude);
    Equal("access,position", string.Join(",", platform.Calls));
});
foreach (var access in new[] { LocationAccess.Denied, LocationAccess.Unspecified })
    Test($"{access} location permission never queries the device", () =>
    {
        var platform = new StubLocationPlatform(access, Fix());
        Throws(() => new DeviceLocationReader(platform).ReadAsync(CancellationToken.None).GetAwaiter().GetResult());
        Equal("access", string.Join(",", platform.Calls));
    });
Test("Cancelling before permission does not access location", () =>
{
    var platform = new StubLocationPlatform(LocationAccess.Allowed, Fix());
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    Throws(() => new DeviceLocationReader(platform).ReadAsync(cancelled.Token).GetAwaiter().GetResult());
    Equal(0, platform.Calls.Count);
});
Test("Cancelling the permission request prevents position lookup", () =>
{
    using var cancellation = new CancellationTokenSource();
    var platform = new StubLocationPlatform(LocationAccess.Allowed, Fix()) { OnAccess = cancellation.Cancel };
    Throws(() => new DeviceLocationReader(platform).ReadAsync(cancellation.Token).GetAwaiter().GetResult());
    Equal("access", string.Join(",", platform.Calls));
});
Test("Invalid or stale device positions are rejected", () =>
{
    foreach (var invalid in new[] { Fix() with { Latitude = double.NaN }, Fix() with { Longitude = 181 },
        Fix() with { AccuracyMeters = -1 }, Fix() with { Timestamp = DateTimeOffset.UtcNow.AddMinutes(-6) },
        Fix() with { Timestamp = DateTimeOffset.UtcNow.AddMinutes(2) } })
    {
        var platform = new StubLocationPlatform(LocationAccess.Allowed, invalid);
        Throws(() => new DeviceLocationReader(platform).ReadAsync(CancellationToken.None).GetAwaiter().GetResult());
    }
});
Test("Approximate Windows positions retain their accuracy and source", () =>
{
    var platform = new StubLocationPlatform(LocationAccess.Allowed, Fix() with { AccuracyMeters = 12000, Source = "IP address estimate" });
    var result = new DeviceLocationReader(platform).ReadAsync(CancellationToken.None).GetAwaiter().GetResult();
    Equal("IP address estimate", result.Source); Equal("±12 km", result.AccuracyDescription);
});
Test("Device failure propagates without a fabricated location", () =>
{
    var platform = new StubLocationPlatform(LocationAccess.Allowed, Fix()) { PositionError = new TimeoutException() };
    Throws(() => new DeviceLocationReader(platform).ReadAsync(CancellationToken.None).GetAwaiter().GetResult());
    Equal("access,position", string.Join(",", platform.Calls));
});
Test("Legacy settings adopt display defaults without losing the location", () =>
{
    var legacy = JsonSerializer.Deserialize<AppSettings>("""{"LocationName":"Bang Rak","Latitude":13.73,"Longitude":100.523}""")!;
    Equal("system", legacy.AppTheme); Equal("C", legacy.TemperatureUnit); Equal("fill", legacy.WallpaperFit);
    Equal("Bang Rak", legacy.Copy().LocationName);
});
Test("Appearance preferences survive saving and reject invalid values", () =>
{
    var options = new AppSettings { AppTheme = "dark", TemperatureUnit = "F", WallpaperFit = "fit" };
    options.Validate(); var copy = options.Copy();
    Equal("dark", copy.AppTheme); Equal("F", copy.TemperatureUnit); Equal("fit", copy.WallpaperFit);
    copy.AppTheme = "invalid"; Throws(copy.Validate);
    copy = options.Copy(); copy.TemperatureUnit = "K"; Throws(copy.Validate);
    copy = options.Copy(); copy.WallpaperFit = "stretch"; Throws(copy.Validate);
});
Test("Feels-like temperature and humidity parse real optional readings", () =>
{
    using var doc = JsonDocument.Parse(Sample().Replace("\"temperature_2m\":28.4", "\"temperature_2m\":28.4,\"apparent_temperature\":32.5,\"relative_humidity_2m\":78"));
    var snapshot = WeatherClient.Parse(doc.RootElement, settings, At("10:00"));
    Equal<double?>(32.5, snapshot.FeelsLike); Equal<double?>(78, snapshot.Humidity);
    var restored = JsonSerializer.Deserialize<WeatherSnapshot>(JsonSerializer.Serialize(snapshot))!;
    Equal(snapshot.FeelsLike, restored.FeelsLike); Equal(snapshot.Humidity, restored.Humidity);
});
Test("Missing optional readings and invalid humidity do not fabricate values", () =>
{
    using var old = JsonDocument.Parse(Sample());
    var cached = WeatherClient.Parse(old.RootElement, settings, At("10:00"));
    Equal<double?>(null, cached.FeelsLike); Equal<double?>(null, cached.Humidity);
    using var bad = JsonDocument.Parse(Sample().Replace("\"temperature_2m\":28.4", "\"temperature_2m\":28.4,\"apparent_temperature\":null,\"relative_humidity_2m\":101"));
    var parsed = WeatherClient.Parse(bad.RootElement, settings, At("10:00"));
    Equal<double?>(null, parsed.FeelsLike); Equal<double?>(null, parsed.Humidity);
    Equal(Scene.Rain, new SceneRules().Decide(settings, parsed, At("10:00")).Scene);
});
Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;

sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
}

sealed class StubLocationPlatform(LocationAccess access, DeviceLocation position) : ILocationPlatform
{
    public List<string> Calls { get; } = [];
    public Action? OnAccess { get; init; }
    public Exception? PositionError { get; init; }
    public Task<LocationAccess> RequestAccessAsync(CancellationToken token)
    {
        Calls.Add("access"); OnAccess?.Invoke(); return Task.FromResult(access);
    }
    public Task<DeviceLocation> GetPositionAsync(CancellationToken token)
    {
        Calls.Add("position");
        return PositionError is null ? Task.FromResult(position) : Task.FromException<DeviceLocation>(PositionError);
    }
}
