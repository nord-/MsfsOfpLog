using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;
using MsfsOfpLog.Models;

namespace MsfsOfpLog.Services
{
    public class RealSimConnectService
    {
        private SimConnect? simConnect;
        private readonly object simConnectLock = new object();
        private bool isConnected = false;
        private double? initialFuelAmount = null; // Track initial fuel for actual burn calculation
        
        public bool IsConnected => isConnected;
        
        public event EventHandler<AircraftData>? DataReceived;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        
        private const int WM_USER_SIMCONNECT = 0x0402;
        
        public enum DATA_REQUESTS
        {
            AIRCRAFT_DATA = 0
        }
        
        public enum DATA_DEFINITIONS
        {
            AIRCRAFT_DATA = 0
        }
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct AircraftDataStruct
        {
            public double Latitude;
            public double Longitude;
            public double FuelTotalQuantity;
            public double FuelTotalCapacity;
            public double GroundSpeed;
            public double Altitude;
            public double AltitudeStandard; // Added for calibrated altitude
            public double Heading;
            public double TrueAirspeed;
            public double MachNumber;
            public double OutsideAirTemperature;
            public double FuelBurnRate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string AircraftTitle;
        }
        
        public bool Connect()
        {
            try
            {
                lock (simConnectLock)
                {
                    if (simConnect != null)
                    {
                        Console.WriteLine("⚠️ Already connected to SimConnect");
                        return isConnected;
                    }
                    
                    Console.WriteLine("🔌 Initializing SimConnect connection...");
                    Console.WriteLine($"   Using window handle: {IntPtr.Zero}");
                    Console.WriteLine($"   Message ID: {WM_USER_SIMCONNECT}");
                    
                    simConnect = new SimConnect("MSFS OFP Log", IntPtr.Zero, WM_USER_SIMCONNECT, null, 0);
                    
                    Console.WriteLine("✅ SimConnect object created successfully");
                    
                    // Define data structure
                    Console.WriteLine("📋 Setting up data definitions...");
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "FUEL TOTAL QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "FUEL TOTAL CAPACITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "GROUND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "INDICATED ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "INDICATED ALTITUDE CALIBRATED", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "HEADING INDICATOR", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "AIRSPEED INDICATED", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "AIRSPEED MACH", "mach", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "AMBIENT TEMPERATURE", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "ENG FUEL FLOW GPH:1", "gallons per hour", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    simConnect.AddToDataDefinition(DATA_DEFINITIONS.AIRCRAFT_DATA, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                    
                    Console.WriteLine("✅ Data definitions added");
                    
                    // Register data structure
                    simConnect.RegisterDataDefineStruct<AircraftDataStruct>(DATA_DEFINITIONS.AIRCRAFT_DATA);
                    Console.WriteLine("✅ Data structure registered");
                    
                    // Set up events
                    simConnect.OnRecvOpen += OnRecvOpen;
                    simConnect.OnRecvQuit += OnRecvQuit;
                    simConnect.OnRecvSimobjectDataBytype += OnRecvSimobjectDataBytype;
                    simConnect.OnRecvException += OnRecvException;
                    Console.WriteLine("✅ Event handlers registered");
                    
                    Console.WriteLine("🎯 SimConnect initialized successfully - waiting for connection confirmation...");
                    return true;
                }
            }
            catch (COMException ex)
            {
                Console.WriteLine($"❌ SimConnect COM Exception:");
                Console.WriteLine($"   HRESULT: 0x{ex.HResult:X8}");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Source: {ex.Source}");
                
                // Common SimConnect error codes
                switch ((uint)ex.HResult)
                {
                    case 0x887A0004:
                        Console.WriteLine("💡 Suggestion: MSFS is not running or SimConnect is not available");
                        break;
                    case 0x887A0005:
                        Console.WriteLine("💡 Suggestion: SimConnect version mismatch");
                        break;
                    case 0x887A0006:
                        Console.WriteLine("💡 Suggestion: Too many SimConnect clients");
                        break;
                    default:
                        Console.WriteLine("💡 Suggestion: Unknown SimConnect error - check MSFS status");
                        break;
                }
                
                Console.WriteLine("\n🔧 Troubleshooting checklist:");
                Console.WriteLine("   1. Is MSFS running?");
                Console.WriteLine("   2. Are you in a flight (not main menu)?");
                Console.WriteLine("   3. Are other SimConnect apps running?");
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Unexpected error connecting to SimConnect:");
                Console.WriteLine($"   Type: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                return false;
            }
        }
        
        public void StartDataRequest()
        {
            if (simConnect != null && isConnected)
            {
                try
                {
                    // Request data on every frame (continuous updates)
                    simConnect.RequestDataOnSimObjectType(DATA_REQUESTS.AIRCRAFT_DATA, DATA_DEFINITIONS.AIRCRAFT_DATA, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                    Console.WriteLine("Started requesting aircraft data from MSFS");
                    
                    // Set up periodic data requests
                    Task.Run(async () =>
                    {
                        while (isConnected)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(2)); // Request data every other second

                                if (simConnect != null && isConnected)
                                {
                                    simConnect.RequestDataOnSimObjectType(DATA_REQUESTS.AIRCRAFT_DATA, DATA_DEFINITIONS.AIRCRAFT_DATA, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error in periodic data request: {ex.Message}");
                                break;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error starting data request: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Cannot start data request - not connected to MSFS");
            }
        }
        
        public void ReceiveMessage()
        {
            if (simConnect != null)
            {
                try
                {
                    simConnect.ReceiveMessage();
                }
                catch (COMException ex)
                {
                    Console.WriteLine($"SimConnect message error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error receiving message: {ex.Message}");
                }
            }
        }
        
        private void OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            isConnected = true;
            Console.WriteLine("✅ Successfully connected to MSFS!");
            Console.WriteLine($"   Application Name: {data.szApplicationName}");
            Console.WriteLine($"   Application Version: {data.dwApplicationVersionMajor}.{data.dwApplicationVersionMinor}");
            Console.WriteLine($"   SimConnect Version: {data.dwSimConnectVersionMajor}.{data.dwSimConnectVersionMinor}");
            Connected?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            isConnected = false;
            Console.WriteLine("❌ MSFS has quit");
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            if (data.dwRequestID == (uint)DATA_REQUESTS.AIRCRAFT_DATA)
            {
                var aircraftData = (AircraftDataStruct)data.dwData[0];
                
                // Set initial fuel amount on first data reception
                if (!initialFuelAmount.HasValue)
                {
                    initialFuelAmount = aircraftData.FuelTotalQuantity;
                }
                
                // Calculate actual burn
                var actualBurn = initialFuelAmount.HasValue ? FuelConverter.GallonsToKg(initialFuelAmount.Value - aircraftData.FuelTotalQuantity) : 0;
               
               var dataObj = new AircraftData
                {
                    Latitude = aircraftData.Latitude,
                    Longitude = aircraftData.Longitude,
                    FuelTotalQuantity = aircraftData.FuelTotalQuantity,
                    FuelTotalCapacity = aircraftData.FuelTotalCapacity,
                    GroundSpeed = aircraftData.GroundSpeed,
                    Altitude = aircraftData.Altitude,
                    AltitudeStandard = aircraftData.AltitudeStandard, // Use calibrated altitude
                    Heading = aircraftData.Heading,
                    TrueAirspeed = aircraftData.TrueAirspeed,
                    MachNumber = aircraftData.MachNumber,
                    OutsideAirTemperature = aircraftData.OutsideAirTemperature,
                    FuelBurnRate = FuelConverter.GallonsToKg(aircraftData.FuelBurnRate), // Convert from GPH to kg/hr
                    ActualBurn = actualBurn,
                    AircraftTitle = aircraftData.AircraftTitle
                }; 
                // var dataObj = new AircraftData
                // {
                //     Latitude = aircraftData.Latitude,
                //     Longitude = aircraftData.Longitude,
                //     FuelTotalQuantity = aircraftData.FuelTotalQuantity,
                //     FuelTotalCapacity = aircraftData.FuelTotalCapacity,
                //     GroundSpeed = aircraftData.GroundSpeed,
                //     Altitude = aircraftData.Altitude,
                //     AltitudeStandard = aircraftData.AltitudeStandard,
                //     Heading = aircraftData.Heading,
                //     TrueAirspeed = aircraftData.TrueAirspeed,
                //     MachNumber = aircraftData.MachNumber,
                //     OutsideAirTemperature = aircraftData.OutsideAirTemperature,
                //     FuelBurnRate = FuelConverter.GallonsToKg(aircraftData.FuelBurnRate), // Convert from GPH to kg/hr
                //     ActualBurn = actualBurn,
                //     AircraftTitle = aircraftData.AircraftTitle
                // };

                DataReceived?.Invoke(this, dataObj);
            }
        }
        
        private void OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Console.WriteLine($"❌ SimConnect exception: {data.dwException}");
            Console.WriteLine($"   Send ID: {data.dwSendID}");
            Console.WriteLine($"   Index: {data.dwIndex}");
        }
        
        public void Disconnect()
        {
            lock (simConnectLock)
            {
                if (simConnect != null)
                {
                    try
                    {
                        simConnect.Dispose();
                        Console.WriteLine("✅ Disconnected from MSFS");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disconnecting from SimConnect: {ex.Message}");
                    }
                    finally
                    {
                        simConnect = null;
                        isConnected = false;
                        Disconnected?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
    }
}
