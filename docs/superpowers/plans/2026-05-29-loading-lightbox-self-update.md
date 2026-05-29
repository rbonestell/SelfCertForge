# Loading Lightbox in the Self-Update Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the inline progress bar / status-text indicators in the "Download & Install" step of the self-update flow with the existing loading lightbox modal, shown indeterminately with a two-phase message ("Downloading Update…" → "Installing Update…").

**Architecture:** `SettingsViewModel` (Core) gains an optional `ILoadingOverlay`. `DownloadAndInstallAsync` runs the download behind the overlay and nests a second `RunOrDirectAsync` for the install phase — the overlay's existing nested-call mechanism swaps the live message in place (no second popup, no flicker). The determinate progress members are deleted; the outcome `Label` stays. DI passes the already-registered `ILoadingOverlay` into the VM.

**Tech Stack:** .NET 10, C# (nullable enabled), custom MVVM (`ObservableObject` / `AsyncCommand`), xUnit + FluentAssertions, CommunityToolkit.Maui Popup (`MauiLoadingOverlay`).

**Spec:** `docs/superpowers/specs/2026-05-29-loading-lightbox-self-update-design.md`

**Reference — relevant existing signatures (do not redefine):**
- `SelfCertForge.Core/Abstractions/LoadingOverlayExtensions.cs`: `static Task RunOrDirectAsync(this ILoadingOverlay? overlay, string message, Func<Task> operation)` — runs `operation` directly when `overlay` is null.
- `IUpdateService`: `Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)`; `Task ApplyUpdateAndRestartAsync(UpdateInfo update)`.
- `SelfCertForge.Core.Tests/FakeLoadingOverlay.cs`: records each message into `List<string> Messages` and runs the operation (nested calls record in order).

**Run tests with:** `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj` (or `make test`). The App project is intentionally excluded from `make test`.

---

### Task 1: Inject `ILoadingOverlay` into `SettingsViewModel`

No behavior change — just make the overlay available. The suite stays green because nothing uses it yet.

**Files:**
- Modify: `SelfCertForge.Core/Presentation/SettingsViewModel.cs`

- [ ] **Step 1: Add the backing field**

In the readonly-field block (currently ends at `private readonly IGithubReleaseService? _githubRelease;`, around line 16), add a line directly after it:

```csharp
    private readonly IGithubReleaseService? _githubRelease;
    private readonly ILoadingOverlay? _overlay;
```

- [ ] **Step 2: Add the optional constructor parameter**

Change the full constructor's signature (around lines 56–62) from:

```csharp
    public SettingsViewModel(
        IUpdateService updateService,
        IUserPreferencesStore? preferencesStore,
        IActivityLog? activityLog,
        IDataFolderService? dataFolderService,
        IConfirmationDialog? confirmationDialog,
        IGithubReleaseService? githubRelease = null)
```

to:

```csharp
    public SettingsViewModel(
        IUpdateService updateService,
        IUserPreferencesStore? preferencesStore,
        IActivityLog? activityLog,
        IDataFolderService? dataFolderService,
        IConfirmationDialog? confirmationDialog,
        IGithubReleaseService? githubRelease = null,
        ILoadingOverlay? loadingOverlay = null)
```

The single-arg convenience constructor (`: this(updateService, null, null, null, null, null)`) is unaffected — the new parameter is optional.

- [ ] **Step 3: Assign the field in the constructor body**

After `_githubRelease = githubRelease;` (around line 70), add:

```csharp
        _githubRelease = githubRelease;
        _overlay = loadingOverlay;
```

- [ ] **Step 4: Build/test to verify no regression**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: PASS (all existing tests still green; `_overlay` is unused for now — a CS0169/unused warning is acceptable at this step and disappears in Task 2).

- [ ] **Step 5: Commit**

```bash
git add SelfCertForge.Core/Presentation/SettingsViewModel.cs
git commit -m "feat(settings): inject loading overlay into SettingsViewModel"
```

