#!/bin/bash
# Gera certificado autoassinado para desenvolvimento
openssl req -x509 -newkey rsa:2048 -keyout certs/key.pem -out certs/cert.pem \
  -days 365 -nodes -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,DNS:gateway,DNS:users,DNS:products,DNS:orders"

# Converte para .pfx (formato que o Kestrel aceita)
openssl pkcs12 -export -out certs/devcert.pfx -inkey certs/key.pem -in certs/cert.pem \
  -passout pass:devpassword

echo "Certificado gerado em certs/devcert.pfx"
