using System.Text.Json;

namespace Client;

class Program
{
    private static string? _currentToken;
    private static string? _currentTokenId;
    private static readonly HttpClient _httpClient = new();

    static async Task Main(string[] args)
    {
        Console.WriteLine("WCF Rolling Certs Client");
        Console.WriteLine("========================");
        Console.WriteLine("1. Press '1' to Log In (get SAML token)");
        Console.WriteLine("2. Press '2' to Call Server");
        Console.WriteLine("3. Press 'q' to quit");
        Console.WriteLine();
        
        while (true)
        {
            Console.Write("Enter your choice: ");
            var key = Console.ReadKey(true);
            Console.WriteLine();
            
            switch (key.KeyChar)
            {
                case '1':
                    await LoginAsync();
                    break;
                case '2':
                    await CallServerAsync();
                    break;
                case 'q':
                    return;
                default:
                    Console.WriteLine("Invalid option. Try again.");
                    break;
            }
            
            Console.WriteLine();
        }
    }
    
    private static async Task LoginAsync()
    {
        try
        {
            Console.WriteLine("[LOG] Calling TokenProvider to get SAML token...");
            
            var loginRequest = new { username = "testuser", password = "testpass" };
            var json = JsonSerializer.Serialize(loginRequest);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("http://localhost:5128/api/token/login", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (tokenResponse != null)
                {
                    _currentToken = tokenResponse.Token;
                    _currentTokenId = tokenResponse.TokenId;
                    
                    Console.WriteLine("[LOG] SAML token retrieved successfully");
                    Console.WriteLine($"[LOG] Token ID: {tokenResponse.TokenId}");
                    Console.WriteLine($"[LOG] Certificate Thumbprint: {tokenResponse.CertificateThumbprint}");
                    Console.WriteLine($"[LOG] Expires At: {tokenResponse.ExpiresAt}");
                }
                else
                {
                    Console.WriteLine("[ERROR] Failed to parse token response");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] Failed to get token: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception during login: {ex.Message}");
        }
    }
    
    private static async Task CallServerAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_currentToken))
            {
                Console.WriteLine("[ERROR] No token available. Please log in first.");
                return;
            }
            
            Console.WriteLine("[LOG] Calling WCF Server with SAML token...");
            Console.WriteLine($"[LOG] Using Token ID: {_currentTokenId}");
            
            var message = $"Hello from client at {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            Console.WriteLine($"[LOG] Sending message: {message}");
            
            var wcfRequest = new WcfRequest 
            { 
                Message = message,
                Token = _currentToken
            };
            
            var json = JsonSerializer.Serialize(wcfRequest);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("http://localhost:8080/WcfService/", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var wcfResponse = JsonSerializer.Deserialize<WcfResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (wcfResponse != null)
                {
                    Console.WriteLine($"[LOG] WCF Server response: {wcfResponse.Result}");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] Failed to call server: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception during server call: {ex.Message}");
        }
    }
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public string TokenId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string CertificateThumbprint { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}

public class WcfRequest
{
    public string Message { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class WcfResponse
{
    public string Result { get; set; } = string.Empty;
}
