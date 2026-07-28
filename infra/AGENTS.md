# Infrastructure guidance

These rules add detail to the repository guidance for infrastructure under `infra/`.

- Keep Helm, Helmfile, YAML, Docker changes declarative, deterministic, secret-free, and consistent with existing deployment patterns.
- Pin image versions and use minimal suitable container images with non-root execution where supported.
- Keep CI steps clear and fail-fast; preserve the repository's GitHub Actions conventions and pin action versions where practical.
- Use the existing `global-vars` ConfigMap for values shared between infrastructure and service deployments. Keep phase-only values in the applicable defaults file.
- Verify unfamiliar image names and versions with authoritative upstream documentation or the relevant registry instead of guessing.
- For infrastructure-only changes, use relevant manifest rendering, linting, or schema validation. An application build is usually unnecessary.
