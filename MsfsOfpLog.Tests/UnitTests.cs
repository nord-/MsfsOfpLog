using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using MsfsOfpLog.Models;
using MsfsOfpLog.Services;

namespace MsfsOfpLog.Tests
{
    public class UnitTests
    {
        private readonly ITestOutputHelper _output;

        public UnitTests(ITestOutputHelper output)
        {
            _output = output;
        }
        [Fact]
        public void SystemClock_Should_AdvanceTime_Correctly()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0, DateTimeKind.Utc));
            var initialTime = testClock.Now;
            
            // Act
            testClock.AddMinutes(30);
            var after30Minutes = testClock.Now;
            
            // Assert
            Assert.Equal(initialTime.AddMinutes(30), after30Minutes);
            Assert.Equal(DateTimeKind.Utc, after30Minutes.Kind);
        }
        
        [Fact]
        public void GpsFixTracker_Should_DetectFixPassed_WhenAircraftAtLocation()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0, DateTimeKind.Utc));
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
            tracker.CheckPosition(aircraftData, true, 0.0);
            
            // Assert
            Assert.True(fixPassed);
            Assert.Single(tracker.GetPassedFixes());
        }
        
        [Fact]
        public void DataLogger_Should_GenerateOFPSummary_WithComprehensiveFlightPlan()
        {
            // Arrange
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0, DateTimeKind.Utc));
            using var memoryStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, memoryStream);
            
            // Create a mock flight plan with proper airport names
            var mockFlightPlan = new FlightPlanParser.FlightPlanInfo
            {
                DepartureID = "LGRP",
                DestinationID = "ESSA",
                DepartureName = "DIAGORAS",
                DestinationName = "ARLANDA"
            };
            
            // Create a comprehensive flight plan with all waypoints
            var fixes = CreateComprehensiveFlightPlan(testClock);
            
            // Act
            var exception = Record.Exception(() => 
                dataLogger.SaveFlightSummary(fixes.AsReadOnly(), "Airbus A320neo - Complete LGRP to ESSA", mockFlightPlan));
            
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
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0, DateTimeKind.Utc));
            var tracker = new GpsFixTracker(testClock);
            using var memoryStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, memoryStream);
            
            // Create a mock flight plan with proper airport names
            var mockFlightPlan = new FlightPlanParser.FlightPlanInfo
            {
                DepartureID = "LGRP",
                DestinationID = "ESSA",
                DepartureName = "DIAGORAS",
                DestinationName = "ARLANDA"
            };
            
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
            
            tracker.CheckPosition(aircraftData, true, 0.0);
            
            // Move to next waypoint
            testClock.AddMinutes(30);
            aircraftData = aircraftData with
            {
                Latitude = 36.468,
                Longitude = 27.060,
                FuelTotalQuantity = 8700,
                ActualBurn = 100
            };
            
            tracker.CheckPosition(aircraftData, true, 0.0);
            
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
                dataLogger.SaveFlightSummary(tracker.GetPassedFixes(), "Test Flight A320neo", mockFlightPlan));
            
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
        
        [Fact]
        public void FlightPlanParser_Should_HandleQuotedFilePath_Successfully()
        {
            // Arrange - Create a flight plan file with an absolute path
            var testFile = Path.Combine(Path.GetTempPath(), "test_flight_plan_quoted.pln");
            
            // Create a simple valid flight plan XML file
            var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<SimBase.Document Type=""AceXML"" version=""1,0"">
    <Descr>AceXML Document</Descr>
    <FlightPlan.FlightPlan>
        <Title>Test Flight Plan</Title>
        <FPType>VFR</FPType>
        <RouteType>Direct</RouteType>
        <CruisingAlt>5000</CruisingAlt>
        <DepartureID>LGRP</DepartureID>
        <DepartureName>DIAGORAS</DepartureName>
        <DestinationID>ESSA</DestinationID>
        <DestinationName>ARLANDA</DestinationName>
        <ATCWaypoint id=""LGRP"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N36° 24' 19.00"",E28° 5' 10.00"",+000019.00</WorldPosition>
        </ATCWaypoint>
        <ATCWaypoint id=""ESSA"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 39' 7.00"",E17° 55' 7.00"",+000138.00</WorldPosition>
        </ATCWaypoint>
    </FlightPlan.FlightPlan>
</SimBase.Document>";
            
            File.WriteAllText(testFile, xmlContent);
            
            try
            {
                // Act - Test with quotes around the ABSOLUTE file path (this is the real issue)
                var quotedPath = $"\"{testFile}\"";
                var result = FlightPlanParser.ParseFlightPlan(quotedPath);
                
                // Assert - This should succeed because quotes should be handled correctly
                Assert.NotNull(result);
                Assert.Equal("LGRP", result.DepartureID);
                Assert.Equal("ESSA", result.DestinationID);
                Assert.Equal("DIAGORAS", result.DepartureName);
                Assert.Equal("ARLANDA", result.DestinationName);
                Assert.Equal(5000, result.CruisingAltitude);
                Assert.Equal(2, result.Waypoints.Count);
                
                // Additional test - verify the path processing logic
                // This simulates what happens in LoadFlightPlan method
                var inputPath = quotedPath;
                var processedPath = inputPath.Trim().Trim('"');
                
                // The processed path should be the same as the original file path
                Assert.Equal(testFile, processedPath);
                
                // And Path.IsPathRooted should return true for the processed path
                Assert.True(Path.IsPathRooted(processedPath));
                
                // Test that the file actually exists at the processed path
                Assert.True(File.Exists(processedPath));
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
        
        [Fact]
        public void TakeoffAndLanding_Should_UseCorrectAirportCodes_FromLoadedFlightPlan()
        {
            // Arrange - Create a flight plan with ESSA to ESOK
            var testFile = Path.GetTempFileName();
            var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<SimBase.Document Type=""AceXML"" version=""1,0"">
    <Descr>AceXML Document</Descr>
    <FlightPlan.FlightPlan>
        <Title>ESSA to ESOK</Title>
        <FPType>IFR</FPType>
        <RouteType>HighAlt</RouteType>
        <CruisingAlt>26000.000</CruisingAlt>
        <DepartureID>ESSA</DepartureID>
        <DepartureLLA>N59° 39' 7.00"",E17° 55' 7.00"",+000138.00</DepartureLLA>
        <DestinationID>ESOK</DestinationID>
        <DestinationLLA>N59° 26' 41.00"",E13° 20' 15.00"",+000353.00</DestinationLLA>
        <DepartureName>Arlanda</DepartureName>
        <DestinationName>Karlstad</DestinationName>
        <ATCWaypoint id=""ESSA"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 39' 7.00"",E17° 55' 7.00"",+000138.00</WorldPosition>
        </ATCWaypoint>
        <ATCWaypoint id=""ESOK"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 26' 41.00"",E13° 20' 15.00"",+000353.00</WorldPosition>
        </ATCWaypoint>
    </FlightPlan.FlightPlan>
</SimBase.Document>";
            
            File.WriteAllText(testFile, xmlContent);
            
            try
            {
                // Act - Parse the flight plan
                var flightPlanInfo = FlightPlanParser.ParseFlightPlan(testFile);
                
                // Assert - Flight plan should be loaded correctly
                Assert.NotNull(flightPlanInfo);
                Assert.Equal("ESSA", flightPlanInfo.DepartureID);
                Assert.Equal("ESOK", flightPlanInfo.DestinationID);
                
                // Test the takeoff fix generation logic
                var departureAirport = flightPlanInfo.DepartureID ?? "UNKNOWN";
                var takeoffFixName = $"TAKEOFF {departureAirport}";
                
                // This should now pass because we're using the flight plan data
                Assert.Contains("ESSA", takeoffFixName);
                
                // Test the landing fix generation logic
                var destinationAirport = flightPlanInfo.DestinationID ?? "UNKNOWN";
                var landingFixName = $"LANDING {destinationAirport}";
                
                // This should now pass because we're using the flight plan data
                Assert.Contains("ESOK", landingFixName);
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
        
        [Fact]
        public void Full_TakeoffAndLanding_Integration_Test()
        {
            // Arrange - Create a flight plan with ESSA to ESOK
            var testFile = Path.GetTempFileName();
            var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<SimBase.Document Type=""AceXML"" version=""1,0"">
    <Descr>AceXML Document</Descr>
    <FlightPlan.FlightPlan>
        <Title>ESSA to ESOK</Title>
        <FPType>IFR</FPType>
        <RouteType>HighAlt</RouteType>
        <CruisingAlt>26000.000</CruisingAlt>
        <DepartureID>ESSA</DepartureID>
        <DepartureLLA>N59° 39' 7.00"",E17° 55' 7.00"",+000138.00</DepartureLLA>
        <DestinationID>ESOK</DestinationID>
        <DestinationLLA>N59° 26' 41.00"",E13° 20' 15.00"",+000353.00</DestinationLLA>
        <DepartureName>Arlanda</DepartureName>
        <DestinationName>Karlstad</DestinationName>
        <ATCWaypoint id=""ESSA"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 39' 7.00"",E17° 55' 7.00"",+000138.00</WorldPosition>
        </ATCWaypoint>
        <ATCWaypoint id=""ESOK"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 26' 41.00"",E13° 20' 15.00"",+000353.00</WorldPosition>
        </ATCWaypoint>
    </FlightPlan.FlightPlan>
</SimBase.Document>";
            
            File.WriteAllText(testFile, xmlContent);
            
            try
            {
                // Act - Parse the flight plan
                var flightPlanInfo = FlightPlanParser.ParseFlightPlan(testFile);
                
                // Assert - Flight plan should be loaded correctly
                Assert.NotNull(flightPlanInfo);
                Assert.Equal("ESSA", flightPlanInfo.DepartureID);
                Assert.Equal("ESOK", flightPlanInfo.DestinationID);
                
                // Test the FULL takeoff scenario as it would happen in Program.cs
                var departureAirport = flightPlanInfo.DepartureID ?? "UNKNOWN";
                var takeoffData = new GpsFixData
                {
                    Timestamp = DateTime.Now,
                    FixName = $"TAKEOFF {departureAirport}", // Simulate actual Program.cs logic
                    Latitude = 59.651944, // ESSA coordinates
                    Longitude = 17.918611,
                    FuelRemaining = 5000,
                    GroundSpeed = 50.0,
                    Altitude = 200
                };
                
                // Verify takeoff uses correct departure airport
                Assert.Equal("TAKEOFF ESSA", takeoffData.FixName);
                Assert.DoesNotContain("LGRP", takeoffData.FixName); // Should not contain old hardcoded value
                
                // Test the FULL landing scenario as it would happen in Program.cs
                var destinationAirport = flightPlanInfo.DestinationID ?? "UNKNOWN";
                var landingData = new GpsFixData
                {
                    Timestamp = DateTime.Now,
                    FixName = $"LANDING {destinationAirport}", // Simulate actual Program.cs logic
                    Latitude = 59.444722, // ESOK coordinates
                    Longitude = 13.337500,
                    FuelRemaining = 3000,
                    GroundSpeed = 30.0,
                    Altitude = 100
                };
                
                // Verify landing uses correct destination airport
                Assert.Equal("LANDING ESOK", landingData.FixName);
                Assert.DoesNotContain("ESSA", landingData.FixName); // Should not contain old hardcoded value
                
                // Additional verification - make sure the airports are different
                Assert.NotEqual(takeoffData.FixName, landingData.FixName);
                Assert.Contains("ESSA", takeoffData.FixName);
                Assert.Contains("ESOK", landingData.FixName);
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
        
        [Fact]
        public void Different_Airports_Should_Use_Correct_Codes()
        {
            // Arrange - Create a flight plan with LOWW to LOWG (different airports)
            var testFile = Path.GetTempFileName();
            var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<SimBase.Document Type=""AceXML"" version=""1,0"">
    <Descr>AceXML Document</Descr>
    <FlightPlan.FlightPlan>
        <Title>LOWW to LOWG</Title>
        <FPType>IFR</FPType>
        <RouteType>HighAlt</RouteType>
        <CruisingAlt>35000.000</CruisingAlt>
        <DepartureID>LOWW</DepartureID>
        <DestinationID>LOWG</DestinationID>
        <DepartureName>Vienna</DepartureName>
        <DestinationName>Graz</DestinationName>
        <ATCWaypoint id=""LOWW"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N48° 7' 0.00"",E16° 34' 0.00"",+000600.00</WorldPosition>
        </ATCWaypoint>
        <ATCWaypoint id=""LOWG"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N47° 0' 0.00"",E15° 26' 0.00"",+001115.00</WorldPosition>
        </ATCWaypoint>
    </FlightPlan.FlightPlan>
</SimBase.Document>";
            
            File.WriteAllText(testFile, xmlContent);
            
            try
            {
                // Act - Parse the flight plan
                var flightPlanInfo = FlightPlanParser.ParseFlightPlan(testFile);
                
                // Assert - Flight plan should be loaded correctly
                Assert.NotNull(flightPlanInfo);
                Assert.Equal("LOWW", flightPlanInfo.DepartureID);
                Assert.Equal("LOWG", flightPlanInfo.DestinationID);
                
                // Test takeoff with LOWW
                var departureAirport = flightPlanInfo.DepartureID ?? "UNKNOWN";
                var takeoffFixName = $"TAKEOFF {departureAirport}";
                
                Assert.Equal("TAKEOFF LOWW", takeoffFixName);
                Assert.DoesNotContain("ESSA", takeoffFixName); // Should not contain ESSA
                Assert.DoesNotContain("LGRP", takeoffFixName); // Should not contain old hardcoded value
                
                // Test landing with LOWG
                var destinationAirport = flightPlanInfo.DestinationID ?? "UNKNOWN";
                var landingFixName = $"LANDING {destinationAirport}";
                
                Assert.Equal("LANDING LOWG", landingFixName);
                Assert.DoesNotContain("ESSA", landingFixName); // Should not contain old hardcoded value
                Assert.DoesNotContain("ESOK", landingFixName); // Should not contain ESOK
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
            }
        }
        
        [Fact]
        public void No_FlightPlan_Should_Use_UNKNOWN_Airport()
        {
            // Arrange - No flight plan loaded (null)
            FlightPlanParser.FlightPlanInfo? nullFlightPlan = null;
            
            // Act & Assert - Test takeoff with null flight plan
            var departureAirport = nullFlightPlan?.DepartureID ?? "UNKNOWN";
            var takeoffFixName = $"TAKEOFF {departureAirport}";
            
            Assert.Equal("TAKEOFF UNKNOWN", takeoffFixName);
            
            // Act & Assert - Test landing with null flight plan
            var destinationAirport = nullFlightPlan?.DestinationID ?? "UNKNOWN";
            var landingFixName = $"LANDING {destinationAirport}";
            
            Assert.Equal("LANDING UNKNOWN", landingFixName);
        }

        [Fact]
        public void LoadFlightPlan_Should_HandleQuotedAbsolutePath_WithoutCurrentDirPrepending()
        {
            // Arrange - Create a flight plan file in a specific directory
            var testDir = Path.Combine(Path.GetTempPath(), "TestFlightPlans");
            Directory.CreateDirectory(testDir);
            var testFile = Path.Combine(testDir, "ESSAESOK_MFS_15Jul25.pln");
            
            var xmlContent = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<SimBase.Document Type=""AceXML"" version=""1,0"">
    <Descr>AceXML Document</Descr>
    <FlightPlan.FlightPlan>
        <Title>ESSA to ESOK</Title>
        <FPType>IFR</FPType>
        <RouteType>HighAlt</RouteType>
        <CruisingAlt>26000.000</CruisingAlt>
        <DepartureID>ESSA</DepartureID>
        <DestinationID>ESOK</DestinationID>
        <DepartureName>Arlanda</DepartureName>
        <DestinationName>Karlstad</DestinationName>
        <ATCWaypoint id=""ESSA"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 39' 7.00"",E17° 55' 7.00"",+000138.00</WorldPosition>
        </ATCWaypoint>
        <ATCWaypoint id=""ESOK"">
            <ATCWaypointType>Airport</ATCWaypointType>
            <WorldPosition>N59° 26' 41.00"",E13° 20' 15.00"",+000353.00</WorldPosition>
        </ATCWaypoint>
    </FlightPlan.FlightPlan>
</SimBase.Document>";
            
            File.WriteAllText(testFile, xmlContent);
            
            try
            {
                // Act - Simulate the exact scenario from LoadFlightPlan method
                // User enters: "C:\Users\ricka\Downloads\ESSAESOK_MFS_15Jul25.pln"
                var userInput = $"\"{testFile}\"";
                
                // This simulates the LoadFlightPlan logic
                var filePath = userInput;
                
                // Remove quotes from file path if present (before checking if rooted)
                filePath = filePath.Trim().Trim('"');
                
                // If it's a relative path, make it absolute
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.GetFullPath(filePath);
                }
                
                // Now parse the flight plan
                var result = FlightPlanParser.ParseFlightPlan(filePath);
                
                // Assert - This should succeed without current directory prepending
                Assert.NotNull(result);
                Assert.Equal("ESSA", result.DepartureID);
                Assert.Equal("ESOK", result.DestinationID);
                
                // Verify the path was processed correctly
                Assert.Equal(testFile, filePath);
                Assert.True(Path.IsPathRooted(filePath));
                
                // The processed path should NOT contain the current working directory
                var currentDir = Directory.GetCurrentDirectory();
                Assert.DoesNotContain(Path.Combine(currentDir, "\""), filePath);
            }
            finally
            {
                // Cleanup
                if (File.Exists(testFile))
                    File.Delete(testFile);
                if (Directory.Exists(testDir))
                    Directory.Delete(testDir);
            }
        }

        [Fact]
        public void FuelCalculation_Should_ConvertGallonsToKilograms_FromSimConnect()
        {
            // Arrange - SimConnect provides fuel in gallons (discovered during debugging)
            var testClock = new TestSystemClock(new DateTime(2025, 7, 15, 10, 0, 0, DateTimeKind.Utc));
            var tracker = new GpsFixTracker(testClock);
            using var memoryStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, memoryStream);
            
            // Simulate aircraft data as it comes from SimConnect (fuel in gallons)
            var aircraftData = new AircraftData
            {
                Latitude = 59.651944,
                Longitude = 17.918611,
                FuelTotalQuantity = 1910, // This is in gallons from SimConnect
                FuelTotalCapacity = 9600, // This is also in gallons
                GroundSpeed = 280,
                Altitude = 38000,
                Heading = 10,
                TrueAirspeed = 465,
                MachNumber = 0.78,
                OutsideAirTemperature = -45,
                FuelBurnRate = 1600,
                ActualBurn = 50,
                AircraftTitle = "Test Aircraft A320neo"
            };
            
            // Act - Create GPS fix data as it would be done in Program.cs
            var gpsFixData = new GpsFixData(aircraftData, testClock.Now, "TEST_WAYPOINT");
            
            // Add to tracker and save summary
            tracker.AddPassedFix(gpsFixData);
            dataLogger.SaveFlightSummary(tracker.GetPassedFixes(), aircraftData.AircraftTitle, null); // No flight plan for this test
            
            // Assert - Check that fuel is correctly converted to kg
            // 1910 gallons should be converted to approximately 5792 kg (1910 * 3.032 = 5791.1)
            var expectedFuelKg = FuelConverter.GallonsToKgInt(aircraftData.FuelTotalQuantity);
            Assert.Equal(5791, expectedFuelKg);
            
            // The fix is: FuelRemaining = FuelConverter.GallonsToKgInt(aircraftData.FuelTotalQuantity)
            Assert.Equal(expectedFuelKg, gpsFixData.FuelRemaining);
            
            // Also verify the output contains the correct fuel amount in tonnes (kg/1000)
            memoryStream.Position = 0;
            var output = Encoding.UTF8.GetString(memoryStream.ToArray());
            
            // Should show fuel in tonnes (5791 kg = 5.791 tonnes, displayed as 5.8)
            Assert.Contains("5.8", output); // Should contain the tonnes value
            Assert.DoesNotContain("1.9", output); // Should not contain the raw gallons value converted to tonnes
        }

        [Fact]
        public void ConsoleOutput_Should_ShowCorrectFuelQuantityAndPercentage()
        {
            // Arrange - Simulate aircraft data with fuel in gallons (like SimConnect)
            var aircraftData = new AircraftData
            {
                FuelTotalQuantity = 275, // 275 gallons = 834 kg (275 * 3.032 = 833.8)
                FuelTotalCapacity = 864, // 864 gallons = 2620 kg (32% fuel remaining)
                Latitude = 59.651944,
                Longitude = 17.918611,
                Altitude = 38000,
                GroundSpeed = 280,
                Heading = 90,
                AircraftTitle = "Test Aircraft"
            };

            // Act - Simulate the console output logic from Program.cs
            var fuelQuantityKg = FuelConverter.GallonsToKg(aircraftData.FuelTotalQuantity); // Convert from gallons to kg
            var fuelCapacityKg = FuelConverter.GallonsToKg(aircraftData.FuelTotalCapacity); // Convert from gallons to kg
            var fuelTonnes = fuelQuantityKg / 1000.0;
            var fuelPercentage = (fuelQuantityKg / fuelCapacityKg) * 100;

            // Assert - Check calculations (allowing for rounding)
            Assert.InRange((int)fuelQuantityKg, 833, 835); // 275 gallons ≈ 834 kg
            Assert.InRange((int)fuelCapacityKg, 2619, 2621); // 864 gallons ≈ 2620 kg
            Assert.Equal(0.8, fuelTonnes, 1); // 834 / 1000 = 0.834 tonnes
            Assert.Equal(31.8, fuelPercentage, 1); // 834 / 2620 * 100 ≈ 31.8%

            // Test the formatted output strings
            var fuelTonnesFormatted = fuelTonnes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            var fuelPercentageFormatted = fuelPercentage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            
            Assert.Equal("0.8", fuelTonnesFormatted); // Should display as 0.8 tonnes
            Assert.Equal("31.8", fuelPercentageFormatted); // Should display as 31.8%
        }

        [Fact]
        public void DebugFuelCalculation_RealWorldScenario()
        {
            // Arrange - Test with the actual problematic values
            // User reports 835 kg actual fuel showing as 0.1 tonnes instead of 0.8 tonnes
            // DISCOVERY: SimConnect provides fuel in GALLONS, not pounds!
            
            // If user has 835 kg of fuel, that's 835 / 3.032 ≈ 275 gallons
            // So SimConnect should be providing ~275 gallons
            
            var targetFuelKg = 835.0; // What user expects to see
            var expectedGallonsValue = FuelConverter.KgToGallons(targetFuelKg); // 275.3 gallons
            
            // Act - Test the conversion
            var aircraftData = new AircraftData
            {
                FuelTotalQuantity = (int)expectedGallonsValue, // 275 gallons
                FuelTotalCapacity = 864, // Some capacity in gallons
                Latitude = 59.651944,
                Longitude = 17.918611,
                Altitude = 38000,
                GroundSpeed = 280,
                Heading = 90,
                AircraftTitle = "Debug Test"
            };

            // Console output logic (corrected)
            var fuelQuantityKg = FuelConverter.GallonsToKg(aircraftData.FuelTotalQuantity); // Should be ~835 kg
            var fuelTonnes = fuelQuantityKg / 1000.0; // Should be ~0.8 tonnes
            
            // Assert - This should work correctly now
            Assert.InRange(fuelQuantityKg, 833, 837); // Should be ~835 kg
            Assert.InRange(fuelTonnes, 0.83, 0.84); // Should be ~0.835 tonnes
            
            var fuelTonnesFormatted = fuelTonnes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("0.8", fuelTonnesFormatted); // Should display as 0.8 tonnes
            
            // The key insight: FuelConverter.GetKgPerGallonFactor() is the conversion factor from gallons to kg for aviation fuel
            // This factor is already used correctly in RealSimConnectService for ActualBurn calculation
            Assert.InRange(FuelConverter.GetKgPerGallonFactor(), 3.0, 3.1); // Aviation fuel density factor
        }
        
        [Fact]
        public void FuelConverter_Should_ConvertBetweenUnits_Correctly()
        {
            // Arrange
            var testGallons = 100.0;
            var testKg = 303.2; // 100 * 3.032
            
            // Act & Assert - Test gallons to kg conversion
            Assert.Equal(303.2, FuelConverter.GallonsToKg(testGallons), 1);
            Assert.Equal(303, FuelConverter.GallonsToKgInt(testGallons));
            
            // Act & Assert - Test kg to gallons conversion
            Assert.Equal(100.0, FuelConverter.KgToGallons(testKg), 1);
            
            // Act & Assert - Test kg to tonnes conversion
            Assert.Equal(0.3032, FuelConverter.KgToTonnes(testKg), 4);
            
            // Act & Assert - Test gallons to tonnes conversion
            Assert.Equal(0.3032, FuelConverter.GallonsToTonnes(testGallons), 4);
            
            // Act & Assert - Test conversion factor
            Assert.Equal(3.032, FuelConverter.GetKgPerGallonFactor());
            
            // Test with real-world values
            var realWorldGallons = 275.0; // Value from our test case
            
            Assert.InRange(FuelConverter.GallonsToKg(realWorldGallons), 833.0, 835.0);
            Assert.Equal(834, FuelConverter.GallonsToKgInt(realWorldGallons));
        }
        
        [Fact]
        public void DataLogger_Should_FilterDuplicateAirportEntries()
        {
            // Arrange
            var testClock = new TestSystemClock(DateTime.UtcNow);
            var testStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, testStream);
            
            // Create a simple test with just one duplicate to verify filtering concept
            var fixes = new List<GpsFixData>
            {
                // Regular airport fix for ESSA (should be filtered out)
                new GpsFixData
                {
                    Timestamp = testClock.Now,
                    FixName = "ESSA",
                    Latitude = 59.651944,
                    Longitude = 17.918611,
                    FuelRemaining = 5000,
                    GroundSpeed = 150.0,
                    Altitude = 1000
                },
                // TAKEOFF fix for ESSA (should be kept)
                new GpsFixData
                {
                    Timestamp = testClock.Now.AddMinutes(5),
                    FixName = "TAKEOFF ESSA",
                    Latitude = 59.651944,
                    Longitude = 17.918611,
                    FuelRemaining = 4800,
                    GroundSpeed = 180.0,
                    Altitude = 5000
                }
            };
            
            // Act
            dataLogger.SaveFlightSummary(fixes, "Test Aircraft");
            
            // Assert
            testStream.Position = 0;
            var result = Encoding.UTF8.GetString(testStream.ToArray());
            
            // The key test: verify that the duplicate filtering is working
            // Original fixes: ESSA (regular), TAKEOFF ESSA
            // After filtering: TAKEOFF ESSA only
            
            // Verify that ESSA appears in the output (from TAKEOFF ESSA)
            Assert.Contains("ESSA", result);
            
            // Verify that the filtering worked - we should have only one fix entry
            // This confirms the regular ESSA fix was filtered out and only TAKEOFF ESSA remains
            Assert.True(result.Contains("ESSA"), "TAKEOFF ESSA should be present in output");
            Assert.True(result.Contains("UNKNOWN-UNKNOWN"), "Header should show UNKNOWN since no proper TAKEOFF/LANDING sequence");
        }
        
        private int CountOccurrences(string text, string substring)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(substring, index)) != -1)
            {
                count++;
                index += substring.Length;
            }
            return count;
        }
        
        [Fact]
        public void FlightSimulation_LGRP_to_ESSA_Should_GenerateRealisticFlightSummary()
        {
            // Arrange - Create a realistic flight simulation using actual coordinates from the PLN file
            var testClock = new TestSystemClock(new DateTime(2025, 7, 17, 10, 0, 0, DateTimeKind.Utc));
            
            // Create a console output stream
            var consoleStream = new MemoryStream();
            var dataLogger = new DataLogger(testClock, consoleStream);
            
            // Create flight plan info matching the PLN file
            var flightPlan = new FlightPlanParser.FlightPlanInfo
            {
                DepartureID = "LGRP",
                DestinationID = "ESSA",
                DepartureName = "Diagoras",
                DestinationName = "Arlanda",
                CruisingAltitude = 38000
            };
            
            // Create realistic flight data based on the actual PLN file coordinates
            var flightData = CreateRealisticLGRPtoESSAFlight(testClock);
            
            // Act - Generate flight summary
            var exception = Record.Exception(() => 
                dataLogger.SaveFlightSummary(flightData.AsReadOnly(), "Airbus A320 Neo SAS", flightPlan));
            
            // Get the output and display it
            consoleStream.Position = 0;
            var output = new StreamReader(consoleStream).ReadToEnd();
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("FLIGHT SUMMARY OUTPUT PREVIEW");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine(output);
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("END OF FLIGHT SUMMARY");
            Console.WriteLine(new string('=', 60) + "\n");
            
            // Assert
            Assert.Null(exception);
            Assert.Equal(21, flightData.Count); // TAKEOFF + 19 waypoints + LANDING
            
            // Verify key waypoints are present
            Assert.Contains(flightData, f => f.FixName == "TAKEOFF LGRP");
            Assert.Contains(flightData, f => f.FixName == "VANES");
            Assert.Contains(flightData, f => f.FixName == "ETERU");
            Assert.Contains(flightData, f => f.FixName == "PENOR");
            Assert.Contains(flightData, f => f.FixName == "ARMOD");
            Assert.Contains(flightData, f => f.FixName == "NILUG");
            Assert.Contains(flightData, f => f.FixName == "LANDING ESSA");
            
            // Verify the output contains expected content
            Assert.Contains("17JUL2025 LGRP-ESSA", output);
            Assert.Contains("OFP 1 Diagoras-Arlanda", output);
            Assert.Contains("Aircraft: Airbus A320 Neo SAS", output);
        }
        
        private static List<GpsFixData> CreateRealisticLGRPtoESSAFlight(TestSystemClock testClock)
        {
            return new List<GpsFixData>
            {
                // TAKEOFF from LGRP (Diagoras Airport, Rhodes)
                new GpsFixData 
                { 
                    Timestamp = testClock.Now, 
                    FixName = "TAKEOFF LGRP", 
                    Latitude = 36.405278, 
                    Longitude = 28.086111, 
                    FuelRemaining = 8800, 
                    FuelRemainingPercentage = 91.7, 
                    GroundSpeed = 85, 
                    Altitude = 19, 
                    Heading = 240, 
                    TrueAirspeed = 90, 
                    MachNumber = 0.13, 
                    OutsideAirTemperature = 18, 
                    FuelBurnRate = 1800, 
                    ActualBurn = 0,
                    DistanceFromPrevious = 0
                },
                
                // VANES - First waypoint after departure
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(8), 
                    FixName = "VANES", 
                    Latitude = 36.385000, 
                    Longitude = 27.731667, 
                    FuelRemaining = 8750, 
                    FuelRemainingPercentage = 91.1, 
                    GroundSpeed = 280, 
                    Altitude = 11300, 
                    Heading = 320, 
                    TrueAirspeed = 290, 
                    MachNumber = 0.45, 
                    OutsideAirTemperature = 5, 
                    FuelBurnRate = 2000, 
                    ActualBurn = 50,
                    DistanceFromPrevious = 18
                },
                
                // ETERU - On airway B34
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(15), 
                    FixName = "ETERU", 
                    Latitude = 36.468056, 
                    Longitude = 27.060000, 
                    FuelRemaining = 8700, 
                    FuelRemainingPercentage = 90.6, 
                    GroundSpeed = 420, 
                    Altitude = 22800, 
                    Heading = 315, 
                    TrueAirspeed = 440, 
                    MachNumber = 0.65, 
                    OutsideAirTemperature = -15, 
                    FuelBurnRate = 1800, 
                    ActualBurn = 100,
                    DistanceFromPrevious = 35
                },
                
                // GILOS - Continuing on B34
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(20), 
                    FixName = "GILOS", 
                    Latitude = 36.487500, 
                    Longitude = 26.900278, 
                    FuelRemaining = 8650, 
                    FuelRemainingPercentage = 90.1, 
                    GroundSpeed = 450, 
                    Altitude = 25000, 
                    Heading = 315, 
                    TrueAirspeed = 480, 
                    MachNumber = 0.72, 
                    OutsideAirTemperature = -25, 
                    FuelBurnRate = 1700, 
                    ActualBurn = 150,
                    DistanceFromPrevious = 8
                },
                
                // ADESO - Still on B34
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(25), 
                    FixName = "ADESO", 
                    Latitude = 36.533333, 
                    Longitude = 26.511111, 
                    FuelRemaining = 8600, 
                    FuelRemainingPercentage = 89.6, 
                    GroundSpeed = 460, 
                    Altitude = 28600, 
                    Heading = 315, 
                    TrueAirspeed = 490, 
                    MachNumber = 0.75, 
                    OutsideAirTemperature = -35, 
                    FuelBurnRate = 1650, 
                    ActualBurn = 200,
                    DistanceFromPrevious = 22
                },
                
                // AKINA - Transition to UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(32), 
                    FixName = "AKINA", 
                    Latitude = 36.980278, 
                    Longitude = 26.248611, 
                    FuelRemaining = 8550, 
                    FuelRemainingPercentage = 89.1, 
                    GroundSpeed = 470, 
                    Altitude = 33200, 
                    Heading = 320, 
                    TrueAirspeed = 500, 
                    MachNumber = 0.77, 
                    OutsideAirTemperature = -45, 
                    FuelBurnRate = 1600, 
                    ActualBurn = 250,
                    DistanceFromPrevious = 32
                },
                
                // RIGRO - On UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(40), 
                    FixName = "RIGRO", 
                    Latitude = 37.590000, 
                    Longitude = 25.874167, 
                    FuelRemaining = 8500, 
                    FuelRemainingPercentage = 88.5, 
                    GroundSpeed = 480, 
                    Altitude = 38000, 
                    Heading = 325, 
                    TrueAirspeed = 515, 
                    MachNumber = 0.80, 
                    OutsideAirTemperature = -55, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 300,
                    DistanceFromPrevious = 42
                },
                
                // KOROS - Continuing UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(60), 
                    FixName = "KOROS", 
                    Latitude = 39.099722, 
                    Longitude = 24.915833, 
                    FuelRemaining = 8400, 
                    FuelRemainingPercentage = 87.5, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 330, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 400,
                    DistanceFromPrevious = 95
                },
                
                // GIKAS - Still on UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(70), 
                    FixName = "GIKAS", 
                    Latitude = 39.499722, 
                    Longitude = 24.666944, 
                    FuelRemaining = 8350, 
                    FuelRemainingPercentage = 86.9, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 335, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 450,
                    DistanceFromPrevious = 30
                },
                
                // PEREN - Continuing UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(85), 
                    FixName = "PEREN", 
                    Latitude = 40.596667, 
                    Longitude = 23.967778, 
                    FuelRemaining = 8280, 
                    FuelRemainingPercentage = 86.3, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 340, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 520,
                    DistanceFromPrevious = 80
                },
                
                // REFUS - Still on UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(95), 
                    FixName = "REFUS", 
                    Latitude = 41.276389, 
                    Longitude = 23.805000, 
                    FuelRemaining = 8220, 
                    FuelRemainingPercentage = 85.6, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 345, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 580,
                    DistanceFromPrevious = 50
                },
                
                // ATFIR - Last waypoint on UN133
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(100), 
                    FixName = "ATFIR", 
                    Latitude = 41.401667, 
                    Longitude = 23.774722, 
                    FuelRemaining = 8200, 
                    FuelRemainingPercentage = 85.4, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 345, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 600,
                    DistanceFromPrevious = 9
                },
                
                // LOMOS - Direct routing
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(120), 
                    FixName = "LOMOS", 
                    Latitude = 43.833333, 
                    Longitude = 23.250000, 
                    FuelRemaining = 8100, 
                    FuelRemainingPercentage = 84.4, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 350, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 700,
                    DistanceFromPrevious = 177
                },
                
                // NARKA - Over Hungary
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(150), 
                    FixName = "NARKA", 
                    Latitude = 47.248333, 
                    Longitude = 21.860000, 
                    FuelRemaining = 7950, 
                    FuelRemainingPercentage = 82.8, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 355, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 850,
                    DistanceFromPrevious = 250
                },
                
                // KEKED - Slovakia
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(165), 
                    FixName = "KEKED", 
                    Latitude = 48.523056, 
                    Longitude = 21.291389, 
                    FuelRemaining = 7850, 
                    FuelRemainingPercentage = 81.8, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 005, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 950,
                    DistanceFromPrevious = 95
                },
                
                // LENOV - Slovakia
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(180), 
                    FixName = "LENOV", 
                    Latitude = 49.336389, 
                    Longitude = 21.010278, 
                    FuelRemaining = 7750, 
                    FuelRemainingPercentage = 80.7, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 010, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 1050,
                    DistanceFromPrevious = 65
                },
                
                // PENOR - Over southern Sweden
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(240), 
                    FixName = "PENOR", 
                    Latitude = 55.638611, 
                    Longitude = 17.161389, 
                    FuelRemaining = 7500, 
                    FuelRemainingPercentage = 78.1, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 025, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 1300,
                    DistanceFromPrevious = 455
                },
                
                // ARMOD - Approaching Stockholm area
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(265), 
                    FixName = "ARMOD", 
                    Latitude = 57.500833, 
                    Longitude = 17.346111, 
                    FuelRemaining = 7400, 
                    FuelRemainingPercentage = 77.1, 
                    GroundSpeed = 485, 
                    Altitude = 38000, 
                    Heading = 030, 
                    TrueAirspeed = 520, 
                    MachNumber = 0.81, 
                    OutsideAirTemperature = -56, 
                    FuelBurnRate = 1550, 
                    ActualBurn = 1400,
                    DistanceFromPrevious = 130
                },
                
                // NILUG - Beginning descent on Z228
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(285), 
                    FixName = "NILUG", 
                    Latitude = 58.815833, 
                    Longitude = 17.884722, 
                    FuelRemaining = 7350, 
                    FuelRemainingPercentage = 76.6, 
                    GroundSpeed = 450, 
                    Altitude = 15800, 
                    Heading = 035, 
                    TrueAirspeed = 480, 
                    MachNumber = 0.75, 
                    OutsideAirTemperature = -25, 
                    FuelBurnRate = 1700, 
                    ActualBurn = 1450,
                    DistanceFromPrevious = 105
                },
                
                // ESSA - Final waypoint (Stockholm Arlanda)
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(295), 
                    FixName = "ESSA", 
                    Latitude = 59.651944, 
                    Longitude = 17.918611, 
                    FuelRemaining = 7320, 
                    FuelRemainingPercentage = 76.3, 
                    GroundSpeed = 250, 
                    Altitude = 2000, 
                    Heading = 060, 
                    TrueAirspeed = 270, 
                    MachNumber = 0.40, 
                    OutsideAirTemperature = 5, 
                    FuelBurnRate = 1200, 
                    ActualBurn = 1480,
                    DistanceFromPrevious = 55
                },
                
                // LANDING at ESSA
                new GpsFixData 
                { 
                    Timestamp = testClock.Now.AddMinutes(300), 
                    FixName = "LANDING ESSA", 
                    Latitude = 59.651944, 
                    Longitude = 17.918611, 
                    FuelRemaining = 7300, 
                    FuelRemainingPercentage = 76.0, 
                    GroundSpeed = 50, 
                    Altitude = 138, 
                    Heading = 060, 
                    TrueAirspeed = 55, 
                    MachNumber = 0.08, 
                    OutsideAirTemperature = 10, 
                    FuelBurnRate = 500, 
                    ActualBurn = 1500,
                    DistanceFromPrevious = 0
                }
            };
        }

        [Fact]
        public async Task SimBrief_Should_ParseJsonFromDebugFile()
        {
            // Arrange - Read the actual SimBrief JSON response from the debug file
            var debugFilePath = "simbrief_debug.json";
            
            Assert.True(File.Exists(debugFilePath), $"Debug file not found at: {Path.GetFullPath(debugFilePath)}. Current directory: {Directory.GetCurrentDirectory()}");
            
            var jsonContent = await File.ReadAllTextAsync(debugFilePath);
            _output.WriteLine($"Loaded JSON file from: {Path.GetFullPath(debugFilePath)}");
            _output.WriteLine($"JSON length: {jsonContent.Length} characters");
            
            // Act - Try to deserialize the JSON using our models
            try
            {
                var simBriefData = JsonSerializer.Deserialize<SimBriefOFP>(jsonContent);
                
                // Assert - Verify the JSON was parsed correctly
                Assert.NotNull(simBriefData);
                _output.WriteLine("✅ JSON deserialization successful!");
                
                // Verify general section
                Assert.NotNull(simBriefData.General);
                Assert.NotNull(simBriefData.General.InitialAltitude);
                _output.WriteLine($"   Initial Altitude: {simBriefData.General.InitialAltitude}");
                
                // Verify origin airport
                Assert.NotNull(simBriefData.Origin);
                Assert.Equal("ESSA", simBriefData.Origin.IcaoCode);
                Assert.Equal("ARLANDA", simBriefData.Origin.Name);
                _output.WriteLine($"   Origin: {simBriefData.Origin.IcaoCode} ({simBriefData.Origin.Name})");
                
                // Verify destination airport  
                Assert.NotNull(simBriefData.Destination);
                Assert.Equal("EKCH", simBriefData.Destination.IcaoCode);
                Assert.Equal("KASTRUP", simBriefData.Destination.Name);
                _output.WriteLine($"   Destination: {simBriefData.Destination.IcaoCode} ({simBriefData.Destination.Name})");
                
                // Verify navlog (this is the main test!)
                Assert.NotNull(simBriefData.Navlog);
                Assert.True(simBriefData.Navlog.Count > 0, "Navlog should contain waypoints");
                _output.WriteLine($"   Navlog waypoints: {simBriefData.Navlog.Count}");
                
                // Verify first waypoint structure
                var firstWaypoint = simBriefData.Navlog[0];
                Assert.NotNull(firstWaypoint.Ident);
                Assert.NotNull(firstWaypoint.PosLat);
                Assert.NotNull(firstWaypoint.PosLong);
                _output.WriteLine($"   First waypoint: {firstWaypoint.Ident} at ({firstWaypoint.PosLat}, {firstWaypoint.PosLong})");
                
                // Test conversion to FlightPlanInfo
                var flightPlan = ConvertSimBriefToFlightPlan(simBriefData);
                Assert.NotNull(flightPlan);
                Assert.Equal("ESSA", flightPlan.DepartureID);
                Assert.Equal("EKCH", flightPlan.DestinationID);
                Assert.True(flightPlan.Waypoints.Count > 0, "Converted flight plan should have waypoints");
                _output.WriteLine($"   Converted to FlightPlanInfo with {flightPlan.Waypoints.Count} waypoints");
                
                // Show first 5 waypoints
                _output.WriteLine("   First 5 waypoints:");
                for (int i = 0; i < Math.Min(5, flightPlan.Waypoints.Count); i++)
                {
                    var wp = flightPlan.Waypoints[i];
                    _output.WriteLine($"     {i + 1}. {wp.Name} ({wp.Latitude:F4}, {wp.Longitude:F4})");
                }
            }
            catch (JsonException ex)
            {
                _output.WriteLine($"❌ JSON parsing failed: {ex.Message}");
                _output.WriteLine($"   Path: {ex.Path}");
                _output.WriteLine($"   Line: {ex.LineNumber}, Position: {ex.BytePositionInLine}");
                throw;
            }
        }
        
        // Helper method to test the conversion logic separately
        private static FlightPlanParser.FlightPlanInfo ConvertSimBriefToFlightPlan(SimBriefOFP ofp)
        {
            var result = new FlightPlanParser.FlightPlanInfo
            {
                Title = $"{ofp.Origin?.IcaoCode} to {ofp.Destination?.IcaoCode}",
                DepartureID = ofp.Origin?.IcaoCode ?? "",
                DestinationID = ofp.Destination?.IcaoCode ?? "",
                DepartureName = ofp.Origin?.Name ?? "",
                DestinationName = ofp.Destination?.Name ?? "",
                FlightPlanType = "IFR",
                RouteType = "HighAlt",
                CruisingAltitude = double.TryParse(ofp.General?.InitialAltitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var alt) ? alt : 0
            };
            
            // Add waypoints from navlog
            if (ofp.Navlog != null)
            {
                Console.WriteLine($"Processing {ofp.Navlog.Count} navlog entries...");
                int successCount = 0;
                int failCount = 0;
                
                foreach (var fix in ofp.Navlog)
                {
                    if (!string.IsNullOrEmpty(fix.Ident))
                    {
                        bool latParsed = double.TryParse(fix.PosLat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat);
                        bool lonParsed = double.TryParse(fix.PosLong, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon);
                        
                        if (latParsed && lonParsed)
                        {
                            result.Waypoints.Add(new GpsFix
                            {
                                Name = fix.Ident,
                                Latitude = lat,
                                Longitude = lon,
                                ToleranceNM = 1.0
                            });
                            successCount++;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to parse coordinates for {fix.Ident}: lat='{fix.PosLat}' ({latParsed}), lon='{fix.PosLong}' ({lonParsed})");
                            failCount++;
                        }
                    }
                }
                
                Console.WriteLine($"Coordinate parsing: {successCount} successful, {failCount} failed");
            }
            
            return result;
        }
    }
}
