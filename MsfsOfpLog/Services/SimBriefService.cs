using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using MsfsOfpLog.Models;

namespace MsfsOfpLog.Services
{
    public class SimBriefService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string SimBriefApiBase = "https://www.simbrief.com/api";
        
        // SimBrief uses a simple public API - no authentication required
        // Users provide their Navigraph username or SimBrief Pilot ID
        
        private string? _simBriefUserId;
        
        // Configuration file paths
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "MsfsOfpLog");
        private static readonly string UserConfigFile = Path.Combine(ConfigDir, "user_config.json");
        
        public SimBriefService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MSFS-OFP-Log/1.0");
            
            // Create config directory if it doesn't exist
            Directory.CreateDirectory(ConfigDir);
            
            // Load user config
            LoadUserConfig();
        }
        
        /// <summary>
        /// Gets or sets the SimBrief user ID (Navigraph username or Pilot ID)
        /// </summary>
        public string? SimBriefUserId 
        { 
            get => _simBriefUserId;
            set 
            {
                _simBriefUserId = value;
                SaveUserConfig();
            }
        }
        
        /// <summary>
        /// SimBrief public API doesn't require authentication
        /// </summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_simBriefUserId);
        
        /// <summary>
        /// Loads user configuration (SimBrief ID)
        /// </summary>
        private void LoadUserConfig()
        {
            try
            {
                if (File.Exists(UserConfigFile))
                {
                    var json = File.ReadAllText(UserConfigFile);
                    var config = JsonSerializer.Deserialize<UserConfig>(json);
                    if (config != null)
                    {
                        _simBriefUserId = config.SimBriefUserId;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user config: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Saves user configuration to disk
        /// </summary>
        private void SaveUserConfig()
        {
            try
            {
                var config = new UserConfig
                {
                    SimBriefUserId = _simBriefUserId
                };
                
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(UserConfigFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving user config: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clears stored user configuration
        /// </summary>
        public void ClearStoredData()
        {
            _simBriefUserId = null;
            
            try
            {
                if (File.Exists(UserConfigFile))
                    File.Delete(UserConfigFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing stored data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Fetches the latest flight plan for the user using SimBrief public API
        /// </summary>
        /// <returns>Flight plan information or null if not found</returns>
        public async Task<FlightPlanParser.FlightPlanInfo?> GetLatestFlightPlanAsync()
        {
            if (string.IsNullOrEmpty(_simBriefUserId))
            {
                Console.WriteLine("SimBrief User ID not set. Please configure your Navigraph username or SimBrief Pilot ID.");
                return null;
            }
            
            try
            {
                // Try to parse as numeric pilot ID first, otherwise use as username
                string endpoint;
                if (int.TryParse(_simBriefUserId, out _))
                {
                    endpoint = $"{SimBriefApiBase}/xml.fetcher.php?userid={_simBriefUserId}&json=v2";
                    Console.WriteLine($"Fetching latest flight plan using Pilot ID: {_simBriefUserId}");
                }
                else
                {
                    endpoint = $"{SimBriefApiBase}/xml.fetcher.php?username={_simBriefUserId}&json=v2";
                    Console.WriteLine($"Fetching latest flight plan using username: {_simBriefUserId}");
                }
                
                var response = await _httpClient.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"API Response received, length: {responseJson.Length} characters");
                    
                    try
                    {
                        var simBriefData = JsonSerializer.Deserialize<SimBriefOFP>(responseJson);
                        
                        if (simBriefData != null)
                        {
                            Console.WriteLine($"JSON parsed successfully!");
                            Console.WriteLine($"  Origin: {simBriefData.Origin?.IcaoCode ?? "null"}");
                            Console.WriteLine($"  Destination: {simBriefData.Destination?.IcaoCode ?? "null"}");
                            Console.WriteLine($"  Initial Altitude: {simBriefData.General?.InitialAltitude ?? "null"}");
                            Console.WriteLine($"  Navlog waypoints: {simBriefData.Navlog?.Count ?? 0}");
                            
                            if (simBriefData.Navlog != null && simBriefData.Navlog.Count > 0)
                            {
                                var firstWp = simBriefData.Navlog[0];
                                Console.WriteLine($"  First waypoint: {firstWp.Ident ?? "null"} at ({firstWp.PosLat ?? "null"}, {firstWp.PosLong ?? "null"})");
                            }
                            
                            var result = ConvertToFlightPlanInfo(simBriefData);
                            Console.WriteLine($"Converted to FlightPlanInfo with {result.Waypoints.Count} waypoints");
                            return result;
                        }
                        else
                        {
                            Console.WriteLine("JSON deserialization returned null");
                            return null;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"JSON parsing error: {jsonEx.Message}");
                        Console.WriteLine($"Path: {jsonEx.Path}");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to fetch flight plan. Status: {response.StatusCode}");
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        Console.WriteLine("This usually means the user ID is invalid or the user has no saved flight plans.");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching SimBrief flight plan: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Downloads the MSFS flight plan file from SimBrief (not supported by public API)
        /// </summary>
        /// <param name="downloadPath">Path where to save the .pln file</param>
        /// <param name="flightId">Not used in public API</param>
        /// <returns>False - not supported by public API</returns>
        public Task<bool> DownloadMsfsFlightPlanAsync(string downloadPath, string? flightId = null)
        {
            Console.WriteLine("❌ MSFS flight plan download is not available through the public SimBrief API.");
            Console.WriteLine("Please use the SimBrief website to download .pln files manually.");
            Console.WriteLine("Visit: https://dispatch.simbrief.com/");
            return Task.FromResult(false);
        }
        
        private static FlightPlanParser.FlightPlanInfo ConvertToFlightPlanInfo(SimBriefOFP ofp)
        {
            var result = new FlightPlanParser.FlightPlanInfo
            {
                Title = $"{ofp.Origin?.IcaoCode} to {ofp.Destination?.IcaoCode}",
                DepartureID = ofp.Origin?.IcaoCode ?? "",
                DestinationID = ofp.Destination?.IcaoCode ?? "",
                DepartureName = ofp.Origin?.Name ?? "",
                DestinationName = ofp.Destination?.Name ?? "",
                FlightPlanType = "IFR", // SimBrief typically generates IFR plans
                RouteType = "HighAlt",
                CruisingAltitude = double.TryParse(ofp.General?.InitialAltitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var alt) ? alt : 0 // Use initial_altitude directly (already in feet)
            };
            
            // Add waypoints from route (SimBrief navlog is a direct array of fixes)
            if (ofp.Navlog != null)
            {
                Console.WriteLine($"Processing {ofp.Navlog.Count} navlog entries...");
                int successCount = 0;
                int failCount = 0;
                
                foreach (var fix in ofp.Navlog)
                {
                    if (!string.IsNullOrEmpty(fix.Ident))
                    {
                        bool latParsed = double.TryParse(fix.PosLat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat);
                        bool lonParsed = double.TryParse(fix.PosLong, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon);
                        
                        if (latParsed && lonParsed)
                        {
                            result.Waypoints.Add(new GpsFix
                            {
                                Name = fix.Ident,
                                Latitude = lat,
                                Longitude = lon,
                                ToleranceNM = 1.0
                            });
                            successCount++;
                        }
                        else
                        {
                            Console.WriteLine($"Failed to parse coordinates for {fix.Ident}: lat='{fix.PosLat}' ({latParsed}), lon='{fix.PosLong}' ({lonParsed})");
                            failCount++;
                        }
                    }
                }
                
                Console.WriteLine($"Coordinate parsing: {successCount} successful, {failCount} failed");
            }
            
            return result;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
    
    // Configuration storage models
    public class UserConfig
    {
        [JsonPropertyName("simbrief_user_id")]
        public string? SimBriefUserId { get; set; }
    }
    
    // SimBrief OFP JSON response models (based on public API)
    public class SimBriefOFP
    {
        [JsonPropertyName("general")]
        public SimBriefGeneral? General { get; set; }
        
        [JsonPropertyName("origin")]
        public SimBriefAirport? Origin { get; set; }
        
        [JsonPropertyName("destination")]
        public SimBriefAirport? Destination { get; set; }
        
        [JsonPropertyName("navlog")]
        public List<SimBriefFix>? Navlog { get; set; }
    }
    
    public class SimBriefGeneral
    {
        [JsonPropertyName("initial_altitude")]
        public string? InitialAltitude { get; set; }
        
        [JsonPropertyName("flight_number")]
        public string? FlightNumber { get; set; }
        
        [JsonPropertyName("icao_airline")]
        public string? IcaoAirline { get; set; }
    }
    
    public class SimBriefAirport
    {
        [JsonPropertyName("icao_code")]
        public string? IcaoCode { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("pos_lat")]
        public string? PosLat { get; set; }
        
        [JsonPropertyName("pos_long")]
        public string? PosLong { get; set; }
    }
    
    public class SimBriefFix
    {
        [JsonPropertyName("ident")]
        public string? Ident { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        
        [JsonPropertyName("pos_lat")]
        public string? PosLat { get; set; }
        
        [JsonPropertyName("pos_long")]
        public string? PosLong { get; set; }
    }
}
