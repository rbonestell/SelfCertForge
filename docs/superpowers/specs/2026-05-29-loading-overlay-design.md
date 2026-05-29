# Loading Overlay (Lottie) — Design

**Date:** 2026-05-29
**Status:** Approved (brainstorming) — awaiting implementation plan

## Summary

Add a reusable, app-wide loading overlay: a dimmed lightbox that presents the SelfCertForge brand animation plus a caller-supplied caption (e.g. "Forging Root Certificate…", "Exporting PFX…") while a background operation runs. ViewModels invoke it through a single Core abstraction, `ILoadingOverlay.RunAsync(message, operation)`, which shows the overlay before the work and hides it afterward in a `finally`, so it can never be left stuck on screen. The animation is the brand icon exported from SVGator as Lottie JSON and rendered natively with `SKLottieView` (SkiaSharp.Extended.UI.Maui) — no WebView.

## Goals

- One reusable overlay reachable from any ViewModel via a testable Core abstraction.
- Guaranteed show/hide semantics: a thrown operation always hides the overlay and propagates the exception unchanged.
- The brand animation renders natively (Skia), with a transparent background, over a dimmed scrim that blocks input.
- A caption string supplied per call, displayed beneath the animation.
- No visible flicker for fast operations; no strobing for ones that finish just after the overlay appears.
- Core unit tests remain MAUI-free.
- Functionally and aesthetically equivalent on macCatalyst and Windows.

## Non-goals

- A cancel button / cooperative cancellation of in-flight operations. The forge and export operations are not cleanly cancellable; revisit later.
- Determinate progress (percent / progress bar). The overlay is indeterminate.
- Mid-operation caption updates (the `RunAsync` wrapper sets the caption once). Revisit if a multi-phase operation needs it.
- Wrapping instantaneous operations such as CSR file read/parse (anti-flicker would suppress the overlay anyway).
- Replacing the OS splash screen or animating the launch sequence. The native splash stays a static image.
- Re-theming or relocating the existing static brand mark in the Shell header.

## Rendering rationale

The brand animation exists in two forms. The **animated SVG** drives its animation with an embedded JavaScript player (`__SVGATOR_PLAYER__`); MAUI's image/SVG pipeline (SkiaSharp) renders SVG statically and never executes embedded scripts, so an animated SVG would only ever show a frozen first frame in an `<Image>`. Running it as-is would require hosting it in a WebView (a full browser engine for a small looping icon).

The **Lottie JSON** export (`icon-new.json`) is pure vector shape data a native player can animate directly. `SKLottieView` plays it on the same Skia engine MAUI already uses for SVG rasterization — lightweight, GPU-rendered, transparent background, with loop/play control. This is the idiomatic MAUI approach and avoids a WebView. WebView remains the documented fallback only if SkiaSharp cannot be made to build/run on net10 (see Risks).

The Lottie export is visually clean: its only SVGator references are invisible JSON metadata (`meta`, `metadata.customProps`), not rendered layers. There is no watermark layer to remove. The metadata block may optionally be stripped for tidiness.

### Package

- `SkiaSharp.Extended.UI.Maui` **3.0.0** (stable). Targets net9.0 platform TFMs; depends on SkiaSharp 3.119.1 and Microsoft.Maui.Controls 9.0.82.
- A net10.0-maccatalyst / net10.0-windows app consumes the net9 platform assets via standard backward-compatibility; the transitive MAUI dependency unifies up to the project's 10.x. No net10-native build of the package exists yet, so a restore/build smoke-test is the first implementation step.

## Architecture

The three-layer split is preserved. The design mirrors the existing dialog-abstraction pattern (`IConfirmationDialog` → `MauiConfirmationDialog`).

| Piece | Layer | Responsibility |
|---|---|---|
| `ILoadingOverlay` | `Core/Abstractions` | The `RunAsync` contract. No MAUI types. |
| `MauiLoadingOverlay` | `App/Services` | Implements `ILoadingOverlay`. Marshals to the main thread, ref-counts concurrent calls, owns anti-flicker timing, shows/closes the popup. |
| `LoadingOverlayPopup` | `App/Controls` | A `CommunityToolkit.Maui.Views.Popup`: dim scrim + centered `SKLottieView` + caption `Label`. Non-dismissable. |
| `loading.json` | `App/Resources/Raw` | The Lottie asset (`MauiAsset`), copied from `icon-new.json`. |

### `SelfCertForge.Core` — additions

`Abstractions/ILoadingOverlay.cs`:

```csharp
namespace SelfCertForge.Core.Abstractions;

public interface ILoadingOverlay
{
    Task RunAsync(string message, Func<Task> operation);
    Task<T> RunAsync<T>(string message, Func<Task<T>> operation);
}
```

Call-site shape in a ViewModel:

```csharp
await _overlay.RunAsync("Forging Root Certificate…",
    () => _forge.ForgeAsync(request));
```

