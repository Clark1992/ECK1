#!/bin/sh
# REQUIRED ENV: ES_HOST, ES_USER, ES_PASSWORD, MAX_RETRIES
#
# Strategy: versioned indices + alias
#   - Index name from the schema file (e.g. "sample-full-records") is always an alias
#   - Actual data lives in "<name>-v<N>" indices
#   - On mapping changes that ES rejects in-place, a new version is created,
#     docs are reindexed once, and the alias is atomically swapped.
set -e

echo "Starting Elasticsearch schema loader..."
echo "Using Elasticsearch: $ES_HOST"
echo "Loading schemas..."

# --- helper: resolve alias → backing index name (empty if alias doesn't exist) ---
get_backing_index() {
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        "$ES_HOST/_alias/$1" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    if [ "$HTTP_CODE" -eq 200 ]; then
        echo "$BODY" | jq -r 'keys[0]'
    fi
}

# --- helper: extract version number from "<base>-v<N>" (returns 0 for non-versioned) ---
extract_version() {
    case "$1" in
        "$2"-v[0-9]*)
            echo "$1" | sed "s/^$2-v//"
            ;;
        *)
            echo "0"
            ;;
    esac
}

# --- helper: reindex via alias swap (single reindex) ---
#   $1 = alias name, $2 = old backing index, $3 = schema file, $4 = new version
reindex_with_alias_swap() {
    ALIAS_NAME="$1"
    OLD_INDEX="$2"
    SCHEMA_FILE="$3"
    NEW_VERSION="$4"
    NEW_INDEX="${ALIAS_NAME}-v${NEW_VERSION}"

    echo "=== Alias reindex: $OLD_INDEX → $NEW_INDEX (alias: $ALIAS_NAME) ==="

    # clean up leftover from a previous failed run
    curl -s -k -u "$ES_USER:$ES_PASSWORD" -X DELETE "$ES_HOST/$NEW_INDEX" > /dev/null 2>&1 || true

    # 1 – create new versioned index with the updated mapping
    echo "[1/4] Creating $NEW_INDEX with new mapping..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X PUT "$ES_HOST/$NEW_INDEX" \
        -H "Content-Type: application/json" \
        --data-binary "@$SCHEMA_FILE" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Failed to create $NEW_INDEX (HTTP $HTTP_CODE)"
        return 1
    fi

    # 2 – reindex old → new
    echo "[2/4] Reindexing $OLD_INDEX → $NEW_INDEX ..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X POST "$ES_HOST/_reindex?wait_for_completion=true" \
        -H "Content-Type: application/json" \
        -d "{\"source\":{\"index\":\"$OLD_INDEX\"},\"dest\":{\"index\":\"$NEW_INDEX\"}}" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Reindex $OLD_INDEX → $NEW_INDEX failed (HTTP $HTTP_CODE)"
        curl -s -k -u "$ES_USER:$ES_PASSWORD" -X DELETE "$ES_HOST/$NEW_INDEX" > /dev/null 2>&1 || true
        return 1
    fi

    # 3 – atomically swap alias from old to new
    echo "[3/4] Swapping alias $ALIAS_NAME: $OLD_INDEX → $NEW_INDEX ..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X POST "$ES_HOST/_aliases" \
        -H "Content-Type: application/json" \
        -d "{\"actions\":[{\"remove\":{\"index\":\"$OLD_INDEX\",\"alias\":\"$ALIAS_NAME\"}},{\"add\":{\"index\":\"$NEW_INDEX\",\"alias\":\"$ALIAS_NAME\"}}]}" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Alias swap failed (HTTP $HTTP_CODE)"
        return 1
    fi

    # 4 – delete old index
    echo "[4/4] Deleting old index $OLD_INDEX ..."
    curl -s -k -u "$ES_USER:$ES_PASSWORD" -X DELETE "$ES_HOST/$OLD_INDEX" > /dev/null 2>&1 || true

    echo "=== Done: $ALIAS_NAME → $NEW_INDEX ==="
    return 0
}

# --- helper: one-time migration of a concrete index to versioned + alias ---
#   $1 = index name (currently concrete), $2 = schema file
migrate_concrete_to_alias() {
    ALIAS_NAME="$1"
    SCHEMA_FILE="$2"
    NEW_INDEX="${ALIAS_NAME}-v1"

    echo "=== Migrating concrete index $ALIAS_NAME → $NEW_INDEX + alias ==="

    curl -s -k -u "$ES_USER:$ES_PASSWORD" -X DELETE "$ES_HOST/$NEW_INDEX" > /dev/null 2>&1 || true

    # 1 – create versioned index
    echo "[1/4] Creating $NEW_INDEX ..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X PUT "$ES_HOST/$NEW_INDEX" \
        -H "Content-Type: application/json" \
        --data-binary "@$SCHEMA_FILE" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Failed to create $NEW_INDEX (HTTP $HTTP_CODE)"
        return 1
    fi

    # 2 – reindex concrete → versioned
    echo "[2/4] Reindexing $ALIAS_NAME → $NEW_INDEX ..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X POST "$ES_HOST/_reindex?wait_for_completion=true" \
        -H "Content-Type: application/json" \
        -d "{\"source\":{\"index\":\"$ALIAS_NAME\"},\"dest\":{\"index\":\"$NEW_INDEX\"}}" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Reindex failed (HTTP $HTTP_CODE)"
        curl -s -k -u "$ES_USER:$ES_PASSWORD" -X DELETE "$ES_HOST/$NEW_INDEX" > /dev/null 2>&1 || true
        return 1
    fi

    # 3 – delete the concrete index
    echo "[3/4] Deleting concrete index $ALIAS_NAME ..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X DELETE "$ES_HOST/$ALIAS_NAME" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Failed to delete concrete index (HTTP $HTTP_CODE)"
        return 1
    fi

    # 4 – create alias
    echo "[4/4] Creating alias $ALIAS_NAME → $NEW_INDEX ..."
    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
        -X POST "$ES_HOST/_aliases" \
        -H "Content-Type: application/json" \
        -d "{\"actions\":[{\"add\":{\"index\":\"$NEW_INDEX\",\"alias\":\"$ALIAS_NAME\"}}]}" \
        -w "\n%{http_code}")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
        echo "ERROR: Failed to create alias (HTTP $HTTP_CODE)"
        return 1
    fi

    echo "=== Migrated: $ALIAS_NAME → $NEW_INDEX ==="
    return 0
}

for FILE in /schemas/*.json; do
    # detect no files case
    if [ "$FILE" = "/schemas/*.json" ]; then
        echo "No JSON files found in /schemas"
        break
    fi

    if [ ! -f "$FILE" ]; then
        continue
    fi

    INDEX=$(basename "$FILE" .json)
    echo "Processing index: $INDEX (file: $FILE)"

    UPDATED_INDEX="/tmp/${INDEX}.updated.json"

    # generate updated schema with _source.includes
    sh /scripts/add-source-includes.sh "$FILE" "$UPDATED_INDEX"
    if [ $? -ne 0 ]; then
        echo "ERROR: Failed generating updated schema for $INDEX"
        exit 1
    fi

    ATTEMPTS=0
    SUCCESS=0

    while [ $ATTEMPTS -lt $MAX_RETRIES ]; do
        ATTEMPTS=$((ATTEMPTS+1))
        echo "Attempt $ATTEMPTS for index $INDEX"

        # -- Determine current state: alias, concrete index, or nothing --
        BACKING=$(get_backing_index "$INDEX")

        if [ -n "$BACKING" ]; then
            # INDEX is an alias → try in-place mapping update on the backing index
            echo "'$INDEX' is alias → backing index: $BACKING"
            CUR_VER=$(extract_version "$BACKING" "$INDEX")
            NEXT_VER=$((CUR_VER + 1))

            UPDATED_MAPPING="/tmp/${INDEX}.mapping.json"
            jq '{properties: .mappings.properties, _source: .mappings._source, _meta: .mappings._meta, dynamic: .mappings.dynamic}' "$UPDATED_INDEX" > "$UPDATED_MAPPING"

            RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
                -X PUT "$ES_HOST/$BACKING/_mapping" \
                -H "Content-Type: application/json" \
                --data-binary "@$UPDATED_MAPPING" \
                -w "\n%{http_code}")
            HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
            BODY=$(echo "$RESPONSE" | sed '$d')
            echo "$BODY (HTTP $HTTP_CODE)"

            if [ "$HTTP_CODE" -ge 200 ] && [ "$HTTP_CODE" -lt 300 ]; then
                echo "Mapping updated in-place on $BACKING."
                SUCCESS=1
                break
            fi

            echo "In-place update failed → reindex with alias swap..."
            if reindex_with_alias_swap "$INDEX" "$BACKING" "$UPDATED_INDEX" "$NEXT_VER"; then
                SUCCESS=1
                break
            fi

        else
            # Not an alias – check if it exists as a concrete index
            HTTP_EXISTS=$(curl -s -o /dev/null -w "%{http_code}" -k -u "$ES_USER:$ES_PASSWORD" -I "$ES_HOST/$INDEX")

            if [ "$HTTP_EXISTS" -eq 200 ]; then
                echo "'$INDEX' is a concrete index (legacy, no alias)"

                # try in-place mapping update first
                UPDATED_MAPPING="/tmp/${INDEX}.mapping.json"
                jq '{properties: .mappings.properties, _source: .mappings._source, _meta: .mappings._meta, dynamic: .mappings.dynamic}' "$UPDATED_INDEX" > "$UPDATED_MAPPING"

                RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
                    -X PUT "$ES_HOST/$INDEX/_mapping" \
                    -H "Content-Type: application/json" \
                    --data-binary "@$UPDATED_MAPPING" \
                    -w "\n%{http_code}")
                HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
                BODY=$(echo "$RESPONSE" | sed '$d')
                echo "$BODY (HTTP $HTTP_CODE)"

                if [ "$HTTP_CODE" -ge 200 ] && [ "$HTTP_CODE" -lt 300 ]; then
                    echo "Mapping updated on concrete index $INDEX."
                    SUCCESS=1
                    break
                fi

                echo "In-place update failed → migrating concrete index to alias scheme..."
                if migrate_concrete_to_alias "$INDEX" "$UPDATED_INDEX"; then
                    SUCCESS=1
                    break
                fi
            else
                # Index doesn't exist at all → create versioned index + alias
                echo "'$INDEX' does not exist → creating ${INDEX}-v1 + alias"

                NEW_INDEX="${INDEX}-v1"
                RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
                    -X PUT "$ES_HOST/$NEW_INDEX" \
                    -H "Content-Type: application/json" \
                    --data-binary "@$UPDATED_INDEX" \
                    -w "\n%{http_code}")
                HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
                BODY=$(echo "$RESPONSE" | sed '$d')
                echo "$BODY (HTTP $HTTP_CODE)"

                if [ "$HTTP_CODE" -ge 200 ] && [ "$HTTP_CODE" -lt 300 ]; then
                    # create alias
                    RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
                        -X POST "$ES_HOST/_aliases" \
                        -H "Content-Type: application/json" \
                        -d "{\"actions\":[{\"add\":{\"index\":\"$NEW_INDEX\",\"alias\":\"$INDEX\"}}]}" \
                        -w "\n%{http_code}")
                    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
                    if [ "$HTTP_CODE" -ge 200 ] && [ "$HTTP_CODE" -lt 300 ]; then
                        echo "Created: $INDEX (alias) → $NEW_INDEX"
                        SUCCESS=1
                        break
                    fi
                fi
            fi
        fi

        echo "Retrying in 20s..."
        sleep 20
    done

    if [ $SUCCESS -ne 1 ]; then
        echo "ERROR: Could not process index $INDEX after $MAX_RETRIES attempts"
        exit 1
    fi

done

echo "All schemas processed."
