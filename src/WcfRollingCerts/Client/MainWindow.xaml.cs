using System;
using System.Net.Http;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IdentityModel.Tokens;
using System.ServiceModel.Security;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Runtime.Serialization;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _currentSamlToken = null;
        private readonly HttpClient _httpClient;
        private const string TOKEN_PROVIDER_URL = "http://localhost:5000/api/token/login";
        private const string WCF_SERVICE_URL = "http://localhost:8080/WcfService";

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            LogMessage("Application started");
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnLogin.IsEnabled = false;
                LogMessage("Requesting SAML token from TokenProvider...");

                var loginRequest = new LoginRequest
                {
                    Username = txtUsername.Text,
                    Password = "password" // For demo purposes
                };

                var json = SerializeToJson(loginRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(TOKEN_PROVIDER_URL, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var tokenResponse = DeserializeFromJson<TokenResponse>(responseJson);
                    
                    _currentSamlToken = tokenResponse.Token;
                    
                    lblAuthStatus.Text = $"Authenticated (Certificate: {tokenResponse.CertificateThumbprint.Substring(0, 8)}...)";
                    btnCallService.IsEnabled = true;
                    
                    LogMessage($"SAML token received successfully");
                    LogMessage($"Token ID: {tokenResponse.TokenId}");
                    LogMessage($"Certificate Thumbprint: {tokenResponse.CertificateThumbprint}");
                    LogMessage($"Expires: {tokenResponse.ExpiresAt}");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    LogMessage($"Authentication failed: {response.StatusCode} - {error}");
                    lblAuthStatus.Text = "Authentication failed";
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Login error: {ex.Message}");
                lblAuthStatus.Text = "Authentication error";
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private async void BtnCallService_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentSamlToken))
            {
                LogMessage("No SAML token available. Please login first.");
                return;
            }

            try
            {
                btnCallService.IsEnabled = false;
                LogMessage("Calling WCF service with SAML token...");

                // Create WCF client with basic HTTP binding (security handled through SAML token in headers)
                var binding = new BasicHttpBinding();
                binding.Security.Mode = BasicHttpSecurityMode.None;

                var endpoint = new EndpointAddress(WCF_SERVICE_URL);
                
                using (var factory = new ChannelFactory<IWcfService>(binding, endpoint))
                {
                    // Configure the channel to use SAML token
                    factory.Credentials.SupportInteractive = false;
                    
                    // Create channel and call service
                    var channel = factory.CreateChannel();
                    
                    // Use custom message headers to send SAML token
                    using (var scope = new OperationContextScope((IContextChannel)channel))
                    {
                        // Add SAML token to message headers
                        var header = MessageHeader.CreateHeader("SamlToken", "http://schemas.wcf.rolling.certs", _currentSamlToken);
                        OperationContext.Current.OutgoingMessageHeaders.Add(header);

                        LogMessage($"Sending request: {txtRequest.Text}");
                        
                        var response = channel.GetSecureData(txtRequest.Text);
                        
                        txtResponse.Text = response;
                        LogMessage($"Service response received: {response}");
                    }
                    
                    ((IClientChannel)channel).Close();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Service call error: {ex.Message}");
                txtResponse.Text = $"Error: {ex.Message}";
            }
            finally
            {
                btnCallService.IsEnabled = true;
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}\r\n";
            
            txtLog.Dispatcher.Invoke(() =>
            {
                txtLog.AppendText(logEntry);
                txtLog.ScrollToEnd();
            });
        }

        private string SerializeToJson<T>(T obj)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, obj);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private T DeserializeFromJson<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _httpClient?.Dispose();
            base.OnClosed(e);
        }
    }

    // Service interface matching the server
    [ServiceContract]
    public interface IWcfService
    {
        [OperationContract]
        string GetSecureData(string request);
    }

    // Token response model
    [DataContract]
    public class TokenResponse
    {
        [DataMember]
        public string Token { get; set; } = string.Empty;
        
        [DataMember]
        public string TokenId { get; set; } = string.Empty;
        
        [DataMember]
        public DateTime ExpiresAt { get; set; }
        
        [DataMember]
        public string CertificateThumbprint { get; set; } = string.Empty;
        
        [DataMember]
        public DateTime IssuedAt { get; set; }
    }

    // Login request model
    [DataContract]
    public class LoginRequest
    {
        [DataMember]
        public string Username { get; set; } = string.Empty;
        
        [DataMember]
        public string Password { get; set; } = string.Empty;
    }
}
