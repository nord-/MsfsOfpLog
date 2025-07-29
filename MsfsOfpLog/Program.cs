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
        private static AudioService? audioService;
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
        private static int _v1Speed = 0; // V1 speed in knots
        private static int _vrSpeed = 0; // VR (rotate) speed in knots
        private static bool _hundredKnotsCalloutMade = false; // Track if 100 knots callout has been made
        private static bool _v1CalloutMade = false; // Track if V1 callout has been made
        private static bool _vrCalloutMade = false; // Track if VR callout has been made
        private static bool _positiveRateCalloutMade = false; // Track if positive rate callout has been made
        private static bool _lightsOffCalloutMade = false; // Track if lights off callout has been made
        private static bool _lightsOnCalloutMade = false; // Track if lights on callout has been made
        private static bool _hasBeenAbove10000 = false; // Track if aircraft has been above 10,000 feet
        private static bool _inTakeoffRoll = false; // Track if we're currently in takeoff roll
        
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
            audioService = new AudioService();
            cancellationTokenSource = new CancellationTokenSource();

            // Auto-connect on startup (but continue even if it fails)
            var initialConnection = await ConnectToMsfs();
            if (!initialConnection)
            {
                Console.WriteLine("\n⚠️  SimConnect connection failed, but you can still load flight plans.");
                Console.WriteLine("   The application will retry connecting when you start monitoring.");
            }

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
                Console.WriteLine("3. Load SimBrief flight plan (starts monitoring automatically)");
                Console.WriteLine("4. Start monitoring (manual start)");
                Console.WriteLine("5. Stop monitoring");
                Console.WriteLine("6. Clear stored SimBrief User ID");
                Console.WriteLine("7. Test audio system");
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
                        await LoadSimBriefFlightPlan();
                        // LoadSimBriefFlightPlan now automatically starts monitoring
                        return;
                    case "4":
                        // For manual start, offer to configure takeoff speeds if not set
                        if (_v1Speed <= 0 || _vrSpeed <= 0)
                        {
                            Console.WriteLine("\nTakeoff speeds not configured.");
                            Console.Write("Would you like to configure takeoff speeds for audio callouts? (y/n): ");
                            if (Console.ReadLine()?.ToLower() == "y")
                            {
                                GetTakeoffSpeeds();
                            }
                        }
                        await StartMonitoring();
                        // StartMonitoring now runs continuously until Ctrl+C
                        return;
                    case "5":
                        StopMonitoringAndExit();
                        return;
                    case "6":
                        ClearSimBriefUserId();
                        break;
                    case "7":
                        await TestAudioSystem();
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }
        
        private static async Task<bool> ConnectToMsfs()
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
                
                return true;
            }
            else
            {
                Console.WriteLine("❌ Failed to connect to MSFS. Please ensure:");
                Console.WriteLine("   1. MSFS is running");
                Console.WriteLine("   2. You are in a flight (not in the main menu)");
                Console.WriteLine("   3. SimConnect is enabled in MSFS settings");
                return false;
            }
        }
        
        private static void ClearSimBriefUserId()
        {
            Console.WriteLine("\nClear SimBrief User ID:");
            
            using var simBriefService = new SimBriefService();
            
            if (string.IsNullOrEmpty(simBriefService.SimBriefUserId))
            {
                Console.WriteLine("No SimBrief User ID is currently stored.");
            }
            else
            {
                Console.WriteLine($"Current SimBrief User ID: {simBriefService.SimBriefUserId}");
                Console.Write("Are you sure you want to clear all stored SimBrief data? (y/n): ");
                
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    simBriefService.ClearStoredData();
                    Console.WriteLine("✅ All SimBrief data cleared successfully.");
                }
                else
                {
                    Console.WriteLine("Operation cancelled.");
                }
            }
        }
        
        private static void GetTakeoffSpeeds()
        {
            Console.WriteLine("\n=== Takeoff Speeds Configuration ===");
            Console.WriteLine("Enter your takeoff speeds for audio callouts (or press Enter to skip):");
            
            // Get V1 speed
            Console.Write("Enter V1 speed (knots): ");
            var v1Input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(v1Input))
            {
                Console.WriteLine("⏭️  Skipping takeoff speed configuration - no audio callouts will be played.");
                _v1Speed = 0;
                _vrSpeed = 0;
                return;
            }
            
            if (int.TryParse(v1Input, out var v1) && v1 > 0 && v1 < 400)
            {
                _v1Speed = v1;
            }
            else
            {
                Console.WriteLine("❌ Invalid V1 speed. Skipping takeoff speed configuration.");
                _v1Speed = 0;
                _vrSpeed = 0;
                return;
            }
            
            // Get VR speed
            Console.Write("Enter VR (rotate) speed (knots): ");
            var vrInput = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(vrInput))
            {
                Console.WriteLine("⏭️  Skipping VR speed - no audio callouts will be played.");
                _v1Speed = 0;
                _vrSpeed = 0;
                return;
            }
            
            if (int.TryParse(vrInput, out var vr) && vr > 0 && vr < 400)
            {
                _vrSpeed = vr;
            }
            else
            {
                Console.WriteLine("❌ Invalid VR speed. Skipping takeoff speed configuration.");
                _v1Speed = 0;
                _vrSpeed = 0;
                return;
            }
            
            // Validate that VR >= V1
            if (_vrSpeed < _v1Speed)
            {
                Console.WriteLine("⚠️  Warning: VR speed should typically be equal to or greater than V1 speed.");
                Console.Write("Continue anyway? (y/n): ");
                if (Console.ReadLine()?.ToLower() != "y")
                {
                    _v1Speed = 0;
                    _vrSpeed = 0;
                    GetTakeoffSpeeds(); // Restart the process
                    return;
                }
            }
            
            Console.WriteLine($"✅ Takeoff speeds configured:");
            Console.WriteLine($"   V1: {_v1Speed} kts");
            Console.WriteLine($"   VR: {_vrSpeed} kts");
            Console.WriteLine("   Audio callouts will be played during takeoff roll.");
        }
        
        private static async Task TestAudioSystem()
        {
            Console.WriteLine("\n=== Audio System Test ===");
            
            if (audioService == null)
            {
                Console.WriteLine("❌ Audio service not initialized.");
                return;
            }
            
            var availableFiles = audioService.GetAvailableAudioFiles().ToList();
            Console.WriteLine($"📁 Available audio files: {string.Join(", ", availableFiles)}");
            
            if (availableFiles.Count == 0)
            {
                Console.WriteLine("❌ No audio files found.");
                return;
            }
            
            Console.WriteLine("\nTesting each audio file:");
            foreach (var file in availableFiles)
            {
                var audioName = Path.GetFileNameWithoutExtension(file);
                Console.WriteLine($"🔊 Playing: {file}");
                audioService.PlayAudio(audioName);
                
                Console.WriteLine("   Press any key to continue to next audio or 'q' to quit test...");
                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    break;
                    
                audioService.StopAudio();
                await Task.Delay(500); // Small delay between audio files
            }
            
            audioService.StopAudio();
            Console.WriteLine("✅ Audio test completed.");
        }
        
        private static async Task LoadSimBriefFlightPlan()
        {
            Console.WriteLine("\nLoad SimBrief Flight Plan:");
            
            using var simBriefService = new SimBriefService();
            
            // Check if we need to configure SimBrief User ID
            if (string.IsNullOrEmpty(simBriefService.SimBriefUserId))
            {
                Console.WriteLine("SimBrief User ID not configured.");
                Console.WriteLine("\nYou need either:");
                Console.WriteLine("• Your Navigraph username (from https://navigraph.com/account/settings)");
                Console.WriteLine("• Your SimBrief Pilot ID (from https://dispatch.simbrief.com/account)");
                Console.Write("\nEnter your Navigraph username or SimBrief Pilot ID: ");
                var userId = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(userId))
                {
                    Console.WriteLine("❌ SimBrief User ID is required.");
                    return;
                }
                
                simBriefService.SimBriefUserId = userId;
                Console.WriteLine($"✅ SimBrief User ID saved: {userId}");
            }
            else
            {
                Console.WriteLine($"📋 Using stored SimBrief User ID: {simBriefService.SimBriefUserId}");
            }
            
            // Always get the latest flight plan
            Console.WriteLine("\n⏳ Fetching latest flight plan from SimBrief...");
            var flightPlanInfo = await simBriefService.GetLatestFlightPlanAsync();
            
            if (flightPlanInfo != null)
            {
                // Store the flight plan for use in takeoff/landing recording
                currentFlightPlan = flightPlanInfo;
                
                // Display flight plan information
                await DisplaySimBriefFlightPlanInfoAsync(flightPlanInfo);
                
                // Reset current GPS fixes and load the new ones
                gpsFixTracker?.Reset();
                
                // Add all waypoints from the flight plan
                foreach (var waypoint in flightPlanInfo.Waypoints)
                {
                    gpsFixTracker?.AddGpsFix(waypoint);
                }
                
                Console.WriteLine($"✅ Successfully loaded {flightPlanInfo.Waypoints.Count} waypoints from SimBrief!");
                Console.WriteLine($"   Route: {flightPlanInfo.DepartureID} → {flightPlanInfo.DestinationID}");
                Console.WriteLine($"   Ready to monitor your flight from {flightPlanInfo.DepartureName} to {flightPlanInfo.DestinationName}");
                
                // Get takeoff speeds before continuing
                GetTakeoffSpeeds();
                
                Console.WriteLine("\nPress any key to continue (auto-continuing in 20 seconds)…");
                
                // Wait for key press with 20 second timeout
                var keyTask = Task.Run(() => Console.ReadKey());
                var timeoutTask = Task.Delay(20000);
                
                if (await Task.WhenAny(keyTask, timeoutTask) == timeoutTask)
                {
                    Console.WriteLine("\nTimeout reached, continuing automatically…");
                }
                
                // Automatically start monitoring
                Console.WriteLine("\n🚀 Starting monitoring automatically…");
                await StartMonitoring();
            }
            else
            {
                Console.WriteLine("❌ Failed to load flight plan from SimBrief.");
            }
        }
        
        private static async Task DisplaySimBriefFlightPlanInfoAsync(FlightPlanParser.FlightPlanInfo flightPlan)
        {
            Console.WriteLine("\n=== SimBrief Flight Plan Loaded ===");
            Console.WriteLine($"Route: {flightPlan.DepartureID} → {flightPlan.DestinationID}");
            Console.WriteLine($"Departure: {flightPlan.DepartureName} ({flightPlan.DepartureID})");
            Console.WriteLine($"Destination: {flightPlan.DestinationName} ({flightPlan.DestinationID})");
            Console.WriteLine($"Cruise Altitude: FL{(flightPlan.CruisingAltitude / 100):000}");
            Console.WriteLine($"Total Waypoints: {flightPlan.Waypoints.Count}");
            
            if (flightPlan.Waypoints.Count > 0)
            {
                Console.WriteLine("\nRoute Preview:");
                var previewCount = Math.Min(5, flightPlan.Waypoints.Count);
                for (int i = 0; i < previewCount; i++)
                {
                    var wp = flightPlan.Waypoints[i];
                    Console.WriteLine($"  {i + 1,2}. {wp.Name,-8} ({wp.Latitude:F4}, {wp.Longitude:F4})");
                }
                
                if (flightPlan.Waypoints.Count > 5)
                {
                    Console.WriteLine($"  ... and {flightPlan.Waypoints.Count - 5} more waypoints");
                }
            }
            
            Console.WriteLine();
            await Task.Delay(1); // Make it async
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
                await FlightPlanParser.DisplayFlightPlanInfoAsync(flightPlanInfo);
                
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
                
                // Get takeoff speeds before continuing
                GetTakeoffSpeeds();
                
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
                
                // Get takeoff speeds before continuing
                GetTakeoffSpeeds();
                
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
            
            // Ensure SimConnect is connected before starting monitoring
            bool isConnected = realSimConnectService?.IsConnected ?? false;
            if (!isConnected)
            {
                Console.WriteLine("🔄 SimConnect not connected. Attempting to connect...");
                
                // Try to connect with retries every 30 seconds
                while (!isConnected && !cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    isConnected = await ConnectToMsfs();
                    
                    if (!isConnected)
                    {
                        Console.WriteLine("⏳ Connection failed. Retrying in 30 seconds... (Press Ctrl+C to cancel)");
                        try
                        {
                            await Task.Delay(30000, cancellationTokenSource?.Token ?? CancellationToken.None);
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("Connection retry cancelled by user.");
                            return;
                        }
                    }
                }
                
                if (!isConnected)
                {
                    Console.WriteLine("❌ Could not establish SimConnect connection. Monitoring cancelled.");
                    return;
                }
            }
            
            Console.WriteLine("✅ SimConnect connected successfully!");
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
            ResetTakeoffCallouts();
            ResetLightingCallouts();
            Position? previousPosition = null;
            
            // Continuously display updated position with dynamic polling frequency
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
                    {
                        // Dynamic polling frequency: 200ms (5 Hz) during takeoff roll, 5000ms otherwise
                        int delayMs = _inTakeoffRoll ? 200 : 5000;
                        await Task.Delay(delayMs, cancellationTokenSource?.Token ?? CancellationToken.None);
                    }
                }
                catch (OperationCanceledException) { break; }
            }
            
            // Monitoring loop has ended, shutdown gracefully
            StopMonitoringAndExit();
        }
        
        private static void HandleTakeoffAudioCallouts(AircraftData aircraftData)
        {
            // Only make callouts if speeds are configured and we have audio service
            if ((_v1Speed <= 0 || _vrSpeed <= 0) || audioService == null)
                return;
            
            var indicatedAirspeed = (int)Math.Round(aircraftData.TrueAirspeed); // Using IAS instead of GS
            
            // 100 knots callout
            if (!_hundredKnotsCalloutMade && indicatedAirspeed >= 100)
            {
                _hundredKnotsCalloutMade = true;
                audioService.PlayAudio("100knots");
                Console.WriteLine($"🔊 Audio: 100 knots ({indicatedAirspeed} kts IAS)");
            }
            
            // V1 callout
            if (!_v1CalloutMade && indicatedAirspeed >= _v1Speed)
            {
                _v1CalloutMade = true;
                audioService.PlayAudio("v1");
                Console.WriteLine($"🔊 Audio: V1 ({indicatedAirspeed} kts IAS)");
            }
            
            // VR callout (only if different from V1 or both are the same but V1 hasn't been called yet)
            if (!_vrCalloutMade && indicatedAirspeed >= _vrSpeed)
            {
                _vrCalloutMade = true;
                // If V1 and VR are the same speed and V1 hasn't been called, call both
                if (_vrSpeed == _v1Speed && !_v1CalloutMade)
                {
                    _v1CalloutMade = true;
                    audioService.PlayAudio("v1");
                    Console.WriteLine($"🔊 Audio: V1 ({indicatedAirspeed} kts IAS)");
                    // Small delay between callouts
                    Task.Delay(500).ContinueWith(_ => {
                        audioService?.PlayAudio("rotate");
                        Console.WriteLine($"🔊 Audio: Rotate ({indicatedAirspeed} kts IAS)");
                    });
                }
                else
                {
                    audioService.PlayAudio("rotate");
                    Console.WriteLine($"🔊 Audio: Rotate ({indicatedAirspeed} kts IAS)");
                }
            }
            
            // Positive rate callout: Above 50ft AGL, vertical speed > 500ft/min, and gear still down
            if (!_positiveRateCalloutMade && 
                aircraftData.AltitudeAGL > 50 && 
                aircraftData.VerticalSpeed > 500 && 
                aircraftData.GearPosition > 0.5) // Gear is down (position > 0.5 means gear is down or partially down)
            {
                _positiveRateCalloutMade = true;
                audioService.PlayAudio("positive_rate");
                Console.WriteLine($"🔊 Audio: Positive rate ({aircraftData.AltitudeAGL:F0} ft AGL, VS: {aircraftData.VerticalSpeed:F0} fpm)");
            }
        }
        
        private static void HandleLightingCallouts(AircraftData aircraftData)
        {
            // Only make callouts if we have audio service
            if (audioService == null)
                return;
            
            var altitude = aircraftData.Altitude;
            
            // Lights off callout when climbing and passing 10,000 feet (first time)
            if (!_lightsOffCalloutMade && !_hasBeenAbove10000 && altitude > 10000)
            {
                _lightsOffCalloutMade = true;
                _hasBeenAbove10000 = true;
                audioService.PlayAudio("lights_off");
                Console.WriteLine($"🔊 Audio: Lights off ({altitude:F0} ft)");
            }
            
            // Track if we've been above 10,000 feet (for descent tracking)
            if (altitude > 10000)
            {
                _hasBeenAbove10000 = true;
            }
            
            // Lights on callout when descending below 10,000 feet (after having been above)
            if (!_lightsOnCalloutMade && altitude < 10000 && _hasBeenAbove10000)
            {
                _lightsOnCalloutMade = true;
                audioService.PlayAudio("lights_on");
                Console.WriteLine($"🔊 Audio: Lights on ({altitude:F0} ft)");
            }
        }
        
        private static void ResetTakeoffCallouts()
        {
            _hundredKnotsCalloutMade = false;
            _v1CalloutMade = false;
            _vrCalloutMade = false;
            _positiveRateCalloutMade = false;
            _inTakeoffRoll = false;
        }
        
        private static void ResetLightingCallouts()
        {
            _lightsOffCalloutMade = false;
            _lightsOnCalloutMade = false;
            _hasBeenAbove10000 = false;
        }
        
        private static void StopMonitoring()
        {
            Console.WriteLine("\nStopping monitoring…");
            
            // Reset takeoff speeds and callouts for next flight
            _v1Speed = 0;
            _vrSpeed = 0;
            ResetTakeoffCallouts();
            ResetLightingCallouts();
            
            // Save flight summary
            var passedFixes = gpsFixTracker?.GetPassedFixes();
            if (passedFixes != null && passedFixes.Count > 0)
            {
                dataLogger?.SaveFlightSummary(passedFixes, currentAircraftTitle, currentFlightPlan);
                Console.WriteLine($"Flight completed! {passedFixes.Count} GPS fixes were logged.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("No GPS fixes were passed during this monitoring session.");
                Console.ResetColor();
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
            
            // Dispose audio service
            audioService?.Dispose();
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Goodbye! ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Press any key to quit…");
            Console.ResetColor();
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
            
            // Detect takeoff roll: ground speed > 40 kts but not yet airborne
            bool wasInTakeoffRoll = _inTakeoffRoll;
            _inTakeoffRoll = !_isCurrentlyAirborne && !_hasBeenAirborne && aircraftData.GroundSpeed > 40.0;
            
            // If we just entered takeoff roll, reset callouts for this takeoff
            if (!wasInTakeoffRoll && _inTakeoffRoll)
            {
                ResetTakeoffCallouts();
                _inTakeoffRoll = true; // Ensure it's set after reset
                Console.WriteLine($"🛫 Takeoff roll detected at {aircraftData.GroundSpeed:F0} kts");
            }
            
            // Handle audio callouts during takeoff roll and initial climb
            if (_inTakeoffRoll || (!_positiveRateCalloutMade && _hasBeenAirborne && _isCurrentlyAirborne))
            {
                HandleTakeoffAudioCallouts(aircraftData);
            }
            
            // Handle lighting callouts throughout the flight
            HandleLightingCallouts(aircraftData);

            // Check if aircraft just became airborne (takeoff)
            if (!wasAirborne && _isCurrentlyAirborne && !_takeoffRecorded)
            {
                _hasBeenAirborne = true;
                _firstAirborneTime = systemClock.Now;
                _takeoffRecorded = true;
                _inTakeoffRoll = false; // No longer in takeoff roll once airborne

                _distance = 0; // Reset distance for new flight

                // Record takeoff event with airport information
                var departureAirport = currentFlightPlan?.DepartureID ?? "UNKNOWN";
                var takeoffData = new GpsFixData(aircraftData, systemClock.Now, $"TAKEOFF {departureAirport}");
                takeoffData.DistanceFromPrevious = (int)Math.Round(_distance);

                gpsFixTracker?.AddPassedFix(takeoffData);
                Console.WriteLine($"✈️ Aircraft airborne at {aircraftData.GroundSpeed:F0} kts");
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
                if (_inTakeoffRoll)
                {
                    flightPhase = "TAKEOFF ROLL";
                }
                else if (!_hasBeenAirborne && !_isCurrentlyAirborne)
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
                Console.WriteLine($"  Position:  {_currentAircraftData.Position}");
                Console.WriteLine($"  Altitude:  {_currentAircraftData.Altitude.ToDecString(0)} ft (FL{_currentAircraftData.FlightLevel:000}) | AGL: {_currentAircraftData.AltitudeAGL.ToDecString(0)} ft");
                Console.WriteLine($"  Speed:     {_currentAircraftData.GroundSpeed.ToDecString(0)} kts (IAS: {_currentAircraftData.TrueAirspeed.ToDecString(0)} kts, M{_currentAircraftData.MachNumber.ToDecString(2)})");
                Console.WriteLine($"  V/S & Hdg: {_currentAircraftData.VerticalSpeed.ToDecString(0)} fpm | {_currentAircraftData.Heading.ToDecString(0)}°");
                Console.WriteLine($"  Fuel:      {_currentAircraftData.FuelTotalQuantity.GallonsToTonnesString()} t ({_currentAircraftData.FuelRemainingPercentage.ToDecString(1)}%)");
                Console.WriteLine($"  Aircraft:  {_currentAircraftData.AircraftTitle}");
                Console.WriteLine($"  Gear:      {(_currentAircraftData.GearPosition > 0.5 ? "DOWN" : "UP")} ({_currentAircraftData.GearPosition.ToDecString(1)})");
                
                // Show takeoff speeds and callout status if configured
                if (_v1Speed > 0 && _vrSpeed > 0)
                {
                    var calloutsStatus = new List<string>();
                    if (_hundredKnotsCalloutMade) calloutsStatus.Add("100kts✓");
                    if (_v1CalloutMade) calloutsStatus.Add($"V1({_v1Speed})✓");
                    if (_vrCalloutMade) calloutsStatus.Add($"VR({_vrSpeed})✓");
                    if (_positiveRateCalloutMade) calloutsStatus.Add("PosRate✓");
                    
                    var statusText = calloutsStatus.Count > 0 ? string.Join(" ", calloutsStatus) : "None yet";
                    Console.WriteLine($"  Callouts:  {statusText}");
                }
                
                // Show lighting callout status
                var lightingStatus = new List<string>();
                if (_lightsOffCalloutMade) lightingStatus.Add("LightsOff✓");
                if (_lightsOnCalloutMade) lightingStatus.Add("LightsOn✓");
                if (_hasBeenAbove10000) lightingStatus.Add("Above10k");
                
                if (lightingStatus.Count > 0)
                {
                    var lightingText = string.Join(" ", lightingStatus);
                    Console.WriteLine($"  Lighting:  {lightingText}");
                }
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
                    Console.WriteLine($"{prefix}{fix.FixName,-12} {fix.Timestamp:HHmm} • {fix.FuelRemaining.KgToTonnesString()} t ({fix.FuelRemainingPercentage.ToString("F1", InvariantCulture)}%)");
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
