---
name: creating-new-app
description: Scaffold a new application in this repository when a task asks to create, start, initialize, or bootstrap a new app. Do not use for changes to an existing app.
---

# Creating a new app

## Invariants

Don't implement features that weren't asked for.

Create or reuse a CRAP score workflow. Always block the PR if score did not decrease below a certain threshold. Reference: https://github.com/shirubasoft/aspire-modular-apphosts/blob/main/.github/workflows/ci.yml

Create workflows for building, testing and deploying the app.

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