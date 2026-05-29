# Loading lightbox in the self-update flow

**Date:** 2026-05-29
**Status:** Approved design, pending implementation

## Summary

Replace the inline progress indicators in the "Download & Install" step of the
self-update flow with the existing loading lightbox modal (`ILoadingOverlay` →
`MauiLoadingOverlay` + `LoadingOverlayContent`). The modal shows the brand
animation plus a single message that moves through two phases —
"Downloading Update…" then "Installing Update…" — with no percentage and no
progress bar. The quick "Check for updates" action is unchanged.

## Decisions

- **Scope:** lightbox covers **Download + Apply** only. "Check for updates"
  keeps its inline button-disable + outcome label.
- **Progress:** **indeterminate** — animation + message only. No percentage,
  no progress bar in the modal.
- **Mechanism:** reuse the overlay's existing nested-call message update
  (re-entering `RunAsync` while shown updates the live message rather than
  opening a second popup). **No new `ILoadingOverlay` API.**

## Behavior / data flow

`SettingsViewModel.DownloadAndInstallAsync` is rewritten to run the work behind
the lightbox:

```csharp
private async Task DownloadAndInstallAsync()
{
    if (AvailableUpdate is null || IsDownloading) return;
    IsDownloading = true;
    UpdateStatusMessage = null;          // clear any prior outcome text
    try
    {
        await _overlay.RunOrDirectAsync("Downloading Update…", async () =>
        {
            await _updateService.DownloadUpdateAsync(AvailableUpdate);   // progress arg omitted (optional)
            await _overlay.RunOrDirectAsync("Installing Update…", () =>
                _updateService.ApplyUpdateAndRestartAsync(AvailableUpdate));
        });
    }
    catch { UpdateStatusMessage = "Download failed. Please try again."; }
    finally { IsDownloading = false; }
}
```

- On success the inner phase ends in the app restart, so the modal stays up
  through the hand-off — no flash of the Settings page.
- The nested call swaps the live message instantly (the inner enter takes the
  `!firstEntry` path: no show-delay, no min-visible gate), so there is no
  close/reopen flicker between phases.
- `RunOrDirectAsync` runs the operation directly when the overlay is null,
  keeping `SettingsViewModel` testable in Core without MAUI.

## Wiring

Mirrors how every other ViewModel receives the overlay.

- `SelfCertForge.Core/Presentation/SettingsViewModel.cs`
  - Add `ILoadingOverlay? loadingOverlay = null` as the **last** constructor
    parameter on the full constructor; store it in an `_overlay` field.
  - The single-argument convenience constructor is unaffected (the new
    parameter is optional).
- `SelfCertForge.App/MauiProgram.cs` (~line 106, the `SettingsViewModel`
  registration): append `sp.GetRequiredService<ILoadingOverlay>()` as the final
  constructor argument. `ILoadingOverlay` is already registered
  (`AddSingleton<ILoadingOverlay, MauiLoadingOverlay>()`).

## Replacing existing indicators

**Remove:**
- The determinate `<ProgressBar>` in `SelfCertForge.App/Pages/SettingsView.xaml`
  (the block bound to `DownloadProgressNormalized` / `IsDownloading`, ~lines
  536–539).
- `DownloadProgress` property, `DownloadProgressNormalized` property, the
  `_downloadProgress` field, and the per-percent `Progress<int>` reporter in
  `SettingsViewModel`.

**Keep:**
- `IsDownloading` — still hides the "Download & Install" button and guards
  against re-entry.
- `UpdateStatusMessage` / `HasUpdateStatusMessage` and its `Label` — now used
  **only for outcomes**: "Version X is available.", "You're on the latest
  version.", "Update check failed. Check your internet connection.", and
  "Download failed. Please try again."

Clean split: **modal = work in progress, label = outcome.**

## Copy

Title-Case gerund + ellipsis, matching the sibling overlay strings already in
the app ("Forging Root Certificate…", "Exporting PFX…", "Signing CSR…"):

- Phase 1: `Downloading Update…`
- Phase 2: `Installing Update…`

No emoji, no percentage (per the brand design rules and the indeterminate
decision). The download-failure label keeps its existing wording.

## Testing

Core tests only (the boundary is null-safe — `RunOrDirectAsync` runs directly
when the overlay is null).

- **New:** `DownloadAndInstall_ShowsDownloadingThenInstalling` — construct
  `SettingsViewModel` with a `FakeLoadingOverlay`, execute the command, assert
  `overlay.Messages` equals `["Downloading Update…", "Installing Update…"]` and
  that the fake update service's apply-and-restart was invoked.
- **Update:** delete `DownloadProgressNormalized_ReflectsDownloadProgress`
  (property removed); trim the test fake update service's now-unused
  `DownloadProgressToReport`.
- **Keep:** `DownloadAndInstall_WhenDownloadFails_ResetsIsDownloading` — still
  valid (`IsDownloading == false`, label contains "failed"). On the failure
  path the modal closes via the overlay's exit logic and the outcome surfaces
  in the label.
- `make test` green. No assertions on README content or static strings beyond
  behavior.

## Out of scope (YAGNI)

- No progress bar or percentage in the modal.
- No new `ILoadingOverlay` API surface.
- No change to the "Check for updates" path.
- No Windows/macCatalyst-specific handling — the existing
  `VelopackUpdateService` behavior is untouched.
