{{/*
  config-watcher: Watches ConfigMaps by label and syncs data keys as files to a shared /config volume.
  Uses init container for first fetch + sidecar for live updates.

  Required values:
    .Values.configWatcher.label  - label selector (default: "config-type=integration-config")
*/}}

{{- define "config-watcher.label" -}}
{{- if and .Values.configWatcher .Values.configWatcher.label -}}
{{- .Values.configWatcher.label -}}
{{- else -}}
config-type=integration-config
{{- end -}}
{{- end -}}


{{/*
  Pod-level spec: serviceAccountName + initContainers.
  Include at spec level, before containers.
*/}}
{{- define "config-watcher.podSpec" -}}
serviceAccountName: config-watcher-sa-{{ .Chart.Name }}
initContainers:
  - name: config-init
    image: alpine/k8s:1.31.13
    command: ["/bin/sh", "-c"]
    args:
      - |
        {{- include "config-watcher.syncScript" . | nindent 8 }}
    volumeMounts:
      - name: config
        mountPath: /config
{{- end -}}


{{/*
  Sync script used by init container (one-shot fetch).
*/}}
{{- define "config-watcher.syncScript" -}}
#!/bin/sh
set -e

LABEL="{{ include "config-watcher.label" . }}"
CONFIG_DIR="/config"
TMP="/tmp/configmaps.json"

echo "[config-init] Fetching ConfigMaps with label=$LABEL"
kubectl get configmaps -l "$LABEL" -o json > "$TMP"

COUNT=$(jq '.items | length' "$TMP")
echo "[config-init] Found $COUNT ConfigMaps"

jq -r '.items[] | .data // {} | to_entries[] | (.key + "=" + (.value | @base64))' "$TMP" \
| while IFS='=' read -r key b64value; do
  echo "$b64value" | base64 -d > "$CONFIG_DIR/$key"
done

rm -f "$TMP"
echo "[config-init] Wrote config files to $CONFIG_DIR"
{{- end -}}
