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
        private static MockSimConnectService? mockSimConnectService;
        private static RealSimConnectService? realSimConnectService;
        private static GpsFixTracker? gpsFixTracker;
        private static DataLogger? dataLogger;
        private static CancellationTokenSource? cancellationTokenSource;
        private static string currentAircraftTitle = "";
        private static bool useRealSimConnect = false;
        private static AircraftData? currentAircraftData;
        
        static async Task Main(string[] args)
        {
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
            
            // Check for test mode command line argument
            bool testMode = args.Length > 0 && args[0] == "-test";
            
            if (testMode)
            {
                Console.WriteLine("🎮 Running in TEST MODE with simulated data");
                useRealSimConnect = false;
            }
            else
            {
                Console.WriteLine("🚀 Using REAL MSFS SimConnect");
                Console.WriteLine("Make sure MSFS is running and you're in a flight!");
                useRealSimConnect = true;
            }
            Console.WriteLine();
            
            // Initialize services
            if (useRealSimConnect)
            {
                realSimConnectService = new RealSimConnectService();
                realSimConnectService.Connected += OnSimConnectConnected;
                realSimConnectService.Disconnected += OnSimConnectDisconnected;
                realSimConnectService.DataReceived += OnDataReceived;
            }
            else
            {
                mockSimConnectService = new MockSimConnectService();
                mockSimConnectService.Connected += OnSimConnectConnected;
                mockSimConnectService.Disconnected += OnSimConnectDisconnected;
                mockSimConnectService.DataReceived += OnDataReceived;
            }
            
            gpsFixTracker = new GpsFixTracker();
            dataLogger = new DataLogger();
            cancellationTokenSource = new CancellationTokenSource();
            
            // Set up event handlers
            gpsFixTracker.FixPassed += OnFixPassed;
            
            // Add some sample GPS fixes for demo (only in demo mode)
            if (!useRealSimConnect)
            {
                AddSampleGpsFixes();
            }
            
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
                Console.WriteLine("1. Load route string");
                Console.WriteLine("2. Load MSFS flight plan (.pln)");
                Console.WriteLine("3. Start monitoring");
                Console.WriteLine("4. Stop monitoring");
                Console.Write("Select option: ");
                
                var input = Console.ReadLine();
                
                switch (input)
                {
                    case "1":
                        LoadRouteString();
                        break;
                    case "2":
                        LoadFlightPlan();
                        break;
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
            if (useRealSimConnect)
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
            else
            {
                Console.WriteLine("Connecting to MSFS (Demo Mode)...");
                
                if (mockSimConnectService?.Connect() == true)
                {
                    Console.WriteLine("Demo connection successful!");
                    
                    // Start a background task to process messages
                    _ = Task.Run(async () =>
                    {
                        while (!cancellationTokenSource?.Token.IsCancellationRequested == true)
                        {
                            mockSimConnectService?.ReceiveMessage();
                            await Task.Delay(100);
                        }
                    });
                }
                else
                {
                    Console.WriteLine("Demo connection failed.");
                }
            }
        }
        
        private static void AddSampleGpsFixes()
        {
            // Add some sample GPS fixes for the demo flight path
            gpsFixTracker?.AddGpsFix(new GpsFix
            {
                Name = "VIE",
                Latitude = 48.1103,
                Longitude = 16.5697,
                ToleranceNM = 1.0
            });
            
            gpsFixTracker?.AddGpsFix(new GpsFix
            {
                Name = "RIVER",
                Latitude = 47.7000,
                Longitude = 15.7000,
                ToleranceNM = 0.5
            });
            
            gpsFixTracker?.AddGpsFix(new GpsFix
            {
                Name = "LOWG",
                Latitude = 47.0077,
                Longitude = 15.4396,
                ToleranceNM = 1.0
            });
            
            Console.WriteLine("Sample GPS fixes loaded for demo:");
            Console.WriteLine("- VIE (Vienna): 48.1103, 16.5697");
            Console.WriteLine("- RIVER (Waypoint): 47.7000, 15.7000");
            Console.WriteLine("- LOWG (Graz): 47.0077, 15.4396");
        }
        
        private static void LoadFlightPlan()
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
            
            // If it's a relative path, make it absolute
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }
            
            var flightPlanInfo = FlightPlanParser.ParseFlightPlan(filePath);
            if (flightPlanInfo != null)
            {
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
            }
            else
            {
                Console.WriteLine("❌ Failed to load flight plan. Please check the file path and format.");
            }
        }
        
        private static void LoadRouteString()
        {
            Console.WriteLine("\nLoad Route String:");
            Console.WriteLine("Enter your flight route (e.g., 'LOWW DCT VIE DCT RIVER DCT LOWG'):");
            Console.Write("Route: ");
            var route = Console.ReadLine();
            
            if (!string.IsNullOrWhiteSpace(route))
            {
                gpsFixTracker?.LoadGpsFixesFromRoute(route);
                Console.WriteLine("Note: In this version, GPS coordinates need to be added manually.");
                Console.WriteLine("Consider using navigation databases for automatic coordinate lookup.");
            }
        }
        
        private static async Task StartMonitoring()
        {
            Console.WriteLine("\nStarting GPS fix monitoring...");
            Console.WriteLine("The system will now track your position and log when you pass GPS fixes.");
            Console.WriteLine("Position will be updated every 5 seconds. Press Ctrl+C to stop monitoring.");
            Console.WriteLine("Monitoring will automatically stop when aircraft speed drops below 45 knots.\n");
            
            if (useRealSimConnect)
            {
                realSimConnectService?.StartDataRequest();
            }
            else
            {
                mockSimConnectService?.StartDataRequest();
            }
            
            // Wait a moment for data to be received
            await Task.Delay(500);
            
            bool monitoringActive = true;
            
            // Continuously display updated position every 5 seconds
            while (!cancellationTokenSource?.Token.IsCancellationRequested == true && monitoringActive)
            {
                try
                {
                    // Clear the console area where we show position (move cursor up and clear lines)
                    if (currentAircraftData != null)
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.WriteLine($"Current Aircraft Position: {DateTime.Now:HH:mm:ss}");
                        Console.WriteLine($"  Latitude:  {currentAircraftData.Latitude:F6}°");
                        Console.WriteLine($"  Longitude: {currentAircraftData.Longitude:F6}°");
                        Console.WriteLine($"  Altitude:  {currentAircraftData.Altitude:F0} ft");
                        Console.WriteLine($"  Ground Speed: {currentAircraftData.GroundSpeed:F0} kts");
                        Console.WriteLine($"  Heading:   {currentAircraftData.Heading:F0}°");
                        var fuelQuantityKg = currentAircraftData.FuelTotalQuantity * 3.032; // Convert gallons to kg (adjusted for accuracy)
                        var fuelCapacityKg = currentAircraftData.FuelTotalCapacity * 3.032; // Convert gallons to kg
                        Console.WriteLine($"  Fuel:      {fuelQuantityKg:F0} kg ({(fuelQuantityKg / fuelCapacityKg * 100):F1}%)");
                        Console.WriteLine($"  Aircraft:  {currentAircraftData.AircraftTitle}");
                        Console.WriteLine("────────────────────────────────────────────────────────────────");
                        
                        // Check if aircraft has slowed down below 45 knots (landed/taxiing)
                        if (currentAircraftData.GroundSpeed < 45.0)
                        {
                            Console.WriteLine($"\nAircraft speed dropped below 45 knots ({currentAircraftData.GroundSpeed:F0} kts).");
                            Console.WriteLine("Automatically stopping monitoring (aircraft likely landed or taxiing)...");
                            monitoringActive = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Waiting for aircraft data from MSFS...");
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
                dataLogger?.SaveFlightSummary(passedFixes, currentAircraftTitle);
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
            if (useRealSimConnect)
            {
                realSimConnectService?.Disconnect();
            }
            else
            {
                mockSimConnectService?.Disconnect();
            }
            
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
            
            // Check if we're near any GPS fixes
            gpsFixTracker?.CheckPosition(aircraftData);
        }
        
        private static void OnFixPassed(object? sender, GpsFixData fixData)
        {
            // Log the GPS fix data
            dataLogger?.LogGpsFixData(fixData);
            
            // Display notification
            Console.WriteLine($"\n🎯 GPS FIX PASSED: {fixData.FixName}");
            Console.WriteLine($"   Time: {fixData.Timestamp:HH:mm:ss}");
            Console.WriteLine($"   Fuel: {fixData.FuelRemaining:F2} kg ({fixData.FuelRemainingPercentage:F1}%)");
            Console.WriteLine($"   Position: {fixData.Latitude:F6}, {fixData.Longitude:F6}");
            Console.WriteLine();
        }
    }
}
