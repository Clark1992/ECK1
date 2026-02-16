{{- define "config-merge.initConfigAggregator" -}}
serviceAccountName: config-aggregator-sa-{{ .Chart.Name }}
initContainers:
  - name: config-merge
    image: alpine/k8s:1.31.13
    command: ["/bin/sh", "-c"]
    args:
      - |
        {{- include "config-merge.mergeScript" . | nindent 8 }}
    volumeMounts:
      - name: config
        mountPath: /config

{{- end -}}

{{- define "config-merge.initConfigVolumes" -}}

- name: config
  emptyDir: {}

{{- end -}}

{{- define "config-merge.initConfigVolumeMounts" -}}

- name: config
  mountPath: /config

{{- end -}}


{{- define "config-merge.mergeScript" -}}
#!/bin/sh
set -e

OUTPUT="/config/merged.json"
TMP="/tmp/configmaps.json"

echo "[config-merge] Collecting ConfigMaps..."
kubectl get configmaps -l config-type=failure-handling-config -o json > "$TMP"

COUNT=$(jq '.items | length' "$TMP")
echo "[config-merge] Found $COUNT ConfigMaps"

if [ "$COUNT" -eq 0 ]; then
  echo '{ "FailureHandlingConfig": {} }' > "$OUTPUT"
  exit 0
fi

echo "[config-merge] Processing each ConfigMap entry..."

PARSED=$(
  jq -c '.items[] | .data | to_entries[] | @base64' "$TMP" \
  | while read -r entry; do
      _jq() { echo "$entry" | base64 -d | jq -r "$1"; }
      key=$(_jq '.key')
      val=$(_jq '.value')
      json_val=$(echo "$val" | yq eval -o=json -)
      echo "{\"$key\": $json_val}"
    done
)

echo "[config-merge] Merging all entries into FailureHandlingConfig..."

echo "$PARSED" \
  | jq -s 'reduce .[] as $item ({}; . * $item) | { FailureHandlingConfig: . }' \
  > "$OUTPUT"

echo "[config-merge] Successfully wrote merged config to $OUTPUT"
{{- end -}}

{{- define "config-merge.initConfigRbac" -}}
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: config-aggregator-sa-{{ .Chart.Name }}
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: config-aggregator-reader-{{ .Chart.Name }}
rules:
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: config-aggregator-binding-{{ .Chart.Name }}
subjects:
  - kind: ServiceAccount
    name: config-aggregator-sa-{{ .Chart.Name }}
roleRef:
  kind: Role
  name: config-aggregator-reader-{{ .Chart.Name }}
  apiGroup: rbac.authorization.k8s.io

{{- end -}}
