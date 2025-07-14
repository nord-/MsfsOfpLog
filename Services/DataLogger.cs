using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using MsfsOfpLog.Models;
using Newtonsoft.Json;

namespace MsfsOfpLog.Services
{
    public class DataLogger
    {
        private readonly string _logDirectory;
        private readonly string _currentFlightFile;
        
        public DataLogger()
        {
            _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MSFS OFP Log");
            Directory.CreateDirectory(_logDirectory);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFlightFile = Path.Combine(_logDirectory, $"flight_{timestamp}");
        }
        
        public void LogGpsFixData(GpsFixData fixData)
        {
            try
            {
                // Save as CSV
                var csvFile = _currentFlightFile + ".csv";
                var fileExists = File.Exists(csvFile);
                
                using (var writer = new StreamWriter(csvFile, append: true))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    if (!fileExists)
                    {
                        csv.WriteHeader<GpsFixData>();
                        csv.NextRecord();
                    }
                    
                    csv.WriteRecord(fixData);
                    csv.NextRecord();
                }
                
                // Save as JSON (append to array)
                var jsonFile = _currentFlightFile + ".json";
                var existingData = new List<GpsFixData>();
                
                if (File.Exists(jsonFile))
                {
                    var existingJson = File.ReadAllText(jsonFile);
                    existingData = JsonConvert.DeserializeObject<List<GpsFixData>>(existingJson) ?? new List<GpsFixData>();
                }
                
                existingData.Add(fixData);
                
                var json = JsonConvert.SerializeObject(existingData, Formatting.Indented);
                File.WriteAllText(jsonFile, json);
                
                Console.WriteLine($"Logged GPS fix data to: {csvFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging GPS fix data: {ex.Message}");
            }
        }
        
        public void SaveFlightSummary(IReadOnlyList<GpsFixData> passedFixes, string aircraftTitle)
        {
            try
            {
                var summaryFile = _currentFlightFile + "_summary.txt";
                
                using (var writer = new StreamWriter(summaryFile))
                {
                    writer.WriteLine("MSFS OFP Log - Flight Summary");
                    writer.WriteLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"Aircraft: {aircraftTitle}");
                    writer.WriteLine();
                    
                    if (passedFixes.Count == 0)
                    {
                        writer.WriteLine("No GPS fixes were passed during this flight.");
                    }
                    else
                    {
                        writer.WriteLine($"GPS Fixes Passed: {passedFixes.Count}");
                        writer.WriteLine();
                        
                        foreach (var fix in passedFixes)
                        {
                            writer.WriteLine($"Fix: {fix.FixName}");
                            writer.WriteLine($"  Time: {fix.Timestamp:yyyy-MM-dd HH:mm:ss}");
                            writer.WriteLine($"  Position: {fix.Latitude:F6}, {fix.Longitude:F6}");
                            writer.WriteLine($"  Fuel: {fix.FuelRemaining:F2} kg ({fix.FuelRemainingPercentage:F1}%)");
                            writer.WriteLine($"  Altitude: {fix.Altitude:F0} ft");
                            writer.WriteLine($"  Ground Speed: {fix.GroundSpeed:F0} kts");
                            writer.WriteLine($"  Heading: {fix.Heading:F0}°");
                            writer.WriteLine();
                        }
                        
                        // Calculate fuel consumption between fixes
                        if (passedFixes.Count > 1)
                        {
                            writer.WriteLine("Fuel Consumption Analysis:");
                            writer.WriteLine();
                            
                            for (int i = 1; i < passedFixes.Count; i++)
                            {
                                var prevFix = passedFixes[i - 1];
                                var currentFix = passedFixes[i];
                                
                                var fuelConsumed = prevFix.FuelRemaining - currentFix.FuelRemaining;
                                var timeSpan = currentFix.Timestamp - prevFix.Timestamp;
                                
                                writer.WriteLine($"  {prevFix.FixName} → {currentFix.FixName}:");
                                writer.WriteLine($"    Time: {timeSpan.TotalMinutes:F1} minutes");
                                writer.WriteLine($"    Fuel consumed: {fuelConsumed:F2} kg");
                                writer.WriteLine($"    Fuel flow: {(fuelConsumed / timeSpan.TotalHours):F2} kg/hr");
                                writer.WriteLine();
                            }
                        }
                    }
                }
                
                Console.WriteLine($"Flight summary saved to: {summaryFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving flight summary: {ex.Message}");
            }
        }
        
        public List<string> GetPreviousFlights()
        {
            try
            {
                var files = Directory.GetFiles(_logDirectory, "flight_*.csv")
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderByDescending(f => f)
                    .ToList();
                
                return files;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting previous flights: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
