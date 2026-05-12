#!/bin/bash
set -e

echo "Creating S3 bucket..."
awslocal s3 mb s3://talaria-statements 2>/dev/null || echo "Bucket already exists"

echo "Downloading sample PDF..."
curl -L -o /tmp/sample.pdf "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf" 2>/dev/null || {
    echo "Download failed, creating placeholder file"
    echo "%PDF-1.4 test" > /tmp/sample.pdf
}

echo "Uploading sample PDFs to S3..."
for i in $(seq 1 10); do
    awslocal s3 cp /tmp/sample.pdf s3://talaria-statements/statements/statement-$i.pdf
done

echo "Creating KMS key..."
KMS_KEY=$(awslocal kms create-key --description "Talaria test key" --query 'KeyMetadata.KeyId' --output text 2>/dev/null) || {
    KMS_KEY="test-key-id"
    echo "KMS key creation skipped or failed, using placeholder"
}
echo "KMS Key: $KMS_KEY"

echo "Creating Secrets Manager secret for CloudFront..."
awslocal secretsmanager create-secret \
    --name cloudfront-signing-key \
    --secret-string '{"PrivateKey":"-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA0Z9VnjXc3YXxkYzNL5l8Y2F3m8P7N4vX6L9K2h5T8R1s3D4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z6a7B8c9D0e1F2g3H4i5J6k7L8m9N0o1P2q3R4s5T6u7V8w9X0y1Z2a3B4c5D6e7F8g9H0i1J2k3L4m5N6o7P8q9R0s1T2u3V4w5X6y7Z8a9B0c1D2e3F4g5H6i7J8k9L0m1N2o3P4q5R6s7T8u9V0w1X2y3Z4a5B6c7D8e9F0g1H2i3J4k5L6m7N8o9P0q1R2s3T4u5V6w7X8y9Z0a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z6a7B8c9D0e1F2g3H4i5J6k7L8m9N0o1P2q3R4s5T6u7V8w9X0y1Z2a3B4c5D6e7F8g9H0i1J2k3L4m5N6o7P8q9R0s1T2u3V4w5X6y7Z8a9B0c1D2e3F4g5H6i7J8k9L0m1N2o3P4q5R6s7T8u9V0w1X2y3Z4a5B6c7D8e9F0g1H2i3J4k5L6m7N8o9P0q1R2s3T4u5V6w7X8y9Z0a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z6a7B8c9D0e1F2g3H4i5J6k7L8m9N0o1P2q3R4s5T6u7V8w9X0y1Z2a3B4c5D6e7F8g9H0i1J2k3L4m5N6o7P8q9R0s1T2u3V4w5X6y7Z8a9B0c1D2e3F4g5H6i7J8k9L0m1N2o3P4q5R6s7T8u9V0w1X2y3Z4a5B6c7D8e9F0g1H2i3J4k5L6m7N8o9P0q1R2s3T4u5V6w7X8y9Z0a1B2c3D4e5F6g7H8i9J0k1L2m3N4o5P6q7R8s9T0u1V2w3X4y5Z6a7\n-----END RSA PRIVATE KEY-----"}' \
    2>/dev/null || echo "Secret already exists, skipping..."

echo "LocalStack initialization complete"