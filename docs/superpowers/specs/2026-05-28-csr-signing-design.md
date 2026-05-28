# CSR Signing — Design

**Date:** 2026-05-28
**Status:** Approved (brainstorming) — awaiting implementation plan

## Summary

Add a new "Create From Certificate Signing Request" action to the Authorities view detail panel, next to the existing "Create Signed Certificate" button. The action lets an operator pick a `.csr` file, validates it, opens a signing dialog populated from the CSR (Subject and public key locked, validity and SANs editable, KU/EKU/hash honored when present in the CSR), and issues a certificate signed by the selected CA. The issued certificate appears on the Certificates page with a "From CSR" badge and no private key on disk.

## Goals

- Operators can sign externally-generated CSRs using any CA managed by SelfCertForge.
- Validation rejects malformed CSRs, invalid proof-of-possession signatures, non-RSA keys, RSA keys below 2048 bits, and empty/malformed Subject DNs, surfacing a single explanatory alert.
- Issued CSR-signed certs participate in the same store, export, and activity-log surfaces as keypair-generated certs.
- Functionally and aesthetically identical on macCatalyst and Windows.

## Non-goals

- Non-RSA CSRs (ECDSA, Ed25519). Reject for now; revisit when the workflow supports non-RSA leaves generally.
- Save-as-at-signing-time UX. Operator exports from the Certificates page using the existing flow.
- Editing the Subject DN at signing time. Subject is bound to the CSR.
- Importing a private key for a CSR-signed cert after the fact.
- Public CA / WebPKI policy enforcement (Baseline Requirements, domain validation).

## PKI rationale

The CA controls cert properties (validity, KU, EKU, hash, signature, AKI, AIA/CRL URLs). The CSR controls the bound identity material (public key, Subject). Per private-CA convention (ADCS, EJBCA, smallstep), the issuer may filter or extend the CSR's requested SANs at sign time. This design encodes that split:

- **Locked to CSR**: public key, Subject DN. Editing either would break the trust relationship the CSR establishes.
- **Honored when present, otherwise editable**: KU and EKU from the CSR's `extensionRequest`. Operator can set them when the CSR makes no request.
- **Editable, prefilled from CSR**: SANs. CSR-requested SANs appear pre-populated and tagged; operator can remove them or add new ones.
- **CA-only**: validity, signature hash, serial, AKI.

## Architecture

Three-layer split is preserved.

### `SelfCertForge.Core` — additions

**Models** (`Models/`):

```csharp
public sealed record CsrInspectionResult(
    bool IsValid,
    CsrSummary? Summary,
    IReadOnlyList<CsrValidationError> Errors);

public enum CsrValidationError
{
    Malformed,
    InvalidProofOfPossession,
    UnsupportedKeyAlgorithm,
    KeyTooSmall,
    SubjectDnEmptyOrMalformed
}

public sealed record CsrSummary(
    string SubjectDistinguishedName,
    string PublicKeyAlgorithm,            // "RSA"
    int PublicKeyBits,
    string PublicKeyFingerprintSha256,    // hex
    IReadOnlyList<string> RequestedSans,  // SANs are strings, matches StoredCertificate.Sans
    CsrRequestedKeyUsages? RequestedKeyUsage,  // null when CSR did not request KU
    CsrRequestedEkus? RequestedEku,            // null when CSR did not request EKU
    string RawCsrPem);

// Mirrors the boolean shape used by ForgeRequest / SignedCertificateRequest.
public sealed record CsrRequestedKeyUsages(
    bool DigitalSignature,
    bool NonRepudiation,
    bool KeyEncipherment,
    bool DataEncipherment,
    bool KeyAgreement,
    bool KeyCertSign,
    bool CrlSign);

public sealed record CsrRequestedEkus(
    bool ServerAuth,
    bool ClientAuth,
    bool CodeSigning,
    bool EmailProtection,
    bool TimeStamping);

public sealed record CsrSigningRequest(
    string SigningAuthorityId,             // string id, matches existing ForgeRequest convention
    string RawCsrPem,
    int ValidityDays,
    IReadOnlyList<CsrSignedSanEntry> Sans,
    bool KeyUsageDigitalSignature,
    bool KeyUsageNonRepudiation,
    bool KeyUsageKeyEncipherment,
    bool KeyUsageDataEncipherment,
    bool KeyUsageKeyAgreement,
    bool KeyUsageKeyCertSign,
    bool KeyUsageCrlSign,
    bool EkuServerAuth,
    bool EkuClientAuth,
    bool EkuCodeSigning,
    bool EkuEmailProtection,
    bool EkuTimeStamping,
    HashAlgorithmKind SignatureHashAlgorithm);

public sealed record CsrSignedSanEntry(string Value, CsrSignedSanOrigin Origin);

public enum CsrSignedSanOrigin { FromCsr, AddedByOperator }

public sealed record CsrFilePickResult(string FilePath, string Contents);

public sealed record ForgeFromCsrRequest(
    string SigningAuthorityId,
    CsrSigningRequest SigningRequest,
    string SourceCsrFilename);
```

