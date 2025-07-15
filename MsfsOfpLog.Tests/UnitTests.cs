using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using MsfsOfpLog.Models;
using MsfsOfpLog.Services;

namespace MsfsOfpLog.Tests
{
    public class UnitTests
    {
        [Fact]
        public void SystemClock_Should_AdvanceTime_Correctly()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0));
            var initialTime = testClock.Now;
            
            // Act
            testClock.AddMinutes(30);
            var after30Minutes = testClock.Now;
            
            // Assert
            Assert.Equal(initialTime.AddMinutes(30), after30Minutes);
        }
        
        [Fact]
        public void GpsFixTracker_Should_DetectFixPassed_WhenAircraftAtLocation()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0));
            var tracker = new GpsFixTracker(testClock);
            
            bool fixPassed = false;
            tracker.FixPassed += (sender, fixData) =>
            {
                fixPassed = true;
            };
            
            // Add a GPS fix
            tracker.AddGpsFix(new GpsFix
            {
                Name = "VANES",
                Latitude = 36.385,
                Longitude = 27.731,
                ToleranceNM = 1.0
            });
            
            // Simulate aircraft at the fix location
            var aircraftData = new AircraftData
            {
                Latitude = 36.385,
                Longitude = 27.731,
                FuelTotalQuantity = 8750,
                FuelTotalCapacity = 9600,
                GroundSpeed = 460,
                Altitude = 38000,
                Heading = 10,
                TrueAirspeed = 465,
                MachNumber = 0.78,
                OutsideAirTemperature = -45,
                FuelBurnRate = 1600,
                ActualBurn = 50,
                AircraftTitle = "Test Aircraft"
            };
            
            // Act
            tracker.CheckPosition(aircraftData, true);
            
            // Assert
            Assert.True(fixPassed);
            Assert.Single(tracker.GetPassedFixes());
        }
        
        [Fact]
        public void DataLogger_Should_GenerateOFPSummary_WithComprehensiveFlightPlan()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0));
            using var memoryStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, memoryStream);
            
            // Create a comprehensive flight plan with all waypoints
            var fixes = CreateComprehensiveFlightPlan(testClock);
            
            // Act
            var exception = Record.Exception(() => 
                dataLogger.SaveFlightSummary(fixes.AsReadOnly(), "Airbus A320neo - Complete LGRP to ESSA"));
            
            // Assert
            Assert.Null(exception);
            Assert.Equal(20, fixes.Count); // Verify we have all 20 waypoints
            
            // Verify the output was written to the stream
            memoryStream.Position = 0;
            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.Contains("15JUL2025 LGRP-ESSA", output);
            Assert.Contains("OFP 1 DIAGORAS-ARLANDA", output);
            Assert.Contains("Aircraft: Airbus A320neo - Complete LGRP to ESSA", output);
            Assert.Contains("DIAGORAS", output); // Position name for takeoff
            Assert.Contains("LGRP", output); // Display name for takeoff (without "TAKEOFF ")
            Assert.Contains("ARLANDA", output); // Position name for landing
            Assert.Contains("ESSA", output); // Display name for landing (without "LANDING ")
        }
        
        [Fact]
        public void FlightSimulation_Should_ProcessWaypoints_InCorrectOrder()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0));
            var tracker = new GpsFixTracker(testClock);
            using var memoryStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, memoryStream);
            
            var passedFixes = new List<GpsFixData>();
            
            tracker.FixPassed += (sender, fixData) =>
            {
                passedFixes.Add(fixData);
            };
            
            // Add route waypoints
            tracker.AddGpsFix(new GpsFix { Name = "VANES", Latitude = 36.385, Longitude = 27.731, ToleranceNM = 1.0 });
            tracker.AddGpsFix(new GpsFix { Name = "ETERU", Latitude = 36.468, Longitude = 27.060, ToleranceNM = 1.0 });
            
            // Simulate takeoff
            tracker.AddPassedFix(new GpsFixData
            {
                Timestamp = testClock.Now,
                FixName = "TAKEOFF LGRP",
                Latitude = 36.400,
                Longitude = 28.080,
                FuelRemaining = 8800,
                FuelRemainingPercentage = 91.7,
                GroundSpeed = 80,
                Altitude = 500,
                Heading = 240,
                TrueAirspeed = 85,
                MachNumber = 0.12,
                OutsideAirTemperature = 15,
                FuelBurnRate = 1800,
                ActualBurn = 0
            });
            
            // Simulate flight progression
            testClock.AddMinutes(15);
            var aircraftData = new AircraftData
            {
                Latitude = 36.385,
                Longitude = 27.731,
                FuelTotalQuantity = 8750,
                FuelTotalCapacity = 9600,
                GroundSpeed = 460,
                Altitude = 38000,
                Heading = 10,
                TrueAirspeed = 465,
                MachNumber = 0.78,
                OutsideAirTemperature = -45,
                FuelBurnRate = 1600,
                ActualBurn = 50,
                AircraftTitle = "Test Aircraft"
            };
            
            tracker.CheckPosition(aircraftData, true);
            
            // Move to next waypoint
            testClock.AddMinutes(30);
            aircraftData.Latitude = 36.468;
            aircraftData.Longitude = 27.060;
            aircraftData.FuelTotalQuantity = 8700;
            aircraftData.ActualBurn = 100;
            
            tracker.CheckPosition(aircraftData, true);
            
            // Simulate landing
            testClock.AddMinutes(60);
            tracker.AddPassedFix(new GpsFixData
            {
                Timestamp = testClock.Now,
                FixName = "LANDING ESSA",
                Latitude = 59.656,
                Longitude = 17.923,
                FuelRemaining = 8340,
                FuelRemainingPercentage = 86.9,
                GroundSpeed = 50,
                Altitude = 138,
                Heading = 60,
                TrueAirspeed = 55,
                MachNumber = 0.08,
                OutsideAirTemperature = 10,
                FuelBurnRate = 500,
                ActualBurn = 460
            });
            
            // Act
            var exception = Record.Exception(() => 
                dataLogger.SaveFlightSummary(tracker.GetPassedFixes(), "Test Flight A320neo"));
            
            // Assert
            Assert.Null(exception);
            Assert.Equal(4, tracker.GetPassedFixes().Count); // TAKEOFF, VANES, ETERU, LANDING
            
            // Verify the output was written to the stream
            memoryStream.Position = 0;
            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            Assert.Contains("15JUL2025 LGRP-ESSA", output);
            Assert.Contains("Aircraft: Test Flight A320neo", output);
            Assert.Contains("DIAGORAS", output); // Position name for takeoff
            Assert.Contains("LGRP", output); // Display name for takeoff (without "TAKEOFF ")
            Assert.Contains("VANES", output);
            Assert.Contains("ETERU", output);
            Assert.Contains("ARLANDA", output); // Position name for landing
            Assert.Contains("ESSA", output); // Display name for landing (without "LANDING ")
        }
        
        private static List<GpsFixData> CreateComprehensiveFlightPlan(TestSystemClock testClock)
        {
            return new List<GpsFixData>
            {
                new GpsFixData { Timestamp = testClock.Now, FixName = "TAKEOFF LGRP", Latitude = 36.406389, Longitude = 28.086111, FuelRemaining = 8800, FuelRemainingPercentage = 91.7, GroundSpeed = 80, Altitude = 19, Heading = 240, TrueAirspeed = 85, MachNumber = 0.12, OutsideAirTemperature = 15, FuelBurnRate = 1800, ActualBurn = 0 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(8), FixName = "VANES", Latitude = 36.385000, Longitude = 27.731667, FuelRemaining = 8750, FuelRemainingPercentage = 91.1, GroundSpeed = 280, Altitude = 11300, Heading = 320, TrueAirspeed = 290, MachNumber = 0.45, OutsideAirTemperature = 5, FuelBurnRate = 2000, ActualBurn = 50 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(15), FixName = "ETERU", Latitude = 36.468056, Longitude = 27.060000, FuelRemaining = 8700, FuelRemainingPercentage = 90.6, GroundSpeed = 420, Altitude = 22800, Heading = 315, TrueAirspeed = 440, MachNumber = 0.65, OutsideAirTemperature = -15, FuelBurnRate = 1800, ActualBurn = 100 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(20), FixName = "GILOS", Latitude = 36.487500, Longitude = 26.900278, FuelRemaining = 8650, FuelRemainingPercentage = 90.1, GroundSpeed = 450, Altitude = 25000, Heading = 315, TrueAirspeed = 480, MachNumber = 0.72, OutsideAirTemperature = -25, FuelBurnRate = 1700, ActualBurn = 150 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(25), FixName = "ADESO", Latitude = 36.533333, Longitude = 26.511111, FuelRemaining = 8600, FuelRemainingPercentage = 89.6, GroundSpeed = 460, Altitude = 28600, Heading = 315, TrueAirspeed = 490, MachNumber = 0.75, OutsideAirTemperature = -35, FuelBurnRate = 1650, ActualBurn = 200 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(32), FixName = "AKINA", Latitude = 36.980278, Longitude = 26.248611, FuelRemaining = 8550, FuelRemainingPercentage = 89.1, GroundSpeed = 470, Altitude = 33200, Heading = 320, TrueAirspeed = 500, MachNumber = 0.77, OutsideAirTemperature = -45, FuelBurnRate = 1600, ActualBurn = 250 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(40), FixName = "RIGRO", Latitude = 37.590000, Longitude = 25.874167, FuelRemaining = 8500, FuelRemainingPercentage = 88.5, GroundSpeed = 480, Altitude = 38000, Heading = 325, TrueAirspeed = 515, MachNumber = 0.80, OutsideAirTemperature = -55, FuelBurnRate = 1550, ActualBurn = 300 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(60), FixName = "KOROS", Latitude = 39.099722, Longitude = 24.915833, FuelRemaining = 8400, FuelRemainingPercentage = 87.5, GroundSpeed = 485, Altitude = 38000, Heading = 330, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 400 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(70), FixName = "GIKAS", Latitude = 39.499722, Longitude = 24.666944, FuelRemaining = 8350, FuelRemainingPercentage = 86.9, GroundSpeed = 485, Altitude = 38000, Heading = 335, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 450 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(85), FixName = "PEREN", Latitude = 40.596667, Longitude = 23.967778, FuelRemaining = 8280, FuelRemainingPercentage = 86.3, GroundSpeed = 485, Altitude = 38000, Heading = 340, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 520 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(95), FixName = "REFUS", Latitude = 41.276389, Longitude = 23.805000, FuelRemaining = 8220, FuelRemainingPercentage = 85.6, GroundSpeed = 485, Altitude = 38000, Heading = 345, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 580 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(100), FixName = "ATFIR", Latitude = 41.401667, Longitude = 23.774722, FuelRemaining = 8200, FuelRemainingPercentage = 85.4, GroundSpeed = 485, Altitude = 38000, Heading = 345, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 600 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(120), FixName = "LOMOS", Latitude = 43.833333, Longitude = 23.250000, FuelRemaining = 8100, FuelRemainingPercentage = 84.4, GroundSpeed = 485, Altitude = 38000, Heading = 350, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 700 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(150), FixName = "NARKA", Latitude = 47.248333, Longitude = 21.860000, FuelRemaining = 7950, FuelRemainingPercentage = 82.8, GroundSpeed = 485, Altitude = 38000, Heading = 355, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 850 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(165), FixName = "KEKED", Latitude = 48.523056, Longitude = 21.291389, FuelRemaining = 7850, FuelRemainingPercentage = 81.8, GroundSpeed = 485, Altitude = 38000, Heading = 005, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 950 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(180), FixName = "LENOV", Latitude = 49.336389, Longitude = 21.010278, FuelRemaining = 7750, FuelRemainingPercentage = 80.7, GroundSpeed = 485, Altitude = 38000, Heading = 010, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 1050 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(240), FixName = "PENOR", Latitude = 55.638611, Longitude = 17.161389, FuelRemaining = 7500, FuelRemainingPercentage = 78.1, GroundSpeed = 485, Altitude = 38000, Heading = 025, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 1300 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(265), FixName = "ARMOD", Latitude = 57.500833, Longitude = 17.346111, FuelRemaining = 7400, FuelRemainingPercentage = 77.1, GroundSpeed = 485, Altitude = 38000, Heading = 030, TrueAirspeed = 520, MachNumber = 0.81, OutsideAirTemperature = -56, FuelBurnRate = 1550, ActualBurn = 1400 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(285), FixName = "NILUG", Latitude = 58.815833, Longitude = 17.884722, FuelRemaining = 7350, FuelRemainingPercentage = 76.6, GroundSpeed = 450, Altitude = 15800, Heading = 035, TrueAirspeed = 480, MachNumber = 0.75, OutsideAirTemperature = -25, FuelBurnRate = 1700, ActualBurn = 1450 },
                new GpsFixData { Timestamp = testClock.Now.AddMinutes(300), FixName = "LANDING ESSA", Latitude = 59.651944, Longitude = 17.918611, FuelRemaining = 7300, FuelRemainingPercentage = 76.0, GroundSpeed = 50, Altitude = 138, Heading = 060, TrueAirspeed = 55, MachNumber = 0.08, OutsideAirTemperature = 10, FuelBurnRate = 500, ActualBurn = 1500 }
            };
        }
    }
}
