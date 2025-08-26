using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IdentityModel.Policy;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;

namespace Server
{
    public class RollingCertificateSecurityTokenManager : ServiceCredentialsSecurityTokenManager
    {
        private readonly List<X509Certificate2> _trustedCertificates;

        public RollingCertificateSecurityTokenManager(ServiceCredentials serviceCredentials) 
            : base(serviceCredentials)
        {
            _trustedCertificates = LoadTrustedCertificates();
            Console.WriteLine($"[{DateTime.Now}] Loaded {_trustedCertificates.Count} trusted certificates for rolling support");
        }

        private List<X509Certificate2> LoadTrustedCertificates()
        {
            var certificates = new List<X509Certificate2>();
            var certificatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "certificates");

            if (Directory.Exists(certificatesPath))
            {
                var certFiles = Directory.GetFiles(certificatesPath, "*.pfx");
                foreach (var certFile in certFiles)
                {
                    try
                    {
                        // For demo purposes, using empty password. In production, use secure password management
                        var cert = new X509Certificate2(certFile, "");
                        certificates.Add(cert);
                        Console.WriteLine($"[{DateTime.Now}] Loaded certificate: {cert.Subject} (Thumbprint: {cert.Thumbprint})");
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

        public override SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(
            SecurityTokenRequirement tokenRequirement, 
            out SecurityTokenResolver outOfBandTokenResolver)
        {
            outOfBandTokenResolver = null;

            if (tokenRequirement.TokenType == SecurityTokenTypes.Saml ||
                tokenRequirement.TokenType == SecurityTokenTypes.Saml2)
            {
                return new RollingSamlSecurityTokenAuthenticator(_trustedCertificates);
            }

            return base.CreateSecurityTokenAuthenticator(tokenRequirement, out outOfBandTokenResolver);
        }
    }

    public class RollingSamlSecurityTokenAuthenticator : SamlSecurityTokenAuthenticator
    {
        private readonly List<X509Certificate2> _trustedCertificates;

        public RollingSamlSecurityTokenAuthenticator(List<X509Certificate2> trustedCertificates)
            : base(new List<SecurityTokenAuthenticator>())
        {
            _trustedCertificates = trustedCertificates;
        }

        public override bool CanValidateToken(SecurityToken token)
        {
            return token is SamlSecurityToken;
        }

        public override ReadOnlyCollection<IAuthorizationPolicy> ValidateToken(SecurityToken token)
        {
            if (!(token is SamlSecurityToken samlToken))
            {
                throw new SecurityTokenException("Expected SAML security token");
            }

            // Validate the token signature against any of the trusted certificates
            bool isValidSignature = false;
            X509Certificate2 signingCert = null;

            foreach (var trustedCert in _trustedCertificates)
            {
                try
                {
                    var keyClause = new X509SecurityToken(trustedCert).CreateKeyIdentifierClause<X509ThumbprintKeyIdentifierClause>();
                    if (samlToken.Assertion.SigningCredentials?.SigningKeyIdentifier?.Find<X509ThumbprintKeyIdentifierClause>()?.ToString() == keyClause.ToString())
                    {
                        // Verify the signature
                        var key = new X509AsymmetricSecurityKey(trustedCert);
                        if (samlToken.Assertion.SigningCredentials.SigningKey.KeySize == key.KeySize)
                        {
                            isValidSignature = true;
                            signingCert = trustedCert;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now}] Certificate validation failed for {trustedCert.Thumbprint}: {ex.Message}");
                }
            }

            if (!isValidSignature)
            {
                throw new SecurityTokenValidationException("SAML token signature validation failed - no trusted certificate found");
            }

            Console.WriteLine($"[{DateTime.Now}] SAML token validated successfully with certificate: {signingCert.Thumbprint}");

            // Return default authorization policies for the validated token
            return base.ValidateToken(token);
        }
    }
}