---

### Task 2: Drive the download/install behind the lightbox (TDD)

**Files:**
- Modify: `SelfCertForge.Core.Tests/SettingsViewModelTests.cs` (add `WasApplied` to the fake; add a new test)
- Modify: `SelfCertForge.Core/Presentation/SettingsViewModel.cs` (rewrite `DownloadAndInstallAsync`)

- [ ] **Step 1: Add a `WasApplied` flag to the test fake**

In `SettingsViewModelTests.cs`, in the nested `FakeUpdateService` class, change the apply method (currently around line 139):

```csharp
        public Task ApplyUpdateAndRestartAsync(UpdateInfo update) => Task.CompletedTask;
```

to:

```csharp
        public bool WasApplied { get; private set; }

        public Task ApplyUpdateAndRestartAsync(UpdateInfo update)
        {
            WasApplied = true;
            return Task.CompletedTask;
        }
```

- [ ] **Step 2: Write the failing test**

Add this test to the `SettingsViewModelTests` class (next to `DownloadAndInstall_WhenDownloadFails_ResetsIsDownloading`):

```csharp
    [Fact]
    public async Task DownloadAndInstall_ShowsLightbox_WithDownloadingThenInstalling()
    {
        var update = new UpdateInfo("2.0.0", null, null, null);
        var service = FakeUpdateService.WithUpdate(update);
        var overlay = new FakeLoadingOverlay();
        var vm = new SettingsViewModel(service, null, null, null, null, null, overlay);

        await vm.CheckForUpdateAsync();
        await vm.DownloadAndInstallCommand.ExecuteAsync();

        overlay.Messages.Should().Equal("Downloading Update…", "Installing Update…");
        service.WasApplied.Should().BeTrue();
        vm.IsDownloading.Should().BeFalse();
    }
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~DownloadAndInstall_ShowsLightbox"`
Expected: FAIL — `overlay.Messages` is empty because `DownloadAndInstallAsync` does not yet call the overlay.

- [ ] **Step 4: Rewrite `DownloadAndInstallAsync`**

Replace the entire current method (around lines 434–458):

```csharp
    private async Task DownloadAndInstallAsync()
    {
        if (AvailableUpdate is null || IsDownloading) return;

        IsDownloading = true;
        DownloadProgress = 0;
        UpdateStatusMessage = "Downloading update…";

        try
        {
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                UpdateStatusMessage = $"Downloading update… {p}%";
            });

            await _updateService.DownloadUpdateAsync(AvailableUpdate, progress);
            UpdateStatusMessage = "Applying update and restarting…";
            await _updateService.ApplyUpdateAndRestartAsync(AvailableUpdate);
        }
        catch
        {
            UpdateStatusMessage = "Download failed. Please try again.";
            IsDownloading = false;
        }
    }
```

with:

```csharp
    private async Task DownloadAndInstallAsync()
    {
        if (AvailableUpdate is null || IsDownloading) return;

        IsDownloading = true;
        UpdateStatusMessage = null;

        try
        {
            await _overlay.RunOrDirectAsync("Downloading Update…", async () =>
            {
                await _updateService.DownloadUpdateAsync(AvailableUpdate);
                await _overlay.RunOrDirectAsync("Installing Update…", () =>
                    _updateService.ApplyUpdateAndRestartAsync(AvailableUpdate));
            });
        }
        catch
        {
            UpdateStatusMessage = "Download failed. Please try again.";
        }
        finally
        {
            IsDownloading = false;
        }
    }
```

Notes: `RunOrDirectAsync` is an extension method on `ILoadingOverlay?`, so calling it on the nullable `_overlay` is valid (null → operation runs directly). The per-percent `Progress<int>` reporter is gone (indeterminate). On the success path the inner phase ends in the app restart, so the modal stays up through the hand-off.

- [ ] **Step 5: Run the new test to verify it passes**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~DownloadAndInstall_ShowsLightbox"`
Expected: PASS.

