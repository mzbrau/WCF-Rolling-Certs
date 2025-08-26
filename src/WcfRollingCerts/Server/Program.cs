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
                // Configure the service with custom security token manager for rolling certificates
                var serviceCredentials = host.Credentials;
                
                // Load a certificate for the service identity (this can be any of the trusted certificates)
                var certificatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "certificates");
                if (Directory.Exists(certificatesPath))
                {
                    var certFiles = Directory.GetFiles(certificatesPath, "*.pfx");
                    if (certFiles.Length > 0)
                    {
                        var serviceCert = new X509Certificate2(certFiles[0], ""); // For demo, using empty password
                        serviceCredentials.ServiceCertificate.Certificate = serviceCert;
                        Console.WriteLine($"Service identity certificate: {serviceCert.Subject}");
                    }
                }

                // Set up custom security token manager for rolling certificate support
                serviceCredentials.SecurityTokenManager = new RollingCertificateSecurityTokenManager(serviceCredentials);

                // Configure message security mode
                var binding = new WSHttpBinding();
                binding.Security.Mode = SecurityMode.Message;
                binding.Security.Message.ClientCredentialType = MessageCredentialType.None;
                binding.Security.Message.EstablishSecurityContext = false;
                
                // Add endpoint
                var baseAddress = "http://localhost:8080/WcfService";
                host.AddServiceEndpoint(typeof(IWcfService), binding, baseAddress);

                Console.WriteLine($"Service endpoint: {baseAddress}");
                Console.WriteLine("Waiting for client connections...");
                Console.WriteLine("Rolling certificate support enabled - server accepts tokens from any trusted certificate");
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
