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
                // Extract SAML token from message headers
                var samlToken = ExtractSamlTokenFromHeaders();
                
                if (string.IsNullOrEmpty(samlToken))
                {
                    throw new SecurityAccessDeniedException("SAML token not found in message headers");
                }

                // Validate the SAML token
                ValidateSamlToken(samlToken);

                // Log authentication details
                Console.WriteLine($"[{DateTime.Now}] Processing authenticated request: {request}");
                Console.WriteLine($"[{DateTime.Now}] SAML token validated successfully");

                // Process the request
                var response = $"Secure response to: {request} (processed at {DateTime.Now})";
                Console.WriteLine($"[{DateTime.Now}] Returning response: {response}");
                
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] Error processing request: {ex.Message}");
                throw;
            }
        }

        private string ExtractSamlTokenFromHeaders()
        {
            var context = OperationContext.Current;
            if (context?.IncomingMessageHeaders == null)
                return null;

            // Look for SAML token in message headers
            var headerIndex = context.IncomingMessageHeaders.FindHeader("SamlToken", "http://schemas.wcf.rolling.certs");
            if (headerIndex >= 0)
            {
                return context.IncomingMessageHeaders.GetHeader<string>(headerIndex);
            }

            return null;
        }

        private void ValidateSamlToken(string samlTokenXml)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(samlTokenXml);

                // Extract certificate thumbprint from attributes
                var thumbprintNode = doc.SelectSingleNode("//saml:Attribute[@Name='CertificateThumbprint']/saml:AttributeValue", GetNamespaceManager(doc));
                if (thumbprintNode == null)
                {
                    throw new SecurityTokenValidationException("Certificate thumbprint not found in SAML token");
                }

                var certThumbprint = thumbprintNode.InnerText;
                Console.WriteLine($"[{DateTime.Now}] Token claims certificate thumbprint: {certThumbprint}");

                // Find matching trusted certificate
                var trustedCert = _trustedCertificates.FirstOrDefault(c => 
                    c.Thumbprint.Equals(certThumbprint, StringComparison.OrdinalIgnoreCase));

                if (trustedCert == null)
                {
                    throw new SecurityTokenValidationException($"Certificate with thumbprint {certThumbprint} is not trusted");
                }

                // Verify the signature
                var signedXml = new SignedXml(doc);
                var signatureNode = doc.SelectSingleNode("//ds:Signature", GetNamespaceManager(doc));
                if (signatureNode == null)
                {
                    throw new SecurityTokenValidationException("No signature found in SAML token");
                }

                signedXml.LoadXml((XmlElement)signatureNode);
                
                if (!signedXml.CheckSignature(trustedCert, true))
                {
                    throw new SecurityTokenValidationException("SAML token signature validation failed");
                }

                // Check expiration
                var conditionsNode = doc.SelectSingleNode("//saml:Conditions", GetNamespaceManager(doc));
                if (conditionsNode != null)
                {
                    var notOnOrAfter = conditionsNode.Attributes["NotOnOrAfter"]?.Value;
                    if (!string.IsNullOrEmpty(notOnOrAfter))
                    {
                        if (DateTime.Parse(notOnOrAfter).ToUniversalTime() < DateTime.UtcNow)
                        {
                            throw new SecurityTokenValidationException("SAML token has expired");
                        }
                    }
                }

                Console.WriteLine($"[{DateTime.Now}] SAML token validated successfully with certificate: {trustedCert.Thumbprint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] SAML token validation failed: {ex.Message}");
                throw new SecurityTokenValidationException($"SAML token validation failed: {ex.Message}");
            }
        }

        private XmlNamespaceManager GetNamespaceManager(XmlDocument doc)
        {
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
            nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            return nsmgr;
        }

        private List<X509Certificate2> LoadTrustedCertificates()
        {
            var certificates = new List<X509Certificate2>();
            var certificatesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "certificates");

            if (Directory.Exists(certificatesPath))
            {
                var certFiles = Directory.GetFiles(certificatesPath, "*.pfx");
                foreach (var certFile in certFiles)
                {
                    try
                    {
                        var cert = new X509Certificate2(certFile, "P@ssw0rd123");
                        certificates.Add(cert);
                        Console.WriteLine($"[{DateTime.Now}] Loaded trusted certificate: {cert.Subject} (Thumbprint: {cert.Thumbprint})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now}] Failed to load certificate {certFile}: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now}] Certificates directory not found: {certificatesPath}");
            }

            return certificates;
        }
    }
}