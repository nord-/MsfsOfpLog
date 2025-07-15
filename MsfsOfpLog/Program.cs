using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MsfsOfpLog.Models;
using MsfsOfpLog.Services;

namespace MsfsOfpLog
{
    class Program
    {
        private static RealSimConnectService? realSimConnectService;
        private static GpsFixTracker? gpsFixTracker;
        private static DataLogger? dataLogger;
        private static CancellationTokenSource? cancellationTokenSource;
        private static string currentAircraftTitle = "";
        private static AircraftData? currentAircraftData;
        private static bool hasBeenAirborne = false; // Track if aircraft has been airborne
        private static bool isCurrentlyAirborne = false; // Track current airborne status
        private static DateTime? firstAirborneTime = null; // When aircraft first became airborne
        private static bool takeoffRecorded = false; // Track if takeoff has been recorded
        private static bool landingRecorded = false; // Track if landing has been recorded
        private static ISystemClock systemClock = new SystemClock();
        private static FlightPlanParser.FlightPlanInfo? currentFlightPlan = null; // Store current flight plan
        
        static async Task Main(string[] args)
        {
            // Set console encoding to UTF-8 for proper emoji display
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            Console.WriteLine("Welcome to MSFS OFP Log!");
            Console.WriteLine("This tool will track your GPS fixes and fuel consumption during flight.");
            Console.WriteLine();
            
            // Set up Ctrl+C handler for graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate exit
                Console.WriteLine("\n\nReceived Ctrl+C - stopping monitoring gracefully...");
                cancellationTokenSource?.Cancel();
            };
            
            Console.WriteLine("🚀 Using REAL MSFS SimConnect");
            Console.WriteLine("Make sure MSFS is running and you're in a flight!");
            Console.WriteLine();
            
            // Initialize services
            realSimConnectService = new RealSimConnectService();
            realSimConnectService.Connected += OnSimConnectConnected;
            realSimConnectService.Disconnected += OnSimConnectDisconnected;
            realSimConnectService.DataReceived += OnDataReceived;
            
            gpsFixTracker = new GpsFixTracker(systemClock);
            dataLogger = new DataLogger(systemClock);
            cancellationTokenSource = new CancellationTokenSource();
            
            // Auto-connect on startup
            await ConnectToMsfs();
            
            // Display menu
            await ShowMainMenu();
        }
        
        private static async Task ShowMainMenu()
        {
            while (true)
            {
                Console.WriteLine("\n=== MSFS OFP Log Menu ===");
                Console.WriteLine("1. Load route string (starts monitoring automatically)");
                Console.WriteLine("2. Load MSFS flight plan (.pln) (starts monitoring automatically)");
                Console.WriteLine("3. Start monitoring (manual start)");
                Console.WriteLine("4. Stop monitoring");
                Console.Write("Select option: ");
                
                var input = Console.ReadLine();
                
                switch (input)
                {
                    case "1":
                        await LoadRouteString();
                        // LoadRouteString now automatically starts monitoring
                        return;
                    case "2":
                        await LoadFlightPlan();
                        // LoadFlightPlan now automatically starts monitoring
                        return;
                    case "3":
                        await StartMonitoring();
                        // StartMonitoring now runs continuously until Ctrl+C
                        return;
                    case "4":
                        await StopMonitoringAndExit();
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }
        
        private static async Task ConnectToMsfs()
        {
            Console.WriteLine("Connecting to MSFS...");
            Console.WriteLine("Make sure MSFS is running and you're in a flight (not in the main menu).");
            
            if (realSimConnectService?.Connect() == true)
            {
                Console.WriteLine("Connection initiated. Waiting for MSFS response...");
                
                // Give it some time to connect
                await Task.Delay(2000);
                
                // Start a background task to process SimConnect messages
                _ = Task.Run(async () =>
                {
                    while (!cancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        realSimConnectService?.ReceiveMessage();
                        await Task.Delay(100);
                    }
                });
            }
            else
            {
                Console.WriteLine("❌ Failed to connect to MSFS. Please ensure:");
                Console.WriteLine("   1. MSFS is running");
                Console.WriteLine("   2. You are in a flight (not in the main menu)");
                Console.WriteLine("   3. SimConnect is enabled in MSFS settings");
            }
        }
        
        private static async Task LoadFlightPlan()
        {
            Console.WriteLine("\nLoad MSFS Flight Plan:");
            Console.WriteLine("Enter the path to your .pln file:");
            Console.Write("File path: ");
            var filePath = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Console.WriteLine("No file path provided.");
                return;
            }
            
            // Remove quotes from file path if present (before checking if rooted)
            filePath = filePath.Trim().Trim('"');
            
            // If it's a relative path, make it absolute
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }
            
            var flightPlanInfo = FlightPlanParser.ParseFlightPlan(filePath);
            if (flightPlanInfo != null)
            {
                // Store the flight plan for use in takeoff/landing recording
                currentFlightPlan = flightPlanInfo;
                
                // Display flight plan information
                FlightPlanParser.DisplayFlightPlanInfo(flightPlanInfo);
                
                // Reset current GPS fixes and load the new ones
                gpsFixTracker?.Reset();
                
                // Add all waypoints from the flight plan
                foreach (var waypoint in flightPlanInfo.Waypoints)
                {
                    gpsFixTracker?.AddGpsFix(waypoint);
                }
                
                Console.WriteLine($"✅ Successfully loaded {flightPlanInfo.Waypoints.Count} waypoints from flight plan!");
                Console.WriteLine($"   Route: {flightPlanInfo.DepartureID} → {flightPlanInfo.DestinationID}");
                Console.WriteLine($"   Ready to monitor your flight from {flightPlanInfo.DepartureName} to {flightPlanInfo.DestinationName}");
                
                // Automatically start monitoring
                Console.WriteLine("\n🚀 Starting monitoring automatically...");
                await StartMonitoring();
            }
            else
            {
                Console.WriteLine("❌ Failed to load flight plan. Please check the file path and format.");
            }
        }
        
