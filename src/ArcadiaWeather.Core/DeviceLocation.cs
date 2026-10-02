using System.Globalization;

namespace ArcadiaWeather.Core;

public enum LocationAccess { Allowed, Denied, Unspecified }

public sealed record DeviceLocation(double Latitude, double Longitude, double AccuracyMeters, DateTimeOffset Timestamp, string Source)
{
    public string AccuracyDescription => AccuracyMeters >= 1000
        ? string.Create(CultureInfo.InvariantCulture, $"±{AccuracyMeters / 1000:0.#} km")
        : string.Create(CultureInfo.InvariantCulture, $"±{Math.Ceiling(AccuracyMeters):0} m");

    public void Validate(DateTimeOffset now)
    {
        if (!double.IsFinite(Latitude) || Latitude is < -90 or > 90 || !double.IsFinite(Longitude) || Longitude is < -180 or > 180
            || !double.IsFinite(AccuracyMeters) || AccuracyMeters < 0)
            throw new LocationUnavailableException("Windows returned an invalid position. Try again or enter coordinates manually.");
        if (now - Timestamp > TimeSpan.FromMinutes(5) || Timestamp - now > TimeSpan.FromMinutes(1))
            throw new LocationUnavailableException("Windows returned an old location. Try again when a fresh position is available.");
    }
}

public sealed class LocationUnavailableException(string message, bool openSettings = false) : Exception(message)
{
    public bool OpenSettings { get; } = openSettings;
}

public interface ILocationPlatform
{
    Task<LocationAccess> RequestAccessAsync(CancellationToken token);
    Task<DeviceLocation> GetPositionAsync(CancellationToken token);
}

/// <summary>Access is always requested first; a denied request never queries the device.</summary>
public sealed class DeviceLocationReader(ILocationPlatform platform)
{
    public async Task<DeviceLocation> ReadAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var access = await platform.RequestAccessAsync(token);
        token.ThrowIfCancellationRequested();
        if (access == LocationAccess.Denied)
            throw new LocationUnavailableException("Location access is off or denied. Open Windows location settings, allow location access for desktop apps, then try again.", true);
        if (access != LocationAccess.Allowed)
            throw new LocationUnavailableException("Windows could not confirm location permission. Keep this app in the foreground and try again.", true);
        var position = await platform.GetPositionAsync(token);
        token.ThrowIfCancellationRequested();
        position.Validate(DateTimeOffset.UtcNow);
        return position;
    }
}
