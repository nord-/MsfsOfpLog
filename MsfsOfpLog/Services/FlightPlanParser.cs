using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using MsfsOfpLog.Models;

namespace MsfsOfpLog.Services
{
    public class FlightPlanParser
    {
        public class FlightPlanInfo
        {
            public string Title { get; set; } = "";
            public string DepartureID { get; set; } = "";
            public string DestinationID { get; set; } = "";
            public string DepartureName { get; set; } = "";
            public string DestinationName { get; set; } = "";
            public string FlightPlanType { get; set; } = "";
            public string RouteType { get; set; } = "";
            public double CruisingAltitude { get; set; }
            public List<GpsFix> Waypoints { get; set; } = new List<GpsFix>();
        }
        
        public static FlightPlanInfo? ParseFlightPlan(string filePath)
        {
            try
            {
                // Remove quotes from file path if present
                filePath = filePath.Trim().Trim('"');
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Flight plan file not found: {filePath}");
                    return null;
                }
                
                var doc = XDocument.Load(filePath);
                var flightPlan = doc.Root?.Element("FlightPlan.FlightPlan");
                
                if (flightPlan == null)
                {
                    Console.WriteLine("Invalid flight plan format");
                    return null;
                }
                
                var info = new FlightPlanInfo
                {
                    Title = flightPlan.Element("Title")?.Value ?? "",
                    DepartureID = flightPlan.Element("DepartureID")?.Value ?? "",
                    DestinationID = flightPlan.Element("DestinationID")?.Value ?? "",
                    DepartureName = flightPlan.Element("DepartureName")?.Value ?? "",
                    DestinationName = flightPlan.Element("DestinationName")?.Value ?? "",
                    FlightPlanType = flightPlan.Element("FPType")?.Value ?? "",
                    RouteType = flightPlan.Element("RouteType")?.Value ?? "",
                    CruisingAltitude = double.TryParse(flightPlan.Element("CruisingAlt")?.Value ?? "0", out var alt) ? alt : 0
                };
                
                // Parse waypoints
                var waypoints = flightPlan.Elements("ATCWaypoint");
                foreach (var waypoint in waypoints)
                {
                    var id = waypoint.Attribute("id")?.Value ?? "";
                    var worldPosition = waypoint.Element("WorldPosition")?.Value ?? "";
                    var waypointType = waypoint.Element("ATCWaypointType")?.Value ?? "";
                    var airway = waypoint.Element("ATCAirway")?.Value ?? "";
                    
                    if (!string.IsNullOrEmpty(worldPosition))
                    {
                        var coordinates = ParseWorldPosition(worldPosition);
                        if (coordinates.HasValue)
                        {
                            var gpsFix = new GpsFix
                            {
                                Name = id,
                                Latitude = coordinates.Value.Latitude,
                                Longitude = coordinates.Value.Longitude,
                                ToleranceNM = GetToleranceForWaypointType(waypointType)
                            };
                            
                            info.Waypoints.Add(gpsFix);
                        }
                    }
                }
                
                return info;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing flight plan: {ex.Message}");
                return null;
            }
        }
        
