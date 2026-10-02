namespace ArcadiaWeather.Core;

/// <summary>Stateful cloud hysteresis, with rain above every time-of-day scene.</summary>
public sealed class SceneRules
{
    private bool _cloudy;
    public void Reset() => _cloudy = false;

    public SceneDecision Decide(AppSettings settings, WeatherSnapshot? weather, DateTimeOffset now)
    {
        var phase = GetPhase(settings, weather, now);
        bool fresh = IsFresh(settings, weather, now);
        if (fresh)
        {
            _cloudy = _cloudy ? weather!.CloudCover > settings.CloudExitPercent : weather!.CloudCover >= settings.CloudEnterPercent;
            if (IsWet(weather)) return new(Scene.Rain, phase, "Rain / wet weather · overrides every time-of-day scene", true);
        }
        else _cloudy = false;

        if (phase == DayPhase.Night) return new(Scene.Night, phase, fresh ? "Nighttime · clouds do not override night" : "Nighttime · weather unavailable or stale", fresh);
        if (_cloudy) return new(Scene.Cloudy, phase, $"{weather!.CloudCover:0}% cloud cover · cloudy override", true);
        return phase == DayPhase.Evening
            ? new(Scene.Evening, phase, fresh ? "Evening light around sunset" : "Evening · using the time schedule", fresh)
            : new(Scene.Day, phase, fresh ? "Morning / afternoon in your location" : "Daylight · using the time schedule", fresh);
    }

    public static bool IsWet(WeatherSnapshot weather) => weather.WeatherCode is
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 or 95 or 96 or 97 or 99
        || weather.Rain > 0 || weather.Showers > 0;

    public static bool MatchesLocation(AppSettings settings, WeatherSnapshot? weather) => weather is not null
        && Math.Abs(settings.Latitude - weather.Latitude) < 0.001
        && Math.Abs(settings.Longitude - weather.Longitude) < 0.001;

    public static bool IsFresh(AppSettings settings, WeatherSnapshot? weather, DateTimeOffset now) =>
        MatchesLocation(settings, weather)
        && now - weather!.FetchedAt >= TimeSpan.FromMinutes(-5)
        && now - weather.FetchedAt <= AppSettings.WeatherMaxAge
        && now - weather.ObservedAt >= TimeSpan.FromMinutes(-15)
        && now - weather.ObservedAt <= AppSettings.WeatherMaxAge;

    public static DayPhase GetPhase(AppSettings settings, WeatherSnapshot? weather, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var date = DateOnly.FromDateTime(local.DateTime);
        var sun = MatchesLocation(settings, weather) ? weather!.Sun?.FirstOrDefault(s => s.Date == date) : null;
        if (sun is not null && sun.Sunset > sun.Sunrise)
        {
            var evening = sun.Sunset.AddMinutes(-settings.EveningMinutesBeforeSunset);
            if (evening < sun.Sunrise) evening = sun.Sunrise;
            if (now < sun.Sunrise || now >= sun.Sunset.AddMinutes(settings.NightMinutesAfterSunset)) return DayPhase.Night;
            return now >= evening ? DayPhase.Evening : DayPhase.Day;
        }
        // Cached astronomical times are usable without fresh weather, but never for the wrong date.
        var hour = local.TimeOfDay.TotalHours;
        if (hour < settings.FallbackDayHour || hour >= settings.FallbackNightHour) return DayPhase.Night;
        return hour >= settings.FallbackEveningHour ? DayPhase.Evening : DayPhase.Day;
    }
}
