#!/bin/sh
# REQUIRED ENV: ES_HOST, ES_USER, ES_PASSWORD, MAX_RETRIES
set -e

echo "Starting Elasticsearch schema loader..."
echo "Using Elasticsearch: $ES_HOST"
echo "Loading schemas..."

# echo "[*] sleeping indefinitely..."
# while true; do
#   sleep 3600
# done

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

        # check if index exists
        HTTP_EXISTS=$(curl -s -o /dev/null -w "%{http_code}" -k -u "$ES_USER:$ES_PASSWORD" -I "$ES_HOST/$INDEX")

        if [ "$HTTP_EXISTS" -eq 200 ]; then
            echo "Index exists → updating mapping"

            # extract only mapping + _source for update
            UPDATED_MAPPING="/tmp/${INDEX}.mapping.json"
            jq '{properties: .mappings.properties, _source: .mappings._source, _meta: .mappings._meta, dynamic: .mappings.dynamic}' "$UPDATED_INDEX" > "$UPDATED_MAPPING"

            RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
                -X PUT "$ES_HOST/$INDEX/_mapping" \
                -H "Content-Type: application/json" \
                --data-binary "@$UPDATED_MAPPING" \
                -w "\n%{http_code}"
            )
        else
            echo "Index does not exist → creating index"

            RESPONSE=$(curl -s -k -u "$ES_USER:$ES_PASSWORD" \
                -X PUT "$ES_HOST/$INDEX" \
                -H "Content-Type: application/json" \
                --data-binary "@$UPDATED_INDEX" \
                -w "\n%{http_code}"
            )
        fi

        HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
        BODY=$(echo "$RESPONSE" | sed '$d')

        echo "----- Elasticsearch response for index '$INDEX' -----"
        echo "$BODY"
        echo "HTTP code: $HTTP_CODE"
        echo "-----------------------------------------------------"

        if [ "$HTTP_CODE" -ge 200 ] && [ "$HTTP_CODE" -lt 300 ]; then
            echo "Index $INDEX processed successfully."
            SUCCESS=1
            break
        fi

        echo "Failed to process index $INDEX (HTTP $HTTP_CODE), retrying in 20s..."
        sleep 20
    done

    if [ $SUCCESS -ne 1 ]; then
        echo "ERROR: Could not process index $INDEX"
        exit 1
    fi

done

echo "All schemas processed."

# echo "[*] Finished work, sleeping indefinitely..."
# while true; do
#   sleep 3600
# done
