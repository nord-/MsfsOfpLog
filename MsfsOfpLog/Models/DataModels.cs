using System;

namespace MsfsOfpLog.Models
{
    public class GpsFixData
    {
        public DateTime Timestamp { get; set; }
        public string FixName { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double FuelRemaining { get; set; } // in kilograms (will be displayed as tonnes)
        public double FuelRemainingPercentage { get; set; }
        public double GroundSpeed { get; set; } // GS
        public double Altitude { get; set; }
        public double Heading { get; set; }
        public double TrueAirspeed { get; set; } // TAS
        public double MachNumber { get; set; } // MN
        public double OutsideAirTemperature { get; set; } // OAT in Celsius
        public double FuelBurnRate { get; set; } // Fuel flow rate in kg/hr (for calculations)
        public double ActualBurn { get; set; } // ABRN - cumulative fuel consumed since takeoff in kg
        
        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} - {FixName} - Fuel: {(FuelRemaining/1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} t ({FuelRemainingPercentage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%) - Alt: {Altitude.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} ft";
        }
    }
    
    public class AircraftData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double FuelTotalQuantity { get; set; }
        public double FuelTotalCapacity { get; set; }
        public double GroundSpeed { get; set; } // GS
        public double Altitude { get; set; }
        public double Heading { get; set; }
        public double TrueAirspeed { get; set; } // TAS
        public double MachNumber { get; set; } // MN
        public double OutsideAirTemperature { get; set; } // OAT
        public double FuelBurnRate { get; set; } // Fuel flow rate in kg/hr (for calculations)
        public double ActualBurn { get; set; } // ABRN - cumulative fuel consumed since takeoff in kg
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