- [ ] **Step 6: Run the full suite (confirm no regression)**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: PASS. `DownloadAndInstall_WhenDownloadFails_ResetsIsDownloading` still passes (overlay is null in that test → runs directly; download throws → `catch` sets "failed", `finally` clears `IsDownloading`). `DownloadProgressNormalized_ReflectsDownloadProgress` still passes (property removed in Task 4).

- [ ] **Step 7: Commit**

```bash
git add SelfCertForge.Core/Presentation/SettingsViewModel.cs SelfCertForge.Core.Tests/SettingsViewModelTests.cs
git commit -m "feat(update): show loading lightbox during download and install"
```

---

### Task 3: Remove the inline progress bar from the Settings page

**Files:**
- Modify: `SelfCertForge.App/Pages/SettingsView.xaml`

- [ ] **Step 1: Delete the `<ProgressBar>` block**

Remove these lines (around 535–539) in full, including the comment:

```xml
                                <!-- Download progress -->
                                <ProgressBar Progress="{Binding DownloadProgressNormalized}"
                                             ProgressColor="{StaticResource ColorAccentPrimary}"
                                             IsVisible="{Binding IsDownloading}"
                                             HeightRequest="3" />
```

Leave the surrounding `VerticalStackLayout`, the "Download & Install" button (its `IsVisible="{Binding IsDownloading, Converter=...}"` stays), the release-notes label, and the outcome `Label` bound to `UpdateStatusMessage` untouched.

- [ ] **Step 2: Confirm no other markup references the removed bindings**

Run: `grep -rn "DownloadProgressNormalized\|DownloadProgress\b" SelfCertForge.App`
Expected: no matches (the only reference was the deleted `ProgressBar`).

- [ ] **Step 3: Commit**

```bash
git add SelfCertForge.App/Pages/SettingsView.xaml
git commit -m "refactor(settings): remove inline download progress bar"
```

---

### Task 4: Remove the now-dead progress members and obsolete test

**Files:**
- Modify: `SelfCertForge.Core/Presentation/SettingsViewModel.cs`
- Modify: `SelfCertForge.Core.Tests/SettingsViewModelTests.cs`

- [ ] **Step 1: Delete the obsolete test**

In `SettingsViewModelTests.cs`, delete this test in full (around lines 81–88):

```csharp
    [Fact]
    public void DownloadProgressNormalized_ReflectsDownloadProgress()
    {
        var service = FakeUpdateService.WithNoUpdate();
        var vm = new SettingsViewModel(service);

        vm.DownloadProgressNormalized.Should().BeApproximately(0.0, 0.001);
    }
```

- [ ] **Step 2: Remove the `DownloadProgress` property**

In `SettingsViewModel.cs`, delete the `DownloadProgress` property in full (around lines 203–217 — the `get`/`set` that calls `SetProperty(ref _downloadProgress, …)` and raises `OnPropertyChanged(nameof(DownloadProgressNormalized))`).

- [ ] **Step 3: Remove the `DownloadProgressNormalized` property**

Delete the line (around line 231):

```csharp
    public double DownloadProgressNormalized => _downloadProgress / 100.0;
```

- [ ] **Step 4: Remove the backing field**

In the update-fields block (around line 21), delete:

```csharp
    private int _downloadProgress;
```

- [ ] **Step 5: Trim the now-unused reporter from the test fake**

In `SettingsViewModelTests.cs` `FakeUpdateService`, delete the unused property (around line 107):

```csharp
        public int DownloadProgressToReport { get; set; }
```

and simplify `DownloadUpdateAsync` (around lines 133–138) from:

```csharp
        public Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (ThrowOnDownload) throw new InvalidOperationException("download failed");
            progress?.Report(DownloadProgressToReport);
            return Task.CompletedTask;
        }
```

to:

```csharp
        public Task DownloadUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
        {
            if (ThrowOnDownload) throw new InvalidOperationException("download failed");
            return Task.CompletedTask;
        }
```

