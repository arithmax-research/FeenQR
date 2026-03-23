# Copilot Instructions For FeenQR

## Startup boundaries (critical)

- CLI startup entry point: Program.cs
- Web server startup entry point: WebApp/Server/Program.cs
- Web client bootstrap: WebApp/Client/Program.cs

When the request is about web app behavior, API endpoints, controller wiring, CORS, middleware, or web DI registrations:
- Edit WebApp/Server/Program.cs (and related WebApp files)
- Do not add those registrations to Program.cs

When the request is about CLI behavior, interactive shell flow, or console-only DI registrations:
- Edit Program.cs and InteractiveCLI.cs

## Change routing checklist

Before editing service registration code, verify target file by intent:
- Web intent -> WebApp/Server/Program.cs
- CLI intent -> Program.cs

If a feature must exist in both web and CLI:
- Register in both startup paths intentionally
- Mention both files explicitly in the change summary

## Safety

- Preserve existing project split unless user explicitly requests unification.
- Avoid moving registrations across startup files unless requested.
