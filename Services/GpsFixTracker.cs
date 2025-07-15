using System;
using System.Collections.Generic;
using System.Linq;
using MsfsOfpLog.Models;

namespace MsfsOfpLog.Services
{
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
        
        public void CheckPosition(AircraftData aircraftData, bool hasBeenAirborne = false)
        {
            // Only record GPS fixes if aircraft is moving faster than minimum speed
            // AND we're not in the initial taxi phase (before takeoff)
            if (aircraftData.GroundSpeed < MinimumSpeedKnots && !hasBeenAirborne)
            {
                return; // Skip GPS fix detection during pre-flight taxi
            }
            
            foreach (var fix in _gpsFixes)
            {
                // Skip if we've already passed this fix
                if (_passedFixNames.Contains(fix.Name))
                    continue;
                
                // Calculate distance to fix
                var distance = CalculateDistance(aircraftData.Latitude, aircraftData.Longitude, fix.Latitude, fix.Longitude);
                
                // Check if we're within tolerance
                if (distance <= fix.ToleranceNM)
                {
                    var fixData = new GpsFixData
                    {
                        Timestamp = _systemClock.Now,
                        FixName = fix.Name,
                        Latitude = aircraftData.Latitude,
                        Longitude = aircraftData.Longitude,
                        FuelRemaining = aircraftData.FuelTotalQuantity, // Already in kg for mock service
                        FuelRemainingPercentage = aircraftData.FuelTotalCapacity > 0 ? (aircraftData.FuelTotalQuantity / aircraftData.FuelTotalCapacity) * 100 : 0,
                        GroundSpeed = aircraftData.GroundSpeed,
                        Altitude = aircraftData.Altitude,
                        Heading = aircraftData.Heading,
                        TrueAirspeed = aircraftData.TrueAirspeed,
                        MachNumber = aircraftData.MachNumber,
                        OutsideAirTemperature = aircraftData.OutsideAirTemperature,
                        FuelBurnRate = aircraftData.FuelBurnRate,
                        ActualBurn = aircraftData.ActualBurn
                    };
                    
                    _passedFixes.Add(fixData);
                    _passedFixNames.Add(fix.Name);
                    
                    Console.WriteLine($"✅ Passed GPS fix: {fixData.FixName} at {fixData.Timestamp:HH:mm:ss} - Speed: {fixData.GroundSpeed:F0} kts");
                    FixPassed?.Invoke(this, fixData);
                }
            }
        }
        
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula to calculate distance in nautical miles
            const double R = 3440.065; // Earth's radius in nautical miles
            
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }
        
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }
        
        public IReadOnlyList<GpsFixData> GetPassedFixes()
        {
            return _passedFixes.AsReadOnly();
        }
        
        public void Reset()
        {
            _passedFixes.Clear();
            _passedFixNames.Clear();
            Console.WriteLine("GPS fix tracker reset");
        }
        
        public void AddPassedFix(GpsFixData fixData)
        {
            _passedFixes.Add(fixData);
            _passedFixNames.Add(fixData.FixName);
            Console.WriteLine($"Added manual GPS fix: {fixData.FixName} at {fixData.Timestamp:HH:mm:ss}");
            FixPassed?.Invoke(this, fixData);
        }
    }
}
