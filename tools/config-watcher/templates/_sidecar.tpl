{{/*
  Sidecar container that watches for ConfigMap changes.
  Include inside the containers list.
*/}}
{{- define "config-watcher.sidecar" -}}
- name: config-watcher
  image: alpine/k8s:1.31.13
  command: ["/bin/sh", "-c"]
  args:
    - |
      LABEL="{{ include "config-watcher.label" . }}"
      CONFIG_DIR="/config"

      config_hash() {
        find "$CONFIG_DIR" -type f ! -name '.*' -exec md5sum {} + 2>/dev/null | sort | md5sum | cut -d' ' -f1
      }

      sync_configs() {
        TMP="/tmp/configmaps.json"
        kubectl get configmaps -l "$LABEL" -o json > "$TMP"
        COUNT=$(jq '.items | length' "$TMP")
        echo "[config-watcher] Found $COUNT ConfigMaps"

        rm -f "$CONFIG_DIR"/*

        jq -r '.items[] | .data // {} | to_entries[] | .key, (.value | @base64)' "$TMP" \
        | while read -r key; do
          read -r b64value
          echo "$b64value" | base64 -d > "$CONFIG_DIR/$key"
        done

        rm -f "$TMP"
        echo "[config-watcher] Synced config files to $CONFIG_DIR"
      }

      echo "[config-watcher] Starting watch loop for label=$LABEL"
      while true; do
        kubectl get configmaps -l "$LABEL" --watch-only -o name 2>/dev/null | head -n 1 > /dev/null
        echo "[config-watcher] Change detected, re-syncing..."
        sleep 1
        HASH_BEFORE=$(config_hash)
        sync_configs
        HASH_AFTER=$(config_hash)
        if [ "$HASH_BEFORE" != "$HASH_AFTER" ]; then
          echo "[config-watcher] Config content changed, restarting deployment/$DEPLOYMENT_NAME..."
          kubectl rollout restart deployment/"$DEPLOYMENT_NAME" -n "$POD_NAMESPACE"
        else
          echo "[config-watcher] Config content unchanged, skipping restart."
        fi
      done
  env:
    - name: DEPLOYMENT_NAME
      valueFrom:
        fieldRef:
          fieldPath: metadata.labels['app']
    - name: POD_NAMESPACE
      valueFrom:
        fieldRef:
          fieldPath: metadata.namespace
  volumeMounts:
    - name: config
      mountPath: /config
  resources:
    requests:
      cpu: 10m
      memory: 32Mi
    limits:
      cpu: 50m
      memory: 64Mi
{{- end -}}
