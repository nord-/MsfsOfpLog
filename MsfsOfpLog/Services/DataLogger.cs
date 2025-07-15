using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MsfsOfpLog.Models;
using MsfsOfpLog.Services;
using static System.Globalization.CultureInfo;

namespace MsfsOfpLog.Services
{
    public class DataLogger
    {
        private readonly string _logDirectory;
        private readonly string _currentFlightFile;
        private readonly ISystemClock _systemClock;
        private readonly Stream? _outputStream;
        
        public DataLogger(ISystemClock? systemClock = null, Stream? outputStream = null)
        {
            _systemClock = systemClock ?? new SystemClock();
            _outputStream = outputStream;
            
            if (_outputStream == null)
            {
                _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MSFS OFP Log");
                Directory.CreateDirectory(_logDirectory);
                
                var timestamp = _systemClock.Now.ToString("yyyyMMdd_HHmmss");
                _currentFlightFile = Path.Combine(_logDirectory, $"flight_{timestamp}");
            }
            else
            {
                _logDirectory = string.Empty;
                _currentFlightFile = string.Empty;
            }
        }
        
        public void SaveFlightSummary(IReadOnlyList<GpsFixData> passedFixes, string aircraftTitle, FlightPlanParser.FlightPlanInfo? flightPlan = null)
        {
            try
            {
                Stream stream;
                bool shouldDisposeStream = false;
                
                if (_outputStream != null)
                {
                    stream = _outputStream;
                }
                else
                {
                    var summaryFile = _currentFlightFile + "_summary.txt";
                    stream = new FileStream(summaryFile, FileMode.Create, FileAccess.Write);
                    shouldDisposeStream = true;
                }
                
                using (var writer = new StreamWriter(stream, leaveOpen: !shouldDisposeStream))
                {
                    // Extract departure and destination from first and last fixes
                    var departureCode = "UNKNOWN";
                    var destinationCode = "UNKNOWN";
                    var departureFullName = "UNKNOWN";
                    var destinationFullName = "UNKNOWN";
                    
                    if (passedFixes.Count > 0)
                    {
                        var firstFix = passedFixes[0];
                        var lastFix = passedFixes[passedFixes.Count - 1];
                        
                        if (firstFix.FixName.StartsWith("TAKEOFF"))
                        {
                            var parts = firstFix.FixName.Split(' ');
                            if (parts.Length > 1) 
                            {
                                departureCode = parts[1];
                                // Use flight plan departure name if available, otherwise use airport code
                                departureFullName = flightPlan?.DepartureName ?? departureCode;
                            }
                        }
                        
                        if (lastFix.FixName.StartsWith("LANDING"))
                        {
                            var parts = lastFix.FixName.Split(' ');
                            if (parts.Length > 1) 
                            {
                                destinationCode = parts[1];
                                // Use flight plan destination name if available, otherwise use airport code
                                destinationFullName = flightPlan?.DestinationName ?? destinationCode;
                            }
                        }
                    }
                    
                    // Header in OFP format
                    var headerDate = _systemClock.Now.ToString("ddMMMyyyy", InvariantCulture).ToUpper();
                    writer.WriteLine($"{headerDate} {departureCode}-{destinationCode}");
                    writer.WriteLine($"OFP 1 {departureFullName}-{destinationFullName}");
                    writer.WriteLine($"Generated: {_systemClock.Now.ToString("yyyy-MM-dd HHmm", InvariantCulture)}Z");
                    writer.WriteLine($"Aircraft: {aircraftTitle}");
                    writer.WriteLine();
                    
                    // Column headers - OFP format with reordered columns
                    // Define columns:
                    // 1st 14 characters for position name
                    // 2nd 9 characters for latitude
                    // 3rd 4 characters for ETO (elapsed time over)
                    // 4th 4 characters for distance
                    // 5th 4 characters for TAS (true airspeed)
                    // 6th 3 characters for OAT (outside air temperature)
                    // 7th 4 characters for AFOB (amount of fuel on board in tonnes)
                    // Every column is left-aligned and is separated by a single space
                    var columns = new[] { 14, 9, 4, 4, 4, 3, 4 };
                    var firstHeaderLine = new[] { "", "", "", "FL", "MN", "OAT", "AFOB" };
                    var secondHeaderLine = new[] { "POSITION", "LAT", "ETO", "DIS", "TAS", "", "" };
                    var thirdHeaderLine = new[] { "IDENT", "LONG", "ATO", "RDIS", "GS", "", "ABRN" };
                    writer.WriteLine(string.Join(" ", firstHeaderLine.Select((s, i) => s.PadRight(columns[i]))));
                    writer.WriteLine(string.Join(" ", secondHeaderLine.Select((s, i) => s.PadRight(columns[i]))));
                    writer.WriteLine(string.Join(" ", thirdHeaderLine.Select((s, i) => s.PadRight(columns[i]))));
                    
                    if (passedFixes.Count == 0)
                    {
                        writer.WriteLine("No GPS fixes were passed during this flight.");
                    }
                    else
                    {
                        DateTime? flightStartTime = null;
                        double totalDistance = 0;
                        var distanceAtFix = new List<double>();
                        double? initialFuelAmount = null;
                        
                        // First pass: calculate all distances and find initial fuel amount
                        for (int i = 0; i < passedFixes.Count; i++)
                        {
                            var fix = passedFixes[i];
                            
                            if (flightStartTime == null && fix.FixName.StartsWith("TAKEOFF"))
                            {
                                flightStartTime = fix.Timestamp;
                                initialFuelAmount = fix.FuelRemaining;
                                distanceAtFix.Add(0); // Takeoff is at distance 0
                            }
                            else if (i > 0)
                            {
                                var prevFix = passedFixes[i - 1];
                                var dist = CalculateDistance(prevFix.Latitude, prevFix.Longitude, 
                                                           fix.Latitude, fix.Longitude);
                                totalDistance += dist;
                                distanceAtFix.Add(totalDistance);
                            }
                            else
                            {
                                distanceAtFix.Add(0);
                            }
                        }
                        
                        // If no TAKEOFF fix was found, use the first fix as the start time
                        if (flightStartTime == null && passedFixes.Count > 0)
                        {
                            flightStartTime = passedFixes[0].Timestamp;
                            initialFuelAmount = passedFixes[0].FuelRemaining;
                        }
                        
                        // Second pass: write the data with correct formatting
                        for (int i = 0; i < passedFixes.Count; i++)
                        {
                            var fix = passedFixes[i];
                            
                            // Calculate ETO (elapsed time over) in HHMM format
                            var etoTime = "0000";
                            if (flightStartTime.HasValue)
                            {
                                var elapsedTime = fix.Timestamp - flightStartTime.Value;
                                var totalMinutes = (int)elapsedTime.TotalMinutes;
                                var hours = totalMinutes / 60;
                                var minutes = totalMinutes % 60;
                                etoTime = $"{hours:D2}{minutes:D2}";
                            }
                            
                            // Calculate remaining distance
                            var remainingDistance = totalDistance - distanceAtFix[i];
                            
                            // Format coordinates
                            var latDeg = (int)Math.Abs(fix.Latitude);
                            var latMin = (Math.Abs(fix.Latitude) - latDeg) * 60;
                            var latDir = fix.Latitude >= 0 ? "N" : "S";
                            var latStr = $"{latDir}{latDeg:D2}{latMin.ToString("F1", InvariantCulture)}";
                            
                            var lonDeg = (int)Math.Abs(fix.Longitude);
                            var lonMin = (Math.Abs(fix.Longitude) - lonDeg) * 60;
                            var lonDir = fix.Longitude >= 0 ? "E" : "W";
                            var lonStr = $"{lonDir}{lonDeg:D3}{lonMin.ToString("F1", InvariantCulture)}";
                            
                            // Flight level from altitude
                            var flightLevel = (int)(fix.Altitude / 100);
                            
                            // Actual time at fix in Zulu format (HHMM)
                            var atoTime = fix.Timestamp.ToString("HHmm");
                            
                            // Format fix name and get airport/position name
                            var displayName = fix.FixName;
                            var positionName = fix.FixName;
                            
                            if (displayName.StartsWith("TAKEOFF ")) 
                            {
                                displayName = displayName.Substring(8);
                                positionName = departureFullName;
                            }
                            else if (displayName.StartsWith("LANDING ")) 
                            {
                                displayName = displayName.Substring(8);
                                positionName = destinationFullName;
                            }
                            
                            // Safety checks for NaN/infinity values
                            var safeOAT = double.IsNaN(fix.OutsideAirTemperature) || double.IsInfinity(fix.OutsideAirTemperature) ? 0 : fix.OutsideAirTemperature;
                            var safeTAS = double.IsNaN(fix.TrueAirspeed) || double.IsInfinity(fix.TrueAirspeed) ? 0 : fix.TrueAirspeed;
                            var safeMach = double.IsNaN(fix.MachNumber) || double.IsInfinity(fix.MachNumber) ? 0 : fix.MachNumber;
                            
                            // Calculate actual burn (cumulative fuel consumed since takeoff)
                            var actualBurn = initialFuelAmount.HasValue ? (initialFuelAmount.Value - fix.FuelRemaining) : 0;
                            
                            // Convert fuel to tonnes
                            var fuelTonnes = fix.FuelRemaining / 1000.0;
                            var actualBurnTonnes = actualBurn / 1000.0;
                            
                            // Write fix information in OFP format with reordered columns
                            // all values are right-aligned except for position name and display name
                            // first line: flight level in column 4, mach number in column 5, OAT in column 6, fuel in column 7
                            // second line: position name in column 1, latitude in column 2, ETO in column 3, distance in column 4, TAS in column 5
                            // third line: display name in column 1, longitude in column 2, ATO in column 3, remaining distance in column 4, ground speed in column 5, actual burn in column 7
                            var firstLine = new[]
                            {
                                "", "", "", 
                                flightLevel.ToString("D3", InvariantCulture),
                                safeMach.ToString("F2", InvariantCulture),
                                safeOAT.ToString("F0", InvariantCulture),
                                fuelTonnes.ToString("F1", InvariantCulture)
                            };
                            var secondLine = new[]
                            {
                                positionName,
                                latStr,
                                etoTime,
                                ((int)distanceAtFix[i]).ToString(),
                                safeTAS.ToString("F0", InvariantCulture)
                            };
                            var thirdLine = new[]
                            {
                                displayName,
                                lonStr,
                                atoTime,
                                ((int)remainingDistance).ToString(),
                                fix.GroundSpeed.ToString("F0", InvariantCulture),
                                "",
                                actualBurnTonnes.ToString("F1", InvariantCulture)
                            };
                            writer.WriteLine(string.Join(" ", firstLine.Select((s, j) => s.PadLeft(columns[j]))));
                            // first column should be left-aligned
                            writer.Write(secondLine.First().PadRight(columns[0]) + " ");
                            writer.WriteLine(string.Join(" ", secondLine.Skip(1).Select((s, j) => s.PadLeft(columns[j+1]))));

                            writer.Write(thirdLine.First().PadRight(columns[0]) + " ");
                            writer.WriteLine(string.Join(" ", thirdLine.Skip(1).Select((s, j) => s.PadLeft(columns[j+1]))));
                            writer.WriteLine();

                        }
                        
                        // Add fuel consumption summary at the end
                        if (passedFixes.Count > 1)
                        {
                            writer.WriteLine();
                            writer.WriteLine("FUEL CONSUMPTION ANALYSIS:");
                            writer.WriteLine("------------------------------------------------------------------------");
                            
                            for (int i = 1; i < passedFixes.Count; i++)
                            {
                                var prevFix = passedFixes[i - 1];
                                var currentFix = passedFixes[i];
                                
                                var fuelConsumed = prevFix.FuelRemaining - currentFix.FuelRemaining;
                                var timeSpan = currentFix.Timestamp - prevFix.Timestamp;
                                
                                var prevName = prevFix.FixName;
                                var currentName = currentFix.FixName;
                                if (prevName.StartsWith("TAKEOFF ")) prevName = prevName.Substring(8);
                                if (prevName.StartsWith("LANDING ")) prevName = prevName.Substring(8);
                                if (currentName.StartsWith("TAKEOFF ")) currentName = currentName.Substring(8);
                                if (currentName.StartsWith("LANDING ")) currentName = currentName.Substring(8);
                                
                                writer.WriteLine($"{prevName} → {currentName}:");
                                writer.WriteLine($"  Time: {timeSpan.TotalMinutes.ToString("F1", InvariantCulture)} minutes");
                                writer.WriteLine($"  Fuel consumed: {(fuelConsumed/1000).ToString("F1", InvariantCulture)} tonnes");
                                writer.WriteLine($"  Fuel flow: {((fuelConsumed / timeSpan.TotalHours)/1000).ToString("F1", InvariantCulture)} tonnes/hr");
                                writer.WriteLine();
                            }
                        }
                    }
                }
                
                if (shouldDisposeStream)
                {
                    stream.Dispose();
                    var summaryFile = _currentFlightFile + "_summary.txt";
                    Console.WriteLine($"Flight summary saved to: {summaryFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving flight summary: {ex.Message}");
            }
        }
        
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula to calculate distance in nautical miles
            const double R = 3440.065; // Earth's radius in nautical miles
            
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }
        
        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }           
    }
}
