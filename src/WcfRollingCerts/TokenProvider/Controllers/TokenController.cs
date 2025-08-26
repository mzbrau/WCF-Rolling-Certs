using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TokenProvider.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TokenController : ControllerBase
{
    private readonly ILogger<TokenController> _logger;
    private readonly string _certificatesPath;

    public TokenController(ILogger<TokenController> logger)
    {
        _logger = logger;
        _certificatesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "certificates");
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

            // Create JWT token with SAML-like claims
            var tokenId = Guid.NewGuid().ToString();
            var jwtToken = CreateJwtToken(request.Username, certificate, tokenId);

            var response = new TokenResponse
            {
                Token = jwtToken,
                TokenId = tokenId,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CertificateThumbprint = certificate.Thumbprint,
                IssuedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Token issued successfully. TokenId: {TokenId}, Certificate: {Thumbprint}", 
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

            // Load certificate with password
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

    private string CreateJwtToken(string username, X509Certificate2 certificate, string tokenId)
    {
        // Create claims (SAML-like claims in JWT format)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim("TokenId", tokenId),
            new Claim("CertificateThumbprint", certificate.Thumbprint),
            new Claim("TokenType", "SAML-Like-JWT"),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Create signing credentials using the certificate
        var key = new X509SecurityKey(certificate);
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256Signature);

        // Create JWT token
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = $"TokenProvider_{Environment.MachineName}",
            Audience = "WcfRollingCerts",
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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