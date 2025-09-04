# WCF Rolling Certificates Proof of Concept

This repository demonstrates a proof of concept for supporting rolling certificates in WCF message protection scenarios using SAML tokens and .NET Framework 4.8.

## Architecture

The solution consists of three main components:

1. **TokenProvider** - ASP.NET Core Web API (.NET 8.0) that issues SAML tokens signed with X.509 certificates
2. **Server** - WCF service (.NET Framework 4.8) that validates SAML tokens and accepts requests with message-based security
3. **Client** - WPF application (.NET Framework 4.8) that obtains SAML tokens and calls the WCF server
4. **Certificate Generation Scripts** - Tools to generate self-signed certificates for testing

## Key Features

- **Rolling Certificate Support**: The server can validate SAML tokens signed with multiple certificates simultaneously
- **SAML Token Security**: Tokens are signed using X.509 certificates and transmitted in WCF message headers
- **Message-Based Security**: WCF service uses message-level security with custom SAML token validation
- **Multiple Certificate Management**: Server loads and trusts all available certificates from the certificates directory
- **Comprehensive Logging**: All authentication and validation events are logged for debugging and monitoring

## Implementation Details

### SAML Token Flow
1. Client requests SAML token from TokenProvider with username/password
2. TokenProvider signs SAML assertion with latest certificate
3. Client receives signed SAML token containing certificate thumbprint
4. Client calls WCF service, passing SAML token in message headers
5. WCF service validates SAML token signature against trusted certificates
6. Rolling certificate support: server accepts tokens from any trusted certificate

### Security Features
- X.509 certificate-based SAML token signing
- Certificate thumbprint validation
- Token expiration (1-hour lifetime)
- XML signature verification
- Comprehensive audit logging of all authentication events

## Setup and Usage

### Prerequisites

- .NET 8.0 SDK (for TokenProvider)
- .NET Framework 4.8 Developer Pack (for Server and Client)
- Visual Studio or MSBuild (for .NET Framework projects)
- OpenSSL (for certificate generation on Linux/macOS)
- PowerShell (for certificate generation on Windows)

### 1. Generate Certificates

```bash
# Generate initial certificate
cd scripts
./generate-certificate.sh "WcfRollingCert1"

# Generate additional certificates for rolling demonstration
./generate-certificate.sh "WcfRollingCert2"
./generate-certificate.sh "WcfRollingCert3"
```

### 2. Build the Solution

**TokenProvider (.NET 8.0):**
```bash
cd src/WcfRollingCerts/TokenProvider
dotnet build
```

**Server and Client (.NET Framework 4.8):**
```bash
# Requires Visual Studio or MSBuild on Windows
cd src/WcfRollingCerts
# Open in Visual Studio and build, or use:
msbuild WcfRollingCerts.sln
```

### 3. Start the Services

**Terminal 1 - Token Provider:**
```bash
cd src/WcfRollingCerts/TokenProvider
dotnet run
# Service will start on http://localhost:5128
```

**Terminal 2 - WCF Server (Windows only):**
```bash
cd src/WcfRollingCerts/Server/bin/Debug
Server.exe
# Service will start on http://localhost:8080/WcfService
```

**Terminal 3 - WPF Client (Windows only):**
```bash
cd src/WcfRollingCerts/Client/bin/Debug
Client.exe
# WPF application will open
```

### 4. Testing the Workflow

1. **Start all services** as described above
2. **In the WPF Client:**
   - Enter username (default: "testuser")
   - Click "Get SAML Token" - this will call the TokenProvider
   - Verify authentication status shows certificate thumbprint
   - Enter a request message (default: "Hello from WCF client")
   - Click "Call Service" - this will call the WCF server with the SAML token
   - Verify the response appears and check the activity log

3. **Rolling Certificate Test:**
   - Generate a new certificate: `./scripts/generate-certificate.sh "WcfRollingCert3"`
   - Restart the TokenProvider (it uses the latest certificate)
   - The WCF Server continues running (trusts all certificates)
   - Get a new token with the new certificate
   - Call the service - it should work with both old and new tokens

## Project Structure

```
WcfRollingCerts/
├── Client/                 # WPF application (.NET Framework 4.8)
│   ├── MainWindow.xaml     # UI for authentication and service calls
│   └── MainWindow.xaml.cs  # Logic for SAML token handling and WCF calls
├── Server/                 # WCF service (.NET Framework 4.8)
│   ├── IWcfService.cs      # Service contract
│   ├── WcfService.cs       # Service implementation with SAML validation
│   └── Program.cs          # Service host with BasicHttpBinding
├── TokenProvider/          # ASP.NET Core API (.NET 8.0)
│   ├── Controllers/
│   │   └── TokenController.cs  # Issues SAML tokens
│   └── Services/
│       └── SamlTokenCreator.cs # Creates and signs SAML assertions
└── scripts/
    ├── generate-certificate.sh  # Bash script for certificate generation
    └── generate-certificate.ps1 # PowerShell script for certificate generation
```

## Certificate Management

- **Certificate Location**: All certificates are stored in the `certificates/` directory
- **Certificate Format**: PKCS#12 (.pfx) files with password "P@ssw0rd123"
- **Certificate Selection**: TokenProvider always uses the latest certificate (by creation time)
- **Certificate Trust**: Server trusts all certificates found in the certificates directory
- **Rolling Support**: New certificates can be added without restarting the server

## Security Considerations

This is a proof of concept for demonstration purposes:

- Uses self-signed certificates
- Hardcoded certificate password
- Basic authentication (username/password)
- HTTP transport (not HTTPS)
- No certificate revocation checking

For production use, implement:
- CA-signed certificates
- Secure certificate storage (e.g., Azure Key Vault)
- Proper authentication mechanisms
- HTTPS transport
- Certificate revocation checking
- Token encryption in addition to signing

## Troubleshooting

### Common Issues

1. **TokenProvider certificate loading errors**
   - Ensure certificates exist in `certificates/` directory
   - Verify certificate password is "P@ssw0rd123"
   - Check certificate file permissions

2. **WCF Server startup issues**
   - Ensure .NET Framework 4.8 is installed
   - Verify port 8080 is available
   - Check Windows Firewall settings

3. **Client connection issues**
   - Verify TokenProvider is running on port 5128
   - Verify WCF Server is running on port 8080
   - Check service URLs in client configuration

4. **SAML token validation failures**
   - Ensure server certificates directory contains the signing certificate
   - Check certificate thumbprints match between TokenProvider and Server
   - Verify token hasn't expired (1-hour lifetime)

### Logs

- **TokenProvider**: Console output shows certificate loading and token generation
- **WCF Server**: Console output shows SAML token validation and request processing
- **WPF Client**: Activity log shows authentication flow and service calls

## Demo Scenario

This implementation demonstrates a rolling certificate scenario where:

1. **Initial State**: System uses Certificate A for token signing
2. **Certificate Rotation**: Generate Certificate B, restart TokenProvider
3. **Continued Operation**: 
   - New tokens signed with Certificate B
   - Old tokens (signed with Certificate A) still valid
   - Server accepts both certificates simultaneously
   - Zero downtime certificate rotation achieved

The WCF service continues to trust both certificates, enabling seamless certificate transitions in production environments.