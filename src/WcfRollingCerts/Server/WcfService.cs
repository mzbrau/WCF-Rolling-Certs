using System;
using System.IdentityModel.Claims;
using System.IdentityModel.Policy;
using System.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Security.Cryptography.Xml;

namespace Server
{
    public class WcfService : IWcfService
    {
        private readonly List<X509Certificate2> _trustedCertificates;

        public WcfService()
        {
            _trustedCertificates = LoadTrustedCertificates();
        }

        public string GetSecureData(string request)
        {
            try
            {
                // With federation binding, the SAML token is automatically processed by WCF
                // We can access the security context and claims directly
                var context = OperationContext.Current;
                if (context?.ServiceSecurityContext?.AuthorizationContext != null)
                {
                    LogMessage("Processing authenticated request with federation binding");
                    LogMessage($"Identity: {context.ServiceSecurityContext.PrimaryIdentity?.Name ?? "Unknown"}");
                    LogMessage($"Authentication Type: {context.ServiceSecurityContext.PrimaryIdentity?.AuthenticationType ?? "Unknown"}");
                    
                    // Log claims from the SAML token
                    foreach (var claimSet in context.ServiceSecurityContext.AuthorizationContext.ClaimSets)
                    {
                        LogMessage($"ClaimSet Issuer: {claimSet.Issuer}");
                        foreach (var claim in claimSet)
                        {
                            LogMessage($"Claim Type: {claim.ClaimType}, Value: {claim.Resource}");
                        }
                    }
                }
                else
                {
                    LogMessage("No security context found - authentication may have failed");
                }

                // Process the request
                LogMessage($"Processing request: {request}");
                var response = $"Secure response to: {request} (processed at {DateTime.Now})";
                LogMessage($"Returning response: {response}");
                
                return response;
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing request: {ex.Message}");
                throw;
            }
        }

        private void LogMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        private List<X509Certificate2> LoadTrustedCertificates()
        {
            var certificates = new List<X509Certificate2>();
            var certificatesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "certificates");

            if (Directory.Exists(certificatesPath))
            {
                var certFiles = Directory.GetFiles(certificatesPath, "*.pfx");
                foreach (var certFile in certFiles)
                {
                    try
                    {
                        var cert = new X509Certificate2(certFile, "P@ssw0rd123");
                        certificates.Add(cert);
                        LogMessage($"Loaded trusted certificate: {cert.Subject} (Thumbprint: {cert.Thumbprint})");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to load certificate {certFile}: {ex.Message}");
                    }
                }
            }
            else
            {
                LogMessage($"Certificates directory not found: {certificatesPath}");
            }

            return certificates;
        }
    }
}