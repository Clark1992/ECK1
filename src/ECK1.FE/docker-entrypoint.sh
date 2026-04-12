#!/bin/sh
set -e

# Require gateway host from environment
if [ -z "$GATEWAY_HOST" ]; then
  echo "ERROR: GATEWAY_HOST environment variable is required" >&2
  exit 1
fi

# Generate runtime config.js from environment variables
cat > /usr/share/nginx/html/config.js <<EOF
window.__CONFIG__ = {
  ZITADEL_AUTHORITY: "${ZITADEL_AUTHORITY:-}",
  ZITADEL_CLIENT_ID: "${ZITADEL_CLIENT_ID:-}",
};
EOF

# Substitute env vars in nginx config
envsubst '${GATEWAY_HOST}' < /etc/nginx/conf.d/default.conf.template > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
