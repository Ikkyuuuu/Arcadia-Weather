using Windows.Devices.Geolocation;

namespace ArcadiaWeather.Infrastructure;

/// <summary>One foreground lookup through Windows Location Services; no background tracking.</summary>
public sealed class WindowsLocationPlatform : ILocationPlatform
{
    public async Task<LocationAccess> RequestAccessAsync(CancellationToken token)
    {
        // This method must be entered on the foreground window's UI thread.
        var access = await Geolocator.RequestAccessAsync().AsTask(token);
        return access switch
        {
            GeolocationAccessStatus.Allowed => LocationAccess.Allowed,
            GeolocationAccessStatus.Denied => LocationAccess.Denied,
            _ => LocationAccess.Unspecified
        };
    }

    public async Task<DeviceLocation> GetPositionAsync(CancellationToken token)
    {
        var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.High, DesiredAccuracyInMeters = 50 };
        // Do not use AllowFallbackToConsentlessPositions: always respect the access decision above.
        var position = await locator.GetGeopositionAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30)).AsTask(token);
        var coordinate = position.Coordinate;
        var point = coordinate.Point.Position;
        string source = coordinate.PositionSource switch
        {
            PositionSource.Satellite => "GPS / satellite",
            PositionSource.WiFi => "Wi-Fi estimate",
            PositionSource.Cellular => "Cellular estimate",
            PositionSource.IPAddress => "IP address estimate",
            PositionSource.Default => "Windows default location",
            PositionSource.Obfuscated => "Approximate Windows location",
            _ => "Windows location"
        };
        return new(point.Latitude, point.Longitude, coordinate.Accuracy, coordinate.Timestamp, source);
    }
}
