using System.Net;
using System.Text;
using System.Text.Json;

namespace Server.Services;

public class SimpleWcfServer
{
    private HttpListener? _listener;
    private bool _isRunning;
    private readonly WcfService _wcfService;

    public SimpleWcfServer()
    {
        _wcfService = new WcfService();
    }

    public async Task StartAsync()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:8080/WcfService/");
        
        _listener.Start();
        _isRunning = true;
        
        Console.WriteLine("[LOG] WCF Service is running at: http://localhost:8080/WcfService/");
        
        while (_isRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => ProcessRequestAsync(context));
            }
            catch (Exception ex) when (_isRunning)
            {
                Console.WriteLine($"[ERROR] Error processing request: {ex.Message}");
            }
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
        await Task.Delay(100); // Give time for pending requests
        Console.WriteLine("[LOG] Service stopped.");
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod == "POST")
            {
                using var reader = new StreamReader(request.InputStream);
                var requestBody = await reader.ReadToEndAsync();
                
                Console.WriteLine($"[LOG] Received request: {requestBody}");

                // Parse the simple JSON request
                var requestData = JsonSerializer.Deserialize<WcfRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (requestData != null)
                {
                    // Call the WCF service method with token
                    var result = _wcfService.GetCurrentTime(requestData.Message, requestData.Token);
                    
                    var responseData = new WcfResponse { Result = result };
                    var responseJson = JsonSerializer.Serialize(responseData);
                    
                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    response.ContentType = "application/json";
                    response.ContentLength64 = responseBytes.Length;
                    
                    await response.OutputStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                }
                else
                {
                    response.StatusCode = 400;
                    var errorBytes = Encoding.UTF8.GetBytes("Bad Request");
                    await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                }
            }
            else
            {
                response.StatusCode = 405;
                var errorBytes = Encoding.UTF8.GetBytes("Method Not Allowed");
                await response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
            }

            response.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Error processing request: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch { }
        }
    }
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