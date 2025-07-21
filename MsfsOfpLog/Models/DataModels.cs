using static System.Globalization.CultureInfo;
using MsfsOfpLog.Services;

namespace MsfsOfpLog.Models
{
    public record GpsFixData
    {
        public DateTime Timestamp { get; init; }
        public string FixName { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double FuelRemaining { get; init; } // in kilograms (will be displayed as tonnes)
        public double FuelRemainingPercentage { get; init; }
        public double GroundSpeed { get; init; } // GS
        public double Altitude { get; init; }
        public double Heading { get; init; }
        public double TrueAirspeed { get; init; } // TAS
        public double MachNumber { get; init; } // MN
        public double OutsideAirTemperature { get; init; } // OAT in Celsius
        public double FuelBurnRate { get; init; } // Fuel flow rate in kg/hr (for calculations)
        public double ActualBurn { get; init; } // ABRN - cumulative fuel consumed since takeoff in kg
        public int DistanceFromPrevious { get; set; }

        public int FlightLevel => (int)Math.Round(Altitude / 100); // Convert altitude to flight level in hundreds of feet
        
        // Default constructor for backward compatibility
        public GpsFixData() { }
        
        // Constructor that takes aircraft data and creates a GPS fix
        public GpsFixData(AircraftData aircraftData, DateTime timestamp, string fixName)
        {
            Timestamp = timestamp;
            FixName = fixName;
            Latitude = aircraftData.Latitude;
            Longitude = aircraftData.Longitude;
            FuelRemaining = FuelConverter.GallonsToKgInt(aircraftData.FuelTotalQuantity); // Convert from gallons to kg
            FuelRemainingPercentage = aircraftData.FuelRemainingPercentage;
            GroundSpeed = aircraftData.GroundSpeed;
            Altitude = aircraftData.Altitude;
            Heading = aircraftData.Heading;
            TrueAirspeed = aircraftData.TrueAirspeed;
            MachNumber = aircraftData.MachNumber;
            OutsideAirTemperature = aircraftData.OutsideAirTemperature;
            FuelBurnRate = aircraftData.FuelBurnRate;
            ActualBurn = aircraftData.ActualBurn;
        }
        
        public override string ToString()
            => $"{Timestamp:yyyy-MM-dd HH:mm:ss}Z - {FixName} - Fuel: {FuelRemaining.KgToTonnesString()} t ({FuelRemainingPercentage.ToDecString(1)}%) - Alt: {Altitude.ToDecString(0)} ft";
        public string LongitudeString
        {
            get
            {
                var lonDeg = (int)Math.Abs(Longitude);
                var lonMin = (Math.Abs(Longitude) - lonDeg) * 60;
                var lonDir = Longitude >= 0 ? "E" : "W";
                return $"{lonDir}{lonDeg:D3}{lonMin.ToString("F1", InvariantCulture)}";

            }
        }
        public string LatitudeString
        {
            get
            {
                var latDeg = (int)Math.Abs(Latitude);
                var latMin = (Math.Abs(Latitude) - latDeg) * 60;
                var latDir = Latitude >= 0 ? "N" : "S";
                return $"{latDir}{latDeg:D2}{latMin.ToString("F1", InvariantCulture)}";
            }
        }
    }

    public record AircraftData
    {
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double FuelTotalQuantity { get; init; }
        public double FuelTotalCapacity { get; init; }
        public double GroundSpeed { get; init; } // GS
        public double Altitude { get; init; }
        public double AltitudeStandard { get; init; }
        public double AltitudeAGL { get; init; } // Altitude above ground level
        public double VerticalSpeed { get; init; } // Vertical speed in feet per minute
        public double GearPosition { get; init; } // Landing gear position (0 = up, 1 = down)
        public double Heading { get; init; }
        public double TrueAirspeed { get; init; } // TAS
        public double MachNumber { get; init; } // MN
        public double OutsideAirTemperature { get; init; } // OAT
        public double FuelBurnRate { get; init; } // Fuel flow rate in kg/hr (for calculations)
        public double ActualBurn { get; init; } // ABRN - cumulative fuel consumed since takeoff in kg
        public string AircraftTitle { get; init; } = string.Empty;

        /// <summary>
        /// Calculate fuel remaining percentage based on total quantity and capacity
        /// </summary>
        public double FuelRemainingPercentage => FuelTotalCapacity > 0 ?
            (FuelTotalQuantity / FuelTotalCapacity) * 100 : 0;

        public Position Position => new(Latitude, Longitude);
        public int FlightLevel => (int)Math.Round(Altitude / 100); // Convert altitude to flight level in hundreds of feet
    }

    public record GpsFix
    {
        public string Name { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double ToleranceNM { get; init; } = 0.5; // Default tolerance in nautical miles

        public Position Position => new(Latitude, Longitude);
    }
}
