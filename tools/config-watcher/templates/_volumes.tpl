{{/*
  Volume definition (emptyDir shared by init, sidecar, and app).
  Include in volumes list.
*/}}
{{- define "config-watcher.volumes" -}}
- name: config
  emptyDir: {}
{{- end -}}


{{/*
  Volume mount for the app container.
  Include in container volumeMounts list.
*/}}
{{- define "config-watcher.volumeMounts" -}}
- name: config
  mountPath: /config
{{- end -}}
