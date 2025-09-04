#!/usr/bin/env pwsh

param(
    [string]$CertName = "WcfRollingCert",
    [string]$OutputDir = "../certificates",
    [int]$ValidDays = 365
)

# Create output directory if it doesn't exist
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force
}

# Generate a unique certificate name with timestamp
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$certFileName = "$CertName-$timestamp"
$certPath = Join-Path $OutputDir "$certFileName.pfx"
$password = "P@ssw0rd123"

Write-Host "Generating self-signed certificate: $certFileName" -ForegroundColor Green

try {
    # Create a self-signed certificate
    $cert = New-SelfSignedCertificate `
        -Subject "CN=$CertName, O=WCF Rolling Certs POC, C=US" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -NotBefore (Get-Date) `
        -NotAfter (Get-Date).AddDays($ValidDays) `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature, KeyEncipherment `
        -Type DocumentEncryptionCert

    # Export certificate to PFX file
    $securePassword = ConvertTo-SecureString -String $password -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $securePassword

    # Also export public key only (CER format)
    $cerPath = Join-Path $OutputDir "$certFileName.cer"
    Export-Certificate -Cert $cert -FilePath $cerPath

    # Remove certificate from store
    Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)"

    Write-Host "Certificate generated successfully!" -ForegroundColor Green
    Write-Host "PFX file: $certPath" -ForegroundColor Yellow
    Write-Host "CER file: $cerPath" -ForegroundColor Yellow
    Write-Host "Password: $password" -ForegroundColor Yellow
    Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor Yellow

    # Create a JSON file with certificate information
    $certInfo = @{
        Name = $certFileName
        PfxPath = $certPath
        CerPath = $cerPath
        Password = $password
        Thumbprint = $cert.Thumbprint
        Subject = $cert.Subject
        NotBefore = $cert.NotBefore.ToString("yyyy-MM-dd HH:mm:ss")
        NotAfter = $cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss")
        Generated = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }

    $certInfoPath = Join-Path $OutputDir "$certFileName.json"
    $certInfo | ConvertTo-Json -Depth 3 | Set-Content -Path $certInfoPath
    Write-Host "Certificate info saved to: $certInfoPath" -ForegroundColor Yellow

} catch {
    Write-Error "Failed to generate certificate: $($_.Exception.Message)"
    exit 1
}