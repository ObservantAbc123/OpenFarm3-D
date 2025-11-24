#!/bin/bash

# Development Certificate Generator for PaymentProcessingService
# Creates a proper SSL certificate for development that works with multiple hostnames

set -e

CERT_DIR="./cert"
CERT_NAME="aspnetapp"
CERT_PASSWORD="${ASPNETCORE_CERT_PASSWORD:-development123}"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

log() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Create certificate directory
mkdir -p "$CERT_DIR"

log "Generating development SSL certificate..."

# Create OpenSSL config file for multiple domains
cat > "$CERT_DIR/cert.conf" << EOF
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = dn
req_extensions = v3_req

[dn]
C=US
ST=Development
L=Development
O=OpenFarm
OU=PaymentService
CN=localhost

[v3_req]
subjectAltName = @alt_names

[alt_names]
DNS.1 = localhost
DNS.2 = payment-processing-service
DNS.3 = payment-service
DNS.4 = *.localhost
IP.1 = 127.0.0.1
IP.2 = 0.0.0.0
IP.3 = ::1
EOF

# Generate private key
log "Generating private key..."
openssl genrsa -out "$CERT_DIR/key.pem" 2048

# Generate certificate signing request
log "Generating certificate signing request..."
openssl req -new -key "$CERT_DIR/key.pem" -out "$CERT_DIR/cert.csr" -config "$CERT_DIR/cert.conf"

# Generate self-signed certificate
log "Generating self-signed certificate..."
openssl x509 -req -in "$CERT_DIR/cert.csr" -signkey "$CERT_DIR/key.pem" \
    -out "$CERT_DIR/cert.pem" -days 365 \
    -extensions v3_req -extfile "$CERT_DIR/cert.conf"

# Convert to PKCS12 format for .NET
log "Converting to PKCS12 format..."
openssl pkcs12 -export -out "$CERT_DIR/$CERT_NAME.pfx" \
    -inkey "$CERT_DIR/key.pem" -in "$CERT_DIR/cert.pem" \
    -password "pass:$CERT_PASSWORD"

# Create certificate bundle for curl
log "Creating certificate bundle..."
cat "$CERT_DIR/cert.pem" > "$CERT_DIR/ca-bundle.pem"

# Set appropriate permissions
chmod 600 "$CERT_DIR"/*.pem "$CERT_DIR"/*.pfx "$CERT_DIR"/*.csr 2>/dev/null || true
chmod 644 "$CERT_DIR/ca-bundle.pem"

# Clean up temporary files
rm -f "$CERT_DIR/cert.csr" "$CERT_DIR/cert.conf"

log "Certificate generated successfully!"
echo ""
echo "Files created:"
echo "  - $CERT_DIR/cert.pem (Certificate)"
echo "  - $CERT_DIR/key.pem (Private Key)"
echo "  - $CERT_DIR/$CERT_NAME.pfx (PKCS12 for .NET)"
echo "  - $CERT_DIR/ca-bundle.pem (For curl --cacert)"
echo ""

# Display certificate info
log "Certificate information:"
openssl x509 -in "$CERT_DIR/cert.pem" -text -noout | grep -E "(Subject:|DNS:|IP Address:|Not After)"

echo ""
log "Usage options:"
echo ""
echo "1. Use with curl (no --insecure needed):"
echo "   curl --cacert $CERT_DIR/ca-bundle.pem https://localhost:5002/health"
echo ""
echo "2. Trust the certificate (Linux/Mac):"
echo "   # Copy to system trust store (requires sudo)"
echo "   sudo cp $CERT_DIR/cert.pem /usr/local/share/ca-certificates/openfarm-dev.crt"
echo "   sudo update-ca-certificates"
echo ""
echo "3. Docker environment variable:"
echo "   ASPNETCORE_Kestrel__Certificates__Default__Path=/app/cert/$CERT_NAME.pfx"
echo "   ASPNETCORE_Kestrel__Certificates__Default__Password=$CERT_PASSWORD"
echo ""

# Create helper scripts
cat > "$CERT_DIR/trust-cert.sh" << 'EOF'
#!/bin/bash
# Trust the development certificate system-wide

if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    echo "Adding certificate to macOS keychain..."
    sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain cert.pem
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    # Linux
    echo "Adding certificate to Linux trust store..."
    sudo cp cert.pem /usr/local/share/ca-certificates/openfarm-dev.crt
    sudo update-ca-certificates
else
    echo "Manual certificate trust required for this OS"
    echo "Import cert.pem into your system's certificate trust store"
fi

echo "Certificate trusted! You can now use curl without --insecure"
EOF

cat > "$CERT_DIR/test-cert.sh" << 'EOF'
#!/bin/bash
# Test certificate with different methods

PAYMENT_API_KEY="${PAYMENT_API_KEY:-test-key}"
BASE_URL="https://localhost:5002"

echo "Testing certificate with different methods..."
echo ""

echo "1. Using --insecure (should work):"
if curl -s --insecure "$BASE_URL/health" > /dev/null; then
    echo "   ✓ Success with --insecure"
else
    echo "   ✗ Failed with --insecure"
fi

echo ""
echo "2. Using --cacert (should work with our certificate):"
if curl -s --cacert ca-bundle.pem "$BASE_URL/health" > /dev/null; then
    echo "   ✓ Success with --cacert"
else
    echo "   ✗ Failed with --cacert"
fi

echo ""
echo "3. Using system trust store (may fail if not trusted):"
if curl -s "$BASE_URL/health" > /dev/null; then
    echo "   ✓ Success with system trust store"
else
    echo "   ✗ Failed with system trust store (run trust-cert.sh to fix)"
fi

echo ""
echo "Certificate test complete!"
EOF

chmod +x "$CERT_DIR/trust-cert.sh" "$CERT_DIR/test-cert.sh"

echo ""
warn "Next steps:"
echo "1. Run './cert/trust-cert.sh' to trust the certificate system-wide"
echo "2. Run './cert/test-cert.sh' to test different connection methods"
echo "3. Rebuild Docker image with: docker-compose build payment-processing-service"
echo ""

log "Development certificate setup complete!"
