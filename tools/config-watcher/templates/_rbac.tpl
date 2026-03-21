{{/*
  RBAC: ServiceAccount + Role (get/list/watch configmaps) + RoleBinding.
  Include at top level (outside Deployment), or use config-watcher-rbac.yaml in consuming chart.
*/}}
{{- define "config-watcher.rbac" -}}
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: config-watcher-sa-{{ .Chart.Name }}
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: config-watcher-reader-{{ .Chart.Name }}
rules:
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list", "watch"]
  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "patch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: config-watcher-binding-{{ .Chart.Name }}
subjects:
  - kind: ServiceAccount
    name: config-watcher-sa-{{ .Chart.Name }}
roleRef:
  kind: Role
  name: config-watcher-reader-{{ .Chart.Name }}
  apiGroup: rbac.authorization.k8s.io
{{- end -}}
