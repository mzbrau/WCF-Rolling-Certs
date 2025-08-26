using System;
using System.IdentityModel.Claims;
using System.IdentityModel.Policy;
using System.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using System.Threading;

namespace Server
{
    public class WcfService : IWcfService
    {
        public string GetSecureData(string request)
        {
            // Get the current security context to access SAML token
            var context = ServiceSecurityContext.Current;
            
            if (context == null || context.AuthorizationContext == null)
            {
                throw new SecurityAccessDeniedException("No security context found");
            }

            // Log authentication details
            Console.WriteLine($"[{DateTime.Now}] Processing authenticated request: {request}");
            Console.WriteLine($"[{DateTime.Now}] User identity: {context.PrimaryIdentity?.Name ?? "Unknown"}");
            Console.WriteLine($"[{DateTime.Now}] Authentication type: {context.PrimaryIdentity?.AuthenticationType ?? "Unknown"}");

            // Verify that we have claims from a SAML token
            bool hasSamlClaims = false;
            foreach (var claimSet in context.AuthorizationContext.ClaimSets)
            {
                if (claimSet is SamlSecurityTokenClaimSet)
                {
                    hasSamlClaims = true;
                    Console.WriteLine($"[{DateTime.Now}] SAML token validated successfully");
                    break;
                }
            }

            if (!hasSamlClaims)
            {
                throw new SecurityAccessDeniedException("Valid SAML token required");
            }

            // Process the request
            var response = $"Secure response to: {request} (processed at {DateTime.Now})";
            Console.WriteLine($"[{DateTime.Now}] Returning response: {response}");
            
            return response;
        }
    }
}