**Abstractions** (`Abstractions/`):

```csharp
public interface ICsrFilePicker
{
    Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct);
}

public interface ICreateFromCsrDialog
{
    Task<CsrSigningRequest?> ShowAsync(
        StoredAuthority signingAuthority,
        CsrSummary csrSummary,
        CancellationToken ct);
}
```

**ViewModel** (`Presentation/CreateFromCsrDialogViewModel.cs`):

- Seeded from `CsrSummary` at construction.
- Read-only properties: `SubjectDistinguishedName`, `PublicKeyDisplay` (e.g. "RSA 2048-bit"), `PublicKeyFingerprint`.
- Editable: `ValidityDays`, seeded from `IUserPreferencesStore.Current.SignedValidityDays` (existing fallback: 397). Same source as `CreateSignedCertDialogViewModel`.
- Editable observable collection of `SanEntryViewModel` items, each carrying `Origin`. `AddSanCommand` appends with `Origin = AddedByOperator` (and the existing "non-empty `_newSanValue`" gate). `RemoveSanCommand` removes regardless of origin.
- `KeyUsage` / `ExtendedKeyUsages` / `SignatureHashAlgorithm` exposed as editable, with companion `IsKeyUsageLocked`, `IsEkuLocked` flags. When the CSR carried KU/EKU in `extensionRequest`, the corresponding values are seeded from the CSR and the locked flags are `true` (XAML binds to disable the controls). Hash algorithm defaults to SHA-256 and is always editable (CSRs don't request a hash for the issued cert).
- `CanSubmit` gate mirrors `CreateSignedCertDialogViewModel.CanSubmit`: `!_isCreating && ValidityDays > 0 && !string.IsNullOrEmpty(SigningAuthorityId)`. No minimum SAN count (consistent with the existing dialog).
- `SubmitCommand` produces a `CsrSigningRequest` reflecting the on-screen state and resolves the dialog.

**Extended ViewModels**:

- `AuthoritiesViewModel`: new `CreateFromCsrCommand`.
- `CertificatesViewModel` row VM: `HasPrivateKey` bool derived from `StoredCertificate.PrivateKeyPath != null`; `IsFromCsr` bool derived from `StoredCertificate.IssuedFromCsr`.

**Other**:

- `Validation/CsrValidationErrorMessages.cs` — pure helper, `Format(IReadOnlyList<CsrValidationError>)` returns the user-facing message string per the error-handling table below.
- New `ActivityEntry.Kind` string value: `"SignedFromCsr"`. (`ActivityEntry.Kind` is `string` in this codebase, not an enum — values are conventional constants.)
- New field on `StoredCertificate`: `bool IssuedFromCsr` (default `false`). Optional `string? SourceCsrFilename` for audit.

### `SelfCertForge.Infrastructure` — additions

**Extended `ICertificateWorkflowService`** (and `DotNetCryptoCertificateWorkflowService`):

```csharp
Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken ct);

Task<GeneratedCertificate> GenerateCertificateFromCsrAsync(
    StoredAuthority signingAuthority,
    CsrSigningRequest request,
    CancellationToken ct);
```

Implementation uses `System.Security.Cryptography.X509Certificates.CertificateRequest.LoadSigningRequestPem` (.NET 10 BCL — no new dependencies). `GenerateCertificateFromCsrAsync` re-loads the CSR via `LoadSigningRequestPem`, applies the operator-chosen extensions (KU, EKU, SAN, AKI, validity), signs with the CA's RSA private key using the chosen hash, and returns a `GeneratedCertificate` with `PrivateKeyPem = null` and `PrivateKeyPath = null`.

**Extended `IForgeService`** (and `ForgeService`):

```csharp
Task<ForgeResult> ForgeFromCsrAsync(ForgeFromCsrRequest request, CancellationToken ct);
```

Orchestration steps:

1. Re-inspect `request.SigningRequest.RawCsrPem` (defence-in-depth). On failure, return failure result.
2. Resolve the `StoredAuthority` for `request.SigningAuthorityId`; load its private key (via existing CA-key loader, which prompts for PFX password as needed using `IPfxPasswordDialog` — same as the existing signed-cert flow).
3. Call `GenerateCertificateFromCsrAsync`.
4. Write `{AppData}/certificates/{newId}/{filename}.pem` and `{filename}.crt`. Do **not** write a `.key` file.
5. Persist `StoredCertificate { Id, AuthorityId = SigningAuthorityId, Subject, ValidFrom, ValidTo, Sans, CertPath, PrivateKeyPath = null, IssuedFromCsr = true, SourceCsrFilename = request.SourceCsrFilename }`.
6. Record ``ActivityEntry { Kind = "SignedFromCsr" }`` entry.

On any step-3 / step-4 / step-5 failure, best-effort delete the partially-written `{newId}/` directory; do not persist a `StoredCertificate`. Step-6 failures are logged to debug output but do not fail the operation.

### `SelfCertForge.App` — additions

- **`Pages/AuthoritiesView.xaml`**: new "Create From Certificate Signing Request" button placed inline next to the existing "Create Signed Certificate" button, mirroring its layout exactly (same `HorizontalStackLayout`, 16×16 icon grid, `InterSemiBold` 15pt label, brand button styling). Bound to the new command.
- **`Pages/CertificatesView.xaml`**: `Border`-based "From CSR" badge on the row + detail panel, visibility bound to `IsFromCsr`. PFX export menu item / button bound to `HasPrivateKey` (disabled when false; tooltip / accessibility label explains why).
- **`Dialogs/CreateFromCsrDialog.xaml`** + `.xaml.cs`: same dialog chrome as `CreateSignedCertDialog`. Subject DN, public-key display, and public-key fingerprint rendered using a shared `ReadOnlyFieldStyle`. SAN list visually tags each entry with "from CSR" or "added" using a converter. KU/EKU control groups are disabled when `IsKeyUsageLocked` / `IsEkuLocked` is true, with a small "from CSR" annotation.
- **`Dialogs/CreateFromCsrDialogHost.cs`**: implements `ICreateFromCsrDialog`, mirrors `CreateSignedCertDialogHost`.
- **`Services/MauiCsrFilePicker.cs`**: implements `ICsrFilePicker` by delegating to `FilePickerHelper.PickFileAsync(extensions: ["csr", "pem", "req", "txt"])`, then `File.ReadAllTextAsync`.
- **`Services/FilePickerHelper.cs`**: small macOS fix. When one or more requested extensions fail to resolve to a `UTType` (e.g. `"csr"`), append `UTTypes.Data` (or `UTTypes.Item`) to the allow list so unrecognized-extension files remain selectable. Windows behavior unchanged.
- **`Controls/IconPaths.cs`**: add `public static readonly Geometry Signature = G("<Lucide signature path data>");`. Path string is lifted verbatim from Lucide v0.469+.
- **`MauiProgram.cs`**: register `ICsrFilePicker → MauiCsrFilePicker`, `ICreateFromCsrDialog → CreateFromCsrDialogHost`.

## Data flow

```
[Authorities page]
   │  user selects a CA, clicks "Create From Certificate Signing Request"
   ▼
[AuthoritiesViewModel.CreateFromCsrCommand]
   │
   ├─► ICsrFilePicker.PickCsrFileAsync(ct)
   │       null (cancel) → exit silently
   │
   ├─► ICertificateWorkflowService.InspectCsrAsync(contents, ct)
   │       IsValid == false →
   │           IConfirmationDialog.ShowAsync(
   │               title:   "Invalid Certificate Signing Request",
   │               message: CsrValidationErrorMessages.Format(errors),
   │               cancelOnly: true)
   │           exit
   │
   ├─► ICreateFromCsrDialog.ShowAsync(authority, summary, ct)
   │       null (cancel) → exit silently
   │
   └─► IForgeService.ForgeFromCsrAsync(request, ct)
           Re-inspect → sign → write {AppData}/certificates/{id}/{name}.pem + .crt
           → ICertificateStore.SaveAsync(StoredCertificate { IssuedFromCsr=true, PrivateKeyPath=null })
           → IActivityLog.RecordAsync(`ActivityEntry { Kind = "SignedFromCsr" }`)

Store change propagates via existing observation to:
   • CertificatesViewModel (new row appears with "From CSR" badge, PFX disabled)
   • DashboardViewModel (recent-activity entry rendered with SignedFromCsr label)
   • AuthoritiesViewModel (child-cert count for the selected authority refreshes)
```

Cancellation: each `await` honors the command's `CancellationToken`. No on-disk artifact exists prior to the forge step's write phase, so cancellation before that point leaves no cleanup work.

## Error handling

### CSR validation alert

A single `IConfirmationDialog` invocation with:

- **Title**: "Invalid Certificate Signing Request"
- **Message**: formatted by `CsrValidationErrorMessages.Format`, bulleted list when multiple errors.

| `CsrValidationError` | Message |
|---|---|
| `Malformed` | "The file does not appear to be a valid PKCS#10 certificate signing request. Make sure it is a PEM-encoded `.csr` file beginning with `-----BEGIN CERTIFICATE REQUEST-----`." |
| `InvalidProofOfPossession` | "The CSR's signature could not be verified against its embedded public key. The file may be corrupted or tampered with." |
| `UnsupportedKeyAlgorithm` | "The CSR uses an unsupported public-key algorithm. SelfCertForge currently signs RSA certificate requests only." |
| `KeyTooSmall` | "The CSR's RSA key is below the 2048-bit minimum. Ask the requester to regenerate the CSR with a stronger key." |
| `SubjectDnEmptyOrMalformed` | "The CSR has an empty or malformed Subject. SelfCertForge requires a usable Subject Distinguished Name (e.g. `CN=example.local`)." |

### File picker cancel / read failure

- Picker cancel → silent exit.
- `File.ReadAllTextAsync` throws → caught in `MauiCsrFilePicker`; surfaced via the same alert modal with message "Could not read the selected file: `{Exception.Message}`".

### Dialog cancel

`ICreateFromCsrDialog.ShowAsync` returns `null` → silent exit. No on-disk artifact, no activity-log entry.

### Signing failure

- CA key load failures use the existing `IPfxPasswordDialog` retry loop (reused, not duplicated).
- `CryptographicException` from the signing operation → caught at the Authorities command boundary, surfaced via `IConfirmationDialog` with title "Could not sign certificate" and the exception message. No partial artifacts.
- `OperationCanceledException` propagates and is swallowed silently by the command.

### Persistence failure

- File-write or store-save failure → alert "Could not save signed certificate"; best-effort delete the `{newId}/` directory; no `StoredCertificate` committed.
- Activity-log failure → logged to debug output; success result still returned.

### Defence-in-depth re-inspection

`ForgeFromCsrAsync` re-inspects `RawCsrPem` before signing. If the second inspection differs from the first, abort with the same alert modal. Practically never fires; near-zero cost.

## Cross-platform parity

- All UI uses standard MAUI controls + the existing dialog/confirmation/file-picker abstractions. No platform-specific renderers.
- `FilePickerHelper` is the only file with platform branches; the small macOS-UTI fix preserves parity (both platforms accept `.csr`, `.pem`, `.req`, `.txt`; content is validated in-process, not by extension).
- `IconPaths.Signature` is pure `Geometry`, identical on both platforms.
- Read-only field styling and SAN origin tags use a single XAML style + converter — no platform branches.

## Testing strategy

Unit-tested in `SelfCertForge.Core.Tests`. The App project is not tested (consistent with current repo posture).

### CSR inspection corpus

Committed under `SelfCertForge.Core.Tests/TestData/Csrs/`:

| Fixture | Expected result |
|---|---|
| `valid-rsa-2048.csr` | Valid, summary populated, no SAN/KU/EKU requested |
| `valid-rsa-2048-with-sans.csr` | Valid, `RequestedSans = ["example.local", "api.example.local"]` |
| `valid-rsa-2048-with-ku-eku.csr` | Valid, `RequestedKeyUsage` and `RequestedEku` populated |
| `valid-rsa-4096.csr` | Valid, `PublicKeyBits = 4096` |
| `valid-ecdsa-p256.csr` | Invalid, errors = `[UnsupportedKeyAlgorithm]` |
| `valid-rsa-1024.csr` | Invalid, errors = `[KeyTooSmall]` |
| `tampered-rsa-2048.csr` | Invalid, errors = `[InvalidProofOfPossession]` |
| `empty-subject-rsa-2048.csr` | Invalid, errors = `[SubjectDnEmptyOrMalformed]` |
| `not-a-csr.txt` | Invalid, errors = `[Malformed]` |
| `truncated.csr` | Invalid, errors = `[Malformed]` |
| `rsa-1024-with-bad-sig.csr` | Invalid, errors contain both `KeyTooSmall` and `InvalidProofOfPossession` |

Fixtures are generated once with `openssl` (or a one-time C# fixture program) and checked in.

### Workflow signing assertions

For `GenerateCertificateFromCsrAsync`:

- Issued cert's public key byte-equals the CSR's public key.
- `Issuer` matches CA's `Subject`.
- Validity matches `ValidityDays` from now.
- SAN extension contains exactly `request.Sans` values (origin tags are UI metadata, not crypto).
- KU extension matches `request.KeyUsage`.
- EKU extension matches `request.ExtendedKeyUsages` (absent if list is empty).
- Signature algorithm OID matches `request.SignatureHashAlgorithm`.
- AKI extension references CA's SKI.
- Issued cert verifies via `X509Chain` with the CA added as a custom root.
- `GeneratedCertificate.PrivateKeyPem == null && PrivateKeyPath == null`.

Test CAs are generated in-memory per test.

### Forge orchestration

Fakes for `ICertificateWorkflowService`, `ICertificateStore`, `IActivityLog`, plus a temp directory rooted via `IDataFolderService`:

- Happy path: writes `.pem` + `.crt`, does **not** write `.key`, stores `StoredCertificate { IssuedFromCsr=true, PrivateKeyPath=null, SourceCsrFilename="…" }`, records ``ActivityEntry { Kind = "SignedFromCsr" }``.
- Re-inspection failure: no files written, no store/activity-log calls.
- `CryptographicException` from signing: no files written.
- Store-save failure: partially-written `{id}/` directory is cleaned up, no activity-log entry.
- Activity-log failure: success result still returned.
- Cancellation honored at each await.

### ViewModel tests

`CreateFromCsrDialogViewModel`:

- Seeded summary produces read-only Subject + key display, prefilled SAN list with `Origin = FromCsr`, default validity, KU/EKU locked-from-CSR or editable per summary.
- `AddSanCommand` appends `Origin = AddedByOperator`.
- `RemoveSanCommand` removes regardless of origin.
- `SubmitCommand` produces a `CsrSigningRequest` mirroring on-screen state.
- `CanSubmit` returns false when `ValidityDays <= 0` or `SigningAuthorityId` is empty; returns true otherwise (including when SAN list is empty).
- CSR with non-null `RequestedKeyUsage` results in `IsKeyUsageLocked == true`.

`AuthoritiesViewModel.CreateFromCsrCommand`:

- Picker null → no downstream calls.
- Inspection invalid → confirmation dialog shown; no dialog/forge calls.
- Dialog cancel → no forge call.
- Happy path → all four collaborators called in order (Moq verify-in-order).

`CertificatesViewModel` row VM:

- `IssuedFromCsr=true` → `IsFromCsr=true`, `HasPrivateKey=false`, `CanExportPfx=false`.
- `IssuedFromCsr=false` → no badge, full export.

### Error-message helper

`CsrValidationErrorMessages.Format` covered per enum value plus multi-error formatting (stable ordering, bullet list).

### Explicitly out of scope

- XAML / Page-level rendering tests (matches current repo posture).
- Integration tests of dialog adapters or file-picker host.
- README / docs assertion tests.
- Platform-specific unit tests; `FilePickerHelper`'s platform branches sit outside the Core test boundary. Parity is enforced by code review.

## Open follow-ups (not blocking)

- Future: support non-RSA CSRs once the workflow service grows non-RSA signing.
- Future: optional "configurable minimum key size" preference. Today's 2048-bit minimum is hard-coded.
- Future: surface `SourceCsrFilename` somewhere on the Certificates detail panel for audit.
