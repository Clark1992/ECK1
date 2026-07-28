# Repository automation guidance

These rules apply to workflow files and automation scripts under `.github/`.

- Preserve the repository's GitHub Actions conventions and keep workflows deterministic and easy to review.
- Pin action and image versions where the project convention permits, use clear step names, and do not hardcode secrets.
- Prefer existing scripts under `.github/scripts` for setup and shared behavior instead of duplicating logic in workflows.
- Validate workflow or script changes with the most relevant available checks; do not run the application test suite unless the user explicitly requests it.