        private static (double Latitude, double Longitude)? ParseWorldPosition(string worldPosition)
        {
            try
            {
                // Format: N36° 24' 19.00",E28° 5' 10.00",+000019.00
                Console.WriteLine($"Parsing world position: {worldPosition}");
                
                var parts = worldPosition.Split(',');
                if (parts.Length < 2) 
                {
                    Console.WriteLine("Not enough parts in world position");
                    return null;
                }
                
                var latPart = parts[0].Trim();
                var lonPart = parts[1].Trim();
                
                Console.WriteLine($"Latitude part: '{latPart}'");
                Console.WriteLine($"Longitude part: '{lonPart}'");
                
                var latitude = ParseCoordinate(latPart);
                var longitude = ParseCoordinate(lonPart);
                
                if (latitude.HasValue && longitude.HasValue)
                {
                    Console.WriteLine($"Parsed coordinates: {latitude.Value}, {longitude.Value}");
                    return (latitude.Value, longitude.Value);
                }
                else
                {
                    Console.WriteLine($"Failed to parse coordinates: lat={latitude}, lon={longitude}");
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception parsing world position: {ex.Message}");
                return null;
            }
        }
        
        private static double? ParseCoordinate(string coordinate)
        {
            try
            {
                // Input format: N36° 24' 19.00" or E28° 5' 10.00"
                Console.WriteLine($"Parsing coordinate: '{coordinate}'");
                
                // Remove quotes and clean up
                coordinate = coordinate.Replace("\"", "").Trim();
                
                // Determine if it's North/South or East/West
                bool isNegative = coordinate.StartsWith("S") || coordinate.StartsWith("W");
                
                // Remove the direction letter
                coordinate = coordinate.Substring(1);
                
                // Split by degree symbol first
                var parts = coordinate.Split('°');
                if (parts.Length != 2)
                {
                    Console.WriteLine($"Could not split by degree symbol: '{coordinate}'");
                    return null;
                }
                
                var degreesStr = parts[0].Trim();
                var remaining = parts[1].Trim(); // This should be " 24' 19.00"
                
                // Split the remaining part by minute symbol
                var minuteParts = remaining.Split('\'');
                if (minuteParts.Length != 2)
                {
                    Console.WriteLine($"Could not split by minute symbol: '{remaining}'");
                    return null;
                }
                
                var minutesStr = minuteParts[0].Trim();
                var secondsStr = minuteParts[1].Trim();
                
                Console.WriteLine($"Parsed parts: degrees='{degreesStr}', minutes='{minutesStr}', seconds='{secondsStr}'");
                
                // Parse the numeric values
                if (double.TryParse(degreesStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) &&
                    double.TryParse(minutesStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) &&
                    double.TryParse(secondsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                {
                    // Convert to decimal degrees
                    var decimalDegrees = degrees + (minutes / 60.0) + (seconds / 3600.0);
                    
                    Console.WriteLine($"Successfully parsed: {degrees}° {minutes}' {seconds}\" = {decimalDegrees.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}");
                    
                    return isNegative ? -decimalDegrees : decimalDegrees;
                }
                else
                {
                    Console.WriteLine($"Failed to parse numeric values: degrees='{degreesStr}', minutes='{minutesStr}', seconds='{secondsStr}'");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception parsing coordinate '{coordinate}': {ex.Message}");
                return null;
            }
        }
        
        private static double GetToleranceForWaypointType(string waypointType)
        {
            return waypointType switch
            {
                "Airport" => 2.0,      // Larger tolerance for airports
                "Intersection" => 0.5,  // Standard tolerance for intersections
                "VOR" => 1.0,          // Medium tolerance for VORs
                "NDB" => 1.0,          // Medium tolerance for NDBs
                _ => 0.5               // Default tolerance
            };
        }
        
        public static void DisplayFlightPlanInfo(FlightPlanInfo info)
        {
            Console.WriteLine($"\n📋 Flight Plan: {info.Title}");
            Console.WriteLine($"   From: {info.DepartureID} ({info.DepartureName})");
            Console.WriteLine($"   To: {info.DestinationID} ({info.DestinationName})");
            Console.WriteLine($"   Type: {info.FlightPlanType} - {info.RouteType}");
            Console.WriteLine($"   Cruising Altitude: {info.CruisingAltitude.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} ft");
            Console.WriteLine($"   Waypoints: {info.Waypoints.Count}");
            Console.WriteLine();
            
            Console.WriteLine("🛫 Route Waypoints:");
            for (int i = 0; i < info.Waypoints.Count; i++)
            {
                var wp = info.Waypoints[i];
                Console.WriteLine($"   {i + 1:D2}. {wp.Name} - {wp.Latitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)}, {wp.Longitude.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)} (±{wp.ToleranceNM.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} NM)");
            }
            Console.WriteLine();
        }
    }
}
