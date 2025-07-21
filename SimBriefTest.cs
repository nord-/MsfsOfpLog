using System;
using System.Threading.Tasks;
using MsfsOfpLog.Services;

namespace MsfsOfpLog
{
    class SimBriefTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("SimBrief API Test");
            Console.WriteLine("================");
            
            var service = new SimBriefService();
            service.SimBriefUserId = "1029610";
            
            try
            {
                var flightPlan = await service.GetLatestFlightPlanAsync();
                
                if (flightPlan != null)
                {
                    Console.WriteLine($"✅ SUCCESS!");
                    Console.WriteLine($"Route: {flightPlan.DepartureID} → {flightPlan.DestinationID}");
                    Console.WriteLine($"Waypoints: {flightPlan.Waypoints.Count}");
                }
                else
                {
                    Console.WriteLine("❌ Flight plan was null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
            }
            finally
            {
                service.Dispose();
            }
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
