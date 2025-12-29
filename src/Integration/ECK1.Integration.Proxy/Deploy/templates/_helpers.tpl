{{- define "render.env" -}}
{{- $root := index . 0 -}}
{{- $prefix := index . 1 -}}

{{- range $key, $value := $root }}
  {{- $newPrefix := ternary (printf "%s__%s" $prefix $key) $key (ne $prefix "") }}

  {{- if kindIs "map" $value }}
    {{- include "render.env" (list $value $newPrefix) }}
  {{- else }}
- name: {{ $newPrefix }}
  value: {{ $value | quote }}
  {{- end }}
{{- end }}

{{- end }}



{{- define "render.env.secret" -}}
{{- $root := index . 0 -}}
{{- $prefix := index . 1 -}}

{{- range $key, $value := $root }}
  {{- if eq $prefix "" }}
    {{- $envName := $key }}
  {{- else }}
    {{- $envName := printf "%s__%s" $prefix $key }}
  {{- end }}

  {{- $envName := (eq $prefix "" | ternary $key (printf "%s__%s" $prefix $key)) }}

  {{- if and (kindIs "map" $value) (hasKey $value "name") (hasKey $value "key") }}

- name: {{ $envName }}
  valueFrom:
    secretKeyRef:
      name: {{ $value.name }}
      key: {{ $value.key }}

  {{- else if kindIs "map" $value }}

    {{- include "render.env.secret" (list $value $envName) }}

  {{- else }}

    {{- fail (printf "Invalid secret spec: expected map {name,key} for %s" $envName) }}

  {{- end }}
{{- end }}

{{- end }}



{{- define "render.env.configMap" -}}
{{- $root := index . 0 -}}
{{- $prefix := index . 1 -}}

{{- range $key, $value := $root }}
  {{- if eq $prefix "" }}
    {{- $envName := $key }}
  {{- else }}
    {{- $envName := printf "%s__%s" $prefix $key }}
  {{- end }}

  {{- $envName := (eq $prefix "" | ternary $key (printf "%s__%s" $prefix $key)) }}

  {{- if and (kindIs "map" $value) (hasKey $value "name") (hasKey $value "key") }}

- name: {{ $envName }}
  valueFrom:
    configMapKeyRef:
      name: {{ $value.name }}
      key: {{ $value.key }}

  {{- else if kindIs "map" $value }}

    {{- include "render.env.configMap" (list $value $envName) }}

  {{- else }}

    {{- fail (printf "Invalid configMap spec: expected map {name,key} for %s" $envName) }}

  {{- end }}
{{- end }}

{{- end }}

