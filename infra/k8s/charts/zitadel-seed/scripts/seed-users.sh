#!/bin/sh
set -e

# REQUIRED Env: ZITADEL_HOST, ZITADEL_EXTERNAL_DOMAIN, ZITADEL_PAT

ZITADEL_URL="http://${ZITADEL_HOST}"
HOST_HEADER="Host: ${ZITADEL_EXTERNAL_DOMAIN}"

# --------------------------------------------------------
# Wait for Zitadel to be ready
# --------------------------------------------------------

echo "[*] Waiting for Zitadel to be ready at ${ZITADEL_URL}..."

MAX_RETRIES=60
COUNT=0
while [ $COUNT -lt $MAX_RETRIES ]; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" -H "$HOST_HEADER" "${ZITADEL_URL}/debug/healthz" 2>/dev/null || echo "000")
  if [ "$STATUS" = "200" ]; then
    echo "[*] Zitadel is ready"
    break
  fi
  COUNT=$((COUNT + 1))
  echo "[*] Attempt $COUNT/$MAX_RETRIES — status: $STATUS"
  sleep 5
done

if [ $COUNT -ge $MAX_RETRIES ]; then
  echo "[!] Zitadel not ready after $MAX_RETRIES attempts"
  exit 1
fi

# --------------------------------------------------------
# PAT from env var (mounted from K8s secret by the pod spec)
# Strip trailing whitespace/newlines that K8s may include
# --------------------------------------------------------

PAT=$(printf '%s' "${ZITADEL_PAT}" | tr -d '\n\r ')

if [ -z "$PAT" ]; then
  echo "[!] ZITADEL_PAT env var is empty"
  exit 1
fi

echo "[*] PAT obtained from env var (length: $(printf '%s' "$PAT" | wc -c))"

AUTH_HEADER="Authorization: Bearer ${PAT}"

# --------------------------------------------------------
# Get default org ID
# --------------------------------------------------------

echo "[*] Getting default organization..."
ORG_RESP=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" -H "$HOST_HEADER" \
  "${ZITADEL_URL}/admin/v1/orgs/default")

