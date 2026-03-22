FROM alpine:3.22.0

RUN apk add --no-cache \
    curl \
    jq \
    yq \
    bash \
    ca-certificates \
    && ARCH=$(uname -m) \
    && case "$ARCH" in \
         x86_64)  KUBECTL_ARCH="amd64" ;; \
         aarch64) KUBECTL_ARCH="arm64" ;; \
         *)       echo "Unsupported arch: $ARCH" && exit 1 ;; \
       esac \
    && curl -Lo /usr/local/bin/kubectl \
         "https://dl.k8s.io/release/v1.31.0/bin/linux/${KUBECTL_ARCH}/kubectl" \
    && chmod +x /usr/local/bin/kubectl

USER 65534
