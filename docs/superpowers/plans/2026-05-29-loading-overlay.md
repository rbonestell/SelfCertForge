# Loading Overlay (Lottie) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable dimmed loading overlay that shows the brand Lottie animation plus a caption while background operations (certificate forging, CSR signing, exports) run.

**Architecture:** A Core abstraction `ILoadingOverlay.RunAsync(message, op)` is consumed by ViewModels (as a nullable, optional ctor dependency) via a null-safe `RunOrDirectAsync` extension. The App implements it with `MauiLoadingOverlay`, which shows a CommunityToolkit.Maui v2 `Popup` containing a `SKLottieView` (SkiaSharp.Extended.UI.Maui) over a dimmed scrim. Core stays MAUI-free and unit-tested with a fake.

**Tech Stack:** .NET 10 MAUI, SkiaSharp.Extended.UI.Maui 3.0.0 (`SKLottieView`), CommunityToolkit.Maui 13 (`Popup`), xUnit + FluentAssertions.

---

## File Structure

**Create:**
- `SelfCertForge.Core/Abstractions/ILoadingOverlay.cs` — the contract.
- `SelfCertForge.Core/Abstractions/LoadingOverlayExtensions.cs` — `RunOrDirectAsync` null-safe wrapper.
- `SelfCertForge.Core.Tests/FakeLoadingOverlay.cs` — test double.
- `SelfCertForge.Core.Tests/LoadingOverlayExtensionsTests.cs` — extension tests.
- `SelfCertForge.App/Controls/LoadingOverlayContent.xaml` (+ `.xaml.cs`) — the popup content (animation + caption).
- `SelfCertForge.App/Services/MauiLoadingOverlay.cs` — the App adapter.
- `SelfCertForge.App/Resources/Raw/loading.json` — the Lottie asset.

**Modify:**
- `SelfCertForge.App/SelfCertForge.App.csproj` — add SkiaSharp package reference.
- `SelfCertForge.App/MauiProgram.cs` — `.UseSkiaSharp()`, register `ILoadingOverlay`, inject it into 5 ViewModels.
- `SelfCertForge.Core/Presentation/CreateRootDialogViewModel.cs` — ctor + wrap forge.
- `SelfCertForge.Core/Presentation/CreateSignedCertDialogViewModel.cs` — ctor + wrap forge.
- `SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs` — ctor + wrap forge.
- `SelfCertForge.Core/Presentation/CertificatesViewModel.cs` — ctors + wrap 4 exports.
- `SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs` — ctor + wrap 4 exports.

---

## Task 1: Add SkiaSharp package + UseSkiaSharp (build risk gate)

This task is the spec's risk gate. If restore/build fails on net10, **stop and escalate** — the fallback is hosting the de-watermarked SVG in a WebView (see spec § Risks).

**Files:**
- Modify: `SelfCertForge.App/SelfCertForge.App.csproj`
- Modify: `SelfCertForge.App/MauiProgram.cs`

- [ ] **Step 1: Add the package reference**

In `SelfCertForge.App.csproj`, add to the `PackageReference` `ItemGroup` (next to CommunityToolkit.Maui):

```xml
<PackageReference Include="SkiaSharp.Extended.UI.Maui" Version="3.0.0" />
```

- [ ] **Step 2: Register the SkiaSharp handlers in MauiProgram**

In `SelfCertForge.App/MauiProgram.cs`, add the using near the other usings:

```csharp
using SkiaSharp.Views.Maui.Controls.Hosting;
```

Add `.UseSkiaSharp()` to the builder chain, immediately after `.UseMauiCommunityToolkit()`:

```csharp
builder
    .UseMauiApp<App>()
    .UseMauiCommunityToolkit()
    .UseSkiaSharp()
```

- [ ] **Step 3: Restore + build for the current OS TFM**

Run: `make build`
Expected: restore succeeds (pulls SkiaSharp.Extended.UI.Maui 3.0.0, SkiaSharp.Views.Maui 3.119.x) and the build succeeds with no errors.

If restore or build fails with a target-framework / SkiaSharp incompatibility on net10: **STOP.** Do not continue. Report the exact error; the project must switch to the WebView fallback approach from the spec before this plan can proceed.

- [ ] **Step 4: Commit**

```bash
git add SelfCertForge.App/SelfCertForge.App.csproj SelfCertForge.App/MauiProgram.cs
git commit -m "build(app): add SkiaSharp.Extended.UI.Maui for Lottie rendering"
```

---

## Task 2: Add the Lottie asset

**Files:**
- Create: `SelfCertForge.App/Resources/Raw/loading.json`

- [ ] **Step 1: Copy the exported Lottie, stripping the metadata block**

The `meta` / `metadata` keys are invisible SVGator attribution; strip them for tidiness. Run:

```bash
python3 -c "import json; d=json.load(open('/Users/rbonestell/Pictures/SelfCertForge/icon-new.json')); d.pop('meta',None); d.pop('metadata',None); json.dump(d, open('/Users/rbonestell/Development/SelfCertForge/SelfCertForge.App/Resources/Raw/loading.json','w'), separators=(',',':'))"
```

- [ ] **Step 2: Verify it is valid Lottie and the asset is picked up**

Run: `python3 -c "import json; d=json.load(open('SelfCertForge.App/Resources/Raw/loading.json')); print('ok', d['v'], d['w'], d['h'], len(d['layers']), 'svgator' in json.dumps(d).lower())"`
Expected: `ok 5.5.2 4133 4133 9 False` (no `svgator` strings remain). The existing `MauiAsset Include="Resources\Raw\**"` glob in the csproj covers it automatically (addressable as `loading.json`).

- [ ] **Step 3: Build to confirm the asset bundles**

Run: `make build`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add SelfCertForge.App/Resources/Raw/loading.json
git commit -m "feat(app): add brand loading animation (Lottie)"
```

---

## Task 3: ILoadingOverlay interface + FakeLoadingOverlay test double

**Files:**
- Create: `SelfCertForge.Core/Abstractions/ILoadingOverlay.cs`
- Create: `SelfCertForge.Core.Tests/FakeLoadingOverlay.cs`

- [ ] **Step 1: Create the interface**

`SelfCertForge.Core/Abstractions/ILoadingOverlay.cs`:

```csharp
namespace SelfCertForge.Core.Abstractions;

public interface ILoadingOverlay
{
    Task RunAsync(string message, Func<Task> operation);
    Task<T> RunAsync<T>(string message, Func<Task<T>> operation);
}
```

- [ ] **Step 2: Create the test double**

`SelfCertForge.Core.Tests/FakeLoadingOverlay.cs`:

```csharp
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.Core.Tests;

public sealed class FakeLoadingOverlay : ILoadingOverlay
{
    public List<string> Messages { get; } = new();

    public Task RunAsync(string message, Func<Task> operation)
    {
        Messages.Add(message);
        return operation();
    }