ORG_ID=$(echo "$ORG_RESP" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

if [ -z "$ORG_ID" ]; then
  echo "[!] Could not get default org. Response: $ORG_RESP"
  ORG_LIST=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/admin/v1/orgs/_search" -d '{}')
  ORG_ID=$(echo "$ORG_LIST" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
fi

if [ -z "$ORG_ID" ]; then
  echo "[!] Could not determine org ID"
  exit 1
fi

echo "[*] Organization ID: ${ORG_ID}"

# --------------------------------------------------------
# Find or create project
# --------------------------------------------------------

echo "[*] Looking for existing project ECK1..."
PROJECTS_RESP=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" \
  -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
  -X POST "${ZITADEL_URL}/management/v1/projects/_search" \
  -d '{"queries":[{"nameQuery":{"name":"ECK1","method":"TEXT_QUERY_METHOD_EQUALS"}}]}')

PROJECT_ID=$(echo "$PROJECTS_RESP" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

if [ -n "$PROJECT_ID" ]; then
  echo "[*] Project ECK1 already exists: ${PROJECT_ID}"
else
  echo "[*] Creating project ECK1..."
  PROJECT_RESP=$(curl -s -w "\n%{http_code}" -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "x-zitadel-orgid: ${ORG_ID}" \
    -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/management/v1/projects" \
    -d '{"name":"ECK1","projectRoleAssertion":true}')

  PROJECT_STATUS=$(echo "$PROJECT_RESP" | tail -1)
  PROJECT_BODY=$(echo "$PROJECT_RESP" | sed '$d')
  PROJECT_ID=$(echo "$PROJECT_BODY" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  echo "[*] Project created: ${PROJECT_ID} (status: ${PROJECT_STATUS})"
fi

if [ -z "$PROJECT_ID" ]; then
  echo "[!] Failed to get or create project"
  exit 1
fi

# --------------------------------------------------------
# Find or create OIDC application
# --------------------------------------------------------

echo "[*] Looking for existing OIDC app..."
APPS_RESP=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" \
  -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
  -X POST "${ZITADEL_URL}/management/v1/projects/${PROJECT_ID}/apps/_search" -d '{}')

CLIENT_ID=$(echo "$APPS_RESP" | sed -n 's/.*"clientId"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

if [ -n "$CLIENT_ID" ]; then
  echo "[*] OIDC app already exists — clientId: ${CLIENT_ID}"
else
  echo "[*] Creating OIDC application..."
  APP_RESP=$(curl -s -w "\n%{http_code}" -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "x-zitadel-orgid: ${ORG_ID}" \
    -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/management/v1/projects/${PROJECT_ID}/apps/oidc" \
    -d '{
      "name": "ECK1 Platform",
      "redirectUris": ["http://localhost:30090/signin-oidc", "http://localhost:30090/swagger/oauth2-redirect.html", "http://localhost:30200/ui/console/auth/callback"],
      "responseTypes": ["OIDC_RESPONSE_TYPE_CODE"],
      "grantTypes": ["OIDC_GRANT_TYPE_AUTHORIZATION_CODE", "OIDC_GRANT_TYPE_REFRESH_TOKEN", "OIDC_GRANT_TYPE_TOKEN_EXCHANGE"],
      "appType": "OIDC_APP_TYPE_WEB",
      "authMethodType": "OIDC_AUTH_METHOD_TYPE_NONE",
      "postLogoutRedirectUris": ["http://localhost:30090"],
      "devMode": true,
      "accessTokenType": "OIDC_TOKEN_TYPE_JWT",
      "idTokenRoleAssertion": true,
      "accessTokenRoleAssertion": true
    }')

  APP_STATUS=$(echo "$APP_RESP" | tail -1)
  APP_BODY=$(echo "$APP_RESP" | sed '$d')
  CLIENT_ID=$(echo "$APP_BODY" | sed -n 's/.*"clientId"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  echo "[*] OIDC App created — clientId: ${CLIENT_ID} (status: ${APP_STATUS})"
fi

# --------------------------------------------------------
# Find or create human user: user / User1234!
# --------------------------------------------------------

echo "[*] Looking for existing user 'user'..."
USERS_RESP=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" \
  -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
  -X POST "${ZITADEL_URL}/management/v1/users/_search" \
  -d '{"queries":[{"userNameQuery":{"userName":"user","method":"TEXT_QUERY_METHOD_EQUALS"}}]}')

USER_ID=$(echo "$USERS_RESP" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

if [ -n "$USER_ID" ]; then
  echo "[*] User 'user' already exists: ${USER_ID}"
else
  echo "[*] Creating user 'user'..."
  USER_RESP=$(curl -s -w "\n%{http_code}" -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "x-zitadel-orgid: ${ORG_ID}" \
    -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/management/v1/users/human/_import" \
    -d '{
      "userName": "user",
      "profile": {
        "firstName": "Regular",
        "lastName": "User"
      },
      "email": {
        "email": "user@zitadel.localhost",
        "isEmailVerified": true
      },
      "password": "User1234!",
      "passwordChangeRequired": false
    }')

  USER_STATUS=$(echo "$USER_RESP" | tail -1)
  USER_BODY=$(echo "$USER_RESP" | sed '$d')
  USER_ID=$(echo "$USER_BODY" | sed -n 's/.*"userId"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  echo "[*] User created — id: ${USER_ID} (status: ${USER_STATUS})"
fi

# --------------------------------------------------------
# Find or create human user: admin / Admin123!
# --------------------------------------------------------

echo "[*] Looking for existing user 'admin'..."
ADMIN_RESP=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" \
  -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
  -X POST "${ZITADEL_URL}/management/v1/users/_search" \
  -d '{"queries":[{"userNameQuery":{"userName":"admin","method":"TEXT_QUERY_METHOD_EQUALS"}}]}')

ADMIN_USER_ID=$(echo "$ADMIN_RESP" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

if [ -n "$ADMIN_USER_ID" ]; then
  echo "[*] User 'admin' already exists: ${ADMIN_USER_ID}"
else
  echo "[*] Creating user 'admin'..."
  ADMIN_CREATE_RESP=$(curl -s -w "\n%{http_code}" -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "x-zitadel-orgid: ${ORG_ID}" \
    -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/management/v1/users/human/_import" \
    -d '{
      "userName": "admin",
      "profile": {
        "firstName": "Admin",
        "lastName": "User"
      },
      "email": {
        "email": "admin@eck1.zitadel.localhost",
        "isEmailVerified": true
      },
      "password": "Admin123!",
      "passwordChangeRequired": false
    }')

  ADMIN_CREATE_STATUS=$(echo "$ADMIN_CREATE_RESP" | tail -1)
  ADMIN_CREATE_BODY=$(echo "$ADMIN_CREATE_RESP" | sed '$d')
  ADMIN_USER_ID=$(echo "$ADMIN_CREATE_BODY" | sed -n 's/.*"userId"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  echo "[*] Admin user created — id: ${ADMIN_USER_ID} (status: ${ADMIN_CREATE_STATUS})"
fi

# --------------------------------------------------------
# Create project roles
# --------------------------------------------------------

echo "[*] Ensuring project roles exist..."
for ROLE_KEY in admin user; do
  ROLE_RESP=$(curl -s -w "\n%{http_code}" -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/management/v1/projects/${PROJECT_ID}/roles" \
    -d "{\"roleKey\":\"${ROLE_KEY}\",\"displayName\":\"${ROLE_KEY}\"}")
  ROLE_STATUS=$(echo "$ROLE_RESP" | tail -1)
  if [ "$ROLE_STATUS" = "200" ]; then
    echo "[*] Role '${ROLE_KEY}' created"
  else
    echo "[*] Role '${ROLE_KEY}' exists or skipped (status: ${ROLE_STATUS})"
  fi
done

# --------------------------------------------------------
# Grant roles to users (idempotent: create or update)
# --------------------------------------------------------

ensure_user_grant() {
  local user_id=$1
  local user_name=$2
  local roles_json=$3

  echo "[*] Ensuring project roles for '${user_name}'..."

  GRANT_RESP=$(curl -s -w "\n%{http_code}" -H "$AUTH_HEADER" \
    -H "Content-Type: application/json" \
    -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
    -X POST "${ZITADEL_URL}/management/v1/users/${user_id}/grants" \
    -d "{\"projectId\":\"${PROJECT_ID}\",\"roleKeys\":${roles_json}}")

  GRANT_STATUS=$(echo "$GRANT_RESP" | tail -1)

  if [ "$GRANT_STATUS" = "200" ]; then
    echo "[*] Granted roles ${roles_json} to '${user_name}'"
  else
    # Grant may already exist — find and update it
    GRANTS_SEARCH=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" \
      -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
      -X POST "${ZITADEL_URL}/management/v1/users/${user_id}/grants/_search" -d '{}')

    GRANT_ID=$(echo "$GRANTS_SEARCH" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

    if [ -n "$GRANT_ID" ]; then
      curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" \
        -H "x-zitadel-orgid: ${ORG_ID}" -H "$HOST_HEADER" \
        -X PUT "${ZITADEL_URL}/management/v1/users/${user_id}/grants/${GRANT_ID}" \
        -d "{\"roleKeys\":${roles_json}}" > /dev/null
      echo "[*] Updated roles for '${user_name}'"
    else
      echo "[!] Could not create or update grant for '${user_name}' (status: ${GRANT_STATUS})"
    fi
  fi
}

if [ -n "$ADMIN_USER_ID" ]; then
  ensure_user_grant "$ADMIN_USER_ID" "admin" '["admin","user"]'
fi

if [ -n "$USER_ID" ]; then
  ensure_user_grant "$USER_ID" "user" '["user"]'
fi

# --------------------------------------------------------
# Grant SA the IAM_LOGIN_CLIENT role (needed for Sessions API)
# --------------------------------------------------------

echo "[*] Ensuring SA has IAM_LOGIN_CLIENT role..."
SA_SEARCH=$(curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" -H "$HOST_HEADER" \
  -X POST "${ZITADEL_URL}/management/v1/users/_search" \
  -d '{"queries":[{"userNameQuery":{"userName":"zitadel-admin-sa","method":"TEXT_QUERY_METHOD_EQUALS"}}]}')

SA_USER_ID=$(echo "$SA_SEARCH" | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)

if [ -n "$SA_USER_ID" ]; then
  curl -s -H "$AUTH_HEADER" -H "Content-Type: application/json" -H "$HOST_HEADER" \
    -X PUT "${ZITADEL_URL}/admin/v1/members/${SA_USER_ID}" \
    -d '{"roles":["IAM_OWNER","IAM_LOGIN_CLIENT"]}' > /dev/null
  echo "[*] SA IAM roles granted (IAM_OWNER, IAM_LOGIN_CLIENT)"
else
  echo "[!] Could not find SA user 'zitadel-admin-sa'"
fi

# --------------------------------------------------------
# Store OIDC client ID in a K8s secret
# --------------------------------------------------------

if [ -n "$CLIENT_ID" ]; then
  echo "[*] Storing OIDC client ID in secret 'zitadel-oidc-client'..."

  SA_TOKEN=$(cat /var/run/secrets/kubernetes.io/serviceaccount/token)
  K8S_API="https://kubernetes.default.svc"
  NAMESPACE=$(cat /var/run/secrets/kubernetes.io/serviceaccount/namespace)
  CA_CERT="/var/run/secrets/kubernetes.io/serviceaccount/ca.crt"

  CLIENT_ID_B64=$(printf '%s' "${CLIENT_ID}" | base64 | tr -d '\n')

  # Try create first, if exists use PATCH (which doesn't need resourceVersion)
  CREATE_RESP=$(curl -s -w "\n%{http_code}" --cacert "$CA_CERT" \
    -H "Authorization: Bearer ${SA_TOKEN}" \
    -H "Content-Type: application/json" \
    -X POST "${K8S_API}/api/v1/namespaces/${NAMESPACE}/secrets" \
    -d "{
      \"apiVersion\": \"v1\",
      \"kind\": \"Secret\",
      \"metadata\": {
        \"name\": \"zitadel-oidc-client\",
        \"annotations\": {
          \"kubed.appscode.com/sync\": \"sync-zitadel-oidc-client=true\"
        }
      },
      \"type\": \"Opaque\",
      \"data\": {\"client_id\": \"${CLIENT_ID_B64}\"}
    }")

  CREATE_STATUS=$(echo "$CREATE_RESP" | tail -1)

  if [ "$CREATE_STATUS" = "409" ]; then
    echo "[*] Secret exists, patching..."
    curl -s --cacert "$CA_CERT" \
      -H "Authorization: Bearer ${SA_TOKEN}" \
      -H "Content-Type: application/merge-patch+json" \
      -X PATCH "${K8S_API}/api/v1/namespaces/${NAMESPACE}/secrets/zitadel-oidc-client" \
      -d "{\"metadata\":{\"annotations\":{\"kubed.appscode.com/sync\":\"sync-zitadel-oidc-client=true\"}},\"data\":{\"client_id\":\"${CLIENT_ID_B64}\"}}" > /dev/null
  fi

  echo "[*] OIDC client secret stored (kubed will sync to labeled namespaces)"
fi

echo ""
echo "============================================"
echo "  Zitadel seed completed!"
echo "  Admin Console: http://zitadel.localhost:30200"
echo "  Users (login / password):"
echo "    admin@eck1.zitadel.localhost / Admin123!"
echo "    user  / User1234!"
echo "  OIDC Client ID: ${CLIENT_ID}"
echo "============================================"