        private static async Task LoadRouteString()
        {
            Console.WriteLine("\nLoad Route String:");
            Console.WriteLine("Enter your flight route (e.g., 'LOWW DCT VIE DCT RIVER DCT LOWG'):");
            Console.Write("Route: ");
            var route = Console.ReadLine();
            
            if (!string.IsNullOrWhiteSpace(route))
            {
                gpsFixTracker?.LoadGpsFixesFromRoute(route);
                Console.WriteLine("✅ Route string loaded successfully!");
                Console.WriteLine("Note: In this version, GPS coordinates need to be added manually.");
                Console.WriteLine("Consider using navigation databases for automatic coordinate lookup.");
                
                // Automatically start monitoring
                Console.WriteLine("\n🚀 Starting monitoring automatically...");
                await StartMonitoring();
            }
            else
            {
                Console.WriteLine("❌ No route provided.");
            }
        }
        
        private static async Task StartMonitoring()
        {
            Console.WriteLine("\nStarting GPS fix monitoring...");
            Console.WriteLine("The system will now track your position and log when you pass GPS fixes.");
            Console.WriteLine("Position will be updated every 5 seconds. Press Ctrl+C to stop monitoring.");
            Console.WriteLine("Monitoring will automatically stop when aircraft speed drops below 45 knots.\n");
            
            realSimConnectService?.StartDataRequest();
            
            // Wait a moment for data to be received
            await Task.Delay(500);
            
            bool monitoringActive = true;
            
            // Reset flight state tracking
            hasBeenAirborne = false;
            isCurrentlyAirborne = false;
            firstAirborneTime = null;
            takeoffRecorded = false;
            landingRecorded = false;
            
            // Continuously display updated position every 5 seconds
            while (!cancellationTokenSource?.Token.IsCancellationRequested == true && monitoringActive)
            {
                try
                {
                    if (currentAircraftData != null)
                    {
                        // Display the current status (this will clear and redraw everything)
                        DisplayCurrentStatus();
                        
                        // Only stop monitoring if we've been airborne and are now slow (post-flight taxi)
                        if (hasBeenAirborne && currentAircraftData.GroundSpeed < 45.0)
                        {
                            Console.WriteLine($"\n🛬 Aircraft has landed and is now taxiing ({currentAircraftData.GroundSpeed.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} kts).");
                            Console.WriteLine("Automatically stopping monitoring (flight completed)...");
                            monitoringActive = false;
                        }
                    }
                    else
                    {
                        DisplayCurrentStatus();
                    }
                    
                    if (monitoringActive)
                    {
                        await Task.Delay(5000, cancellationTokenSource?.Token ?? CancellationToken.None);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            
            // Monitoring loop has ended, shutdown gracefully
            Console.WriteLine("\nMonitoring stopped.");
            await StopMonitoringAndExit();
        }
        
        private static void StopMonitoring()
        {
            Console.WriteLine("\nStopping monitoring...");
            
            // Save flight summary
            var passedFixes = gpsFixTracker?.GetPassedFixes();
            if (passedFixes != null && passedFixes.Count > 0)
            {
                dataLogger?.SaveFlightSummary(passedFixes, currentAircraftTitle, currentFlightPlan);
                Console.WriteLine($"Flight completed! {passedFixes.Count} GPS fixes were logged.");
            }
            else
            {
                Console.WriteLine("No GPS fixes were passed during this monitoring session.");
            }
        }
        
        private static async Task StopMonitoringAndExit()
        {
            Console.WriteLine("Shutting down...");
            
            // Stop monitoring and save data
            StopMonitoring();
            
            // Cancel background tasks
            cancellationTokenSource?.Cancel();
            
            // Disconnect from MSFS
            realSimConnectService?.Disconnect();
            
            Console.WriteLine("Goodbye!");
            await Task.Delay(1000);
        }
        
        private static void OnSimConnectConnected(object? sender, EventArgs e)
        {
            Console.WriteLine("Successfully connected to MSFS!");
        }
        
        private static void OnSimConnectDisconnected(object? sender, EventArgs e)
        {
            Console.WriteLine("Disconnected from MSFS.");
        }
        
        private static void OnDataReceived(object? sender, AircraftData aircraftData)
        {
            // Store current aircraft data
            currentAircraftData = aircraftData;
            
            // Update current aircraft title
            currentAircraftTitle = aircraftData.AircraftTitle;
            
            // Update flight state tracking in real-time
            bool wasAirborne = isCurrentlyAirborne;
            isCurrentlyAirborne = aircraftData.GroundSpeed > 45.0;
            
            // Check if aircraft just became airborne (takeoff)
            if (!wasAirborne && isCurrentlyAirborne && !takeoffRecorded)
            {
                hasBeenAirborne = true;
                firstAirborneTime = DateTime.Now;
                takeoffRecorded = true;
                
                // Record takeoff event with airport information
                var departureAirport = currentFlightPlan?.DepartureID ?? "UNKNOWN";
                var takeoffData = new GpsFixData
                {
                    Timestamp = DateTime.Now,
                    FixName = $"TAKEOFF {departureAirport}", // Use actual departure airport
                    Latitude = aircraftData.Latitude,
                    Longitude = aircraftData.Longitude,
                    FuelRemaining = (int)(aircraftData.FuelTotalQuantity * 3.032), // Convert from gallons to kg
                    FuelRemainingPercentage = aircraftData.FuelTotalCapacity > 0 ? 
                        (aircraftData.FuelTotalQuantity / aircraftData.FuelTotalCapacity) * 100 : 0,
                    GroundSpeed = aircraftData.GroundSpeed,
                    Altitude = aircraftData.Altitude,
                    Heading = aircraftData.Heading,
                    TrueAirspeed = aircraftData.TrueAirspeed,
                    MachNumber = aircraftData.MachNumber,
                    OutsideAirTemperature = aircraftData.OutsideAirTemperature,
                    FuelBurnRate = aircraftData.FuelBurnRate,
                    ActualBurn = aircraftData.ActualBurn
                };
                
                gpsFixTracker?.AddPassedFix(takeoffData);
            }
            
            // Check if aircraft just landed (was airborne, now slow)
            if (wasAirborne && !isCurrentlyAirborne && hasBeenAirborne && !landingRecorded)
            {
                landingRecorded = true;
                
                // Record landing event with airport information
                var destinationAirport = currentFlightPlan?.DestinationID ?? "UNKNOWN";
                var landingData = new GpsFixData
                {
                    Timestamp = DateTime.Now,
                    FixName = $"LANDING {destinationAirport}", // Use actual destination airport
                    Latitude = aircraftData.Latitude,
                    Longitude = aircraftData.Longitude,
                    FuelRemaining = (int)(aircraftData.FuelTotalQuantity * 3.032), // Convert from gallons to kg
                    FuelRemainingPercentage = aircraftData.FuelTotalCapacity > 0 ? 
                        (aircraftData.FuelTotalQuantity / aircraftData.FuelTotalCapacity) * 100 : 0,
                    GroundSpeed = aircraftData.GroundSpeed,
                    Altitude = aircraftData.Altitude,
                    Heading = aircraftData.Heading,
                    TrueAirspeed = aircraftData.TrueAirspeed,
                    MachNumber = aircraftData.MachNumber,
                    OutsideAirTemperature = aircraftData.OutsideAirTemperature,
                    FuelBurnRate = aircraftData.FuelBurnRate,
                    ActualBurn = aircraftData.ActualBurn
                };
                
                gpsFixTracker?.AddPassedFix(landingData);
            }
            
            // If aircraft just became airborne, mark as has been airborne
            if (!wasAirborne && isCurrentlyAirborne)
            {
                hasBeenAirborne = true;
            }
            
            // Check if we're near any GPS fixes, passing current flight state
            gpsFixTracker?.CheckPosition(aircraftData, hasBeenAirborne);
        }
        
        private static void DisplayCurrentStatus()
        {
            // Clear the console and redraw everything
            Console.Clear();
            
            // Header
            Console.WriteLine("=== MSFS OFP Log - Flight Monitoring ===");
            Console.WriteLine();
            
            // Current position block
            if (currentAircraftData != null)
            {
                // Determine flight phase
                string flightPhase = "";
                if (!hasBeenAirborne && !isCurrentlyAirborne)
                {
                    flightPhase = "TAXI (Pre-flight)";
                }
                else if (hasBeenAirborne && isCurrentlyAirborne)
                {
                    flightPhase = "AIRBORNE";
                }
                else if (hasBeenAirborne && !isCurrentlyAirborne)
                {
                    flightPhase = "TAXI (Post-flight)";
                }
                
                Console.WriteLine($"Current Aircraft Position: {DateTime.Now:HH:mm:ss} [{flightPhase}]");
                Console.WriteLine($"  Latitude:  {currentAircraftData.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}°");
                Console.WriteLine($"  Longitude: {currentAircraftData.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}°");
                Console.WriteLine($"  Altitude:  {currentAircraftData.Altitude.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} ft");
                Console.WriteLine($"  Ground Speed: {currentAircraftData.GroundSpeed.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} kts");
                Console.WriteLine($"  Heading:   {currentAircraftData.Heading.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}°");
                var fuelQuantityKg = currentAircraftData.FuelTotalQuantity * 3.032; // Convert from gallons to kg
                var fuelCapacityKg = currentAircraftData.FuelTotalCapacity * 3.032; // Convert from gallons to kg
                Console.WriteLine($"  Fuel:      {(fuelQuantityKg/1000).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} t ({(fuelQuantityKg / fuelCapacityKg * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%)");
                Console.WriteLine($"  Aircraft:  {currentAircraftData.AircraftTitle}");
            }
            else
            {
                Console.WriteLine("Waiting for aircraft data from MSFS...");
            }
            
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            
            // GPS fixes section
            var passedFixes = gpsFixTracker?.GetPassedFixes();
            if (passedFixes != null && passedFixes.Count > 0)
            {
                Console.WriteLine($"GPS Fixes Passed ({passedFixes.Count}):");
                for (int i = 0; i < passedFixes.Count; i++)
                {
                    var fix = passedFixes[i];
                    // Highlight the most recent fix and use special icons for takeoff/landing
                    string icon = fix.FixName.StartsWith("TAKEOFF") ? "🛫" : 
                                 fix.FixName.StartsWith("LANDING") ? "🛬" : "🎯";
                    string prefix = i == passedFixes.Count - 1 ? $"  {icon}*" : $"  {icon} ";
                    Console.WriteLine($"{prefix}{fix.FixName,-12} {fix.Timestamp:HH:mm:ss} - {fix.FuelRemaining.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} kg ({fix.FuelRemainingPercentage.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%) - {fix.GroundSpeed.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} kts");
                }
                Console.WriteLine("    (* = most recent)");
            }
            else
            {
                Console.WriteLine("No GPS fixes passed yet.");
            }
            
            Console.WriteLine("────────────────────────────────────────────────────────────────");
            Console.WriteLine("Press Ctrl+C to stop monitoring and save flight data");
        }
    }
}
