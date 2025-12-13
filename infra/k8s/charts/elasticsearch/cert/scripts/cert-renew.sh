set -e

# Required ENV: NS, CERT_RENEW_DAYS, CA_SECRET_NAME

# --------------------------------------------------------
# STEP 0: Download deps if missing
# --------------------------------------------------------

# KUBECTL
if ! command -v kubectl &> /dev/null; then
    echo "[*] kubectl not found, downloading..."
    mkdir -p /tmp/bin

    delay=2
    for i in {1..5}; do
    if curl -Lo /tmp/bin/kubectl "https://dl.k8s.io/release/v1.31.0/bin/linux/amd64/kubectl"; then
        break
    fi
    echo "Attempt $i failed, retrying in ${delay}s..."
    sleep $delay
    delay=$((delay * 2))
    done
    
    chmod +x /tmp/bin/kubectl
    export PATH=/tmp/bin:$PATH
    echo "[*] kubectl downloaded to /tmp/bin and added to PATH"
fi

# --------------------------------------------------------
# Helper: generate a random password
# --------------------------------------------------------
gen_pass() {
    head /dev/urandom | tr -dc A-Za-z0-9 | head -c 24
}

# ---------------------------------------------
# check if all required secrets exist
# returns 0 (true) if all exist, 1 (false) otherwise
# ---------------------------------------------
check_exists() {
    local secrets=("es-ca" "es-http" "es-transport")
    for s in "${secrets[@]}"; do
        if ! kubectl get secret "$s" >/dev/null 2>&1; then
        return 1
        fi
    done
    return 0
}

# ---------------------------------------------
# check if any certificate is expired or close to expiration
# returns 0 (true) if any needs renewal, 1 (false) otherwise
# ---------------------------------------------
check_expired_or_close() {
    local N="$CERT_RENEW_DAYS"
    local expired=false

    # check each secret
    declare -A files=( ["es-http"]="http.p12" ["es-transport"]="transport.p12" )

    for secret in "${!files[@]}"; do
        file=${files[$secret]}

        json_key="${file//./\\.}"

        raw=$(kubectl get secret "$secret" -o jsonpath="{.data.$json_key}")

        kubectl get secret "$secret" -o jsonpath="{.data.$json_key}" \
        | base64 -d > current.p12

        kubectl get secret "$secret" -o jsonpath="{.data.password}" \
        | base64 -d > pass.txt

        OUT=$(/usr/share/elasticsearch/jdk/bin/keytool -list -v \
            -storetype PKCS12 \
            -keystore current.p12 \
            -storepass "$(cat pass.txt)" 2>/dev/null)

        LINE=$(echo "$OUT" | grep -m1 "^Valid from:")

        VALID_FROM=$(echo "$LINE" | sed -E 's/^Valid from: (.*) until: .*/\1/')
        VALID_UNTIL=$(echo "$LINE" | sed -E 's/^Valid from: .* until: (.*)/\1/')

        echo "VALID_FROM= $VALID_FROM"
        echo "VALID_UNTIL= $VALID_UNTIL"

        now_ts=$(date +%s)
        from_ts=$(date -d "$VALID_FROM" +%s)
        until_ts=$(date -d "$VALID_UNTIL" +%s)

        if (( now_ts < from_ts )); then
            echo "[!] $secret certificate is not yet valid (starts at $VALID_FROM)"
            expired=true
        fi

        days_left=$(( (until_ts - now_ts) / 86400 ))

        echo "[!] $secret certificate expires in $days_left days (ends at $VALID_UNTIL)"
        if (( days_left < N )); then
            expired=true
        fi

        rm -f current.p12 pass.txt /tmp/discard || true
    done

    $expired && return 0 || return 1
}

# ---------------------------------------------
# universal create_secret function
# $1 = command: ca or cert
# $2 = secret name
# $3 = filename
# $4 = CA password (for cert)
# ---------------------------------------------
create_secret() {
    local cmd="$1"
    local secret="$2"
    local file="$3"
    local ca_pass="$4"

    local pass

    if [[ "$cmd" == "ca" ]]; then
        #
        # --- CREATE CA IN PEM FORMAT ---
        #
        pass="$ca_pass"

        echo "[INFO] Generating CA (PEM archive): ca.zip"
        elasticsearch-certutil ca \
            --pem \
            --silent \
            --out ca.zip \
            --pass "$pass"

        echo "[INFO] Unpacking CA archive"
        rm -rf ca_unpacked
        mkdir -p ca_unpacked
        unzip -o ca.zip -d ca_unpacked >/dev/null

        #
        # Find ca.crt (Elastic version-dependent)
        #
        local ca_crt
        ca_crt=$(find ca_unpacked -type f -name "ca.crt" | head -n1)

        if [[ -z "$ca_crt" ]]; then
        echo "[ERROR] ca.crt not found inside the CA archive!"
        find ca_unpacked -type f
        return 1
        fi

        echo "[INFO] Found CA certificate at: $ca_crt"

        #
        # --- Create Kubernetes secret with CA certificate ---
        #
        echo "[INFO] Creating Kubernetes secret: $secret"
        kubectl create secret generic "$secret" \
            --from-file=$file="$ca_crt" \
            --from-literal=password="$pass" \
            --dry-run=client -o yaml \
        | kubectl annotate -f - \
            kubed.appscode.com/sync="sync-${secret}=true" \
            --overwrite \
            --local -o yaml \
        | kubectl apply -f -

    elif [[ "$cmd" == "cert" ]]; then
        #
        # --- CREATE NODE CERT ---
        #
        pass=$(gen_pass)

        local ca_crt ca_key
        ca_crt=$(find ca_unpacked -type f -name "ca.crt" | head -n1)
        ca_key=$(find ca_unpacked -type f -name "ca.key" | head -n1)

        if [[ -z "$ca_crt" || -z "$ca_key" ]]; then
            echo "[ERROR] CA not found. Run create_secret ca first."
            return 1
        fi
        
        echo "[INFO] Generating node certificate: $file"
        elasticsearch-certutil cert \
            --silent \
            --ca-cert "$ca_crt" \
            --ca-key "$ca_key" \
            --ca-pass "$ca_pass" \
            --out "$file" \
            --pass "$pass"

        #
        # --- Kubernetes secret ---
        #
        echo "[INFO] Creating Kubernetes secret: $secret"
        kubectl create secret generic "$secret" \
            --from-file="$file" \
            --from-literal=password="$pass" \
            --dry-run=client -o yaml | kubectl apply -f -

    else
        echo "[ERROR] Unknown command: $cmd"
        return 1
    fi
}


# ---------------------------------------------
# Main
# ---------------------------------------------
if ! check_exists || check_expired_or_close; then
    echo "[*] Generating new CA and certificates..."
    CA_PASS=$(gen_pass)

    # generate CA
    create_secret ca "$CA_SECRET_NAME" ca.crt "$CA_PASS"

    # generate dependent certs
    create_secret cert es-http http.p12 "$CA_PASS"
    create_secret cert es-transport transport.p12 "$CA_PASS"

    # restart statefulsets
    echo "[*] Restarting Elasticsearch statefulsets to apply new certificates..."
    kubectl rollout restart statefulset -l app=elasticsearch -n ${NS}
else
    echo "[*] All secrets exist and certificates are valid."
fi

# echo "[*] Finished work, sleeping indefinitely..."
# while true; do
#   sleep 3600
# done