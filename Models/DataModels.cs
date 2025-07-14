using System;

namespace MsfsOfpLog.Models
{
    public class GpsFixData
    {
        public DateTime Timestamp { get; set; }
        public string FixName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double FuelRemaining { get; set; } // in kilograms
        public double FuelRemainingPercentage { get; set; }
        public double GroundSpeed { get; set; }
        public double Altitude { get; set; }
        public double Heading { get; set; }
        
        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} - {FixName} - Fuel: {FuelRemaining:F2} kg ({FuelRemainingPercentage:F1}%) - Alt: {Altitude:F0} ft";
        }
    }
    
    public class AircraftData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double FuelTotalQuantity { get; set; }
        public double FuelTotalCapacity { get; set; }
        public double GroundSpeed { get; set; }
        public double Altitude { get; set; }
        public double Heading { get; set; }
        public string AircraftTitle { get; set; } = string.Empty;
    }
    
    public class GpsFix
    {
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double ToleranceNM { get; set; } = 0.5; // Default tolerance in nautical miles
    }
}
