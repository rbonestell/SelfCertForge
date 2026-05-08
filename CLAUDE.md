# SelfCertForge

## What this is
.NET 10 MAUI desktop app (macCatalyst + Windows) that generates and manages root certificates + signed cert bundles using .NET's built-in `System.Security.Cryptography` APIs — no external OpenSSL binary required.

## Solution layout
- `SelfCertForge.App` — MAUI UI. Shell-based nav (`Shell/ShellPage`, `ShellViewModel`, `AppRoute`), pages in `Pages/`, reusable XAML controls in `Controls/`, MAUI bootstrap in `MauiProgram.cs`. Bundle/exe name overridden to `SelfCertForge` via `<AssemblyName>` (root namespace stays `SelfCertForge.App`). Mac Catalyst handler tweaks under `Platforms/MacCatalyst/HandlerCustomizations`.
- `SelfCertForge.Core` — pure .NET. Abstractions (`IActivityLog`, `ICertificateStore`, `ICertificateWorkflowService`), domain `Models/`, `Presentation/` ViewModels (`DashboardViewModel`, `CertificatesViewModel`) with `ObservableObject` + `AsyncCommand`, `Parsing/`, `Validation/`. **No MAUI references — keep it that way (it's the unit-test boundary).**
- `SelfCertForge.Infrastructure` — concrete impls: `DotNetCryptoCertificateWorkflowService`, `ForgeService`, `CertificateExportService`, `JsonCertificateStore`, `JsonActivityLog`, `RootCertificateLocator`. DI wiring in `ServiceCollectionExtensions.AddSelfCertForgeInfrastructure()`.
- `SelfCertForge.Core.Tests` — xUnit. Tests cover ViewModels, parsing, validation, certificate workflow, JSON stores. **Run these before claiming work is done.**

## Architecture rules
- Layering: `App` → `Core` + `Infrastructure`; `Infrastructure` → `Core`; `Core` depends on nothing project-local.
- ViewModels live in `Core/Presentation` and are unit-testable (no MAUI types). Don't move them into `App`.
- DI is composed in `MauiProgram.cs`; stores are singletons rooted at `FileSystem.AppDataDirectory`.
- Certificate operations go through `ICertificateWorkflowService` (`DotNetCryptoCertificateWorkflowService`). Don't call crypto APIs directly from `App` or `Core`.

## Build / run / test
Use the `makefile` (macCatalyst shortcuts):
```
make build     # dotnet build -f net10.0-maccatalyst
make rebuild   # clean + build
make run       # kills running SelfCertForge first, then dotnet run
make kill      # pkill -x SelfCertForge
```
Tests: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`.
Windows TFM: `net10.0-windows10.0.19041.0`. If Xcode gating blocks build: `-p:ValidateXcodeVersion=false`.

## Conventions
- C#: nullable enabled, implicit usings. Async commands via `AsyncCommand`, MVVM via `ObservableObject` (custom, not CommunityToolkit.Mvvm).
- Fonts: Inter (Regular/SemiBold/Bold) and JetBrainsMono (Regular/Medium/SemiBold) — registered in `MauiProgram`. Use these aliases, don't add new font deps casually.
- CommunityToolkit.Maui is enabled (`UseMauiCommunityToolkit`).
- App behavior invariants (don't regress): Separate Files mode requires explicit root cert + key paths; PFX mode requires explicit bundle path + password; Subject DN/SAN are user-editable with script-style defaults.

## Design system
There is a `selfcertforge-design` skill with the brand's colors/type/UI kit. **Invoke it before doing any UI/visual work** (new pages, controls, theming, icons, splash, marketing surfaces). Do not invent palette/typography from scratch.

## Don't
- Don't add MAUI/UI types to `SelfCertForge.Core`.
- Don't bypass `ICertificateWorkflowService` to call crypto APIs directly from `App` or `Core`.
- Don't rename the project dirs/csprojs (bundle name is already overridden via `AssemblyName`).
- Don't change `ApplicationId` casually — it's the bundle identity (Keychain/prefs scope).
