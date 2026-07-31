# Repository guidance

This file contains repository-level guidance for coding agents. It is intentionally provider-neutral; use the agent or editor's own configuration for local approvals, model settings, and one-off prompts.

## Scope and project context

- Follow existing project conventions and preserve behavior unless the task explicitly calls for a change.
- Read [`docs/project-context.md`](docs/project-context.md) when a task depends on the architecture, message flows, schemas, or service layout or if you need a summary on current solution architecture.
- Use the repository-local [`$add-new-entity` skill](.agents/skills/add-new-entity/SKILL.md) for end-to-end entity work.
- More specific guidance is placed in [`src/AGENTS.md`](src/AGENTS.md), [`tests/AGENTS.md`](tests/AGENTS.md), [`infra/AGENTS.md`](infra/AGENTS.md), and [`.github/AGENTS.md](.github/AGENTS.md). Apply it when working in those subtrees.

## Working agreements

- Prefer clear, focused changes over overcomplicated or speculative ones.
- Preserve unrelated working-tree changes; do not reformat or clean up files outside the task.
- Continue with safe, in-scope work when the next step is clear. Ask for clarification only when requirements are materially ambiguous or the next action would expand the requested scope.
- Keep source comments and new documentation in English unless an existing file clearly follows another convention.
- Do not run tests unless the user explicitly requests them. Building or otherwise validating a change is still expected when appropriate.
- The normal development environment is a Windows host with Docker Desktop and WSL2 (Ubuntu 24.04). Invoke PowerShell commands (for Windows) and scripts directly; avoid wrapping them in an unrelated shell.
- If a required CLI is unavailable, inspect the existing setup helpers under `.github/scripts` before inventing a new installation or configuration path.
- Never hardcode secrets or include credentials in source, configuration, examples, or command output.

## Engineering conventions

### .NET and C#

- Application and test projects target .NET 8. Source-generator projects may target `netstandard2.0`; preserve their existing target and language-version requirements.
- Use asynchronous APIs for I/O, dependency injection, file-scoped namespaces, and explicit types for public APIs.
- Use `var` when the type is obvious. Prefer immutability and avoid unnecessary allocations without sacrificing readability.
- Prefer collection expressions when the target type and behavior are clear; do not mechanically replace `ToList`, `ToArray`, or other APIs when that would change semantics.
- Do not introduce obsolete APIs.

### Tests

- Keep tests deterministic, behavior-focused, and named for the behavior they verify.
- Use the libraries already established by the repository: xUnit, `AutoFixture.Xunit2`, and FluentAssertions. Prefer `[AutoData]`, `[InlineAutoData]`, or `[MemberData]` for generated test data where they improve the test.
- Organize tests around Arrange, Act, and Assert. Add comments only when they clarify a non-obvious setup or transition.
- Use assertion scopes for multiple FluentAssertions checks. Do not mock extension methods such as `ILogger` calls; test the observable behavior instead.

### Infrastructure and CI

- Keep infrastructure and pipelines deterministic, declarative, secret-free, and easy to review.
- The repository uses GitHub Actions; preserve its existing workflow conventions, use clear step names, and pin action and image versions where the project convention permits.
- For containers, prefer multi-stage builds, minimal appropriate base images, and non-root execution.
- Put values shared between infrastructure and service deployments in the existing `global-vars` ConfigMap. Keep phase-only values in the applicable defaults file.
- When an image version or repository is unknown, verify it against authoritative upstream documentation or the relevant registry; if that is unavailable, state the uncertainty rather than guessing.

## Verification

- For code changes, use the relevant build. The main solution is `dotnet build src/ECK1.sln -c Release`.
- When `TestPlatform` changes, also use `dotnet build tests/ECK1.TestPlatform/ECK1.TestPlatform.csproj -c Release`.
- For infrastructure-only changes, use the relevant Helm, Helmfile, YAML or Docker validation when available; an application build is usually unnecessary.
