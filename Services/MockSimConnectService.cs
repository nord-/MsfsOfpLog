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
        
        // Mock flight data - simulate realistic flight from LGRP (Rhodes) to ESSA (Stockholm)
        // Based on actual flight plan waypoints
        private readonly (double lat, double lon, double fuel, double speed, double alt, double heading)[] mockFlightPath = 
        {
            // Ground/taxi phase at LGRP (Rhodes/Diagoras Airport)
            (36.400, 28.080, 8800, 15, 19, 240), // LGRP airport - taxiing
            (36.402, 28.082, 8798, 25, 19, 240), // Still taxiing
            (36.405, 28.085, 8796, 35, 19, 240), // Approaching runway
            
            // Takeoff phase
            (36.408, 28.088, 8794, 80, 500, 240), // Takeoff roll
            (36.412, 28.092, 8792, 120, 3000, 240), // Climbing out
            (36.415, 28.095, 8790, 160, 8000, 240), // Continuing climb
            
            // Cruise phase - following actual flight plan waypoints
            (36.385, 27.731, 8750, 460, 38000, 010), // VANES waypoint during cruise
            (36.468, 27.060, 8700, 465, 38000, 015), // ETERU waypoint
            (36.487, 26.900, 8650, 470, 38000, 020), // GILOS waypoint
            (36.533, 26.511, 8600, 475, 38000, 025), // ADESO waypoint
            (55.638, 17.161, 8500, 480, 38000, 030), // PENOR waypoint (skipping middle for demo)
            (57.500, 17.346, 8450, 485, 38000, 035), // ARMOD waypoint
            (58.815, 17.884, 8400, 450, 20000, 040), // NILUG waypoint - descent started
            
            // Descent phase
            (59.200, 17.900, 8380, 400, 15000, 045), // Initial descent
            (59.400, 17.920, 8360, 350, 8000, 050), // Continuing descent
            (59.600, 17.940, 8350, 300, 3000, 055), // Final approach
            
            // Landing phase at ESSA (Stockholm/Arlanda Airport)
            (59.652, 17.919, 8348, 150, 1000, 060), // ESSA approach
            (59.654, 17.921, 8346, 80, 500, 060), // Touchdown
            (59.656, 17.923, 8344, 50, 138, 060), // Rollout
            (59.658, 17.925, 8342, 30, 138, 060), // Taxiing to gate
            (59.660, 17.927, 8340, 15, 138, 060), // Parking
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
        
        private Task SimulateDataReceived()
        {
            if (currentPositionIndex >= mockFlightPath.Length)
            {
                Console.WriteLine("Mock SimConnect: End of simulated flight path - aircraft parked");
                return Task.CompletedTask;
            }
            
            var position = mockFlightPath[currentPositionIndex];
            
            var aircraftData = new AircraftData
            {
                Latitude = position.lat,
                Longitude = position.lon,
                FuelTotalQuantity = position.fuel,
                FuelTotalCapacity = 9600, // Mock fuel capacity in kg (realistic for A320)
                GroundSpeed = position.speed,
                Altitude = position.alt,
                Heading = position.heading,
                AircraftTitle = "Airbus A320neo - LGRP to ESSA"
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
            
            return Task.CompletedTask;
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