- [ ] **Step 6: Verify nothing else references the removed members**

Run: `grep -rn "DownloadProgress\|_downloadProgress\|DownloadProgressToReport" SelfCertForge.Core SelfCertForge.Core.Tests SelfCertForge.App`
Expected: no matches.

- [ ] **Step 7: Run the full suite**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: PASS (no unused-field warnings; no references to deleted members).

- [ ] **Step 8: Commit**

```bash
git add SelfCertForge.Core/Presentation/SettingsViewModel.cs SelfCertForge.Core.Tests/SettingsViewModelTests.cs
git commit -m "refactor(settings): drop determinate download-progress members"
```

---

### Task 5: Wire the overlay into DI

**Files:**
- Modify: `SelfCertForge.App/MauiProgram.cs`

- [ ] **Step 1: Pass `ILoadingOverlay` into the `SettingsViewModel` registration**

Change the registration (around lines 99–106) from:

```csharp
        builder.Services.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<IUpdateService>(),
            sp.GetRequiredService<IUserPreferencesStore>(),
            sp.GetRequiredService<IActivityLog>(),
            // Optional — only registered on macCatalyst/Windows; null on other TFMs.
            sp.GetService<IDataFolderService>(),
            sp.GetRequiredService<IConfirmationDialog>(),
            sp.GetRequiredService<IGithubReleaseService>()));
```

to:

```csharp
        builder.Services.AddSingleton<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<IUpdateService>(),
            sp.GetRequiredService<IUserPreferencesStore>(),
            sp.GetRequiredService<IActivityLog>(),
            // Optional — only registered on macCatalyst/Windows; null on other TFMs.
            sp.GetService<IDataFolderService>(),
            sp.GetRequiredService<IConfirmationDialog>(),
            sp.GetRequiredService<IGithubReleaseService>(),
            sp.GetRequiredService<ILoadingOverlay>()));
```

`ILoadingOverlay` is already registered as a singleton (`builder.Services.AddSingleton<ILoadingOverlay, MauiLoadingOverlay>();`).

- [ ] **Step 2: Confirm registration is correct**

Run: `grep -n "AddSingleton<ILoadingOverlay" SelfCertForge.App/MauiProgram.cs`
Expected: the `MauiLoadingOverlay` registration line is present (proves the service resolves).

- [ ] **Step 3: Commit**

```bash
git add SelfCertForge.App/MauiProgram.cs
git commit -m "feat(di): pass loading overlay into SettingsViewModel"
```

---

## Self-Review

**Spec coverage:**
- Behavior/data-flow (nested two-phase overlay, indeterminate, restart hand-off) → Task 2.
- Wiring (ctor param + field; DI) → Task 1 + Task 5.
- Replacing indicators (remove ProgressBar; remove `DownloadProgress`/`DownloadProgressNormalized`/`_downloadProgress`/reporter; keep `IsDownloading` + outcome `Label`) → Task 3 + Task 4.
- Copy ("Downloading Update…" / "Installing Update…") → Task 2 Step 4 + test in Step 2.
- Testing (new two-phase test; delete obsolete test; trim fake; keep failure test) → Task 2 + Task 4.
- Out-of-scope items (no modal progress bar, no new overlay API, no change to "Check for updates", no platform-specific handling) → respected; no task touches them.

**Placeholder scan:** none — every code step shows full before/after.

**Type consistency:** `_overlay` (field) / `loadingOverlay` (param) used consistently; `RunOrDirectAsync(string, Func<Task>)` matches the existing extension signature; `WasApplied` defined in Task 2 Step 1 before use in Step 2; message strings identical between implementation (Task 2 Step 4) and assertion (Task 2 Step 2).

**Ordering safety:** XAML binding removed (Task 3) before the bound properties are deleted (Task 4), so no commit leaves a dangling binding to a member that the same project still expects to compile against. The Core test suite is green after every task.
