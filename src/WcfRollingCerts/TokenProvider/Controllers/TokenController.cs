using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TokenProvider.Services;

namespace TokenProvider.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly ILogger<TokenController> _logger;
    private readonly string _certificatesPath;
    private readonly SamlTokenCreator _samlTokenCreator;

    public TokenController(ILogger<TokenController> logger)
    {
        _logger = logger;
        _certificatesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "certificates");
        _samlTokenCreator = new SamlTokenCreator();
    }

    [HttpPost("login")]
    public IActionResult GetToken([FromBody] LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Token request received for user: {Username}", request.Username);

            // Get the latest certificate
            var certificate = GetLatestCertificate();
            if (certificate == null)
            {
                return BadRequest("No certificates available");
            }

            // Create SAML token
            var tokenId = Guid.NewGuid().ToString();
            var samlToken = _samlTokenCreator.CreateSamlToken(request.Username, certificate, tokenId);

            var response = new TokenResponse
            {
                Token = samlToken,
                TokenId = tokenId,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CertificateThumbprint = certificate.Thumbprint,
                IssuedAt = DateTime.UtcNow
            };

            _logger.LogInformation("SAML token issued successfully. TokenId: {TokenId}, Certificate: {Thumbprint}", 
                tokenId, certificate.Thumbprint);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating token");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("certificates")]
    public IActionResult GetCertificates()
    {
        try
        {
            var certificates = GetAvailableCertificates();
            return Ok(certificates.Select(c => new
            {
                Thumbprint = c.Thumbprint,
                Subject = c.Subject,
                NotBefore = c.NotBefore,
                NotAfter = c.NotAfter
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificates");
            return StatusCode(500, "Internal server error");
        }
    }

    private X509Certificate2? GetLatestCertificate()
    {
        try
        {
            if (!Directory.Exists(_certificatesPath))
            {
                _logger.LogWarning("Certificates directory not found: {Path}", _certificatesPath);
                return null;
            }

            var pfxFiles = Directory.GetFiles(_certificatesPath, "*.pfx")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToArray();

            if (pfxFiles.Length == 0)
            {
                _logger.LogWarning("No PFX files found in certificates directory");
                return null;
            }

            var latestPfx = pfxFiles[0];
            _logger.LogInformation("Using certificate: {CertPath}", latestPfx);

            // Load certificate with password (matching the script-generated certificates)
            var certificate = new X509Certificate2(latestPfx, "P@ssw0rd123", X509KeyStorageFlags.Exportable);
            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading certificate");
            return null;
        }
    }

    private List<X509Certificate2> GetAvailableCertificates()
    {
        var certificates = new List<X509Certificate2>();

        if (!Directory.Exists(_certificatesPath))
            return certificates;

        var pfxFiles = Directory.GetFiles(_certificatesPath, "*.pfx");

        foreach (var pfxFile in pfxFiles)
        {
            try
            {
                var cert = new X509Certificate2(pfxFile, "P@ssw0rd123", X509KeyStorageFlags.Exportable);
                certificates.Add(cert);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load certificate: {CertPath}", pfxFile);
            }
        }

        return certificates.OrderByDescending(c => c.NotBefore).ToList();
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string TokenId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string CertificateThumbprint { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}