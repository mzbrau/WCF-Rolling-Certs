#!/bin/bash

# Certificate generation script for WCF Rolling Certs POC
# Uses OpenSSL to generate self-signed certificates

CERT_NAME="${1:-WcfRollingCert}"
OUTPUT_DIR="${2:-../certificates}"
VALID_DAYS="${3:-365}"
PASSWORD="P@ssw0rd123"

# Create output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"

# Generate a unique certificate name with timestamp
TIMESTAMP=$(date +"%Y%m%d-%H%M%S")
CERT_FILENAME="${CERT_NAME}-${TIMESTAMP}"
KEY_PATH="${OUTPUT_DIR}/${CERT_FILENAME}.key"
CRT_PATH="${OUTPUT_DIR}/${CERT_FILENAME}.crt"
PFX_PATH="${OUTPUT_DIR}/${CERT_FILENAME}.pfx"

echo "Generating self-signed certificate: $CERT_FILENAME"

# Check if OpenSSL is available
if ! command -v openssl &> /dev/null; then
    echo "Error: OpenSSL is not installed or not in PATH"
    exit 1
fi

# Generate private key
openssl genrsa -out "$KEY_PATH" 2048

# Generate certificate signing request and self-signed certificate
openssl req -new -x509 -key "$KEY_PATH" -out "$CRT_PATH" -days "$VALID_DAYS" \
    -subj "/CN=${CERT_NAME}/O=WCF Rolling Certs POC/C=US"

# Convert to PKCS#12 format (PFX)
openssl pkcs12 -export -out "$PFX_PATH" -inkey "$KEY_PATH" -in "$CRT_PATH" \
    -passout pass:"$PASSWORD"

# Get certificate information
THUMBPRINT=$(openssl x509 -in "$CRT_PATH" -fingerprint -noout | cut -d= -f2 | tr -d ':')
NOT_BEFORE=$(openssl x509 -in "$CRT_PATH" -startdate -noout | cut -d= -f2)
NOT_AFTER=$(openssl x509 -in "$CRT_PATH" -enddate -noout | cut -d= -f2)
SUBJECT=$(openssl x509 -in "$CRT_PATH" -subject -noout | cut -d= -f2-)

echo "Certificate generated successfully!"
echo "Key file: $KEY_PATH"
echo "Certificate file: $CRT_PATH"
echo "PFX file: $PFX_PATH"
echo "Password: $PASSWORD"
echo "Thumbprint: $THUMBPRINT"

# Create JSON file with certificate information
cat > "${OUTPUT_DIR}/${CERT_FILENAME}.json" << EOF
{
  "Name": "$CERT_FILENAME",
  "KeyPath": "$KEY_PATH",
  "CrtPath": "$CRT_PATH", 
  "PfxPath": "$PFX_PATH",
  "Password": "$PASSWORD",
  "Thumbprint": "$THUMBPRINT",
  "Subject": "$SUBJECT",
  "NotBefore": "$NOT_BEFORE",
  "NotAfter": "$NOT_AFTER",
  "Generated": "$(date)"
}
EOF

echo "Certificate info saved to: ${OUTPUT_DIR}/${CERT_FILENAME}.json"