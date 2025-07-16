using MsfsOfpLog.Models;

namespace MsfsOfpLog.Services;

public class GpsFixTracker
{
    private readonly List<GpsFix> _gpsFixes;
    private readonly List<GpsFixData> _passedFixes;
    private readonly HashSet<string> _passedFixNames;
    private readonly ISystemClock _systemClock;
    private const double MinimumSpeedKnots = 45.0; // Minimum speed to record GPS fixes
    
    public event EventHandler<GpsFixData>? FixPassed;
    
    public GpsFixTracker(ISystemClock? systemClock = null)
    {
        _systemClock = systemClock ?? new SystemClock();
        _gpsFixes = new List<GpsFix>();
        _passedFixes = new List<GpsFixData>();
        _passedFixNames = new HashSet<string>();
    }
    
    public void AddGpsFix(GpsFix fix)
    {
        _gpsFixes.Add(fix);
        Console.WriteLine($"Added GPS fix: {fix.Name} at {fix.Latitude:F6}, {fix.Longitude:F6}");
    }
    
    public void LoadGpsFixesFromRoute(string routeString)
    {
        // Parse route string (e.g., "LOWW DCT VIE DCT RIVER DCT LOWG")
        var parts = routeString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            if (part != "DCT" && !string.IsNullOrWhiteSpace(part))
            {
                // In a real implementation, you'd look up fix coordinates from a navigation database
                // For now, we'll add placeholder fixes
                AddGpsFix(new GpsFix { Name = part, Latitude = 0, Longitude = 0 });
            }
        }
    }

    public bool CheckPosition(AircraftData aircraftData, bool hasBeenAirborne, double distanceFromLast)
    {
        // Only record GPS fixes if aircraft is moving faster than minimum speed
        // AND we're not in the initial taxi phase (before takeoff)
        if (aircraftData.GroundSpeed < MinimumSpeedKnots && !hasBeenAirborne)
        {
            return false; // Skip GPS fix detection during pre-flight taxi
        }

        foreach (var fix in _gpsFixes)
        {
            // Skip if we've already passed this fix
            if (_passedFixNames.Contains(fix.Name))
                continue;

            // Calculate distance to fix
            var distance = CalculateDistance(aircraftData.Position, fix.Position);

            // Check if we're within tolerance
            if (distance <= fix.ToleranceNM)
            {
                var fixData = new GpsFixData(aircraftData, _systemClock.Now, fix.Name);
                if (_passedFixes.Count > 0)
                {
                    fixData.DistanceFromPrevious = (int)Math.Round(distanceFromLast);
                }

                _passedFixes.Add(fixData);
                _passedFixNames.Add(fix.Name);

                Console.WriteLine($"✅ Passed GPS fix: {fixData.FixName} at {fixData.Timestamp:HH:mm:ss}Z - Speed: {fixData.GroundSpeed:F0} kts");
                FixPassed?.Invoke(this, fixData);
                return true; // Fix passed, no need to check further
            }
        }
        // No fix passed
        return false;
    }
    
    public static double CalculateDistance(Position position1, Position position2)
    {
        // Haversine formula to calculate distance in nautical miles
        const double R = 3440.065; // Earth's radius in nautical miles
        
        var dLat = ToRadians(position2.Latitude - position1.Latitude);
        var dLon = ToRadians(position2.Longitude - position1.Longitude);
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(position1.Latitude)) * Math.Cos(ToRadians(position2.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }
    
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;        
    
    public IReadOnlyList<GpsFixData> GetPassedFixes() => _passedFixes.AsReadOnly();
    
    public void Reset()
    {
        _passedFixes.Clear();
        _passedFixNames.Clear();
        Console.WriteLine("GPS fix tracker reset");
    }
    
    public void AddPassedFix(GpsFixData fixData)
    {
        if (_passedFixNames.Contains(fixData.FixName))
        {
            Console.WriteLine($"Fix {fixData.FixName} already passed, skipping addition.");
            return;
        }
        _passedFixes.Add(fixData);
        _passedFixNames.Add(fixData.FixName);
        Console.WriteLine($"Added manual GPS fix: {fixData.FixName} at {fixData.Timestamp:HH:mm:ss}");
        FixPassed?.Invoke(this, fixData);
    }
}

public struct Position
{
    public double Latitude { get; }
    public double Longitude { get; }
    
    public Position(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
    
    public override string ToString() => $"{Latitude:F6}, {Longitude:F6}";
}