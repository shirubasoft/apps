---
name: creating-new-app
description: Scaffold a new application in this repository when a task asks to create, start, initialize, or bootstrap a new app. Do not use for changes to an existing app.
---

# Creating a new app

Also use `code-structuring` whenever scaffolding includes source-code architecture or multiple projects/modules.

## Invariants

Always start by creating a new branch from main.

Create a dedicated folder for each new app. Follow this folder structure:

```
/apps
  /<app-name>
    /src
      /<app-name>
        /<app-name>.csproj
    /tests
      /<app-name>.Tests
        /<app-name>.Tests.csproj
    /docs
    /README.md
```

Don't implement features that weren't asked for.

Inspect `.github/workflows`, `.github/release`, and `tools` before adding CI or release code. Reuse the repository's semantic-release workflow, release scripts, and CRAP score tool.

Give each app explicitly named workflows with `push` and `pull_request` path filters for its folder. Keep shared workflows triggerless except for `workflow_call`.

Use an app-specific artifact and tag prefix. Publish the artifact built by successful `main` CI instead of rebuilding it in the release workflow. Scope release commits to the app name.

Use `tools/CrapScore` rather than copying it. Run its reduction check only when covered source or test inputs change, and give each app its own coverage artifact prefix.

Create an Aspire Apphost for orchestrating the app and its dependencies. Every resource should be registered in the Apphost.

Create projects and templates through its corresponding CLI commands (`dotnet new`, `aspire new`), rather than manually creating files.

Prefer off-the-shelf libraries and frameworks over custom implementations.

## Tech stack

### UI

One of the following:

- TanStack
- Blazor
- MAUI

CSS and component libraries over custom implementations.

### Backend

Use ASP.NET Core minimal APIs.

### .NET projects

Use `.editorconfig`, `Directory.Build.props` and `Directory.Packages.props`.

Use `TreatWarningsAsErrors` and `Nullable` in all projects.

Use .NET 11 and the preview language version.
