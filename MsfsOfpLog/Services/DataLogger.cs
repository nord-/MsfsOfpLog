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
        private const string Takeoff = "TAKEOFF";
        private const string Landing = "LANDING";

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

                if (_outputStream is not null)
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

                        if (firstFix.FixName.StartsWith(Takeoff))
                        {
                            var parts = firstFix.FixName.Split(' ');
                            if (parts.Length > 1)
                            {
                                departureCode = parts[1];
                                // Use flight plan departure name if available, otherwise use airport code
                                departureFullName = flightPlan?.DepartureName ?? departureCode;
                            }
                        }

                        if (lastFix.FixName.StartsWith(Landing))
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
                    WriteFlightHeader(aircraftTitle, writer, departureCode, destinationCode, departureFullName, destinationFullName);

                    int[] columns = GenerateTableHeaderLines(writer);

                    if (passedFixes.Count == 0)
                    {
                        writer.WriteLine("No GPS fixes were passed during this flight.");
                        DisposeStreamIfNeeded(stream, shouldDisposeStream);
                        return;
                    }

                    // Filter out duplicate airport entries
                    var fixes = RemoveDuplicateAirports(passedFixes, departureCode, destinationCode);

                    var flightStartTime = fixes.FirstOrDefault()?.Timestamp;
                    var initialFuelAmount = fixes.FirstOrDefault()?.FuelRemaining;
                    var totalDistance = fixes.Sum(f => f.DistanceFromPrevious);
                    var remainingDistance = totalDistance;

                    for (int i = 0; i < fixes.Count; i++)
                    {
                        var fix = fixes[i];

                        // Calculate ETO (elapsed time over) in HHMM format
                        var etoTime = flightStartTime.HasValue ?
                            (fix.Timestamp - flightStartTime.Value).ToString(@"hhmm", InvariantCulture) :
                            "0000"; // Default to 0000 if no flight start time

                        // Format coordinates
                        var latStr = fix.LatitudeString;
                        var lonStr = fix.LongitudeString;

                        // Flight level from altitude
                        var flightLevel = fix.FlightLevel;

                        // Actual time at fix in Zulu format (HHMM)
                        var atoTime = fix.Timestamp.ToString("HHmm");

                        // Format fix name and get airport/position name
                        var displayName = fix.FixName;
                        var positionName = fix.FixName;

                        if (displayName.StartsWith(Takeoff))
                        {
                            displayName = displayName.Substring(Takeoff.Length + 1);
                            positionName = departureFullName;
                        }
                        else if (displayName.StartsWith(Landing))
                        {
                            displayName = displayName.Substring(Landing.Length + 1);
                            positionName = destinationFullName;
                        }

                        // Safety checks for NaN/infinity values
                        var safeOAT = double.IsNaN(fix.OutsideAirTemperature) || double.IsInfinity(fix.OutsideAirTemperature) ? 0 : fix.OutsideAirTemperature;
                        var safeTAS = double.IsNaN(fix.TrueAirspeed) || double.IsInfinity(fix.TrueAirspeed) ? 0 : fix.TrueAirspeed;
                        var safeMach = double.IsNaN(fix.MachNumber) || double.IsInfinity(fix.MachNumber) ? 0 : fix.MachNumber;

                        // Calculate actual burn (cumulative fuel consumed since takeoff)
                        var actualBurn = initialFuelAmount.HasValue ? (initialFuelAmount.Value - fix.FuelRemaining) : 0;

                        // Convert fuel to tonnes
                        var fuelTonnes = fix.FuelRemaining.KgToTonnesString();
                        var actualBurnTonnes = actualBurn.KgToTonnesString();

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
                            fuelTonnes
                        };
                        var secondLine = new[]
                        {
                            positionName,
                            latStr,
                            etoTime,
                            fix.DistanceFromPrevious.ToString("F0", InvariantCulture),
                            safeTAS.ToString("F0", InvariantCulture)
                        };
                        var thirdLine = new[]
                        {
                            displayName,
                            lonStr,
                            atoTime,
                            remainingDistance.ToString(),
                            fix.GroundSpeed.ToString("F0", InvariantCulture),
                            "",
                            actualBurnTonnes
                        };
                        writer.WriteLine(string.Join(" ", firstLine.Select((s, j) => s.PadLeft(columns[j]))));
                        // first column should be left-aligned
                        writer.Write(secondLine.First().PadRight(columns[0]) + " ");
                        writer.WriteLine(string.Join(" ", secondLine.Skip(1).Select((s, j) => s.PadLeft(columns[j + 1]))));

                        writer.Write(thirdLine.First().PadRight(columns[0]) + " ");
                        writer.WriteLine(string.Join(" ", thirdLine.Skip(1).Select((s, j) => s.PadLeft(columns[j + 1]))));
                        writer.WriteLine();

                        // Update remaining distance
                        remainingDistance -= fix.DistanceFromPrevious;
                        if (remainingDistance < 0) remainingDistance = 0; // Prevent negative distance
                    }

                    // Add fuel consumption summary at the end
                    WriteFuelConsumptionAnalysis(writer, fixes);                    
                }

                DisposeStreamIfNeeded(stream, shouldDisposeStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving flight summary: {ex.Message}");
            }
        }

        private void DisposeStreamIfNeeded(Stream stream, bool shouldDisposeStream)
        {
            if (shouldDisposeStream)
            {
                stream.Dispose();
                var summaryFile = _currentFlightFile + "_summary.txt";
                Console.WriteLine($"Flight summary saved to: {summaryFile}");
            }
        }

        private static void WriteFuelConsumptionAnalysis(StreamWriter writer, IReadOnlyList<GpsFixData> filteredFixes)
        {
            if (filteredFixes.Count > 1)
            {
                writer.WriteLine();
                writer.WriteLine("FUEL CONSUMPTION ANALYSIS:");
                writer.WriteLine("------------------------------------------------------------------------");
                
                // Column headers - left-aligned header with right-aligned data
                // writer.WriteLine("Leg           Time   Fuel   FF (kg/h)");
                writer.WriteLine("Leg           Time   Fuel   FF (kg/h)");

                for (int i = 1; i < filteredFixes.Count; i++)
                {
                    var prevFix = filteredFixes[i - 1];
                    var currentFix = filteredFixes[i];

                    var fuelConsumed = prevFix.FuelRemaining - currentFix.FuelRemaining;
                    var timeSpan = currentFix.Timestamp - prevFix.Timestamp;

                    var prevName = prevFix.FixName;
                    var currentName = currentFix.FixName;
                    if (prevName.StartsWith(Takeoff)) prevName = prevName.Substring(Takeoff.Length + 1);
                    if (prevName.StartsWith(Landing)) prevName = prevName.Substring(Landing.Length + 1);
                    if (currentName.StartsWith(Takeoff)) currentName = currentName.Substring(Takeoff.Length + 1);
                    if (currentName.StartsWith(Landing)) currentName = currentName.Substring(Landing.Length + 1);

                    var legName = $"{prevName} → {currentName}";
                    var timeMinutes = timeSpan.TotalMinutes.ToDecString(1);
                    var fuelTonnes = fuelConsumed.KgToTonnesString();
                    var fuelFlow = (fuelConsumed / timeSpan.TotalHours).ToDecString(0);
                    
                    // Format: Leg (left-aligned, 14 chars), Time (right-aligned, 6 chars), Fuel (right-aligned, 5 chars), FF (right-aligned, 5 chars)
                    writer.WriteLine($"{legName,-14}{timeMinutes,6}{fuelTonnes,6}{fuelFlow,6}");
                }
            }
        }

        private static int[] GenerateTableHeaderLines(StreamWriter writer)
        {
            // Column headers - OFP format with reordered columns
            // Define columns:
            // 1st 14 characters for position name
            // 2nd 9 characters for latitude
            // 3rd 4 characters for ET (elapsed time)
            // 4th 4 characters for distance
            // 5th 4 characters for TAS (true airspeed)
            // 6th 3 characters for OAT (outside air temperature)
            // 7th 4 characters for AFOB (amount of fuel on board in tonnes)
            // Every column is left-aligned and is separated by a single space
            var columns = new[] { 14, 9, 4, 4, 4, 3, 4 };
            var firstHeaderLine = new[] { "", "", "", "FL", "MN", "OAT", "AFOB" };
            var secondHeaderLine = new[] { "POSITION", "LAT", "ET", "DIS", "TAS", "", "" };
            var thirdHeaderLine = new[] { "IDENT", "LONG", "ATO", "RDIS", "GS", "", "ABRN" };
            writer.WriteLine(string.Join(" ", firstHeaderLine.Select((s, i) => s.PadRight(columns[i]))));
            writer.WriteLine(string.Join(" ", secondHeaderLine.Select((s, i) => s.PadRight(columns[i]))));
            writer.WriteLine(string.Join(" ", thirdHeaderLine.Select((s, i) => s.PadRight(columns[i]))));
            return columns;
        }

        private void WriteFlightHeader(string aircraftTitle, StreamWriter writer, string departureCode, string destinationCode, string departureFullName, string destinationFullName)
        {
            var headerDate = _systemClock.Now.ToString("ddMMMyyyy", InvariantCulture).ToUpper();
            writer.WriteLine($"{headerDate} {departureCode}-{destinationCode}");
            writer.WriteLine($"OFP 1 {departureFullName}-{destinationFullName}");
            writer.WriteLine($"Generated: {_systemClock.Now.ToString("yyyy-MM-dd HHmm", InvariantCulture)}Z");
            writer.WriteLine($"Aircraft: {aircraftTitle}");
            writer.WriteLine();
        }

        private IReadOnlyList<GpsFixData> RemoveDuplicateAirports(IReadOnlyList<GpsFixData> passedFixes, string departureCode, string destinationCode)
        {
            var filteredFixes = new List<GpsFixData>();
            foreach (var fix in passedFixes)
            {
                // Check if the fix is a duplicate airport entry
                if (fix.FixName == departureCode || fix.FixName == destinationCode)
                {
                    // Skip duplicate airport entries (they're already added with the takeoff/landing fixes)
                    continue;
                }

                filteredFixes.Add(fix);
            }

            return filteredFixes.AsReadOnly();
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
