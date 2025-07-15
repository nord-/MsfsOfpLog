# MsfsOfpLog

A .NET console application for tracking GPS fixes and fuel consumption in Microsoft Flight Simulator using SimConnect API, generating professional OFP (Operational Flight Plan) summaries.

## Features

- **Real-time MSFS integration** using SimConnect API with automatic connection
- **Smart GPS fix tracking** with configurable tolerance zones
- **Intelligent flight state tracking** - distinguishes pre-flight vs post-flight taxi
- **Automatic takeoff and landing detection** - records these key flight events
- **Speed-based recording** - only records GPS fixes when airborne (>45 knots)
- **Fuel consumption monitoring** at each GPS fix
- **Flight plan support** for MSFS .pln files
- **OFP summary generation** - Professional aviation format with fuel consumption analysis
- **Continuous position updates** with real-time monitoring

## Requirements

- .NET 8.0 SDK or later
- Microsoft Flight Simulator (2020 or later)

## Installation

1. **Install .NET SDK** from: https://dotnet.microsoft.com/download

2. **Build the project:**
   ```powershell
   dotnet restore
   dotnet build
   ```

   *Note: The required SimConnect DLLs are included with the project and will be copied to the output directory during build.*

## Usage

1. **Start MSFS** and load into a flight (not the main menu)
2. **Enable SimConnect** in MSFS settings if not already enabled
3. **Run the application:**
   ```powershell
   dotnet run
   ```
4. The application will automatically connect to MSFS and start monitoring

### Workflow

1. **Load flight plan:** Choose to load an MSFS .pln flight plan file
2. **Start monitoring:** Real-time position and fuel updates with automatic GPS fix detection
3. **Fly your route:** Application tracks waypoint passages and fuel consumption
4. **Complete flight:** Monitoring stops automatically when aircraft lands
5. **Review OFP summary:** Professional flight report saved to Documents folder

## Data Output

The application creates OFP summary files in `Documents\MSFS OFP Log\`:

- **OFP Summary files** - Professional aviation format flight reports with:
  - Route information and waypoint details
  - Fuel consumption analysis between fixes
  - Flight timing and distance calculations
  - Coordinate formatting in aviation standard

## Sample OFP Output

Each flight generates a comprehensive OFP summary including:
- Flight header with departure/destination airports
- Waypoint table with coordinates, times, and fuel data
- Fuel consumption analysis between waypoints
- Total flight statistics

## Troubleshooting

**Connection Issues:**
- Ensure MSFS is running and in-flight
- Check that SimConnect is enabled in MSFS settings
- Verify MSFS SDK is properly installed

**GPS Fix Detection:**
- Ensure you're flying through the waypoint tolerance zones
- Check that flight plan coordinates are accurate

## Development

The project is structured with:
- `Models/` - Data structures for aircraft data and GPS fixes
- `Services/` - Core functionality (SimConnect, GPS tracking, OFP generation)
- `Program.cs` - Main application logic
- `Tests/` - Unit tests with in-memory stream testing

## Testing

Run the unit tests with:
```powershell
dotnet test
```

Tests use in-memory streams to verify OFP generation without file I/O.

## Contributing

Feel free to submit issues and pull requests to improve the application!
