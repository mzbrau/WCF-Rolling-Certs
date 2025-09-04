using System;
using System.ServiceModel;

namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting WCF Rolling Certificates Server...");

            // Create service host using configuration from app.config
            var host = new ServiceHost(typeof(WcfService));

            try
            {
                Console.WriteLine("Service configured with WS2007FederationHttpBinding");
                Console.WriteLine("Waiting for client connections...");
                Console.WriteLine("Rolling certificate support enabled - server accepts SAML tokens from any trusted certificate");
                Console.WriteLine("SAML tokens are validated against certificates in the 'certificates' directory");
                Console.WriteLine("Press any key to stop the service.");

                host.Open();
                
                Console.WriteLine("Service is running...");
                foreach (var endpoint in host.Description.Endpoints)
                {
                    Console.WriteLine($"Endpoint: {endpoint.Address}");
                    Console.WriteLine($"Binding: {endpoint.Binding.GetType().Name}");
                    Console.WriteLine($"Contract: {endpoint.Contract.Name}");
                }
                
                Console.ReadKey();
                host.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                host?.Abort();
            }
        }
    }
}
