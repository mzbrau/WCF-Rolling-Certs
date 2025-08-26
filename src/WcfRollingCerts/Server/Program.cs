using System;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting WCF Rolling Certificates Server...");

            // Create service host
            var host = new ServiceHost(typeof(WcfService));

            try
            {
                // Use basic HTTP binding with message-level custom security
                var binding = new BasicHttpBinding();
                binding.Security.Mode = BasicHttpSecurityMode.None; // We handle security through SAML tokens in headers
                
                // Add endpoint
                var baseAddress = "http://localhost:8080/WcfService";
                host.AddServiceEndpoint(typeof(IWcfService), binding, baseAddress);

                Console.WriteLine($"Service endpoint: {baseAddress}");
                Console.WriteLine("Waiting for client connections...");
                Console.WriteLine("Rolling certificate support enabled - server accepts SAML tokens from any trusted certificate");
                Console.WriteLine("SAML tokens are validated against certificates in the 'certificates' directory");
                Console.WriteLine("Press any key to stop the service.");

                host.Open();
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
