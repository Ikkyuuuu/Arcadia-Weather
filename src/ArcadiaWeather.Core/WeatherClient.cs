using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace ArcadiaWeather.Core;

public sealed class WeatherClient(HttpClient http)
{
    public async Task<WeatherSnapshot> FetchAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        string query = string.Create(CultureInfo.InvariantCulture,
            $"https://api.open-meteo.com/v1/forecast?latitude={settings.Latitude}&longitude={settings.Longitude}&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,cloud_cover,rain,showers&daily=sunrise,sunset&timezone=auto&timeformat=unixtime&forecast_days=3");
        using var response = await http.GetAsync(query, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Parse(doc.RootElement, settings, DateTimeOffset.UtcNow);
    }

    public static WeatherSnapshot Parse(JsonElement root, AppSettings settings, DateTimeOffset fetchedAt)
    {
        var current = root.GetProperty("current");
        string timezone = root.GetProperty("timezone").GetString() ?? settings.TimeZoneId;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        double Number(string name) => current.GetProperty(name).GetDouble();
        double? OptionalNumber(string name) => current.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;
        var clouds = Number("cloud_cover");
        var rain = Number("rain");
        var showers = Number("showers");
        var temperature = Number("temperature_2m");
        if (!double.IsFinite(clouds) || clouds is < 0 or > 100 || !double.IsFinite(rain) || rain < 0 || !double.IsFinite(showers) || showers < 0 || !double.IsFinite(temperature))
            throw new FormatException("The weather service returned invalid conditions.");
        var sun = new List<SunTimes>();
        var daily = root.GetProperty("daily");
        var rises = daily.GetProperty("sunrise");
        var sets = daily.GetProperty("sunset");
        for (int i = 0; i < Math.Min(rises.GetArrayLength(), sets.GetArrayLength()); i++)
        {
            if (rises[i].ValueKind != JsonValueKind.Number || sets[i].ValueKind != JsonValueKind.Number
                || !rises[i].TryGetInt64(out var rise) || !sets[i].TryGetInt64(out var set) || rise == 0 || set <= rise) continue;
            var sunrise = DateTimeOffset.FromUnixTimeSeconds(rise);
            var sunset = DateTimeOffset.FromUnixTimeSeconds(set);
            sun.Add(new(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(sunrise, zone).DateTime), sunrise, sunset));
        }
        return new(settings.Latitude, settings.Longitude, timezone, fetchedAt,
            DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64()),
            current.GetProperty("weather_code").GetInt32(), clouds, rain, showers, temperature, sun)
        {
            FeelsLike = OptionalNumber("apparent_temperature"),
            Humidity = OptionalNumber("relative_humidity_2m") is double humidity && humidity is >= 0 and <= 100 ? humidity : null
        };
    }

    public async Task<List<City>> SearchAsync(string query, CancellationToken token)
    {
        if (query.Trim().Length < 2) return [];
        string uri = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query.Trim())}&count=8&language=en&format=json";
        using var response = await http.GetAsync(uri, token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!doc.RootElement.TryGetProperty("results", out var results)) return [];
        return results.EnumerateArray().Where(e => e.TryGetProperty("timezone", out _)).Select(e => new City(
            e.GetProperty("name").GetString()!, e.TryGetProperty("country", out var country) ? country.GetString()! : "",
            e.TryGetProperty("admin1", out var admin) ? admin.GetString() : null,
            e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble(), e.GetProperty("timezone").GetString()!)).ToList();
    }
}
