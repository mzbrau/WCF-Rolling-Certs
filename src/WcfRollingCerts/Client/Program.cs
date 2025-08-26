namespace Client;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("WCF Rolling Certs Client");
        Console.WriteLine("1. Press '1' to Log In (get SAML token)");
        Console.WriteLine("2. Press '2' to Call Server");
        Console.WriteLine("3. Press 'q' to quit");
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            switch (key.KeyChar)
            {
                case '1':
                    await LoginAsync();
                    break;
                case '2':
                    await CallServerAsync();
                    break;
                case 'q':
                    return;
                default:
                    Console.WriteLine("Invalid option. Try again.");
                    break;
            }
        }
    }
    
    private static async Task LoginAsync()
    {
        Console.WriteLine("[LOG] Calling TokenProvider to get SAML token...");
        // TODO: Implement token retrieval
        await Task.Delay(100);
        Console.WriteLine("[LOG] SAML token retrieved successfully");
    }
    
    private static async Task CallServerAsync()
    {
        Console.WriteLine("[LOG] Calling WCF Server with SAML token...");
        // TODO: Implement WCF server call
        await Task.Delay(100);
        Console.WriteLine("[LOG] WCF Server response received");
    }
}
