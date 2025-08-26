using Server.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;

namespace Server.Services;

public class WcfService : IWcfService
{
    private readonly List<X509Certificate2> _trustedCertificates;

    public WcfService()
    {
        _trustedCertificates = LoadTrustedCertificates();
    }

    public string GetCurrentTime(string message)
    {
        return GetCurrentTime(message, "");
    }

    public string GetCurrentTime(string message, string token = "")
    {
        // Validate the token if present
        if (!string.IsNullOrEmpty(token))
        {
            var validationResult = ValidateToken(token);
            if (!validationResult.IsValid)
            {
                throw new UnauthorizedAccessException($"Invalid token: {validationResult.ErrorMessage}");
            }
            
            Console.WriteLine($"[LOG] Valid token received. TokenId: {validationResult.TokenId}, Certificate: {validationResult.CertificateThumbprint}");
        }
        else
        {
            Console.WriteLine("[LOG] No token provided - processing request without authentication");
        }

        var response = $"Server received: '{message}' at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        Console.WriteLine($"[LOG] Processing request: {message}");
        Console.WriteLine($"[LOG] Sending response: {response}");
        
        return response;
    }


    private TokenValidationResult ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jsonToken = tokenHandler.ReadJwtToken(token);

            // Find the certificate used to sign the token
            var certThumbprint = jsonToken.Claims.FirstOrDefault(c => c.Type == "CertificateThumbprint")?.Value;
            if (string.IsNullOrEmpty(certThumbprint))
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = "Certificate thumbprint not found in token" };
            }

            var signingCertificate = _trustedCertificates.FirstOrDefault(c => 
                c.Thumbprint.Equals(certThumbprint, StringComparison.OrdinalIgnoreCase));

            if (signingCertificate == null)
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = $"Certificate not trusted: {certThumbprint}" };
            }

            // Additional validation logic would go here
            var tokenId = jsonToken.Claims.FirstOrDefault(c => c.Type == "TokenId")?.Value ?? "Unknown";

            return new TokenValidationResult 
            { 
                IsValid = true, 
                TokenId = tokenId,
                CertificateThumbprint = certThumbprint
            };
        }
        catch (Exception ex)
        {
            return new TokenValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    private List<X509Certificate2> LoadTrustedCertificates()
    {
        var certificates = new List<X509Certificate2>();
        var certificatesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "certificates");

        if (!Directory.Exists(certificatesPath))
        {
            Console.WriteLine($"[WARNING] Certificates directory not found: {certificatesPath}");
            return certificates;
        }

        var pfxFiles = Directory.GetFiles(certificatesPath, "*.pfx");
        Console.WriteLine($"[LOG] Loading {pfxFiles.Length} certificate(s) from {certificatesPath}");

        foreach (var pfxFile in pfxFiles)
        {
            try
            {
                var cert = new X509Certificate2(pfxFile, "P@ssw0rd123", X509KeyStorageFlags.Exportable);
                certificates.Add(cert);
                Console.WriteLine($"[LOG] Loaded certificate: {cert.Thumbprint} (Subject: {cert.Subject})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to load certificate {pfxFile}: {ex.Message}");
            }
        }

        return certificates;
    }
}

public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string TokenId { get; set; } = string.Empty;
    public string CertificateThumbprint { get; set; } = string.Empty;
}