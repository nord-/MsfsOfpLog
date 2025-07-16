using static System.Globalization.CultureInfo;
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
        private static AircraftData? _currentAircraftData;
        private static bool _hasBeenAirborne = false; // Track if aircraft has been airborne
        private static bool _isCurrentlyAirborne = false; // Track current airborne status
        private static DateTime? _firstAirborneTime = null; // When aircraft first became airborne
        private static bool _takeoffRecorded = false; // Track if takeoff has been recorded
        private static bool _landingRecorded = false; // Track if landing has been recorded
        private static ISystemClock systemClock = new SystemClock();
        private static FlightPlanParser.FlightPlanInfo? currentFlightPlan = null; // Store current flight plan
        private static double _distance;
        
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
                Console.WriteLine("\n\nReceived Ctrl+C - stopping monitoring gracefully…");
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
                        StopMonitoringAndExit();
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }
        
        private static async Task ConnectToMsfs()
        {
            Console.WriteLine("Connecting to MSFS…");
            Console.WriteLine("Make sure MSFS is running and you're in a flight (not in the main menu).");
            
            if (realSimConnectService?.Connect() == true)
            {
                Console.WriteLine("Connection initiated. Waiting for MSFS response…");
                
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
                Console.WriteLine("\n🚀 Starting monitoring automatically…");
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
                Console.WriteLine("\n🚀 Starting monitoring automatically…");
                await StartMonitoring();
            }
            else
            {
                Console.WriteLine("❌ No route provided.");
            }
        }
        
        private static async Task StartMonitoring()
        {
            Console.WriteLine("\nStarting GPS fix monitoring…");
            Console.WriteLine("The system will now track your position and log when you pass GPS fixes.");
            Console.WriteLine("Position will be updated every 5 seconds. Press Ctrl+C to stop monitoring.");
            Console.WriteLine("Monitoring will automatically stop when aircraft speed drops below 45 knots.\n");
            
            realSimConnectService?.StartDataRequest();
            
            // Wait a moment for data to be received
            await Task.Delay(500);
            
            bool monitoringActive = true;
            
            // Reset flight state tracking
            _hasBeenAirborne = false;
            _isCurrentlyAirborne = false;
            _firstAirborneTime = null;
            _takeoffRecorded = false;
            _landingRecorded = false;
            Position? previousPosition = null;
            
            // Continuously display updated position every 5 seconds
            while (!cancellationTokenSource?.Token.IsCancellationRequested == true && monitoringActive)
            {
                try
                {

                    if (_currentAircraftData != null)
                    {
                        if (_isCurrentlyAirborne)
                        {
                            // Calculate distance from previous position if available
                            if (previousPosition != null)
                            {
                                _distance += GpsFixTracker.CalculateDistance(previousPosition.Value, _currentAircraftData.Position);
                            }
                            previousPosition = _currentAircraftData.Position;                            
                        }

                        // Display the current status (this will clear and redraw everything)
                        DisplayCurrentStatus();

                        // Only stop monitoring if we've been airborne and are now slow (post-flight taxi)
                        if (_hasBeenAirborne && _currentAircraftData.GroundSpeed < 45.0)
                        {
                            Console.WriteLine($"\n🛬 Aircraft has landed and is now taxiing ({_currentAircraftData.GroundSpeed.ToString("F0", InvariantCulture)} kts).");
                            Console.WriteLine("Automatically stopping monitoring (flight completed)…");
                            monitoringActive = false;
                            // Calculate distance one last time
                            if (previousPosition != null)
                                _distance += GpsFixTracker.CalculateDistance(previousPosition.Value, _currentAircraftData.Position);
                        }
                    }
                    else
                    {
                        DisplayCurrentStatus();
                    }

                    if (monitoringActive)
                        await Task.Delay(5000, cancellationTokenSource?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException) { break; }
            }
            
            // Monitoring loop has ended, shutdown gracefully
            StopMonitoringAndExit();
        }
        
        private static void StopMonitoring()
        {
            Console.WriteLine("\nStopping monitoring…");
            
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
        
        private static void StopMonitoringAndExit()
        {
            // Stop monitoring and save data
            StopMonitoring();
            
            // Cancel background tasks
            cancellationTokenSource?.Cancel();
            
            // Disconnect from MSFS
            realSimConnectService?.Disconnect();
            
            Console.WriteLine("Goodbye! Press any key to quit…");
            Console.ReadKey();
        }
        
        private static void OnSimConnectConnected(object? sender, EventArgs e) => Console.WriteLine("Successfully connected to MSFS!");
        
        private static void OnSimConnectDisconnected(object? sender, EventArgs e) => Console.WriteLine("Disconnected from MSFS.");
        
        private static void OnDataReceived(object? sender, AircraftData aircraftData)
        {
            // Store current aircraft data
            _currentAircraftData = aircraftData;
            
            // Update current aircraft title
            currentAircraftTitle = aircraftData.AircraftTitle;
            
            // Update flight state tracking in real-time
            bool wasAirborne = _isCurrentlyAirborne;
            _isCurrentlyAirborne = aircraftData.GroundSpeed > 45.0;

            // Check if aircraft just became airborne (takeoff)
            if (!wasAirborne && _isCurrentlyAirborne && !_takeoffRecorded)
            {
                _hasBeenAirborne = true;
                _firstAirborneTime = systemClock.Now;
                _takeoffRecorded = true;

                _distance = 0; // Reset distance for new flight

                // Record takeoff event with airport information
                var departureAirport = currentFlightPlan?.DepartureID ?? "UNKNOWN";
                var takeoffData = new GpsFixData(aircraftData, systemClock.Now, $"TAKEOFF {departureAirport}");
                takeoffData.DistanceFromPrevious = (int)Math.Round(_distance);

                gpsFixTracker?.AddPassedFix(takeoffData);
            }
            
            // Check if aircraft just landed (was airborne, now slow)
            if (wasAirborne && !_isCurrentlyAirborne && _hasBeenAirborne && !_landingRecorded)
            {
                _landingRecorded = true;
                
                // Record landing event with airport information
                var destinationAirport = currentFlightPlan?.DestinationID ?? "UNKNOWN";
                var landingData = new GpsFixData(aircraftData, systemClock.Now, $"LANDING {destinationAirport}");
                landingData.DistanceFromPrevious = (int)Math.Round(_distance);
                
                gpsFixTracker?.AddPassedFix(landingData);
            }
            
            // If aircraft just became airborne, mark as has been airborne
            if (!wasAirborne && _isCurrentlyAirborne)
                _hasBeenAirborne = true;

            // Check if we're near any GPS fixes, passing current flight state
            if (gpsFixTracker?.CheckPosition(aircraftData, _hasBeenAirborne, _distance) is true)
                _distance = 0; // Reset distance after passing a fix
        }
        
        private static void DisplayCurrentStatus()
        {
            // Clear the console and redraw everything
            try
            {
                Console.Clear();
            } catch {}
            
            // Header
            Console.WriteLine("=== MSFS OFP Log - Flight Monitoring ===");
            Console.WriteLine();
            
            // Current position block
            if (_currentAircraftData != null)
            {
                // Determine flight phase
                string flightPhase = "";
                if (!_hasBeenAirborne && !_isCurrentlyAirborne)
                {
                    flightPhase = "TAXI (Pre-flight)";
                }
                else if (_hasBeenAirborne && _isCurrentlyAirborne)
                {
                    flightPhase = "AIRBORNE";
                }
                else if (_hasBeenAirborne && !_isCurrentlyAirborne)
                {
                    flightPhase = "TAXI (Post-flight)";
                }
                
                Console.WriteLine($"Current Aircraft Position: {systemClock.Now:HH:mm}Z [{flightPhase}]");
                Console.WriteLine($"  Latitude:  {_currentAircraftData.Latitude.ToDecString(6)}°");
                Console.WriteLine($"  Longitude: {_currentAircraftData.Longitude.ToDecString(6)}°");
                Console.WriteLine($"  Altitude:  {_currentAircraftData.Altitude.ToDecString(0)} ft (FL{(_currentAircraftData.AltitudeStandard / 100).ToDecString(0)})");
                Console.WriteLine($"  Speed:     {_currentAircraftData.GroundSpeed.ToDecString(0)} kts (IAS: {_currentAircraftData.TrueAirspeed.ToDecString(0)} kts, M{_currentAircraftData.MachNumber.ToDecString(2)})");
                Console.WriteLine($"  Heading:   {_currentAircraftData.Heading.ToDecString(0)}°");
                Console.WriteLine($"  Fuel:      {_currentAircraftData.FuelTotalQuantity.GallonsToTonnesString()} t ({_currentAircraftData.FuelRemainingPercentage.ToDecString(1)}%)");
                Console.WriteLine($"  Aircraft:  {_currentAircraftData.AircraftTitle}");
            }
            else
            {
                Console.WriteLine("Waiting for aircraft data from MSFS…");
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
                    // Use special icons for takeoff/landing
                    string icon = fix.FixName.StartsWith("TAKEOFF") ? "🛫" : 
                                 fix.FixName.StartsWith("LANDING") ? "🛬" : "🎯";
                    string prefix = $"  {icon} ";
                    Console.WriteLine($"{prefix}{fix.FixName,-12} {fix.Timestamp:HH:mm:ss}Z - {fix.FuelRemaining.ToString("F0", InvariantCulture)} kg ({fix.FuelRemainingPercentage.ToString("F1", InvariantCulture)}%) - {fix.GroundSpeed.ToString("F0", InvariantCulture)} kts");
                }
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
