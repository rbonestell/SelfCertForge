# SelfCertForge

## What this is
.NET 10 MAUI desktop app (macCatalyst + Windows) that generates and manages root certificates + signed cert bundles using .NET's built-in `System.Security.Cryptography` APIs — no external OpenSSL binary required. SDK pinned via `global.json` (.NET 10.0.203, MAUI workload 10.0.107).

## Solution layout
- `SelfCertForge.App` — MAUI UI. Shell-based nav (`Shell/ShellPage`, `ShellViewModel`, `AppRoute`), pages in `Pages/` (Dashboard, Authorities, Certificates, Settings, AboutSection), modal dialogs in `Dialogs/` (CreateRoot, CreateSignedCert) with `*Host` adapters that implement Core dialog abstractions, reusable XAML controls + behaviors + converters in `Controls/`, MAUI-side service shims in `Services/` (folder picker, PFX password, confirmation), `Navigation/NavigationService`, MAUI bootstrap in `MauiProgram.cs`. Bundle/exe name overridden to `SelfCertForge` via `<AssemblyName>` (root namespace stays `SelfCertForge.App`). Mac Catalyst tweaks under `Platforms/MacCatalyst` (handler customizations, `AppDelegate`, `MacDataFolderService`); Windows-specific resources under `Platforms/Windows` (multi-size `app.ico` for desktop shortcuts).
- `SelfCertForge.Core` — pure .NET. Abstractions in `Abstractions/` (`IActivityLog`, `ICertificateStore`, `ICertificateWorkflowService`, `ICertificateExportService`, `IForgeService`, `IUpdateService`, `IGithubReleaseService`, `IUserPreferencesStore`, `IDataFolderService`, `ITrustStoreChecker`, `INavigationService`, `IFolderPicker`, `IPfxPasswordDialog`, `IConfirmationDialog`, `ICreateRootDialog`, `ICreateSignedCertDialog`), domain `Models/`, `Presentation/` ViewModels (`DashboardViewModel`, `AuthoritiesViewModel`, `CertificatesViewModel`, `SettingsViewModel`, `CreateRootDialogViewModel`, `CreateSignedCertDialogViewModel`, `SanEntryViewModel`) with `ObservableObject` + `AsyncCommand`, `Parsing/`, `Validation/`. **No MAUI references — keep it that way (it's the unit-test boundary).**
- `SelfCertForge.Infrastructure` — concrete impls: `DotNetCryptoCertificateWorkflowService`, `ForgeService`, `CertificateExportService`, `JsonCertificateStore`, `JsonActivityLog`, `JsonUserPreferencesStore`, `RootCertificateLocator`, `SystemTrustStoreChecker`, `GithubReleaseService`, `VelopackUpdateService`. DI wiring in `ServiceCollectionExtensions.AddSelfCertForgeInfrastructure()` (registers workflow/export/update services; the rest are wired in `MauiProgram.cs` because they need `FileSystem.AppDataDirectory` or MAUI services).
- `SelfCertForge.Core.Tests` — xUnit. Tests cover ViewModels, parsing, validation, certificate workflow + export, JSON stores (incl. activity-log retention), GitHub release polling, user preferences, and the trust-store / root-locator helpers. **Run these before claiming work is done.**

## Architecture rules
- Layering: `App` → `Core` + `Infrastructure`; `Infrastructure` → `Core`; `Core` depends on nothing project-local.
- ViewModels live in `Core/Presentation` and are unit-testable (no MAUI types). Don't move them into `App`.
- DI is composed in `MauiProgram.cs`; JSON stores are singletons rooted at `IDataFolderService` (which returns `FileSystem.AppDataDirectory`, with a Mac-Catalyst override in `MacDataFolderService`).
- Certificate operations go through `ICertificateWorkflowService` (`DotNetCryptoCertificateWorkflowService`) and exports through `ICertificateExportService`. Don't call `System.Security.Cryptography` directly from `App` or `Core`.
- Auto-update goes through `IUpdateService` (`VelopackUpdateService`); GitHub release version polling for the Settings "Check for updates" UX goes through `IGithubReleaseService`. Don't hit GitHub or Velopack APIs directly elsewhere.
- App-side dialogs (`CreateRootDialogHost`, `CreateSignedCertDialogHost`, `MauiConfirmationDialog`, `MauiPfxPasswordDialog`, `MauiFolderPicker`) are thin adapters that implement Core abstractions so ViewModels stay testable.

## Build / run / test
Use the `makefile` (auto-detects OS — macCatalyst on Unix, `net10.0-windows10.0.19041.0` on Windows):
```
make build     # dotnet build the App for the current OS's TFM
make rebuild   # clean + build
make run       # kills running SelfCertForge first, then dotnet run
make kill      # pkill -x SelfCertForge (or taskkill on Windows)
make test      # dotnet test (runs every test project in the solution)
make clean     # dotnet clean + remove App bin/obj
```
Direct invocation also works: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`. If Xcode gating blocks build: `-p:ValidateXcodeVersion=false` (already set in the App csproj). macCatalyst builds produce a fat bundle (`maccatalyst-x64;maccatalyst-arm64`); Windows ships **x64 only** (`win-x64`) — no ARM64 build — and uses unpackaged `WindowsAppSDKSelfContained` so Velopack installers run on machines without WinAppRuntime.

## Release pipeline
- `.github/workflows/release.yml` runs on `release: published`. Matrix: `macos-latest` (universal macCatalyst .pkg) and `windows-latest` (`win-x64` only). Don't add a `win-arm64` row back without an explicit ask — the Windows installer is x64-only by design.
- Velopack (`vpk`) packages each channel (`osx`, `win-x64`), pulls the previous release for delta generation, and the `upload` job pushes each channel's artifacts to the GitHub Release with `--merge`. Channel names must stay in sync between the matrix and the upload loop.
- macOS signing uses an Apple Developer ID cert imported into a temp keychain + a notarytool profile created from App Store Connect API key secrets. Windows signing uses Azure Trusted Signing via OIDC federated auth (`environment: release` — the federated credential subject claim is environment-scoped). Sign published binaries first, then re-sign Velopack's `Setup.exe` after `vpk pack` (Velopack rewrites the stub installer, invalidating prior signatures).
- The workflow stamps `<ApplicationDisplayVersion>` from the release tag (`v1.2.3` → `1.2.3`). Don't hard-code release versions in the csproj.

## Conventions
- C#: nullable enabled, implicit usings. Async commands via `AsyncCommand`, MVVM via `ObservableObject` (custom, not CommunityToolkit.Mvvm).
- Fonts: Inter (Regular/SemiBold/Bold) and JetBrainsMono (Regular/Medium/SemiBold) — registered in `MauiProgram`. Use these aliases, don't add new font deps casually.
- CommunityToolkit.Maui is enabled (`UseMauiCommunityToolkit`); Velopack is referenced for in-app updates.
- App behavior invariants (don't regress): export supports DER, PEM, PFX, and P7B; Separate Files mode requires explicit root cert + key paths; PFX mode requires explicit bundle path + password; Subject DN/SAN are user-editable with script-style defaults; generated root certs can be added to the system trust store via `ITrustStoreChecker` flows.

## Design system
There is a `selfcertforge-design` skill with the brand's colors/type/UI kit. **Invoke it before doing any UI/visual work** (new pages, controls, theming, icons, splash, marketing surfaces). Do not invent palette/typography from scratch.

## Don't
- Don't add MAUI/UI types to `SelfCertForge.Core`.
- Don't bypass `ICertificateWorkflowService` / `ICertificateExportService` to call crypto APIs directly from `App` or `Core`.
- Don't bypass `IUpdateService` / `IGithubReleaseService` to call Velopack or GitHub directly from `App` or `Core`.
- Don't rename the project dirs/csprojs (bundle name is already overridden via `AssemblyName`).
- Don't change `ApplicationId` (`com.rbonestell.selfcertforge.app`) casually — it's the bundle identity (Keychain/prefs scope) and Velopack's update channel ID.
- Don't add a Windows ARM64 build/installer back without explicit direction — Windows is intentionally x64-only.
- Don't write tests asserting README content/strings — test behavior only.
