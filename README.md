# WCF Rolling Certificates Proof of Concept

This repository demonstrates a proof of concept for supporting rolling certificates for WCF message protection using .NET 8.0.

## Architecture

The solution consists of three main components:

1. **TokenProvider** - ASP.NET Core Web API that issues JWT tokens (simulating SAML tokens) signed with certificates
2. **Server** - WCF-style server that validates tokens and accepts requests with message security
3. **Client** - Console application that obtains tokens and calls the server
4. **Certificate Generation Scripts** - Tools to generate self-signed certificates for testing

## Key Features

- **Rolling Certificate Support**: The server can validate tokens signed with multiple certificates
- **JWT Token Security**: Tokens are signed using X.509 certificates and contain certificate thumbprint information
- **Multiple Certificate Management**: Server loads and trusts all available certificates from the certificates directory
- **Comprehensive Logging**: All interactions are logged for debugging and monitoring

## Setup and Usage

### Prerequisites

- .NET 8.0 SDK
- OpenSSL (for certificate generation on Linux/macOS)
- PowerShell (for certificate generation on Windows)

### 1. Generate Certificates

```bash
# Generate initial certificate
cd scripts
./generate-certificate.sh

# Generate additional certificates for rolling
./generate-certificate.sh "WcfRollingCert2"
./generate-certificate.sh "WcfRollingCert3"
```

### 2. Build the Solution

```bash
cd src/WcfRollingCerts
dotnet build
```

### 3. Start the Services

**Terminal 1 - Token Provider:**
```bash
cd src/WcfRollingCerts/TokenProvider
dotnet run
```

**Terminal 2 - WCF Server:**
```bash
cd src/WcfRollingCerts/Server
dotnet run
```

**Terminal 3 - Client:**
```bash
cd src/WcfRollingCerts/Client
dotnet run
```

### 4. Test the Workflow

1. In the client application, press `1` to log in and obtain a token
2. Press `2` to call the server with the token
3. Observe the logging in all three applications

## Rolling Certificate Testing

To test rolling certificates:

1. Start all three applications
2. Use the client to get a token and call the server (note the certificate thumbprint)
3. Generate a new certificate using the scripts
4. Restart the TokenProvider (it will use the newest certificate)
5. Use the client to get a new token (it will use the new certificate)
6. Call the server again - it will accept both old and new certificate-signed tokens

## Configuration

### Certificate Password

The default password for all certificates is `P@ssw0rd123`. This is configured in:
- `scripts/generate-certificate.sh`
- `scripts/generate-certificate.ps1`
- `TokenProvider/Controllers/TokenController.cs`
- `Server/Services/WcfService.cs`

### Service Endpoints

- **TokenProvider**: `http://localhost:5128`
- **WCF Server**: `http://localhost:8080/WcfService/`

## API Endpoints

### TokenProvider

- `POST /api/token/login` - Get JWT token
  ```json
  {
    "username": "testuser",
    "password": "testpass"
  }
  ```

- `GET /api/token/certificates` - List available certificates

### Server

- `POST /WcfService/` - Call WCF service
  ```json
  {
    "message": "Hello World",
    "token": "eyJ..."
  }
  ```

## Security Features

- **Certificate-based Token Signing**: All tokens are signed using X.509 certificates
- **Multi-Certificate Validation**: Server accepts tokens signed by any trusted certificate
- **Token Expiration**: Tokens have a 1-hour expiration time
- **Certificate Thumbprint Tracking**: Each token includes the thumbprint of the signing certificate

## Technical Notes

- **Framework**: Built on .NET 8.0 for cross-platform compatibility
- **WCF Alternative**: Uses HTTP-based communication instead of traditional WCF due to .NET Core limitations
- **Certificate Storage**: Certificates are stored in the `certificates/` directory as PFX files
- **Token Format**: Uses JWT tokens with SAML-like claims for compatibility and ease of validation

## Production Considerations

For production deployment, consider:

1. **Secure Certificate Storage**: Use Azure Key Vault, HSM, or other secure storage
2. **Certificate Rotation Policies**: Implement automated certificate rotation
3. **Token Validation**: Add more robust token validation and signature verification
4. **Message Security**: Implement proper WCF message security or use HTTPS with client certificates
5. **Monitoring**: Add application insights and monitoring
6. **Configuration**: Externalize configuration (certificate paths, passwords, endpoints)

## Development Environment

This POC was developed and tested in a Linux environment using .NET 8.0. The WPF client was converted to a console application due to environment limitations, but the same functionality can be implemented in WPF for Windows environments.