    public Task<T> RunAsync<T>(string message, Func<Task<T>> operation)
    {
        Messages.Add(message);
        return operation();
    }
}
```

- [ ] **Step 3: Build the test project to confirm it compiles**

Run: `dotnet build SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add SelfCertForge.Core/Abstractions/ILoadingOverlay.cs SelfCertForge.Core.Tests/FakeLoadingOverlay.cs
git commit -m "feat(core): add ILoadingOverlay abstraction and test double"
```

---

## Task 4: RunOrDirectAsync extension (TDD)

A null-safe wrapper so ViewModels call one method whether or not an overlay is present. Keeps every call site DRY.

**Files:**
- Create: `SelfCertForge.Core/Abstractions/LoadingOverlayExtensions.cs`
- Test: `SelfCertForge.Core.Tests/LoadingOverlayExtensionsTests.cs`

- [ ] **Step 1: Write the failing tests**

`SelfCertForge.Core.Tests/LoadingOverlayExtensionsTests.cs`:

```csharp
using FluentAssertions;
using SelfCertForge.Core.Abstractions;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class LoadingOverlayExtensionsTests
{
    [Fact]
    public async Task RunOrDirectAsync_NullOverlay_RunsOperation()
    {
        var ran = false;
        ILoadingOverlay? overlay = null;

        await overlay.RunOrDirectAsync("msg", () => { ran = true; return Task.CompletedTask; });

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrDirectAsync_WithOverlay_RecordsMessageAndReturnsValue()
    {
        var overlay = new FakeLoadingOverlay();

        var result = await overlay.RunOrDirectAsync("Working…", () => Task.FromResult(42));

        result.Should().Be(42);
        overlay.Messages.Should().ContainSingle().Which.Should().Be("Working…");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~LoadingOverlayExtensionsTests"`
Expected: FAIL — compile error, `RunOrDirectAsync` is not defined.

- [ ] **Step 3: Create the extension**

`SelfCertForge.Core/Abstractions/LoadingOverlayExtensions.cs`:

```csharp
namespace SelfCertForge.Core.Abstractions;

public static class LoadingOverlayExtensions
{
    public static Task RunOrDirectAsync(this ILoadingOverlay? overlay, string message, Func<Task> operation)
        => overlay is null ? operation() : overlay.RunAsync(message, operation);

    public static Task<T> RunOrDirectAsync<T>(this ILoadingOverlay? overlay, string message, Func<Task<T>> operation)
        => overlay is null ? operation() : overlay.RunAsync(message, operation);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~LoadingOverlayExtensionsTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add SelfCertForge.Core/Abstractions/LoadingOverlayExtensions.cs SelfCertForge.Core.Tests/LoadingOverlayExtensionsTests.cs
git commit -m "feat(core): add RunOrDirectAsync overlay extension"
```

---

## Task 5: Wire CreateRootDialogViewModel (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/CreateRootDialogViewModel.cs`
- Test: `SelfCertForge.Core.Tests/CreateRootDialogViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `CreateRootDialogViewModelTests.cs` (the file already defines `FakeForgeService` and `FakeCert()`):

```csharp
[Fact]
public void Submit_RunsForgeThroughOverlay_WithCaption()
{
    var overlay = new FakeLoadingOverlay();
    var vm = new CreateRootDialogViewModel(new FakeForgeService(_ => FakeCert()), preferences: null, overlay: overlay)
    {
        CommonName = "Test Root CA"
    };

    ((System.Windows.Input.ICommand)vm.CreateCommand).Execute(null);

    overlay.Messages.Should().ContainSingle().Which.Should().Be("Forging Root Certificate…");
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CreateRootDialogViewModelTests.Submit_RunsForgeThroughOverlay"`
Expected: FAIL — compile error, `CreateRootDialogViewModel` has no 3-argument constructor.

- [ ] **Step 3: Add the overlay field and constructor parameter**

In `CreateRootDialogViewModel.cs`, add the field next to `private readonly IForgeService _forge;`:

```csharp
    private readonly ILoadingOverlay? _overlay;
```

Change the two-argument constructor (line ~30) signature from:

```csharp
    public CreateRootDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences)
```

to:

```csharp
    public CreateRootDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences, ILoadingOverlay? overlay = null)
```

In that constructor body, after `_preferences = preferences;`, add:

```csharp
        _overlay = overlay;
```

(The single-argument constructor `: this(forge, null)` continues to work — `overlay` defaults to null.)

- [ ] **Step 4: Wrap the forge call**

In `SubmitAsync`, change the opening line of the forge call from:

```csharp
            var stored = await _forge.ForgeAsync(new ForgeRequest(
```

to:

```csharp
            var request = new ForgeRequest(
```

Then change the closing line of that statement from:

```csharp
                HashAlgorithm: _hashAlgorithm));
```

to:

```csharp
                HashAlgorithm: _hashAlgorithm);
            var forge = _forge;
            var stored = await _overlay.RunOrDirectAsync("Forging Root Certificate…", () => forge.ForgeAsync(request));
```

(The arguments between those two lines are unchanged.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CreateRootDialogViewModelTests"`
Expected: PASS (all tests in the class, including the existing ones — they pass no overlay, so it runs directly).

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Presentation/CreateRootDialogViewModel.cs SelfCertForge.Core.Tests/CreateRootDialogViewModelTests.cs
git commit -m "feat(core): show loading overlay while forging root certificate"
```

---

## Task 6: Wire CreateSignedCertDialogViewModel (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/CreateSignedCertDialogViewModel.cs`
- Test: `SelfCertForge.Core.Tests/CreateSignedCertDialogViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `CreateSignedCertDialogViewModelTests.cs` (the file already uses the shared `FakeForgeService` and `FakeCert()` helpers). The VM seeds a default issuer ("New Root"), so setting `CommonName` makes `CanSubmit` true:

```csharp
[Fact]
public void Submit_RunsForgeThroughOverlay_WithCaption()
{
    var overlay = new FakeLoadingOverlay();
    var vm = new CreateSignedCertDialogViewModel(new FakeForgeService(_ => FakeCert()), preferences: null, overlay: overlay)
    {
        CommonName = "leaf.local"
    };

    ((System.Windows.Input.ICommand)vm.CreateCommand).Execute(null);

    overlay.Messages.Should().ContainSingle().Which.Should().Be("Forging Certificate…");
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CreateSignedCertDialogViewModelTests.Submit_RunsForgeThroughOverlay"`
Expected: FAIL — compile error, no 3-argument constructor.

- [ ] **Step 3: Add the overlay field and constructor parameter**

In `CreateSignedCertDialogViewModel.cs`, add the field next to `_forge`:

```csharp
    private readonly ILoadingOverlay? _overlay;
```

Change the two-argument constructor (line ~41) from:

```csharp
    public CreateSignedCertDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences)
```

to:

```csharp
    public CreateSignedCertDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences, ILoadingOverlay? overlay = null)
```

After `_preferences = preferences;` add:

```csharp
        _overlay = overlay;
```

- [ ] **Step 4: Wrap the forge call**

In `SubmitAsync` (line ~306), change the opening line from:

```csharp
            var stored = await _forge.ForgeAsync(new ForgeRequest(
```

to:

```csharp
            var request = new ForgeRequest(
```

Find the closing line of that `new ForgeRequest(...)` statement — it ends with `));`. Change that `));` to `);`, then on the following lines add:

```csharp
            var forge = _forge;
            var stored = await _overlay.RunOrDirectAsync("Forging Certificate…", () => forge.ForgeAsync(request));
```

(The `new ForgeRequest(...)` arguments themselves are unchanged.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CreateSignedCertDialogViewModelTests"`
Expected: PASS (all tests in the class).

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Presentation/CreateSignedCertDialogViewModel.cs SelfCertForge.Core.Tests/CreateSignedCertDialogViewModelTests.cs
git commit -m "feat(core): show loading overlay while forging signed certificate"
```

---

## Task 7: Wire CreateFromCsrDialogViewModel (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs`
- Test: `SelfCertForge.Core.Tests/CreateFromCsrDialogViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `CreateFromCsrDialogViewModelTests.cs`. This VM is submitted in tests via the internal `SubmitAsyncForTest()` hook (not the command). Mirror the existing `Submit_calls_ForgeFromCsr_and_raises_Created` test's setup — the file defines a nested `FakeForge : IForgeService` and a `BasicSummary()` helper:

```csharp
[Fact]
public async Task Submit_RunsForgeThroughOverlay_WithCaption()
{
    var overlay = new FakeLoadingOverlay();
    var forge = new FakeForge();
    var vm = new CreateFromCsrDialogViewModel(forge, preferences: null, overlay: overlay);
    vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");

    await vm.SubmitAsyncForTest();

    overlay.Messages.Should().ContainSingle().Which.Should().Be("Signing CSR…");
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CreateFromCsrDialogViewModelTests.Submit_RunsForgeThroughOverlay"`
Expected: FAIL — compile error, the constructor does not accept an overlay.

- [ ] **Step 3: Add the overlay field and constructor parameter**

In `CreateFromCsrDialogViewModel.cs`, add the field next to `_forge`:

```csharp
    private readonly ILoadingOverlay? _overlay;
```

Change the constructor (line ~37) from:

```csharp
    public CreateFromCsrDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences = null)
```

to:

```csharp
    public CreateFromCsrDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences = null, ILoadingOverlay? overlay = null)
```

After `_preferences = preferences;` add:

```csharp
        _overlay = overlay;
```

- [ ] **Step 4: Wrap the forge call**

In the submit method (line ~282), replace this single line:

```csharp
            var stored = await _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(request));
```

with:

```csharp
            var forge = _forge;
            var csrRequest = new ForgeFromCsrRequest(request);
            var stored = await _overlay.RunOrDirectAsync("Signing CSR…", () => forge.ForgeFromCsrAsync(csrRequest));
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CreateFromCsrDialogViewModelTests"`
Expected: PASS (all tests in the class).

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs SelfCertForge.Core.Tests/CreateFromCsrDialogViewModelTests.cs
git commit -m "feat(core): show loading overlay while signing CSR"
```

---

## Task 8: Wire CertificatesViewModel exports (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/CertificatesViewModel.cs`
- Create: `SelfCertForge.Core.Tests/CertificatesViewModelExportTests.cs`

- [ ] **Step 1: Write the failing test**

There is no existing CertificatesViewModel export test, so create `SelfCertForge.Core.Tests/CertificatesViewModelExportTests.cs`. The repo defines a per-file `FakeStore` (see `AuthoritiesViewModelTests.cs` and `CertificateExportServiceTests.cs`); copy `FakeStore` plus its `Child(...)` certificate helper and the `NoOpExportService` double from `AuthoritiesViewModelTests.cs` into this new file. CertificatesViewModel lists non-root certificates, so seed a `Child` cert, load, and select it via `SelectById`:

```csharp
using System.Windows.Input;
using FluentAssertions;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public sealed class CertificatesViewModelExportTests
{
    [Fact]
    public async Task ExportDer_RunsThroughOverlay_WithCaption()
    {
        var overlay = new FakeLoadingOverlay();
        var store = new FakeStore(Child("c1", "r1"));
        var vm = new CertificatesViewModel(
            store,
            () => DateTimeOffset.UtcNow,
            exportService: new NoOpExportService(),
            folderPicker: new StubFolderPicker("/tmp/export"),
            loadingOverlay: overlay);
        await vm.LoadAsync();
        vm.SelectById("c1");

        ((ICommand)vm.ExportDerCommand).Execute(null);

        overlay.Messages.Should().ContainSingle().Which.Should().Be("Exporting Certificate…");
    }

    // Copy `FakeStore` + the `Child(...)` helper and `NoOpExportService`
    // verbatim from AuthoritiesViewModelTests.cs into this file.

    private sealed class StubFolderPicker : IFolderPicker
    {
        private readonly string _path;
        public StubFolderPicker(string path) => _path = path;
        public Task<string?> PickAsync(CancellationToken ct = default) => Task.FromResult<string?>(_path);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CertificatesViewModel"`
Expected: FAIL — compile error, the ctor has no `loadingOverlay` parameter.

- [ ] **Step 3: Add the overlay field and constructor parameters**

In `CertificatesViewModel.cs`, add the field next to `_trustChecker`:

```csharp
    private readonly ILoadingOverlay? _overlay;
```

Change the public ctor (line ~22-29) to add the parameter and forward it. Replace:

```csharp
    public CertificatesViewModel(
        ICertificateStore store,
        ICertificateExportService exportService,
        IFolderPicker folderPicker,
        IPfxPasswordDialog pfxPasswordDialog,
        IConfirmationDialog confirmationDialog,
        ITrustStoreChecker? trustChecker = null)
        : this(store, () => DateTimeOffset.UtcNow, exportService, folderPicker, pfxPasswordDialog, confirmationDialog, trustChecker) { }
```

with:

```csharp
    public CertificatesViewModel(
        ICertificateStore store,
        ICertificateExportService exportService,
        IFolderPicker folderPicker,
        IPfxPasswordDialog pfxPasswordDialog,
        IConfirmationDialog confirmationDialog,
        ITrustStoreChecker? trustChecker = null,
        ILoadingOverlay? loadingOverlay = null)
        : this(store, () => DateTimeOffset.UtcNow, exportService, folderPicker, pfxPasswordDialog, confirmationDialog, trustChecker, loadingOverlay) { }
```

Change the internal ctor (line ~31) signature. Replace:

```csharp
    internal CertificatesViewModel(ICertificateStore store, Func<DateTimeOffset> now,
        ICertificateExportService? exportService = null,
        IFolderPicker? folderPicker = null,
        IPfxPasswordDialog? pfxPasswordDialog = null,
        IConfirmationDialog? confirmationDialog = null,
        ITrustStoreChecker? trustChecker = null)
```

with:

```csharp
    internal CertificatesViewModel(ICertificateStore store, Func<DateTimeOffset> now,
        ICertificateExportService? exportService = null,
        IFolderPicker? folderPicker = null,
        IPfxPasswordDialog? pfxPasswordDialog = null,
        IConfirmationDialog? confirmationDialog = null,
        ITrustStoreChecker? trustChecker = null,
        ILoadingOverlay? loadingOverlay = null)
```

In the internal ctor body, after `_trustChecker = trustChecker;`, add:

```csharp
        _overlay = loadingOverlay;
```

- [ ] **Step 4: Wrap the four export calls**

Replace the last line of `ExportKeyPemAsync`:

```csharp
        await _exportService.ExportKeyPemAsync(_selectedRow.Source, folder);
```

with:

```csharp
        var source = _selectedRow.Source;
        var export = _exportService;
        await _overlay.RunOrDirectAsync("Exporting Private Key…", () => export.ExportKeyPemAsync(source, folder));
```

Replace the last line of `ExportPfxAsync`:

```csharp
        await _exportService.ExportPfxAsync(_selectedRow.Source, folder, password);
```

with:

```csharp
        var source = _selectedRow.Source;
        var export = _exportService;
        await _overlay.RunOrDirectAsync("Exporting PFX…", () => export.ExportPfxAsync(source, folder, password));
```

Replace the last line of `ExportDerAsync`:

```csharp
        await _exportService.ExportDerAsync(_selectedRow.Source, folder);
```

with:

```csharp
        var source = _selectedRow.Source;
        var export = _exportService;
        await _overlay.RunOrDirectAsync("Exporting Certificate…", () => export.ExportDerAsync(source, folder));
```

Replace the last line of `ExportP7bAsync`:

```csharp
        await _exportService.ExportP7bAsync(_selectedRow.Source, folder);
```

with:

```csharp
        var source = _selectedRow.Source;
        var export = _exportService;
        await _overlay.RunOrDirectAsync("Exporting Certificate Chain…", () => export.ExportP7bAsync(source, folder));
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~CertificatesViewModel"`
Expected: PASS (new test plus all existing CertificatesViewModel tests).

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Presentation/CertificatesViewModel.cs SelfCertForge.Core.Tests/
git commit -m "feat(core): show loading overlay during certificate exports"
```

---

## Task 9: Wire AuthoritiesViewModel exports (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs`
- Test: `SelfCertForge.Core.Tests/AuthoritiesViewModelTests.cs`

- [ ] **Step 1: Write the failing test**

`AuthoritiesViewModelTests.cs` already defines `FakeStore`, the `Root(...)` helper, `NoOpExportService`, and the other `NoOp*` doubles. The existing `NoOpFolderPicker` returns `null` (which makes export return early), so add a `StubFolderPicker` that returns a real path. Add this test:

```csharp
[Fact]
public void ExportDer_RunsThroughOverlay_WithCaption()
{
    var overlay = new FakeLoadingOverlay();
    var store = new FakeStore(Root("r1"));
    var vm = new AuthoritiesViewModel(
        store,
        new NoOpCreateRootDialog(),
        new NoOpCreateSignedCertDialog(),
        new NoOpNavigationService(),
        new NoOpExportService(),
        new StubFolderPicker("/tmp/export"),
        new NoOpPfxPasswordDialog(),
        new NoOpConfirmationDialog(),
        loadingOverlay: overlay);
    vm.SelectedRow = vm.Rows.Single(r => r.Id == "r1");

    ((System.Windows.Input.ICommand)vm.ExportDerCommand).Execute(null);

    overlay.Messages.Should().ContainSingle().Which.Should().Be("Exporting Certificate…");
}
```

Add this nested double to the test class (mirrors the existing `NoOpFolderPicker` but returns a path):

```csharp
private sealed class StubFolderPicker : IFolderPicker
{
    private readonly string _path;
    public StubFolderPicker(string path) => _path = path;
    public Task<string?> PickAsync(CancellationToken ct = default) => Task.FromResult<string?>(_path);
}
```

(If `SelectedRow` has no public setter, select `r1` using the same mechanism the other tests in this file use.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~AuthoritiesViewModel"`
Expected: FAIL — compile error, the ctor has no `loadingOverlay` parameter.

- [ ] **Step 3: Add the overlay field and constructor parameter**

In `AuthoritiesViewModel.cs`, add the field next to `_workflow`:

```csharp
    private readonly ILoadingOverlay? _overlay;
```

Change the constructor (line ~28). Replace the final parameter line:

```csharp
        ICertificateWorkflowService? workflow = null)
```

with:

```csharp
        ICertificateWorkflowService? workflow = null,
        ILoadingOverlay? loadingOverlay = null)
```

In the ctor body, after `_workflow = workflow;`, add:

```csharp
        _overlay = loadingOverlay;
```

- [ ] **Step 4: Wrap the four export calls**

Apply the exact same four replacements as Task 8 Step 4 (the `ExportKeyPemAsync` / `ExportPfxAsync` / `ExportDerAsync` / `ExportP7bAsync` bodies in `AuthoritiesViewModel.cs` are identical to those in `CertificatesViewModel.cs`):

- `ExportKeyPemAsync` final line → capture `source`/`export` locals, `await _overlay.RunOrDirectAsync("Exporting Private Key…", () => export.ExportKeyPemAsync(source, folder));`
- `ExportPfxAsync` final line → `await _overlay.RunOrDirectAsync("Exporting PFX…", () => export.ExportPfxAsync(source, folder, password));`
- `ExportDerAsync` final line → `await _overlay.RunOrDirectAsync("Exporting Certificate…", () => export.ExportDerAsync(source, folder));`
- `ExportP7bAsync` final line → `await _overlay.RunOrDirectAsync("Exporting Certificate Chain…", () => export.ExportP7bAsync(source, folder));`

The full before/after for each is identical to Task 8 Step 4.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter "FullyQualifiedName~AuthoritiesViewModel"`
Expected: PASS (new test plus all existing AuthoritiesViewModel tests).

- [ ] **Step 6: Run the full Core test suite**

Run: `make test`
Expected: PASS — all tests, no regressions.

- [ ] **Step 7: Commit**

```bash
git add SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs SelfCertForge.Core.Tests/
git commit -m "feat(core): show loading overlay during authority exports"
```

---

## Task 10: LoadingOverlayContent (App popup content)

**Visual values note:** invoke the `selfcertforge-design` skill to confirm the scrim color, card surface, corner radius, caption font size/color, and animation dimensions. The values below are reasonable defaults derived from the design system's Forge Black surface (`#0B0C10`) and Inter type — replace with the skill's exact tokens.

**Files:**
- Create: `SelfCertForge.App/Controls/LoadingOverlayContent.xaml`
- Create: `SelfCertForge.App/Controls/LoadingOverlayContent.xaml.cs`

- [ ] **Step 1: Create the XAML**

`SelfCertForge.App/Controls/LoadingOverlayContent.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:skia="clr-namespace:SkiaSharp.Extended.UI.Controls;assembly=SkiaSharp.Extended.UI"
             x:Class="SelfCertForge.App.Controls.LoadingOverlayContent">
    <Border BackgroundColor="#0B0C10"
            Stroke="#22FFFFFF"
            StrokeThickness="1"
            StrokeShape="RoundRectangle 16"
            Padding="32"
            HorizontalOptions="Center"
            VerticalOptions="Center">
        <VerticalStackLayout Spacing="16" HorizontalOptions="Center">
            <skia:SKLottieView x:Name="Animation"
                               Source="loading.json"
                               RepeatCount="-1"
                               RepeatMode="Restart"
                               WidthRequest="112"
                               HeightRequest="112"
                               HorizontalOptions="Center" />
            <Label x:Name="MessageLabel"
                   FontFamily="Inter"
                   FontSize="15"
                   TextColor="#FFFFFF"
                   HorizontalOptions="Center"
                   HorizontalTextAlignment="Center" />
        </VerticalStackLayout>
    </Border>
</ContentView>
```

- [ ] **Step 2: Create the code-behind with a Message property**

`SelfCertForge.App/Controls/LoadingOverlayContent.xaml.cs`:

```csharp
namespace SelfCertForge.App.Controls;

public partial class LoadingOverlayContent : ContentView
{
    public LoadingOverlayContent()
    {
        InitializeComponent();
    }

    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            if (MessageLabel is not null)
                MessageLabel.Text = value;
        }
    }
}
```

- [ ] **Step 3: Build to confirm XAML + control compile**

Run: `make build`
Expected: build succeeds (the `skia:` namespace resolves, `SKLottieView` is recognized).

- [ ] **Step 4: Commit**

```bash
git add SelfCertForge.App/Controls/LoadingOverlayContent.xaml SelfCertForge.App/Controls/LoadingOverlayContent.xaml.cs
git commit -m "feat(app): add loading overlay content view (Lottie + caption)"
```

---

## Task 11: MauiLoadingOverlay adapter + registration

**Files:**
- Create: `SelfCertForge.App/Services/MauiLoadingOverlay.cs`
- Modify: `SelfCertForge.App/MauiProgram.cs`

- [ ] **Step 1: Create the adapter**

`SelfCertForge.App/Services/MauiLoadingOverlay.cs`:

```csharp
using CommunityToolkit.Maui.Views;
using SelfCertForge.App.Controls;
using SelfCertForge.Core.Abstractions;

namespace SelfCertForge.App.Services;

public sealed class MauiLoadingOverlay : ILoadingOverlay
{
    private static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MinVisible = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private int _depth;
    private string _message = string.Empty;
    private LoadingOverlayContent? _content;
    private Page? _hostPage;
    private DateTime _shownAtUtc;

    public Task RunAsync(string message, Func<Task> operation)
        => RunAsync(message, async () => { await operation(); return true; });

    public async Task<T> RunAsync<T>(string message, Func<Task<T>> operation)
    {
        await EnterAsync(message);
        try
        {
            return await operation();
        }
        finally
        {
            await ExitAsync();
        }
    }

    private async Task EnterAsync(string message)
    {
        bool firstEntry;
        lock (_gate)
        {
            _depth++;
            _message = message;
            firstEntry = _depth == 1;
        }

        if (!firstEntry)
        {
            // Already visible (or pending): just update the caption.
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_content is not null)
                    _content.Message = message;
            });
            return;
        }

        // Anti-flicker: defer the show; skip it if the work already finished.
        await Task.Delay(ShowDelay);
        lock (_gate)
        {
            if (_depth == 0 || _content is not null)
                return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            lock (_gate)
            {
                if (_depth == 0 || _content is not null)
                    return;
            }

            var page = Application.Current?.Windows is { Count: > 0 } windows
                ? windows[0].Page
                : null;
            if (page is null)
                return;

            _hostPage = page;
            _content = new LoadingOverlayContent { Message = _message };
            _shownAtUtc = DateTime.UtcNow;

            _ = page.ShowPopupAsync(_content, new PopupOptions
            {
                CanBeDismissedByTappingOutsideOfPopup = false,
                Shape = null,
                PageOverlayColor = Color.FromRgba(0, 0, 0, 0.55),
            });
        });
    }

    private async Task ExitAsync()
    {
        bool lastExit;
        lock (_gate)
        {
            _depth--;
            lastExit = _depth == 0;
        }

        if (!lastExit)
            return;

        // Honor minimum visible time if the popup actually appeared.
        DateTime shownAt;
        bool isShown;
        lock (_gate)
        {
            isShown = _content is not null;
            shownAt = _shownAtUtc;
        }

        if (isShown)
        {
            var elapsed = DateTime.UtcNow - shownAt;
            if (elapsed < MinVisible)
                await Task.Delay(MinVisible - elapsed);
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            lock (_gate)
            {
                if (_depth != 0)
                    return; // re-entered during the delay; keep it visible
            }

            if (_content is not null && _hostPage is not null)
            {
                await _hostPage.ClosePopupAsync();
                _content = null;
                _hostPage = null;
            }
        });
    }
}
```

- [ ] **Step 2: Register the service in MauiProgram**

In `SelfCertForge.App/MauiProgram.cs`, add alongside the other App-service singletons (e.g. near `AddSingleton<IConfirmationDialog, MauiConfirmationDialog>()` / the dialog host registrations):

```csharp
builder.Services.AddSingleton<ILoadingOverlay, MauiLoadingOverlay>();
```

- [ ] **Step 3: Build to confirm the adapter compiles against CommunityToolkit.Maui v13**

Run: `make build`
Expected: build succeeds. If `ShowPopupAsync` / `ClosePopupAsync` / `PopupOptions` signatures differ from the CommunityToolkit.Maui 13.0.0 API, adjust the calls to match the installed package's `CommunityToolkit.Maui.Views` API (the show takes the content view + `PopupOptions`; the close is the page-level extension that dismisses the current popup), keeping the show fire-and-forget and the close awaited.

- [ ] **Step 4: Commit**

```bash
git add SelfCertForge.App/Services/MauiLoadingOverlay.cs SelfCertForge.App/MauiProgram.cs
git commit -m "feat(app): implement MauiLoadingOverlay popup adapter"
```

---

## Task 12: Inject ILoadingOverlay into the ViewModels (DI)

**Files:**
- Modify: `SelfCertForge.App/MauiProgram.cs`

- [ ] **Step 1: Pass the overlay to each ViewModel registration**

In `MauiProgram.cs`, update the five ViewModel registrations to resolve and pass `ILoadingOverlay`:

For `CreateRootDialogViewModel` — append `sp.GetRequiredService<ILoadingOverlay>()` as the third argument:

```csharp
new CreateRootDialogViewModel(
    sp.GetRequiredService<IForgeService>(),
    sp.GetRequiredService<IUserPreferencesStore>(),
    sp.GetRequiredService<ILoadingOverlay>())
```

For `CreateSignedCertDialogViewModel` — append as third argument:

```csharp
new CreateSignedCertDialogViewModel(
    sp.GetRequiredService<IForgeService>(),
    sp.GetRequiredService<IUserPreferencesStore>(),
    sp.GetRequiredService<ILoadingOverlay>())
```

For `CreateFromCsrDialogViewModel` — locate its registration and append `sp.GetRequiredService<ILoadingOverlay>()` as the final argument (after the preferences argument).

For `CertificatesViewModel` — append `sp.GetRequiredService<ILoadingOverlay>()` as the new final argument (after `trustChecker`).

For `AuthoritiesViewModel` — append `sp.GetRequiredService<ILoadingOverlay>()` as the new final argument (after `workflow`):

```csharp
new AuthoritiesViewModel(
    sp.GetRequiredService<ICertificateStore>(),
    sp.GetRequiredService<ICreateRootDialog>(),
    sp.GetRequiredService<ICreateSignedCertDialog>(),
    sp.GetRequiredService<INavigationService>(),
    sp.GetRequiredService<ICertificateExportService>(),
    sp.GetRequiredService<IFolderPicker>(),
    sp.GetRequiredService<IPfxPasswordDialog>(),
    sp.GetRequiredService<IConfirmationDialog>(),
    sp.GetRequiredService<ITrustStoreChecker>(),
    sp.GetRequiredService<ICreateFromCsrDialog>(),
    sp.GetRequiredService<ICsrFilePicker>(),
    sp.GetRequiredService<ICertificateWorkflowService>(),
    sp.GetRequiredService<ILoadingOverlay>())
```

- [ ] **Step 2: Build**

Run: `make build`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add SelfCertForge.App/MauiProgram.cs
git commit -m "feat(app): inject loading overlay into certificate ViewModels"
```

---

## Task 13: Final verification (build + tests + manual render)

**Files:** none (verification only)

- [ ] **Step 1: Full build**

Run: `make rebuild`
Expected: clean build succeeds for the current OS TFM.

- [ ] **Step 2: Full test suite**

Run: `make test`
Expected: PASS — all tests, no regressions.

- [ ] **Step 3: Manual render verification (run the app)**

Run: `make run`
Then exercise each wrapped flow and confirm visually:
- Create a root certificate with a 4096-bit key → the dimmed overlay appears with the spinning brand animation and caption "Forging Root Certificate…", then dismisses when the cert appears.
- Sign a CSR → caption "Signing CSR…".
- Export a certificate as PFX → after the password prompt and folder picker, the overlay shows "Exporting PFX…".
- Confirm the scrim blocks input (clicking outside does not dismiss it) and that fast exports do not flash the overlay (anti-flicker).

Confirm the same on Mac Catalyst; if building on Windows, repeat there. Note any platform difference in scrim/centering/transparency.

- [ ] **Step 4: Final commit (only if Task 10 visual values were adjusted per the design skill)**

```bash
git add SelfCertForge.App/Controls/LoadingOverlayContent.xaml
git commit -m "style(app): align loading overlay with design system"
```
