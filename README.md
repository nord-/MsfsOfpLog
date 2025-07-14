# MsfsOfpLog

A .NET console application for tracking GPS fixes and fuel consumption in Microsoft Flight Simulator using SimConnect API.

## Features

- **Real-time MSFS integration** using SimConnect API with automatic connection
- **Demo mode** for testing without MSFS running
- **Smart GPS fix tracking** with configurable tolerance zones
- **Intelligent flight state tracking** - distinguishes pre-flight vs post-flight taxi
- **Speed-based recording** - only records GPS fixes when airborne (>45 knots)
- **Automatic monitoring stop** - stops only after aircraft has landed and is taxiing
- **Fuel consumption monitoring** in kilograms at each GPS fix
- **Flight plan support** for MSFS .pln files
- **Data logging** in CSV, JSON, and summary formats
- **Continuous position updates** every 5 seconds during monitoring
- **Route string parsing** for quick GPS fix setup
- **Manual GPS fix entry** for custom waypoints
- **Graceful shutdown** with Ctrl+C to save flight data

## Requirements

- .NET 8.0 SDK or later
- Microsoft Flight Simulator (2020 or later)
- MSFS SDK (for SimConnect library)

## Installation

1. **Install .NET SDK** from: https://dotnet.microsoft.com/download

2. **Install MSFS SDK:**
   - Download from: https://docs.flightsimulator.com/html/Programming_Tools/SimConnect/SimConnect_SDK.html
   - Extract to `C:\MSFS SDK\` (or update the path in the .csproj file)

3. **Build the project:**
   ```powershell
   dotnet restore
   dotnet build
   ```

## Usage

### Real Mode (with MSFS)

1. **Start MSFS** and load into a flight (not the main menu)
2. **Enable SimConnect** in MSFS settings if not already enabled
3. **Run the application:**
   ```powershell
   dotnet run
   ```
4. The application will automatically connect to MSFS

### Demo Mode (without MSFS)

1. **Run the application in demo mode:**
   ```powershell
   dotnet run -- --demo
   ```
2. Uses mock data for testing and development

### Workflow

1. **Load route or flight plan:**
   - Enter route string (e.g., "LOWW DCT VIE DCT RIVER DCT LOWG")
   - Load MSFS .pln flight plan file
   - Or add GPS fixes manually

2. **Start monitoring:**
   - Real-time position and fuel updates every 5 seconds
   - Displays current flight phase (Pre-flight taxi, Airborne, Post-flight taxi)
   - Automatic GPS fix detection and logging (only when airborne >45 knots)
   - Fuel consumption tracking in kilograms
   - Monitoring automatically stops only after aircraft has landed and is taxiing

3. **Stop monitoring:**
   - Press Ctrl+C for manual graceful shutdown
   - Or monitoring stops automatically when aircraft lands/taxis
   - Flight data automatically saved to logs

## Menu Options

1. **Load route string** - Parse route like "LOWW DCT VIE DCT RIVER DCT LOWG"
2. **Load flight plan** - Load MSFS .pln flight plan file
3. **Add GPS fixes manually** - Enter waypoint coordinates manually
4. **Start monitoring** - Begin continuous position and fuel tracking
5. **Stop monitoring** - End tracking and save flight summary
6. **Reset flight data** - Clear current flight data
7. **View previous flights** - List previous flight logs
8. **Exit** - Quit the application

*Note: The application automatically connects to MSFS on startup (or uses demo mode if --demo flag is used)*

## Data Output

The application creates log files in `Documents\MSFS OFP Log\`:

- **CSV files** - Structured data for analysis
- **JSON files** - Machine-readable format
- **Summary files** - Human-readable flight reports

## Sample Data

Each GPS fix passage records:
- Timestamp
- Fix name and coordinates
- Remaining fuel (kilograms and percentage)
- Aircraft altitude and speed
- Heading and ground speed

## Command Line Options

```powershell
dotnet run                # Real mode (connects to MSFS)
dotnet run -- --demo     # Demo mode (uses mock data)
```

## Troubleshooting

**Connection Issues:**
- Ensure MSFS is running and in-flight
- Check that SimConnect is enabled in MSFS settings
- Verify MSFS SDK is properly installed

**GPS Fix Detection:**
- Increase tolerance if fixes are being missed
- Ensure coordinates are accurate
- Check that you're actually flying through the fix area

## Development

The project is structured with:
- `Models/` - Data structures
- `Services/` - Core functionality (SimConnect, GPS tracking, logging)
- `Program.cs` - Main application and menu system

## Contributing

Feel free to submit issues and pull requests to improve the application!
