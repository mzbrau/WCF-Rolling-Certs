using Server.Services;

namespace Server;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("WCF Rolling Certs Server starting...");
        
        // For this POC, we'll create a simple HTTP listener to simulate WCF functionality
        // In a real implementation, you would use CoreWCF for .NET Core
        var server = new SimpleWcfServer();
        
        try
        {
            await server.StartAsync();
            Console.WriteLine("[LOG] Press any key to stop the server...");
            Console.ReadKey();
            await server.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to start server: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