ViewModels receive `ILoadingOverlay?` as a **nullable, optional constructor parameter** (same convention as `IConfirmationDialog?`). When null, the ViewModel runs the operation directly without an overlay, keeping `Core.Tests` free of MAUI.

### `SelfCertForge.App` — additions

`Services/MauiLoadingOverlay.cs` implements the contract:

- **Stuck-proof:** `RunAsync` is `try { await ShowIfStillRunning() } finally { await Hide() }` around `await operation()`; the operation's result/exception passes through untouched.
- **Anti-flicker:** show is deferred by `ShowDelay` (~150 ms). If the operation completes first, the overlay never appears. Once shown, it stays visible at least `MinVisible` (~500 ms) before hiding. Both are tunable constants.
- **Concurrency:** a reference count guards a single popup instance. Overlapping `RunAsync` calls increment the count; the most recent caption wins; the popup closes only when the count returns to zero. No duplicate popups, no orphaned overlay.
- **Threading:** all popup show/close and caption mutations run via `MainThread.InvokeOnMainThreadAsync`. The supplied operation is awaited as provided (it may already hop threads internally).

`Controls/LoadingOverlayPopup.xaml(.cs)`: a `Popup` with `CanBeDismissedByTappingOutsideOfPopup = false`, a semi-transparent scrim, a centered `SKLottieView` (`RepeatCount` infinite, autoplay) bound to `loading.json`, and a caption `Label` bound to a `Message` property.

`MauiProgram.cs`:

- `.UseSkiaSharp()` registration (and any additional SkiaSharp.Extended.UI handler init the package requires — confirmed at implementation).
- `builder.Services.AddSingleton<ILoadingOverlay, MauiLoadingOverlay>();`
- `loading.json` added under `Resources/Raw` (already covered by the existing `MauiAsset Include="Resources\Raw\**"` glob).

## Integration points (scope)

Wrap the user-perceptible operations. Anti-flicker makes wrapping the fast ones harmless.

**Forge (slow — key generation + signing):**

- `CreateRootDialogViewModel` (forge call) → "Forging Root Certificate…"
- `CreateSignedCertDialogViewModel` (forge call) → "Forging Certificate…"
- `CreateFromCsrDialogViewModel` (forge-from-CSR call) → "Signing CSR…"

**Exports — the four export commands on both `CertificatesViewModel` and `AuthoritiesViewModel`:**

- DER certificate export → "Exporting Certificate…"
- PEM private-key export → "Exporting Private Key…"
- PFX export → "Exporting PFX…"
- P7B chain export → "Exporting Certificate Chain…"

**Out of scope for v1:** CSR file read/parse (effectively instant).

Captions are passed literally at each call site (no shared catalog needed yet).

## Visual treatment

Scrim color/opacity, caption typography (Inter family, per the design system), spacing, and animation dimensions (~96–120 px square) are taken from the `selfcertforge-design` skill, which is invoked during implementation. Values are not invented in this spec.

## Cross-platform parity

- macCatalyst and Windows must look and behave the same.
- Verify the CommunityToolkit.Maui (v13) `Popup` scrim, centering, and non-dismissable behavior on Mac Catalyst specifically.
- Verify `SKLottieView` renders with a transparent background on both TFMs.

## Testing strategy

- **Core:** a `FakeLoadingOverlay : ILoadingOverlay` whose `RunAsync` simply invokes the operation and records the caption. ViewModel tests assert that the wrapped commands call `RunAsync` with the expected caption and that the operation's result and exceptions propagate unchanged. No rendering is exercised in Core — the test boundary holds.
- **Null-overlay path:** ViewModel tests with `ILoadingOverlay` = null confirm operations still run.
- **Build smoke-test (first implementation step):** restore + build `net10.0-maccatalyst` and `net10.0-windows` with the SkiaSharp package; launch and visually confirm the scrim + animation + caption on Mac Catalyst. No tests assert rendering.

## Risks & verification-first steps

1. **SkiaSharp on net10:** package is net9-targeted. If restore/build/runtime fails on net10, fall back to hosting the de-watermarked animated SVG in a WebView/HybridWebView (rendering rationale unchanged; only the App adapter differs). Decide before building the rest.
2. **CommunityToolkit.Maui v13 Popup API:** the Popup type/show API changed across CT.Maui versions; confirm the exact `ShowPopupAsync`/`CloseAsync` usage and the non-dismissable + scrim configuration against v13 before wiring the adapter.
3. **`SKLottieView` initialization:** confirm the exact handler/registration call the 3.0.0 package needs in `MauiProgram` (beyond `.UseSkiaSharp()`).

## Open follow-ups (not blocking)

- Cooperative cancellation with a cancel affordance.
- Determinate progress for long multi-step operations.
- Mid-operation caption updates (would add an `IAsyncDisposable Show(message)` overload with an `Update` method).
- Wrapping CSR read/parse if it ever proves slow.
- Strip the `meta`/`metadata` block from `loading.json` for tidiness.
