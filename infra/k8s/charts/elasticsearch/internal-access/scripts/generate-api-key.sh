#!/bin/sh
set -e

# REQUIRED Env
# ES_HOST, ES_USER, ES_PASSWORD, SECRET_NAME, DAYS_LEFT, MAX_CREATE_API_KEY_RETRIES

# --------------------------------------------------------
# Main
# --------------------------------------------------------

echo "Trying to get existing secret"
EXISTING=$(kubectl get secret $SECRET_NAME -o jsonpath='{.data.api_key}' 2>/dev/null || echo "")
EXPIRATION=$(kubectl get secret $SECRET_NAME -o jsonpath='{.data.expiration}' 2>/dev/null | base64 -d || echo "")

echo "Checking result & expiration"

NOW=$(date +%s)
if [ -n "$EXISTING" ] && [ -n "$EXPIRATION" ]; then
  LEFT=$(( (EXPIRATION - NOW) / 86400 ))
  if [ "$LEFT" -gt "$DAYS_LEFT" ]; then
    echo "API key is still valid ($LEFT days left), skipping generation"
    exit 0
  fi
  echo "API key expires in $LEFT days, regenerating..."
fi

# Generate new api key with retry. It fails first time

MAX_RETRIES=$MAX_CREATE_API_KEY_RETRIES
COUNT=0
SUCCESS=0
SLEEP_TIME=20

echo "Starting retry loop for generating API key via Elasticsearch"

while [ $COUNT -lt $MAX_RETRIES ]; do
  RESP=$(curl -sk -u "$ES_USER:$ES_PASSWORD" -X POST "https://$ES_HOST/_security/api_key" \
    -H "Content-Type: application/json" \
    -d '{"name":"external-key","expiration":"365d","role_descriptors":{}}')

  echo "$RESP" | grep -q '"error"' && ERROR_FOUND=1 || ERROR_FOUND=0

  if [ $ERROR_FOUND -eq 0 ]; then
    SUCCESS=1
    break
  else
    COUNT=$((COUNT+1))
    echo "[*] Attempt $COUNT failed, retrying..."
    sleep $SLEEP_TIME
  fi
done

echo "$RESP" | grep -q '"encoded"' && API_KEY_PRESENT=1 || API_KEY_PRESENT=0

if [ $SUCCESS -eq 0 ] || [ $API_KEY_PRESENT -eq 0 ]; then
  echo "[!] Failed to generate API key after $MAX_RETRIES attempts."
  exit 1
fi

API_KEY=$(echo "$RESP" | sed -n 's/.*"encoded":"\([^"]*\)".*/\1/p')

EXPIRATION_MS=$(echo "$RESP" | sed -n 's/.*"expiration":\([0-9]*\).*/\1/p')

EXPIRATION_TS=$((EXPIRATION_MS / 1000))

# Create secret
kubectl create secret generic "$SECRET_NAME" \
  --from-literal=api_key="$API_KEY" \
  --from-literal=expiration="$EXPIRATION_TS" \
  --dry-run=client -o yaml \
| kubectl annotate -f - \
  kubed.appscode.com/sync="sync-${SECRET_NAME}=true" \
  --overwrite \
  --local -o yaml \
| kubectl apply -f -

echo "API Key generated and stored in secret $SECRET_NAME"

# echo "[*] Finished work, sleeping indefinitely..."
# while true; do
#   sleep 3600
# done