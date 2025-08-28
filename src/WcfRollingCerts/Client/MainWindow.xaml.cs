using System;
using System.Net.Http;
using System.ServiceModel;
using System.Text;
using System.Windows;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using Newtonsoft.Json;
using System.IdentityModel.Tokens;
using System.ServiceModel.Security;
using System.Xml;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _currentSamlToken = null;
        private readonly HttpClient _httpClient;
        private const string TOKEN_PROVIDER_URL = "http://localhost:5128/api/token/login";
        private ChannelFactory<IWcfService> _channelFactory;

        public MainWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient();
            InitializeChannelFactory();
            LogMessage("Application started");
        }

        private void InitializeChannelFactory()
        {
            try
            {
                // Create channel factory using configuration from app.config
                _channelFactory = new ChannelFactory<IWcfService>("WcfServiceEndpoint");
                LogMessage("Channel factory initialized with FederatedSecurityBinding");
            }
            catch (Exception ex)
            {
                LogMessage($"Error initializing channel factory: {ex.Message}");
            }
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

                TokenResponse? tokenResponse = null;
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseJson);
                }

                if (tokenResponse?.Token != null)
                {
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
                LogMessage("Calling WCF service with SAML token using CreateChannelWithIssuedToken...");

                // Create channel using the pattern requested by user
                var endpoint = new EndpointAddress("http://localhost:8080/WcfService");
                var channel = CreateChannel(endpoint);

                LogMessage($"Sending request: {txtRequest.Text}");
                
                var response = channel.GetSecureData(txtRequest.Text);
                
                txtResponse.Text = response;
                LogMessage($"Service response received: {response}");
                
                ((IClientChannel)channel).Close();
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

        protected IWcfService CreateChannel(EndpointAddress address)
        {
            IWcfService channelProxy;
            try
            {
                if (!string.IsNullOrEmpty(_currentSamlToken))
                {
                    var currentToken = CreateSamlSecurityToken(_currentSamlToken);
                    channelProxy = _channelFactory.CreateChannelWithIssuedToken(currentToken, address);
                    LogMessage("Channel created with issued SAML token");
                }
                else
                {
                    channelProxy = _channelFactory.CreateChannel(address);
                    LogMessage("Channel created without token");
                }
            }
            catch (CommunicationObjectFaultedException e)
            {
                LogMessage($"Communication object faulted: {e.Message}");
                throw;
            }

            ((IClientChannel)channelProxy)?.Open();
            return channelProxy;
        }

        private SecurityToken CreateSamlSecurityToken(string samlXml)
        {
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(samlXml);
                
                // Create SAML security token from XML
                var reader = new XmlNodeReader(doc.DocumentElement);
                var tokenHandlers = SecurityTokenHandlerCollection.CreateDefaultSecurityTokenHandlerCollection();
                var token = tokenHandlers.ReadToken(reader);
                
                return token;
            }
            catch (Exception ex)
            {
                LogMessage($"Error creating SAML security token: {ex.Message}");
                throw;
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
            _channelFactory?.Close();
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
