using System;
using System.Threading.Tasks;
using System.Threading;
using MsfsOfpLog.Models;

namespace MsfsOfpLog.Services
{
    /// <summary>
    /// Mock SimConnect service for testing without actual MSFS connection
    /// Replace this with the real SimConnectService when you have the proper SDK
    /// </summary>
    public class MockSimConnectService
    {
        private bool isConnected = false;
        private bool isRunning = false;
        private CancellationTokenSource? cancellationTokenSource;
        
        public event EventHandler<AircraftData>? DataReceived;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        
        // Mock flight data - simulate a realistic flight from Vienna to Graz with taxi/takeoff/landing phases
        private readonly (double lat, double lon, double fuel, double speed, double alt, double heading)[] mockFlightPath = 
        {
            // Ground/taxi phase - low speed
            (48.1103, 16.5697, 3020, 15, 1200, 270), // Vienna airport - taxiing
            (48.1105, 16.5700, 3018, 25, 1200, 270), // Still taxiing
            (48.1108, 16.5703, 3016, 35, 1200, 270), // Approaching runway
            
            // Takeoff phase - increasing speed
            (48.1110, 16.5706, 3014, 80, 1500, 270), // Takeoff roll
            (48.1115, 16.5710, 3012, 120, 3000, 270), // Climbing out
            (48.1120, 16.5715, 3010, 160, 8000, 270), // Continuing climb
            
            // Cruise phase - high speed
            (48.0000, 16.3000, 2960, 185, 35000, 270), // Cruise
            (47.9000, 16.1000, 2900, 190, 35000, 270), // Cruise
            (47.8000, 15.9000, 2840, 185, 35000, 270), // Cruise
            (47.7000, 15.7000, 2780, 180, 35000, 270), // Cruise
            (47.6000, 15.5000, 2720, 175, 35000, 270), // Cruise
            (47.5000, 15.3000, 2660, 170, 35000, 270), // Cruise
            
            // Descent phase - decreasing speed
            (47.2000, 15.4000, 2630, 150, 15000, 270), // Initial descent
            (47.1000, 15.4200, 2610, 130, 8000, 270), // Continuing descent
            (47.0500, 15.4300, 2600, 110, 3000, 270), // Final approach
            
            // Landing phase - low speed
            (47.0200, 15.4380, 2598, 90, 1500, 270), // Short final
            (47.0100, 15.4390, 2596, 70, 800, 270), // Touchdown
            (47.0080, 15.4395, 2594, 50, 600, 270), // Rollout
            (47.0077, 15.4396, 2592, 30, 600, 270), // Taxiing to gate
            (47.0075, 15.4398, 2590, 15, 600, 270), // Parking
        };
        
        private int currentPositionIndex = 0;
        
        public bool Connect()
        {
            try
            {
                Console.WriteLine("Mock SimConnect: Simulating connection to MSFS...");
                isConnected = true;
                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mock SimConnect connection failed: {ex.Message}");
                return false;
            }
        }
        
        public void StartDataRequest()
        {
            if (!isConnected)
            {
                Console.WriteLine("Mock SimConnect: Not connected");
                return;
            }
            
            if (isRunning)
            {
                Console.WriteLine("Mock SimConnect: Already running");
                return;
            }
            
            isRunning = true;
            cancellationTokenSource = new CancellationTokenSource();
            
            Console.WriteLine("Mock SimConnect: Starting data simulation...");
            
            // Send initial data immediately
            _ = SimulateDataReceived();
            
            // Start background task to simulate data
            Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested && isRunning)
                {
                    try
                    {
                        await SimulateDataReceived();
                        await Task.Delay(2000, cancellationTokenSource.Token); // Send data every 2 seconds
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }
        
        public void ReceiveMessage()
        {
            // No-op for mock service
        }
        
        private async Task SimulateDataReceived()
        {
            if (currentPositionIndex >= mockFlightPath.Length)
            {
                Console.WriteLine("Mock SimConnect: End of simulated flight path - aircraft parked");
                return;
            }
            
            var position = mockFlightPath[currentPositionIndex];
            
            var aircraftData = new AircraftData
            {
                Latitude = position.lat,
                Longitude = position.lon,
                FuelTotalQuantity = position.fuel,
                FuelTotalCapacity = 3624, // Mock fuel capacity in kg (1200 gal * 3.02 kg/gal)
                GroundSpeed = position.speed,
                Altitude = position.alt,
                Heading = position.heading,
                AircraftTitle = "Mock Aircraft - Boeing 737-800"
            };
            
            // Determine flight phase based on speed and altitude
            string phase = position.speed < 45 ? "GROUND/TAXI" : 
                          position.speed < 100 ? "TAKEOFF/LANDING" : 
                          position.alt < 10000 ? "CLIMB/DESCENT" : "CRUISE";
            
            Console.WriteLine($"Mock SimConnect: Position {currentPositionIndex + 1}/{mockFlightPath.Length} [{phase}] - " +
                            $"Lat: {position.lat:F4}, Lon: {position.lon:F4}, Speed: {position.speed:F0} kts, " +
                            $"Alt: {position.alt:F0} ft, Fuel: {position.fuel:F0} kg");
            
            DataReceived?.Invoke(this, aircraftData);
            
            currentPositionIndex++;
            
            // Reset to beginning when we reach the end (for continuous simulation)
            if (currentPositionIndex >= mockFlightPath.Length)
            {
                currentPositionIndex = 0;
            }
        }
        
        public void Disconnect()
        {
            isRunning = false;
            cancellationTokenSource?.Cancel();
            
            if (isConnected)
            {
                isConnected = false;
                Console.WriteLine("Mock SimConnect: Disconnected");
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
