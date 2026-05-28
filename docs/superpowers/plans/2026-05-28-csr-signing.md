# CSR Signing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Create From Certificate Signing Request" action to the Authorities view detail panel that lets operators pick a `.csr`, validates it, opens a dialog populated from the CSR (Subject + public key locked; validity + SANs editable; KU/EKU honored when present, editable when absent), and issues a certificate signed by the selected CA. The cert lands in the existing store with `IssuedFromCsr=true` and `PrivateKeyPath=null`.

**Architecture:** Approach B — parallel components, shared workflow service. New `CreateFromCsrDialog{ViewModel,XAML,Host}` mirrors the existing `CreateSignedCert*` family but is purpose-built for CSR signing. CSR inspection and CSR-based signing extend `ICertificateWorkflowService`; orchestration extends `IForgeService` via a new `ForgeFromCsrAsync`. UI parity (macCatalyst + Windows) is hard: no platform-specific renderers, shared XAML styles, pure-`Geometry` icons, single SAN-origin converter. `FilePickerHelper` is the only file with platform branches; we add a small fallback to fix the latent macOS UTI gap for `.csr`.

**Tech Stack:** .NET 10 MAUI, `System.Security.Cryptography.X509Certificates.CertificateRequest.LoadSigningRequestPem` (BCL, no new deps), xUnit, Lucide-derived `Geometry` icon paths, custom `ObservableObject` + `AsyncRelayCommand`.

---

## File Structure

**Core — new files:**
- `SelfCertForge.Core/Models/CsrInspectionResult.cs` — record + `CsrValidationError` enum
- `SelfCertForge.Core/Models/CsrSummary.cs` — record + `CsrRequestedKeyUsages` and `CsrRequestedEkus` nested records (one file for cohesion)
- `SelfCertForge.Core/Models/CsrSigningRequest.cs` — request record + `CsrSignedSanEntry` + `CsrSignedSanOrigin` enum
- `SelfCertForge.Core/Models/CsrFilePickResult.cs` — record
- `SelfCertForge.Core/Models/ForgeFromCsrRequest.cs` — record
- `SelfCertForge.Core/Abstractions/ICsrFilePicker.cs` — interface
- `SelfCertForge.Core/Abstractions/ICreateFromCsrDialog.cs` — interface
- `SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs` — VM
- `SelfCertForge.Core/Validation/CsrValidationErrorMessages.cs` — static formatter
- `SelfCertForge.Core/Presentation/CsrSanOriginRowViewModel.cs` — small row VM for SAN list rendering (Value + Origin + Remove command)

**Core — modified files:**
- `SelfCertForge.Core/Models/StoredCertificate.cs` — add `bool IssuedFromCsr = false`, `string? SourceCsrFilename = null`
- `SelfCertForge.Core/Abstractions/ICertificateWorkflowService.cs` — add `InspectCsrAsync` and `GenerateCertificateFromCsrAsync`
- `SelfCertForge.Core/Abstractions/IForgeService.cs` — add `ForgeFromCsrAsync`
- `SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs` — inject `ICreateFromCsrDialog` + `ICsrFilePicker` + `IConfirmationDialog` + `ICertificateWorkflowService`; add `CreateFromCsrCommand` on `AuthorityRowViewModel` and propagate to `AuthorityDetailViewModel`
- `SelfCertForge.Core/Presentation/CertificatesViewModel.cs` — wire `IsFromCsr`, `HasPrivateKey=false`, disable export-PFX/key when `IsFromCsr`

**Infrastructure — modified files:**
- `SelfCertForge.Infrastructure/DotNetCryptoCertificateWorkflowService.cs` — implement `InspectCsrAsync` and `GenerateCertificateFromCsrAsync`
- `SelfCertForge.Infrastructure/ForgeService.cs` — implement `ForgeFromCsrAsync`

**App (MAUI) — new files:**
- `SelfCertForge.App/Dialogs/CreateFromCsrDialog.xaml` + `.xaml.cs`
- `SelfCertForge.App/Dialogs/CreateFromCsrDialogHost.cs` — implements `ICreateFromCsrDialog`
- `SelfCertForge.App/Services/MauiCsrFilePicker.cs` — implements `ICsrFilePicker`, delegates to `FilePickerHelper`
- `SelfCertForge.App/Converters/SanOriginToTagConverter.cs` — single converter for the "From CSR" / "Added" tag

**App (MAUI) — modified files:**
- `SelfCertForge.App/Controls/IconPaths.cs` — add `Signature` geometry
- `SelfCertForge.App/Services/FilePickerHelper.cs` — macOS UTI fallback for unrecognized extensions
- `SelfCertForge.App/Pages/AuthoritiesView.xaml` — add "Create From Certificate Signing Request" button next to existing one
- `SelfCertForge.App/Pages/CertificatesView.xaml` — add "From CSR" badge + disable PFX/Key-export menu items when from CSR
- `SelfCertForge.App/MauiProgram.cs` — register new services and dialog

**Tests — new files:**
- `SelfCertForge.Core.Tests/CsrInspectionTests.cs`
- `SelfCertForge.Core.Tests/CreateFromCsrDialogViewModelTests.cs`
- `SelfCertForge.Core.Tests/ForgeServiceFromCsrTests.cs` (extend existing if natural — separate file is cleaner)
- `SelfCertForge.Core.Tests/AuthoritiesViewModelCsrTests.cs` (extend existing if natural — separate file is cleaner)
- `SelfCertForge.Core.Tests/CsrValidationErrorMessagesTests.cs`
- `SelfCertForge.Core.Tests/Fixtures/Csr/*.csr` — generated test corpus (committed binary-ish PEM text)

---

## Task 1: CSR Validation Error model + formatter

**Files:**
- Create: `SelfCertForge.Core/Models/CsrInspectionResult.cs`
- Create: `SelfCertForge.Core/Validation/CsrValidationErrorMessages.cs`
- Create: `SelfCertForge.Core.Tests/CsrValidationErrorMessagesTests.cs`

- [ ] **Step 1: Write failing tests for the formatter**

```csharp
// SelfCertForge.Core.Tests/CsrValidationErrorMessagesTests.cs
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Validation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class CsrValidationErrorMessagesTests
{
    [Fact]
    public void Format_Malformed_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.Malformed });
        Assert.Contains("could not be parsed", msg);
    }

    [Fact]
    public void Format_InvalidProofOfPossession_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.InvalidProofOfPossession });
        Assert.Contains("proof-of-possession", msg);
    }

    [Fact]
    public void Format_UnsupportedKeyAlgorithm_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.UnsupportedKeyAlgorithm });
        Assert.Contains("RSA", msg);
    }

    [Fact]
    public void Format_KeyTooSmall_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.KeyTooSmall });
        Assert.Contains("2048", msg);
    }

    [Fact]
    public void Format_SubjectDnEmptyOrMalformed_returns_user_friendly_message()
    {
        var msg = CsrValidationErrorMessages.Format(new[] { CsrValidationError.SubjectDnEmptyOrMalformed });
        Assert.Contains("Subject", msg);
    }

    [Fact]
    public void Format_multiple_errors_joins_them_with_newline()
    {
        var msg = CsrValidationErrorMessages.Format(new[]
        {
            CsrValidationError.KeyTooSmall,
            CsrValidationError.SubjectDnEmptyOrMalformed,
        });
        Assert.Contains("2048", msg);
        Assert.Contains("Subject", msg);
        Assert.Contains("\n", msg);
    }

    [Fact]
    public void Format_empty_list_returns_fallback()
    {
        var msg = CsrValidationErrorMessages.Format(Array.Empty<CsrValidationError>());
        Assert.False(string.IsNullOrWhiteSpace(msg));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CsrValidationErrorMessagesTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Create the model and formatter**

```csharp
// SelfCertForge.Core/Models/CsrInspectionResult.cs
namespace SelfCertForge.Core.Models;

public enum CsrValidationError
{
    Malformed,
    InvalidProofOfPossession,
    UnsupportedKeyAlgorithm,
    KeyTooSmall,
    SubjectDnEmptyOrMalformed,
}

public sealed record CsrInspectionResult(
    bool IsValid,
    CsrSummary? Summary,
    IReadOnlyList<CsrValidationError> Errors);
```

```csharp
// SelfCertForge.Core/Validation/CsrValidationErrorMessages.cs
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Validation;

public static class CsrValidationErrorMessages
{
    public static string Format(IReadOnlyCollection<CsrValidationError> errors)
    {
        if (errors.Count == 0)
            return "The certificate signing request could not be validated.";

        var lines = errors.Select(e => e switch
        {
            CsrValidationError.Malformed =>
                "The file could not be parsed as a PKCS#10 certificate signing request.",
            CsrValidationError.InvalidProofOfPossession =>
                "The CSR's proof-of-possession signature is invalid — the request may have been tampered with.",
            CsrValidationError.UnsupportedKeyAlgorithm =>
                "Only RSA public keys are supported. The CSR uses a different algorithm.",
            CsrValidationError.KeyTooSmall =>
                "The CSR's RSA key is smaller than the 2048-bit minimum.",
            CsrValidationError.SubjectDnEmptyOrMalformed =>
                "The CSR's Subject Distinguished Name is empty or malformed.",
            _ => "An unrecognized validation error occurred.",
        });

        return string.Join("\n", lines);
    }
}
```

Note: `CsrSummary` is referenced but not yet declared — the file will not compile until Task 2 adds it. That's intentional; complete Task 2 before running these tests.

- [ ] **Step 4: Create stub for CsrSummary so this task compiles standalone**

Add a temporary placeholder file:

```csharp
// SelfCertForge.Core/Models/CsrSummary.cs (will be expanded in Task 2)
namespace SelfCertForge.Core.Models;

public sealed record CsrSummary(
    string SubjectDistinguishedName,
    string PublicKeyAlgorithm,
    int PublicKeyBits,
    string PublicKeyFingerprintSha256,
    string RawCsrPem);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CsrValidationErrorMessagesTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Models/CsrInspectionResult.cs \
        SelfCertForge.Core/Models/CsrSummary.cs \
        SelfCertForge.Core/Validation/CsrValidationErrorMessages.cs \
        SelfCertForge.Core.Tests/CsrValidationErrorMessagesTests.cs
git commit -m "feat(csr): add CsrInspectionResult, CsrSummary stub, and error messages"
```

---

## Task 2: Expand CSR domain models

**Files:**
- Modify: `SelfCertForge.Core/Models/CsrSummary.cs`
- Create: `SelfCertForge.Core/Models/CsrSigningRequest.cs`
- Create: `SelfCertForge.Core/Models/CsrFilePickResult.cs`
- Create: `SelfCertForge.Core/Models/ForgeFromCsrRequest.cs`
- Modify: `SelfCertForge.Core/Models/StoredCertificate.cs`

- [ ] **Step 1: Expand `CsrSummary` to include KU/EKU/SAN sub-records**

Replace the contents of `SelfCertForge.Core/Models/CsrSummary.cs` with:

```csharp
namespace SelfCertForge.Core.Models;

public sealed record CsrSummary(
    string SubjectDistinguishedName,
    string PublicKeyAlgorithm,
    int PublicKeyBits,
    string PublicKeyFingerprintSha256,
    string RawCsrPem,
    IReadOnlyList<string> RequestedSans,
    CsrRequestedKeyUsages? RequestedKeyUsage,
    CsrRequestedEkus? RequestedEkus);

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
```

- [ ] **Step 2: Create `CsrSigningRequest` and SAN entry**

```csharp
// SelfCertForge.Core/Models/CsrSigningRequest.cs
namespace SelfCertForge.Core.Models;

public enum CsrSignedSanOrigin
{
    FromCsr,
    AddedByOperator,
}

public sealed record CsrSignedSanEntry(string Value, CsrSignedSanOrigin Origin);

public sealed record CsrSigningRequest(
    string SigningAuthorityId,
    string RawCsrPem,
    string SourceCsrFilename,
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
    bool EkuTimeStamping,
    HashAlgorithmKind SignatureHashAlgorithm);
```

- [ ] **Step 3: Create `CsrFilePickResult` and `ForgeFromCsrRequest`**

```csharp
// SelfCertForge.Core/Models/CsrFilePickResult.cs
namespace SelfCertForge.Core.Models;

public sealed record CsrFilePickResult(string FilePath, string Contents);
```

```csharp
// SelfCertForge.Core/Models/ForgeFromCsrRequest.cs
namespace SelfCertForge.Core.Models;

public sealed record ForgeFromCsrRequest(CsrSigningRequest SigningRequest);
```

- [ ] **Step 4: Extend `StoredCertificate` with `IssuedFromCsr` and `SourceCsrFilename`**

Modify `SelfCertForge.Core/Models/StoredCertificate.cs` — add two new positional params at the end with defaults so existing call sites keep compiling:

```csharp
public sealed record StoredCertificate(
    string Id,
    StoredCertificateKind Kind,
    string CommonName,
    string Subject,
    string? IssuerId,
    string? IssuerName,
    IReadOnlyList<string> Sans,
    string Algorithm,
    string Serial,
    string Sha256,
    string Sha1,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool InstalledInTrustStore,
    string? CertificatePath = null,
    string? PrivateKeyPath = null,
    string? OutputDirectory = null,
    IReadOnlyList<string>? KeyUsages = null,
    IReadOnlyList<string>? ExtendedKeyUsages = null,
    bool IssuedFromCsr = false,
    string? SourceCsrFilename = null);
```

- [ ] **Step 5: Build to verify everything still compiles**

Run: `dotnet build SelfCertForge.Core/SelfCertForge.Core.csproj`
Expected: BUILD SUCCEEDED.

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: All existing tests still pass; previously-added formatter tests pass.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Models/CsrSummary.cs \
        SelfCertForge.Core/Models/CsrSigningRequest.cs \
        SelfCertForge.Core/Models/CsrFilePickResult.cs \
        SelfCertForge.Core/Models/ForgeFromCsrRequest.cs \
        SelfCertForge.Core/Models/StoredCertificate.cs
git commit -m "feat(csr): expand CSR domain models, extend StoredCertificate with IssuedFromCsr"
```

---

## Task 3: Core abstractions — ICsrFilePicker, ICreateFromCsrDialog, workflow + forge service extensions

**Files:**
- Create: `SelfCertForge.Core/Abstractions/ICsrFilePicker.cs`
- Create: `SelfCertForge.Core/Abstractions/ICreateFromCsrDialog.cs`
- Modify: `SelfCertForge.Core/Abstractions/ICertificateWorkflowService.cs`
- Modify: `SelfCertForge.Core/Abstractions/IForgeService.cs`

- [ ] **Step 1: Create ICsrFilePicker**

```csharp
// SelfCertForge.Core/Abstractions/ICsrFilePicker.cs
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICsrFilePicker
{
    Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Create ICreateFromCsrDialog**

```csharp
// SelfCertForge.Core/Abstractions/ICreateFromCsrDialog.cs
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface ICreateFromCsrDialog
{
    Task<StoredCertificate?> ShowAsync(
        string signingAuthorityId,
        string signingAuthorityName,
        CsrSummary csrSummary,
        string sourceCsrFilename,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Extend `ICertificateWorkflowService`**

Open `SelfCertForge.Core/Abstractions/ICertificateWorkflowService.cs`. Add two methods to the existing interface (do not change existing methods):

```csharp
Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken cancellationToken = default);

Task<CertificateGenerationResult> GenerateCertificateFromCsrAsync(
    CsrSigningRequest request,
    string issuerCertificatePath,
    string issuerPrivateKeyPath,
    string outputDirectory,
    string outputFileBaseName,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Extend `IForgeService`**

Modify `SelfCertForge.Core/Abstractions/IForgeService.cs`:

```csharp
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Abstractions;

public interface IForgeService
{
    Task<StoredCertificate> ForgeAsync(ForgeRequest request, CancellationToken ct = default);

    Task<StoredCertificate> ForgeFromCsrAsync(ForgeFromCsrRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 5: Verify the solution still builds (implementations not yet provided — only the interface signatures changed)**

Run: `dotnet build SelfCertForge.Core/SelfCertForge.Core.csproj`
Expected: PASS.

Run: `dotnet build SelfCertForge.Infrastructure/SelfCertForge.Infrastructure.csproj`
Expected: FAIL — `DotNetCryptoCertificateWorkflowService` and `ForgeService` no longer implement the interfaces. That's expected; Tasks 4 and 5 add the implementations. Leave failing for now.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Abstractions/ICsrFilePicker.cs \
        SelfCertForge.Core/Abstractions/ICreateFromCsrDialog.cs \
        SelfCertForge.Core/Abstractions/ICertificateWorkflowService.cs \
        SelfCertForge.Core/Abstractions/IForgeService.cs
git commit -m "feat(csr): add ICsrFilePicker, ICreateFromCsrDialog, extend workflow + forge service interfaces"
```

---

## Task 4: Implement `InspectCsrAsync` in DotNetCryptoCertificateWorkflowService (TDD)

**Files:**
- Create: `SelfCertForge.Core.Tests/Fixtures/Csr/` — populated by generator script (run once, commit outputs)
- Create: `SelfCertForge.Core.Tests/CsrFixtureGenerator.cs` — helper that synthesizes CSR PEM strings at test startup
- Create: `SelfCertForge.Core.Tests/CsrInspectionTests.cs`
- Modify: `SelfCertForge.Infrastructure/DotNetCryptoCertificateWorkflowService.cs`

- [ ] **Step 1: Add a helper to synthesize CSR fixtures in memory**

Synthesizing CSRs in test code avoids checking binary-ish PEM blobs into git that could drift from BCL behavior:

```csharp
// SelfCertForge.Core.Tests/CsrFixtureGenerator.cs
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SelfCertForge.Core.Tests;

internal static class CsrFixtureGenerator
{
    public static string ValidRsa(int bits, string subjectDn,
        IEnumerable<string>? sanDnsNames = null,
        X509KeyUsageFlags? keyUsage = null,
        IEnumerable<string>? ekuOids = null)
    {
        using var rsa = RSA.Create(bits);
        var req = new CertificateRequest(subjectDn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        if (sanDnsNames is not null)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            foreach (var dns in sanDnsNames) sanBuilder.AddDnsName(dns);
            req.CertificateExtensions.Add(sanBuilder.Build(critical: false));
        }
        if (keyUsage is not null)
            req.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage.Value, critical: false));
        if (ekuOids is not null)
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { ToOidCollection(ekuOids) }.Single(), critical: false));

        return req.CreateSigningRequestPem();
    }

    public static string ValidEcdsa(string subjectDn)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest(subjectDn, ecdsa, HashAlgorithmName.SHA256);
        return req.CreateSigningRequestPem();
    }

    public static string TamperedRsa(int bits, string subjectDn)
    {
        var pem = ValidRsa(bits, subjectDn);
        // Flip the last 0 to 1 (or vice versa) inside the base64 region to corrupt the signature.
        var lines = pem.Split('\n');
        var bodyLine = Array.FindIndex(lines, l => !l.StartsWith("-----") && !string.IsNullOrWhiteSpace(l));
        var line = lines[bodyLine];
        lines[bodyLine] = line[..^2] + (line[^2] == 'A' ? "B" : "A") + line[^1];
        return string.Join('\n', lines);
    }

    public static string Truncated() =>
        "-----BEGIN CERTIFICATE REQUEST-----\nMIICijCC\n-----END CERTIFICATE REQUEST-----\n";

    public static string NotACsr() => "this is not a certificate signing request";

    private static OidCollection ToOidCollection(IEnumerable<string> values)
    {
        var oids = new OidCollection();
        foreach (var v in values) oids.Add(new Oid(v));
        return oids;
    }
}
```

- [ ] **Step 2: Write failing tests for `InspectCsrAsync`**

```csharp
// SelfCertForge.Core.Tests/CsrInspectionTests.cs
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class CsrInspectionTests
{
    private static readonly DotNetCryptoCertificateWorkflowService Svc = new();

    [Fact]
    public async Task ValidRsa2048_returns_IsValid_with_summary()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Summary);
        Assert.Equal("RSA", result.Summary!.PublicKeyAlgorithm);
        Assert.Equal(2048, result.Summary.PublicKeyBits);
        Assert.Contains("CN=example.local", result.Summary.SubjectDistinguishedName);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidRsa_with_sans_populates_RequestedSans()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local",
            sanDnsNames: new[] { "example.local", "api.example.local" });
        var result = await Svc.InspectCsrAsync(pem);

        Assert.True(result.IsValid);
        Assert.Equal(new[] { "example.local", "api.example.local" }, result.Summary!.RequestedSans);
    }

    [Fact]
    public async Task ValidRsa_with_ku_eku_populates_requested_extensions()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "CN=example.local",
            keyUsage: System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature
                    | System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment,
            ekuOids: new[] { "1.3.6.1.5.5.7.3.1" /* server auth */ });
        var result = await Svc.InspectCsrAsync(pem);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Summary!.RequestedKeyUsage);
        Assert.True(result.Summary.RequestedKeyUsage!.DigitalSignature);
        Assert.True(result.Summary.RequestedKeyUsage.KeyEncipherment);
        Assert.NotNull(result.Summary.RequestedEkus);
        Assert.True(result.Summary.RequestedEkus!.ServerAuth);
    }

    [Fact]
    public async Task Ecdsa_csr_returns_UnsupportedKeyAlgorithm()
    {
        var pem = CsrFixtureGenerator.ValidEcdsa("CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        Assert.False(result.IsValid);
        Assert.Contains(CsrValidationError.UnsupportedKeyAlgorithm, result.Errors);
    }

    [Fact]
    public async Task Rsa1024_returns_KeyTooSmall()
    {
        var pem = CsrFixtureGenerator.ValidRsa(1024, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        Assert.False(result.IsValid);
        Assert.Contains(CsrValidationError.KeyTooSmall, result.Errors);
    }

    [Fact]
    public async Task TamperedRsa_returns_InvalidProofOfPossession()
    {
        var pem = CsrFixtureGenerator.TamperedRsa(2048, "CN=example.local");
        var result = await Svc.InspectCsrAsync(pem);

        Assert.False(result.IsValid);
        Assert.Contains(CsrValidationError.InvalidProofOfPossession, result.Errors);
    }

    [Fact]
    public async Task EmptySubject_returns_SubjectDnEmptyOrMalformed()
    {
        var pem = CsrFixtureGenerator.ValidRsa(2048, "");
        var result = await Svc.InspectCsrAsync(pem);

        Assert.False(result.IsValid);
        Assert.Contains(CsrValidationError.SubjectDnEmptyOrMalformed, result.Errors);
    }

    [Fact]
    public async Task NotACsr_returns_Malformed()
    {
        var result = await Svc.InspectCsrAsync(CsrFixtureGenerator.NotACsr());
        Assert.False(result.IsValid);
        Assert.Contains(CsrValidationError.Malformed, result.Errors);
    }

    [Fact]
    public async Task Truncated_returns_Malformed()
    {
        var result = await Svc.InspectCsrAsync(CsrFixtureGenerator.Truncated());
        Assert.False(result.IsValid);
        Assert.Contains(CsrValidationError.Malformed, result.Errors);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CsrInspectionTests`
Expected: FAIL — `InspectCsrAsync` not implemented.

- [ ] **Step 4: Implement `InspectCsrAsync`**

Add to `SelfCertForge.Infrastructure/DotNetCryptoCertificateWorkflowService.cs`:

```csharp
public Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(csrPem))
        return Task.FromResult(new CsrInspectionResult(false, null, new[] { CsrValidationError.Malformed }));

    CertificateRequest req;
    try
    {
        req = CertificateRequest.LoadSigningRequestPem(
            csrPem,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.SkipSignatureValidation
                | CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
    }
    catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
    {
        return Task.FromResult(new CsrInspectionResult(false, null, new[] { CsrValidationError.Malformed }));
    }

    var errors = new List<CsrValidationError>();

    // PoP signature
    try
    {
        _ = CertificateRequest.LoadSigningRequestPem(
            csrPem,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
    }
    catch (CryptographicException)
    {
        errors.Add(CsrValidationError.InvalidProofOfPossession);
    }

    // Subject
    var subjectDn = req.SubjectName.Name ?? string.Empty;
    if (string.IsNullOrWhiteSpace(subjectDn))
        errors.Add(CsrValidationError.SubjectDnEmptyOrMalformed);

    // Algorithm + key size
    string algorithm = "Unknown";
    int bits = 0;
    using (var rsa = TryGetRsaPublicKey(req))
    {
        if (rsa is not null)
        {
            algorithm = "RSA";
            bits = rsa.KeySize;
            if (bits < 2048)
                errors.Add(CsrValidationError.KeyTooSmall);
        }
        else
        {
            errors.Add(CsrValidationError.UnsupportedKeyAlgorithm);
        }
    }

    if (errors.Count > 0)
        return Task.FromResult(new CsrInspectionResult(false, null, errors));

    var summary = BuildSummary(req, csrPem, algorithm, bits);
    return Task.FromResult(new CsrInspectionResult(true, summary, Array.Empty<CsrValidationError>()));
}

private static RSA? TryGetRsaPublicKey(CertificateRequest req)
{
    try { return req.PublicKey.GetRSAPublicKey(); }
    catch (CryptographicException) { return null; }
}

private static CsrSummary BuildSummary(CertificateRequest req, string pem, string algorithm, int bits)
{
    var spki = req.PublicKey.ExportSubjectPublicKeyInfo();
    var fp = Convert.ToHexString(SHA256.HashData(spki));

    IReadOnlyList<string> sans = ExtractSans(req);
    var ku = ExtractRequestedKeyUsage(req);
    var ekus = ExtractRequestedEkus(req);

    return new CsrSummary(
        SubjectDistinguishedName: req.SubjectName.Name ?? string.Empty,
        PublicKeyAlgorithm: algorithm,
        PublicKeyBits: bits,
        PublicKeyFingerprintSha256: fp,
        RawCsrPem: pem,
        RequestedSans: sans,
        RequestedKeyUsage: ku,
        RequestedEkus: ekus);
}

private static IReadOnlyList<string> ExtractSans(CertificateRequest req)
{
    foreach (var ext in req.CertificateExtensions)
    {
        if (ext is X509SubjectAlternativeNameExtension san)
            return san.EnumerateDnsNames().ToArray();
    }
    return Array.Empty<string>();
}

private static CsrRequestedKeyUsages? ExtractRequestedKeyUsage(CertificateRequest req)
{
    foreach (var ext in req.CertificateExtensions)
    {
        if (ext is X509KeyUsageExtension ku)
        {
            var f = ku.KeyUsages;
            return new CsrRequestedKeyUsages(
                DigitalSignature: f.HasFlag(X509KeyUsageFlags.DigitalSignature),
                NonRepudiation:   f.HasFlag(X509KeyUsageFlags.NonRepudiation),
                KeyEncipherment:  f.HasFlag(X509KeyUsageFlags.KeyEncipherment),
                DataEncipherment: f.HasFlag(X509KeyUsageFlags.DataEncipherment),
                KeyAgreement:     f.HasFlag(X509KeyUsageFlags.KeyAgreement),
                KeyCertSign:      f.HasFlag(X509KeyUsageFlags.KeyCertSign),
                CrlSign:          f.HasFlag(X509KeyUsageFlags.CrlSign));
        }
    }
    return null;
}

private static CsrRequestedEkus? ExtractRequestedEkus(CertificateRequest req)
{
    foreach (var ext in req.CertificateExtensions)
    {
        if (ext is X509EnhancedKeyUsageExtension eku)
        {
            bool s = false, c = false, cs = false, e = false, t = false;
            foreach (var oid in eku.EnhancedKeyUsages)
            {
                switch (oid.Value)
                {
                    case "1.3.6.1.5.5.7.3.1": s = true; break;
                    case "1.3.6.1.5.5.7.3.2": c = true; break;
                    case "1.3.6.1.5.5.7.3.3": cs = true; break;
                    case "1.3.6.1.5.5.7.3.4": e = true; break;
                    case "1.3.6.1.5.5.7.3.8": t = true; break;
                }
            }
            return new CsrRequestedEkus(s, c, cs, e, t);
        }
    }
    return null;
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CsrInspectionTests`
Expected: PASS for all 9 tests.

If `InvalidProofOfPossession` doesn't trigger from byte-flip, adjust `CsrFixtureGenerator.TamperedRsa` to flip a byte inside the signature DER region (parse PEM → DER → flip last byte → re-encode PEM). The test must produce a CSR whose body parses but whose signature fails — keep iterating until the assertion holds.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core.Tests/CsrFixtureGenerator.cs \
        SelfCertForge.Core.Tests/CsrInspectionTests.cs \
        SelfCertForge.Infrastructure/DotNetCryptoCertificateWorkflowService.cs
git commit -m "feat(csr): implement InspectCsrAsync with full validation gate coverage"
```

---

## Task 5: Implement `GenerateCertificateFromCsrAsync` in workflow service (TDD)

**Files:**
- Modify: `SelfCertForge.Infrastructure/DotNetCryptoCertificateWorkflowService.cs`
- Modify: `SelfCertForge.Core.Tests/CsrInspectionTests.cs` (add `GenerateCertificateFromCsrTests` class in same file or split — split for clarity)
- Create: `SelfCertForge.Core.Tests/GenerateCertificateFromCsrTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// SelfCertForge.Core.Tests/GenerateCertificateFromCsrTests.cs
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class GenerateCertificateFromCsrTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly string _caCertPath;
    private readonly string _caKeyPath;
    private readonly DotNetCryptoCertificateWorkflowService _svc = new();

    public GenerateCertificateFromCsrTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "scf-csr-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);

        using var caKey = RSA.Create(2048);
        var caReq = new CertificateRequest("CN=Test CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caReq.PublicKey, false));
        caReq.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var caCert = caReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));

        _caCertPath = Path.Combine(_tmpDir, "ca.pem");
        _caKeyPath = Path.Combine(_tmpDir, "ca.key");
        File.WriteAllText(_caCertPath, caCert.ExportCertificatePem());
        File.WriteAllText(_caKeyPath, caKey.ExportRSAPrivateKeyPem());
    }

    public void Dispose() => Directory.Delete(_tmpDir, true);

    [Fact]
    public async Task Signs_cert_with_csr_public_key_and_writes_pem_and_crt()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-001");
        var outDir = Path.Combine(_tmpDir, "out");
        var req = MakeRequest(csrPem);

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-001");

        Assert.True(File.Exists(result.CertPemPath));
        Assert.True(File.Exists(Path.ChangeExtension(result.CertPemPath, ".crt")));
        Assert.Null(result.KeyPath);

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        Assert.Contains("CN=device-001", issued.Subject);
    }

    [Fact]
    public async Task Issued_cert_signed_by_CA()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-002");
        var outDir = Path.Combine(_tmpDir, "out2");
        var req = MakeRequest(csrPem);

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-002");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        using var ca = X509Certificate2.CreateFromPem(File.ReadAllText(_caCertPath));

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;

        Assert.True(chain.Build(issued));
    }

    [Fact]
    public async Task Honors_operator_chosen_KU_and_EKU_when_csr_has_none()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-003");
        var outDir = Path.Combine(_tmpDir, "out3");
        var req = MakeRequest(csrPem) with
        {
            KeyUsageDigitalSignature = true,
            KeyUsageKeyEncipherment = true,
            EkuServerAuth = true,
            EkuClientAuth = true,
        };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-003");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);

        var ku = issued.Extensions.OfType<X509KeyUsageExtension>().Single();
        Assert.True(ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature));
        Assert.True(ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment));

        var eku = issued.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single();
        Assert.Contains(eku.EnhancedKeyUsages, o => o.Value == "1.3.6.1.5.5.7.3.1");
        Assert.Contains(eku.EnhancedKeyUsages, o => o.Value == "1.3.6.1.5.5.7.3.2");
    }

    [Fact]
    public async Task Sets_AKI_from_issuer_SKI()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-004");
        var outDir = Path.Combine(_tmpDir, "out4");
        var result = await _svc.GenerateCertificateFromCsrAsync(
            MakeRequest(csrPem), _caCertPath, _caKeyPath, outDir, "device-004");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        Assert.Contains(issued.Extensions,
            e => e.Oid?.Value == "2.5.29.35" /* AuthorityKeyIdentifier */);
    }

    [Fact]
    public async Task Includes_operator_SANs_in_issued_cert()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-005",
            sanDnsNames: new[] { "csr.example" });
        var outDir = Path.Combine(_tmpDir, "out5");

        var req = MakeRequest(csrPem) with
        {
            Sans = new[]
            {
                new CsrSignedSanEntry("csr.example", CsrSignedSanOrigin.FromCsr),
                new CsrSignedSanEntry("added.example", CsrSignedSanOrigin.AddedByOperator),
            },
        };

        var result = await _svc.GenerateCertificateFromCsrAsync(
            req, _caCertPath, _caKeyPath, outDir, "device-005");

        var pem = File.ReadAllText(result.CertPemPath);
        using var issued = X509Certificate2.CreateFromPem(pem);
        var san = issued.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
        var names = san.EnumerateDnsNames().ToList();

        Assert.Contains("csr.example", names);
        Assert.Contains("added.example", names);
    }

    private CsrSigningRequest MakeRequest(string csrPem) => new(
        SigningAuthorityId: "test-ca",
        RawCsrPem: csrPem,
        SourceCsrFilename: "test.csr",
        ValidityDays: 397,
        Sans: Array.Empty<CsrSignedSanEntry>(),
        KeyUsageDigitalSignature: false,
        KeyUsageNonRepudiation: false,
        KeyUsageKeyEncipherment: false,
        KeyUsageDataEncipherment: false,
        KeyUsageKeyAgreement: false,
        KeyUsageKeyCertSign: false,
        KeyUsageCrlSign: false,
        EkuServerAuth: false,
        EkuClientAuth: false,
        EkuCodeSigning: false,
        EkuTimeStamping: false,
        SignatureHashAlgorithm: HashAlgorithmKind.Sha256);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~GenerateCertificateFromCsrTests`
Expected: FAIL — method not implemented.

- [ ] **Step 3: Implement `GenerateCertificateFromCsrAsync`**

Add to `DotNetCryptoCertificateWorkflowService`:

```csharp
public Task<CertificateGenerationResult> GenerateCertificateFromCsrAsync(
    CsrSigningRequest request,
    string issuerCertificatePath,
    string issuerPrivateKeyPath,
    string outputDirectory,
    string outputFileBaseName,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(request);
    cancellationToken.ThrowIfCancellationRequested();

    Directory.CreateDirectory(outputDirectory);
    var safeName = RequireSafeToken(outputFileBaseName, "Output file name");

    var csrReq = CertificateRequest.LoadSigningRequestPem(
        request.RawCsrPem,
        ToHashName(request.SignatureHashAlgorithm),
        CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);

    using var issuerCert = X509Certificate2.CreateFromPem(File.ReadAllText(issuerCertificatePath));
    using var issuerKey = RSA.Create();
    issuerKey.ImportFromPem(File.ReadAllText(issuerPrivateKeyPath));

    // Strip CSR-supplied extensions; we apply operator-chosen ones explicitly.
    csrReq.CertificateExtensions.Clear();

    csrReq.CertificateExtensions.Add(
        new X509BasicConstraintsExtension(false, false, 0, critical: true));

    csrReq.CertificateExtensions.Add(
        new X509SubjectKeyIdentifierExtension(csrReq.PublicKey, critical: false));

    csrReq.CertificateExtensions.Add(
        X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
            issuerCert,
            includeKeyIdentifier: true,
            includeIssuerAndSerial: false));

    var kuFlags = BuildKeyUsageFlags(request);
    if (kuFlags != 0)
        csrReq.CertificateExtensions.Add(new X509KeyUsageExtension(kuFlags, critical: true));

    var ekuOids = BuildEkuOids(request);
    if (ekuOids.Count > 0)
        csrReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(ekuOids, critical: false));

    var sans = request.Sans.Select(s => s.Value).Distinct().ToList();
    if (sans.Count > 0)
    {
        var sanBuilder = new SubjectAlternativeNameBuilder();
        foreach (var s in sans) sanBuilder.AddDnsName(s);
        csrReq.CertificateExtensions.Add(sanBuilder.Build(critical: false));
    }

    var notBefore = DateTimeOffset.UtcNow.AddSeconds(-5);
    var notAfter = notBefore.AddDays(request.ValidityDays);

    var serial = new byte[16];
    RandomNumberGenerator.Fill(serial);

    using var issued = csrReq.Create(
        issuerCert.SubjectName,
        X509SignatureGenerator.CreateForRSA(issuerKey, RSASignaturePadding.Pkcs1),
        notBefore,
        notAfter,
        serial);

    var pemPath = Path.Combine(outputDirectory, $"{safeName}.pem");
    var crtPath = Path.Combine(outputDirectory, $"{safeName}.crt");
    File.WriteAllText(pemPath, issued.ExportCertificatePem());
    File.WriteAllBytes(crtPath, issued.Export(X509ContentType.Cert));

    return Task.FromResult(new CertificateGenerationResult
    {
        OutputDirectory = outputDirectory,
        GeneratedFiles = [pemPath, crtPath],
        CertPemPath = pemPath,
        KeyPath = null,
    });
}

private static X509KeyUsageFlags BuildKeyUsageFlags(CsrSigningRequest r)
{
    var f = X509KeyUsageFlags.None;
    if (r.KeyUsageDigitalSignature) f |= X509KeyUsageFlags.DigitalSignature;
    if (r.KeyUsageNonRepudiation)   f |= X509KeyUsageFlags.NonRepudiation;
    if (r.KeyUsageKeyEncipherment)  f |= X509KeyUsageFlags.KeyEncipherment;
    if (r.KeyUsageDataEncipherment) f |= X509KeyUsageFlags.DataEncipherment;
    if (r.KeyUsageKeyAgreement)     f |= X509KeyUsageFlags.KeyAgreement;
    if (r.KeyUsageKeyCertSign)      f |= X509KeyUsageFlags.KeyCertSign;
    if (r.KeyUsageCrlSign)          f |= X509KeyUsageFlags.CrlSign;
    return f;
}

private static OidCollection BuildEkuOids(CsrSigningRequest r)
{
    var oids = new OidCollection();
    if (r.EkuServerAuth)   oids.Add(new Oid("1.3.6.1.5.5.7.3.1"));
    if (r.EkuClientAuth)   oids.Add(new Oid("1.3.6.1.5.5.7.3.2"));
    if (r.EkuCodeSigning)  oids.Add(new Oid("1.3.6.1.5.5.7.3.3"));
    if (r.EkuTimeStamping) oids.Add(new Oid("1.3.6.1.5.5.7.3.8"));
    return oids;
}
```

Note: `KeyPath` becomes `null`; make sure `CertificateGenerationResult.KeyPath` is `string?`. If not, change it to nullable at this step.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~GenerateCertificateFromCsrTests`
Expected: PASS for all 5 tests.

- [ ] **Step 5: Commit**

```bash
git add SelfCertForge.Infrastructure/DotNetCryptoCertificateWorkflowService.cs \
        SelfCertForge.Core.Tests/GenerateCertificateFromCsrTests.cs \
        SelfCertForge.Core/Models/CertificateGenerationResult.cs
git commit -m "feat(csr): implement GenerateCertificateFromCsrAsync"
```

(Adjust the `git add` list if `CertificateGenerationResult.cs` did not need to change.)

---

## Task 6: Implement `ForgeFromCsrAsync` in ForgeService (TDD)

**Files:**
- Modify: `SelfCertForge.Infrastructure/ForgeService.cs`
- Create: `SelfCertForge.Core.Tests/ForgeServiceFromCsrTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// SelfCertForge.Core.Tests/ForgeServiceFromCsrTests.cs
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Infrastructure;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class ForgeServiceFromCsrTests : IDisposable
{
    private readonly string _appDataDir;
    private readonly InMemoryCertificateStore _store = new();
    private readonly InMemoryActivityLog _log = new();
    private readonly DotNetCryptoCertificateWorkflowService _workflow = new();
    private readonly ForgeService _forge;
    private readonly StoredCertificate _ca;

    public ForgeServiceFromCsrTests()
    {
        _appDataDir = Path.Combine(Path.GetTempPath(), "scf-forge-csr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appDataDir);
        _forge = new ForgeService(_store, _log, _workflow, _appDataDir);

        // Forge a root to act as CA
        var root = _forge.ForgeAsync(new ForgeRequest
        {
            Mode = ForgeMode.Root,
            CommonName = "Test CA",
            ValidityDays = 1825,
            KeyBits = 2048,
        }).Result;
        _ca = root;
    }

    public void Dispose() => Directory.Delete(_appDataDir, true);

    [Fact]
    public async Task ForgeFromCsr_persists_StoredCertificate_with_IssuedFromCsr_true_and_no_private_key()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-007");
        var stored = await _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(
            new CsrSigningRequest(
                SigningAuthorityId: _ca.Id,
                RawCsrPem: csrPem,
                SourceCsrFilename: "device-007.csr",
                ValidityDays: 397,
                Sans: Array.Empty<CsrSignedSanEntry>(),
                KeyUsageDigitalSignature: true,
                KeyUsageNonRepudiation: false,
                KeyUsageKeyEncipherment: true,
                KeyUsageDataEncipherment: false,
                KeyUsageKeyAgreement: false,
                KeyUsageKeyCertSign: false,
                KeyUsageCrlSign: false,
                EkuServerAuth: true,
                EkuClientAuth: false,
                EkuCodeSigning: false,
                EkuTimeStamping: false,
                SignatureHashAlgorithm: HashAlgorithmKind.Sha256)));

        Assert.True(stored.IssuedFromCsr);
        Assert.Null(stored.PrivateKeyPath);
        Assert.Equal("device-007.csr", stored.SourceCsrFilename);
        Assert.Equal(_ca.Id, stored.IssuerId);
        Assert.Contains("CN=device-007", stored.Subject);
        Assert.Single(_store.All, c => c.Id == stored.Id);
    }

    [Fact]
    public async Task ForgeFromCsr_appends_SignedFromCsr_activity_entry()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-008");
        var stored = await _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(
            MinimalRequest(_ca.Id, csrPem, "device-008.csr")));

        Assert.Contains(_log.Entries, e => e.Kind == "SignedFromCsr" && e.CertificateId == stored.Id);
    }

    [Fact]
    public async Task ForgeFromCsr_throws_when_csr_fails_reinspection()
    {
        var bad = CsrFixtureGenerator.NotACsr();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(
                MinimalRequest(_ca.Id, bad, "bad.csr"))));
    }

    [Fact]
    public async Task ForgeFromCsr_throws_when_signing_authority_missing()
    {
        var csrPem = CsrFixtureGenerator.ValidRsa(2048, "CN=device-009");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(
                MinimalRequest("does-not-exist", csrPem, "device-009.csr"))));
    }

    private static CsrSigningRequest MinimalRequest(string caId, string csrPem, string filename) => new(
        SigningAuthorityId: caId,
        RawCsrPem: csrPem,
        SourceCsrFilename: filename,
        ValidityDays: 397,
        Sans: Array.Empty<CsrSignedSanEntry>(),
        KeyUsageDigitalSignature: true,
        KeyUsageNonRepudiation: false,
        KeyUsageKeyEncipherment: false,
        KeyUsageDataEncipherment: false,
        KeyUsageKeyAgreement: false,
        KeyUsageKeyCertSign: false,
        KeyUsageCrlSign: false,
        EkuServerAuth: false,
        EkuClientAuth: false,
        EkuCodeSigning: false,
        EkuTimeStamping: false,
        SignatureHashAlgorithm: HashAlgorithmKind.Sha256);

    private sealed class InMemoryCertificateStore : ICertificateStore
    {
        private readonly List<StoredCertificate> _items = new();
        public IReadOnlyList<StoredCertificate> All => _items;
        public event EventHandler? Changed;
        public Task AddAsync(StoredCertificate c, CancellationToken ct = default)
        { _items.Add(c); Changed?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
        public Task UpdateAsync(StoredCertificate c, CancellationToken ct = default)
        {
            var i = _items.FindIndex(x => x.Id == c.Id);
            if (i >= 0) _items[i] = c;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string id, CancellationToken ct = default)
        { _items.RemoveAll(x => x.Id == id); Changed?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemoryActivityLog : IActivityLog
    {
        private readonly List<ActivityEntry> _entries = new();
        public IReadOnlyList<ActivityEntry> Entries => _entries;
        public event EventHandler? Changed;
        public Task AppendAsync(ActivityEntry e, CancellationToken ct = default)
        { _entries.Add(e); Changed?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken ct = default)
        { _entries.Clear(); Changed?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
    }
}
```

Adjust the in-memory store/log shapes if the real interfaces have additional members. Check `SelfCertForge.Core/Abstractions/ICertificateStore.cs` and `IActivityLog.cs` before writing — implement every required member.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~ForgeServiceFromCsrTests`
Expected: FAIL — method not implemented.

- [ ] **Step 3: Implement `ForgeFromCsrAsync`**

Add to `SelfCertForge.Infrastructure/ForgeService.cs`:

```csharp
public async Task<StoredCertificate> ForgeFromCsrAsync(ForgeFromCsrRequest request, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(request);
    var signing = request.SigningRequest;

    var inspection = await _workflow.InspectCsrAsync(signing.RawCsrPem, ct).ConfigureAwait(false);
    if (!inspection.IsValid)
        throw new InvalidOperationException(
            "The CSR failed re-inspection at signing time. It may have been modified after the dialog was opened.");

    var issuer = _store.All.FirstOrDefault(c => c.Id == signing.SigningAuthorityId)
        ?? throw new InvalidOperationException("Issuing root authority not found.");
    if (issuer.CertificatePath is null || issuer.PrivateKeyPath is null)
        throw new InvalidOperationException(
            "Issuing root authority has no key files on disk. It may have been created outside SelfCertForge.");

    var id = Guid.NewGuid().ToString("N");
    var outputDir = Path.Combine(_appDataDirectory, "certificates", id);
    var safeName = ToSafeFileName(
        ExtractCommonName(inspection.Summary!.SubjectDistinguishedName) ?? "csr-signed");

    CertificateGenerationResult result;
    try
    {
        result = await _workflow.GenerateCertificateFromCsrAsync(
            signing, issuer.CertificatePath, issuer.PrivateKeyPath, outputDir, safeName, ct)
            .ConfigureAwait(false);
    }
    catch
    {
        TryCleanupDir(outputDir);
        throw;
    }

    var certPem = await File.ReadAllTextAsync(result.CertPemPath, ct).ConfigureAwait(false);
    using var x509 = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(certPem);

    var sha256 = FormatColonHex(x509.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256));
    var sha1 = FormatColonHex(x509.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA1));
    var serial = FormatColonHex(x509.SerialNumberBytes.Span);
    var algorithm = x509.SignatureAlgorithm.FriendlyName is { Length: > 0 } n ? n : "RSA";

    var (keyUsages, ekuList) = ExtractKuAndEkuStrings(x509);

    var stored = new StoredCertificate(
        Id: id,
        Kind: StoredCertificateKind.Child,
        CommonName: ExtractCommonName(x509.Subject) ?? safeName,
        Subject: x509.Subject,
        IssuerId: issuer.Id,
        IssuerName: issuer.CommonName,
        Sans: signing.Sans.Select(s => s.Value).Distinct().ToList(),
        Algorithm: algorithm,
        Serial: serial,
        Sha256: sha256,
        Sha1: sha1,
        IssuedAt: ToUtcOffset(x509.NotBefore),
        ExpiresAt: ToUtcOffset(x509.NotAfter),
        InstalledInTrustStore: false,
        CertificatePath: result.CertPemPath,
        PrivateKeyPath: null,
        OutputDirectory: result.OutputDirectory,
        KeyUsages: keyUsages.Count > 0 ? keyUsages : null,
        ExtendedKeyUsages: ekuList.Count > 0 ? ekuList : null,
        IssuedFromCsr: true,
        SourceCsrFilename: signing.SourceCsrFilename);

    await _store.AddAsync(stored, ct).ConfigureAwait(false);

    await _log.AppendAsync(new ActivityEntry(
        Id: Guid.NewGuid().ToString("N"),
        At: DateTimeOffset.UtcNow,
        Kind: "SignedFromCsr",
        Message: $"Signed certificate from CSR \"{signing.SourceCsrFilename}\" issued by {issuer.CommonName}.",
        CertificateId: stored.Id), ct).ConfigureAwait(false);

    return stored;
}

private static string? ExtractCommonName(string distinguishedName)
{
    foreach (var part in distinguishedName.Split(','))
    {
        var trimmed = part.Trim();
        if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            return trimmed[3..];
    }
    return null;
}

private static (List<string> ku, List<string> eku) ExtractKuAndEkuStrings(
    System.Security.Cryptography.X509Certificates.X509Certificate2 cert)
{
    var ku = new List<string>();
    var eku = new List<string>();
    foreach (var ext in cert.Extensions)
    {
        if (ext is System.Security.Cryptography.X509Certificates.X509KeyUsageExtension k)
        {
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DigitalSignature)) ku.Add("Digital Signature");
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.NonRepudiation))   ku.Add("Non-Repudiation");
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyEncipherment))  ku.Add("Key Encipherment");
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.DataEncipherment)) ku.Add("Data Encipherment");
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyAgreement))     ku.Add("Key Agreement");
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.KeyCertSign))      ku.Add("Certificate Signing");
            if (k.KeyUsages.HasFlag(System.Security.Cryptography.X509Certificates.X509KeyUsageFlags.CrlSign))          ku.Add("CRL Signing");
        }
        else if (ext is System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension e)
        {
            foreach (var oid in e.EnhancedKeyUsages)
                eku.Add(oid.FriendlyName is { Length: > 0 } fn ? fn : oid.Value ?? "Unknown");
        }
    }
    return (ku, eku);
}

private static void TryCleanupDir(string dir)
{
    try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    catch { /* best-effort */ }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~ForgeServiceFromCsrTests`
Expected: PASS for all 4 tests.

- [ ] **Step 5: Run full suite to confirm no regressions**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Infrastructure/ForgeService.cs \
        SelfCertForge.Core.Tests/ForgeServiceFromCsrTests.cs
git commit -m "feat(csr): implement ForgeFromCsrAsync with re-inspection and activity logging"
```

---

## Task 7: CreateFromCsrDialogViewModel (TDD)

**Files:**
- Create: `SelfCertForge.Core/Presentation/CsrSanOriginRowViewModel.cs`
- Create: `SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs`
- Create: `SelfCertForge.Core.Tests/CreateFromCsrDialogViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// SelfCertForge.Core.Tests/CreateFromCsrDialogViewModelTests.cs
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class CreateFromCsrDialogViewModelTests
{
    private static CsrSummary BasicSummary(
        IReadOnlyList<string>? sans = null,
        CsrRequestedKeyUsages? ku = null,
        CsrRequestedEkus? eku = null) =>
        new("CN=device", "RSA", 2048, "ABCD", "PEM",
            sans ?? Array.Empty<string>(), ku, eku);

    [Fact]
    public void Initialize_locks_subject_and_keysize_seeds_validity()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "thing.csr");

        Assert.Equal("CN=device", vm.SubjectDistinguishedName);
        Assert.Equal(2048, vm.PublicKeyBits);
        Assert.Equal("Test CA", vm.SigningAuthorityName);
        Assert.Equal("thing.csr", vm.SourceCsrFilename);
        Assert.Equal(397, vm.ValidityDays);
    }

    [Fact]
    public void Initialize_prefills_sans_with_FromCsr_origin()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA",
            BasicSummary(sans: new[] { "a.example", "b.example" }), "x.csr");

        Assert.Equal(2, vm.SanEntries.Count);
        Assert.All(vm.SanEntries, s => Assert.Equal(CsrSignedSanOrigin.FromCsr, s.Origin));
    }

    [Fact]
    public void AddSan_appends_AddedByOperator()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");
        vm.NewSanValue = "extra.example";
        vm.AddSanCommand.Execute(null);

        Assert.Single(vm.SanEntries);
        Assert.Equal("extra.example", vm.SanEntries[0].Value);
        Assert.Equal(CsrSignedSanOrigin.AddedByOperator, vm.SanEntries[0].Origin);
        Assert.Equal(string.Empty, vm.NewSanValue);
    }

    [Fact]
    public void RemoveSan_works_for_either_origin()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(sans: new[] { "a" }), "x.csr");
        vm.NewSanValue = "b";
        vm.AddSanCommand.Execute(null);

        vm.SanEntries[0].RemoveCommand.Execute(null);
        Assert.Single(vm.SanEntries);
        Assert.Equal("b", vm.SanEntries[0].Value);
    }

    [Fact]
    public void KeyUsage_locked_when_csr_supplies_one()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        var ku = new CsrRequestedKeyUsages(true, false, true, false, false, false, false);
        vm.Initialize("ca", "Test CA", BasicSummary(ku: ku), "x.csr");

        Assert.True(vm.IsKeyUsageLocked);
        Assert.True(vm.KeyUsageDigitalSignature);
        Assert.True(vm.KeyUsageKeyEncipherment);
        Assert.False(vm.KeyUsageNonRepudiation);
    }

    [Fact]
    public void KeyUsage_editable_when_csr_omits_it()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(ku: null), "x.csr");

        Assert.False(vm.IsKeyUsageLocked);
    }

    [Fact]
    public void Eku_locked_when_csr_supplies_one()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        var eku = new CsrRequestedEkus(true, true, false, false, false);
        vm.Initialize("ca", "Test CA", BasicSummary(eku: eku), "x.csr");

        Assert.True(vm.IsEkuLocked);
        Assert.True(vm.EkuServerAuth);
        Assert.True(vm.EkuClientAuth);
    }

    [Fact]
    public void CanSubmit_false_when_ValidityDays_zero()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");
        vm.ValidityDays = 0;
        Assert.False(vm.CanSubmit);
    }

    [Fact]
    public void CanSubmit_false_when_SigningAuthorityId_empty()
    {
        var vm = new CreateFromCsrDialogViewModel(new FakeForge(), preferences: null);
        vm.Initialize("", "Test CA", BasicSummary(), "x.csr");
        Assert.False(vm.CanSubmit);
    }

    [Fact]
    public async Task Submit_calls_ForgeFromCsr_and_raises_Created()
    {
        var forge = new FakeForge();
        var vm = new CreateFromCsrDialogViewModel(forge, preferences: null);
        vm.Initialize("ca", "Test CA", BasicSummary(), "x.csr");

        StoredCertificate? captured = null;
        vm.Created += (_, c) => captured = c;

        await vm.SubmitAsyncForTest();

        Assert.NotNull(captured);
        Assert.True(forge.LastRequest!.SigningRequest.SigningAuthorityId == "ca");
        Assert.Equal("x.csr", forge.LastRequest.SigningRequest.SourceCsrFilename);
    }

    private sealed class FakeForge : IForgeService
    {
        public ForgeFromCsrRequest? LastRequest { get; private set; }

        public Task<StoredCertificate> ForgeAsync(ForgeRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<StoredCertificate> ForgeFromCsrAsync(ForgeFromCsrRequest r, CancellationToken ct = default)
        {
            LastRequest = r;
            return Task.FromResult(new StoredCertificate(
                "id", StoredCertificateKind.Child, "device", "CN=device",
                r.SigningRequest.SigningAuthorityId, "Test CA", Array.Empty<string>(),
                "RSA", "01", "AA", "BB",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
                false, null, null, null, null, null, true, r.SigningRequest.SourceCsrFilename));
        }
    }
}
```

`SubmitAsyncForTest` is an internal helper we add on the VM to expose `SubmitAsync` to tests without going through `ICommand`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CreateFromCsrDialogViewModelTests`
Expected: FAIL — VM not implemented.

- [ ] **Step 3: Implement `CsrSanOriginRowViewModel`**

```csharp
// SelfCertForge.Core/Presentation/CsrSanOriginRowViewModel.cs
using System.Windows.Input;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CsrSanOriginRowViewModel : ObservableObject
{
    public CsrSanOriginRowViewModel(string value, CsrSignedSanOrigin origin, Action<CsrSanOriginRowViewModel> remove)
    {
        Value = value;
        Origin = origin;
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    public string Value { get; }
    public CsrSignedSanOrigin Origin { get; }
    public bool IsFromCsr => Origin == CsrSignedSanOrigin.FromCsr;
    public ICommand RemoveCommand { get; }
}
```

- [ ] **Step 4: Implement `CreateFromCsrDialogViewModel`**

```csharp
// SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.Core.Presentation;

public sealed class CreateFromCsrDialogViewModel : ObservableObject
{
    private readonly IForgeService _forge;
    private readonly IUserPreferencesStore? _preferences;

    private string _signingAuthorityId = string.Empty;
    private string _signingAuthorityName = string.Empty;
    private string _subjectDn = string.Empty;
    private int _publicKeyBits;
    private string _publicKeyAlgorithm = string.Empty;
    private string _publicKeyFingerprint = string.Empty;
    private string _rawCsrPem = string.Empty;
    private string _sourceCsrFilename = string.Empty;
    private int _validityDays = 397;
    private bool _isCreating;
    private string? _errorMessage;
    private string _newSanValue = string.Empty;

    private bool _isKuLocked;
    private bool _isEkuLocked;

    private bool _kuDigitalSignature, _kuNonRepudiation, _kuKeyEncipherment, _kuDataEncipherment,
                 _kuKeyAgreement, _kuKeyCertSign, _kuCrlSign;
    private bool _ekuServerAuth, _ekuClientAuth, _ekuCodeSigning, _ekuTimeStamping;
    private HashAlgorithmKind _hashAlgorithm = HashAlgorithmKind.Sha256;

    public CreateFromCsrDialogViewModel(IForgeService forge, IUserPreferencesStore? preferences = null)
    {
        _forge = forge;
        _preferences = preferences;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => CanSubmit);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        AddSanCommand = new RelayCommand(AddSan,
            () => !string.IsNullOrWhiteSpace(_newSanValue) && !_isCreating);
    }

    public event EventHandler<StoredCertificate>? Created;
    public event EventHandler? CancelRequested;

    public ICommand SubmitCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddSanCommand { get; }

    public ObservableCollection<CsrSanOriginRowViewModel> SanEntries { get; } = new();

    public string SigningAuthorityId => _signingAuthorityId;
    public string SigningAuthorityName { get => _signingAuthorityName; private set => SetProperty(ref _signingAuthorityName, value); }
    public string SubjectDistinguishedName { get => _subjectDn; private set => SetProperty(ref _subjectDn, value); }
    public int PublicKeyBits { get => _publicKeyBits; private set => SetProperty(ref _publicKeyBits, value); }
    public string PublicKeyAlgorithm { get => _publicKeyAlgorithm; private set => SetProperty(ref _publicKeyAlgorithm, value); }
    public string PublicKeyFingerprintSha256 { get => _publicKeyFingerprint; private set => SetProperty(ref _publicKeyFingerprint, value); }
    public string SourceCsrFilename { get => _sourceCsrFilename; private set => SetProperty(ref _sourceCsrFilename, value); }

    public int ValidityDays { get => _validityDays; set { if (SetProperty(ref _validityDays, value)) Notify(); } }

    public string NewSanValue
    {
        get => _newSanValue;
        set { if (SetProperty(ref _newSanValue, value)) (AddSanCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public bool IsKeyUsageLocked { get => _isKuLocked; private set => SetProperty(ref _isKuLocked, value); }
    public bool IsEkuLocked { get => _isEkuLocked; private set => SetProperty(ref _isEkuLocked, value); }

    public bool KeyUsageDigitalSignature { get => _kuDigitalSignature; set { if (!IsKeyUsageLocked) SetProperty(ref _kuDigitalSignature, value); } }
    public bool KeyUsageNonRepudiation   { get => _kuNonRepudiation;   set { if (!IsKeyUsageLocked) SetProperty(ref _kuNonRepudiation, value); } }
    public bool KeyUsageKeyEncipherment  { get => _kuKeyEncipherment;  set { if (!IsKeyUsageLocked) SetProperty(ref _kuKeyEncipherment, value); } }
    public bool KeyUsageDataEncipherment { get => _kuDataEncipherment; set { if (!IsKeyUsageLocked) SetProperty(ref _kuDataEncipherment, value); } }
    public bool KeyUsageKeyAgreement     { get => _kuKeyAgreement;     set { if (!IsKeyUsageLocked) SetProperty(ref _kuKeyAgreement, value); } }
    public bool KeyUsageKeyCertSign      { get => _kuKeyCertSign;      set { if (!IsKeyUsageLocked) SetProperty(ref _kuKeyCertSign, value); } }
    public bool KeyUsageCrlSign          { get => _kuCrlSign;          set { if (!IsKeyUsageLocked) SetProperty(ref _kuCrlSign, value); } }

    public bool EkuServerAuth   { get => _ekuServerAuth;   set { if (!IsEkuLocked) SetProperty(ref _ekuServerAuth, value); } }
    public bool EkuClientAuth   { get => _ekuClientAuth;   set { if (!IsEkuLocked) SetProperty(ref _ekuClientAuth, value); } }
    public bool EkuCodeSigning  { get => _ekuCodeSigning;  set { if (!IsEkuLocked) SetProperty(ref _ekuCodeSigning, value); } }
    public bool EkuTimeStamping { get => _ekuTimeStamping; set { if (!IsEkuLocked) SetProperty(ref _ekuTimeStamping, value); } }

    public HashAlgorithmKind HashAlgorithm { get => _hashAlgorithm; set => SetProperty(ref _hashAlgorithm, value); }

    public bool IsCreating { get => _isCreating; private set => SetProperty(ref _isCreating, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

    public bool CanSubmit =>
        !_isCreating && _validityDays > 0 && !string.IsNullOrEmpty(_signingAuthorityId);

    public void Initialize(string signingAuthorityId, string signingAuthorityName,
        CsrSummary summary, string sourceCsrFilename)
    {
        _signingAuthorityId = signingAuthorityId;
        SigningAuthorityName = signingAuthorityName;
        SubjectDistinguishedName = summary.SubjectDistinguishedName;
        PublicKeyAlgorithm = summary.PublicKeyAlgorithm;
        PublicKeyBits = summary.PublicKeyBits;
        PublicKeyFingerprintSha256 = summary.PublicKeyFingerprintSha256;
        _rawCsrPem = summary.RawCsrPem;
        SourceCsrFilename = sourceCsrFilename;
        ValidityDays = _preferences?.Current.SignedValidityDays ?? 397;

        SanEntries.Clear();
        foreach (var s in summary.RequestedSans)
            SanEntries.Add(new CsrSanOriginRowViewModel(s, CsrSignedSanOrigin.FromCsr, RemoveSan));

        if (summary.RequestedKeyUsage is { } ku)
        {
            IsKeyUsageLocked = true;
            _kuDigitalSignature = ku.DigitalSignature;
            _kuNonRepudiation = ku.NonRepudiation;
            _kuKeyEncipherment = ku.KeyEncipherment;
            _kuDataEncipherment = ku.DataEncipherment;
            _kuKeyAgreement = ku.KeyAgreement;
            _kuKeyCertSign = ku.KeyCertSign;
            _kuCrlSign = ku.CrlSign;
        }
        else
        {
            IsKeyUsageLocked = false;
            _kuDigitalSignature = true;
            _kuKeyEncipherment = true;
        }

        if (summary.RequestedEkus is { } e)
        {
            IsEkuLocked = true;
            _ekuServerAuth = e.ServerAuth;
            _ekuClientAuth = e.ClientAuth;
            _ekuCodeSigning = e.CodeSigning;
            _ekuTimeStamping = e.TimeStamping;
        }
        else
        {
            IsEkuLocked = false;
        }

        Notify();
    }

    private void AddSan()
    {
        var v = _newSanValue.Trim();
        if (string.IsNullOrEmpty(v)) return;
        SanEntries.Add(new CsrSanOriginRowViewModel(v, CsrSignedSanOrigin.AddedByOperator, RemoveSan));
        NewSanValue = string.Empty;
    }

    private void RemoveSan(CsrSanOriginRowViewModel row) => SanEntries.Remove(row);

    internal Task SubmitAsyncForTest() => SubmitAsync();

    private async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        IsCreating = true;
        ErrorMessage = null;
        try
        {
            var request = new CsrSigningRequest(
                SigningAuthorityId: _signingAuthorityId,
                RawCsrPem: _rawCsrPem,
                SourceCsrFilename: _sourceCsrFilename,
                ValidityDays: _validityDays,
                Sans: SanEntries.Select(r => new CsrSignedSanEntry(r.Value, r.Origin)).ToArray(),
                KeyUsageDigitalSignature: _kuDigitalSignature,
                KeyUsageNonRepudiation: _kuNonRepudiation,
                KeyUsageKeyEncipherment: _kuKeyEncipherment,
                KeyUsageDataEncipherment: _kuDataEncipherment,
                KeyUsageKeyAgreement: _kuKeyAgreement,
                KeyUsageKeyCertSign: _kuKeyCertSign,
                KeyUsageCrlSign: _kuCrlSign,
                EkuServerAuth: _ekuServerAuth,
                EkuClientAuth: _ekuClientAuth,
                EkuCodeSigning: _ekuCodeSigning,
                EkuTimeStamping: _ekuTimeStamping,
                SignatureHashAlgorithm: _hashAlgorithm);

            var stored = await _forge.ForgeFromCsrAsync(new ForgeFromCsrRequest(request));
            Created?.Invoke(this, stored);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsCreating = false;
            Notify();
        }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(CanSubmit));
        (SubmitCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (AddSanCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CreateFromCsrDialogViewModelTests`
Expected: PASS for all 10 tests.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Presentation/CsrSanOriginRowViewModel.cs \
        SelfCertForge.Core/Presentation/CreateFromCsrDialogViewModel.cs \
        SelfCertForge.Core.Tests/CreateFromCsrDialogViewModelTests.cs
git commit -m "feat(csr): add CreateFromCsrDialogViewModel with KU/EKU lock + SAN origin tracking"
```

---

## Task 8: Authorities VM — CreateFromCsrCommand wiring (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs`
- Create: `SelfCertForge.Core.Tests/AuthoritiesViewModelCsrTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// SelfCertForge.Core.Tests/AuthoritiesViewModelCsrTests.cs
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class AuthoritiesViewModelCsrTests
{
    [Fact]
    public async Task CreateFromCsr_pick_cancelled_returns_silently()
    {
        var picker = new FakePicker(result: null);
        var workflow = new FakeWorkflow();
        var dialog = new FakeDialog();
        var confirm = new FakeConfirmation();
        var row = new AuthorityRowViewModel(
            BuildRoot(), isTrusted: false,
            createSignedCertDialog: new NoopSignedDialog(),
            createFromCsrDialog: dialog, csrFilePicker: picker,
            workflow: workflow, confirmation: confirm,
            nav: new NoopNav());

        await ((AsyncRelayCommand)row.CreateFromCsrCommand).ExecuteAsync(null);

        Assert.False(dialog.WasShown);
        Assert.False(confirm.WasShown);
    }

    [Fact]
    public async Task CreateFromCsr_validation_failure_shows_confirmation_alert_and_does_not_open_dialog()
    {
        var picker = new FakePicker(new CsrFilePickResult("/tmp/bad.csr", "garbage"));
        var workflow = new FakeWorkflow(result: new CsrInspectionResult(
            false, null, new[] { CsrValidationError.Malformed }));
        var dialog = new FakeDialog();
        var confirm = new FakeConfirmation();
        var row = new AuthorityRowViewModel(
            BuildRoot(), false, new NoopSignedDialog(),
            dialog, picker, workflow, confirm, new NoopNav());

        await ((AsyncRelayCommand)row.CreateFromCsrCommand).ExecuteAsync(null);

        Assert.True(confirm.WasShown);
        Assert.False(dialog.WasShown);
    }

    [Fact]
    public async Task CreateFromCsr_valid_csr_opens_dialog_with_summary()
    {
        var summary = new CsrSummary("CN=x", "RSA", 2048, "FP", "PEM",
            Array.Empty<string>(), null, null);
        var picker = new FakePicker(new CsrFilePickResult("/tmp/x.csr", "PEM"));
        var workflow = new FakeWorkflow(new CsrInspectionResult(true, summary, Array.Empty<CsrValidationError>()));
        var dialog = new FakeDialog();
        var confirm = new FakeConfirmation();
        var ca = BuildRoot();
        var row = new AuthorityRowViewModel(
            ca, false, new NoopSignedDialog(),
            dialog, picker, workflow, confirm, new NoopNav());

        await ((AsyncRelayCommand)row.CreateFromCsrCommand).ExecuteAsync(null);

        Assert.True(dialog.WasShown);
        Assert.Equal(ca.Id, dialog.LastSigningAuthorityId);
        Assert.Equal("x.csr", dialog.LastSourceCsrFilename);
        Assert.Same(summary, dialog.LastSummary);
    }

    private static StoredCertificate BuildRoot() => new(
        "ca-id", StoredCertificateKind.Root, "Test CA", "CN=Test CA",
        null, null, Array.Empty<string>(), "RSA", "01", "AA", "BB",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(5), false);

    private sealed class FakePicker : ICsrFilePicker
    {
        private readonly CsrFilePickResult? _r;
        public FakePicker(CsrFilePickResult? result) { _r = result; }
        public Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default) => Task.FromResult(_r);
    }

    private sealed class FakeWorkflow : ICertificateWorkflowService
    {
        private readonly CsrInspectionResult? _r;
        public FakeWorkflow(CsrInspectionResult? result = null) { _r = result; }
        public Task<CsrInspectionResult> InspectCsrAsync(string csrPem, CancellationToken ct = default)
            => Task.FromResult(_r ?? new CsrInspectionResult(false, null, new[] { CsrValidationError.Malformed }));
        public Task<CertificateGenerationResult> GenerateRootCertificateAsync(RootCertificateRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CertificateGenerationResult> GenerateSignedCertificateAsync(SignedCertificateRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CertificateGenerationResult> GenerateCertificateFromCsrAsync(
            CsrSigningRequest r, string a, string b, string c, string d, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeDialog : ICreateFromCsrDialog
    {
        public bool WasShown;
        public string? LastSigningAuthorityId;
        public CsrSummary? LastSummary;
        public string? LastSourceCsrFilename;
        public Task<StoredCertificate?> ShowAsync(string signingAuthorityId, string signingAuthorityName,
            CsrSummary csrSummary, string sourceCsrFilename, CancellationToken ct = default)
        {
            WasShown = true;
            LastSigningAuthorityId = signingAuthorityId;
            LastSummary = csrSummary;
            LastSourceCsrFilename = sourceCsrFilename;
            return Task.FromResult<StoredCertificate?>(null);
        }
    }

    private sealed class FakeConfirmation : IConfirmationDialog
    {
        public bool WasShown;
        public Task<bool> ShowAsync(string t, string m, string c = "Confirm", string x = "Cancel")
        {
            WasShown = true;
            return Task.FromResult(false);
        }
    }

    private sealed class NoopSignedDialog : ICreateSignedCertDialog
    {
        public Task<StoredCertificate?> ShowAsync(string a, string b) => Task.FromResult<StoredCertificate?>(null);
    }

    private sealed class NoopNav : INavigationService
    {
        public Task NavigateToAsync(AppRoute route, CancellationToken ct = default) => Task.CompletedTask;
    }
}
```

Adjust the constructor signature for `AuthorityRowViewModel` if the existing one differs; the test pins what the API should look like after this task.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~AuthoritiesViewModelCsrTests`
Expected: FAIL — `CreateFromCsrCommand` not implemented.

- [ ] **Step 3: Modify `AuthoritiesViewModel` and `AuthorityRowViewModel`**

In `SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs`:

1. Inject `ICreateFromCsrDialog`, `ICsrFilePicker`, `IConfirmationDialog`, `ICertificateWorkflowService` into `AuthoritiesViewModel` constructor.
2. Pass them into `AuthorityRowViewModel` ctor when mapping `_certificates.Select(...)`.
3. Add `CreateFromCsrCommand` to `AuthorityRowViewModel`:

```csharp
public AuthorityRowViewModel(
    StoredCertificate source,
    bool isTrusted,
    ICreateSignedCertDialog createSignedCertDialog,
    ICreateFromCsrDialog createFromCsrDialog,
    ICsrFilePicker csrFilePicker,
    ICertificateWorkflowService workflow,
    IConfirmationDialog confirmation,
    INavigationService nav)
{
    // ... existing assignments ...
    CreateFromCsrCommand = new AsyncRelayCommand(async () =>
    {
        var pick = await csrFilePicker.PickCsrFileAsync();
        if (pick is null) return;

        var inspection = await workflow.InspectCsrAsync(pick.Contents);
        if (!inspection.IsValid || inspection.Summary is null)
        {
            await confirmation.ShowAsync(
                title: "Invalid Certificate Signing Request",
                message: CsrValidationErrorMessages.Format(inspection.Errors),
                confirmLabel: "OK",
                cancelLabel: "OK");
            return;
        }

        var filename = Path.GetFileName(pick.FilePath);
        var cert = await createFromCsrDialog.ShowAsync(source.Id, source.CommonName, inspection.Summary, filename);
        if (cert is not null)
            await nav.NavigateToAsync(AppRoute.Certificates);
    });
}

public ICommand CreateFromCsrCommand { get; }
```

4. Propagate `CreateFromCsrCommand` to `AuthorityDetailViewModel` (`internal AuthorityDetailViewModel(... ICommand createFromCsrCommand)`).

`IConfirmationDialog.ShowAsync` currently returns a `bool`; using identical `confirmLabel`/`cancelLabel` of "OK" makes the result irrelevant — that gives us a single-button alert without changing the interface. If the dialog implementation does not visually collapse to a single button when labels match, instead add an `alertOnly: true` overload in a follow-up — but only if the smoke test in Task 13 actually requires it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~AuthoritiesViewModelCsrTests`
Expected: PASS for all 3 tests.

- [ ] **Step 5: Run full Core tests**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj`
Expected: All tests pass — existing `AuthoritiesViewModelTests` may need their fakes updated for the new ctor params; update the tests.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.Core/Presentation/AuthoritiesViewModel.cs \
        SelfCertForge.Core.Tests/AuthoritiesViewModelCsrTests.cs \
        SelfCertForge.Core.Tests/AuthoritiesViewModelTests.cs
git commit -m "feat(csr): add CreateFromCsrCommand to AuthorityRowViewModel"
```

---

## Task 9: Certificates VM — surface IsFromCsr, disable PFX/key export (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/CertificatesViewModel.cs` (and `CertificateRowViewModel` inside)
- Modify or extend: `SelfCertForge.Core.Tests/CertificateStatusTests.cs` (or add `CertificatesViewModelCsrTests.cs`)

- [ ] **Step 1: Write failing tests**

```csharp
// SelfCertForge.Core.Tests/CertificatesViewModelCsrTests.cs
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;
using Xunit;

namespace SelfCertForge.Core.Tests;

public class CertificatesViewModelCsrTests
{
    [Fact]
    public void IsFromCsr_true_when_StoredCertificate_IssuedFromCsr_true()
    {
        var c = MakeStored(issuedFromCsr: true);
        var row = new CertificateRowViewModel(c, isTrusted: false);
        Assert.True(row.IsFromCsr);
        Assert.False(row.HasPrivateKey);
        Assert.False(row.CanExportPfx);
        Assert.False(row.CanExportKeyPem);
    }

    [Fact]
    public void IsFromCsr_false_for_regular_signed_cert()
    {
        var c = MakeStored(issuedFromCsr: false, privateKeyPath: "/tmp/x.key");
        var row = new CertificateRowViewModel(c, isTrusted: false);
        Assert.False(row.IsFromCsr);
        Assert.True(row.HasPrivateKey);
        Assert.True(row.CanExportPfx);
        Assert.True(row.CanExportKeyPem);
    }

    private static StoredCertificate MakeStored(bool issuedFromCsr, string? privateKeyPath = null) => new(
        "id", StoredCertificateKind.Child, "device", "CN=device",
        "ca-id", "Test CA", Array.Empty<string>(),
        "RSA", "01", "AA", "BB",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
        false,
        CertificatePath: "/tmp/x.pem",
        PrivateKeyPath: privateKeyPath,
        OutputDirectory: null, KeyUsages: null, ExtendedKeyUsages: null,
        IssuedFromCsr: issuedFromCsr,
        SourceCsrFilename: issuedFromCsr ? "x.csr" : null);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CertificatesViewModelCsrTests`
Expected: FAIL — `IsFromCsr`/`HasPrivateKey`/`CanExportPfx`/`CanExportKeyPem` may not all exist on `CertificateRowViewModel`.

- [ ] **Step 3: Implement properties on `CertificateRowViewModel`**

Open `SelfCertForge.Core/Presentation/CertificatesViewModel.cs` and locate `CertificateRowViewModel` (it's defined inside the same file). Add:

```csharp
public bool IsFromCsr => _source.IssuedFromCsr;
public bool HasPrivateKey => !string.IsNullOrEmpty(_source.PrivateKeyPath);
public bool CanExportPfx => HasPrivateKey;
public bool CanExportKeyPem => HasPrivateKey;
public string? SourceCsrFilename => _source.SourceCsrFilename;
```

In the parent `CertificatesViewModel`, update the `ExportPfxCommand` and `ExportKeyPemCommand` `canExecute` predicates to additionally check `SelectedRow?.HasPrivateKey == true`:

```csharp
ExportKeyPemCommand = new AsyncRelayCommand(
    execute: ExportKeyPemAsync,
    canExecute: () => HasSelection && SelectedRow!.HasPrivateKey && _exportService is not null && _folderPicker is not null);

ExportPfxCommand = new AsyncRelayCommand(
    execute: ExportPfxAsync,
    canExecute: () => HasSelection && SelectedRow!.HasPrivateKey && _exportService is not null && _pfxPasswordDialog is not null);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SelfCertForge.Core.Tests/SelfCertForge.Core.Tests.csproj --filter FullyQualifiedName~CertificatesViewModelCsrTests`
Expected: PASS for both tests. Existing `CertificateStatusTests` should still pass.

- [ ] **Step 5: Commit**

```bash
git add SelfCertForge.Core/Presentation/CertificatesViewModel.cs \
        SelfCertForge.Core.Tests/CertificatesViewModelCsrTests.cs
git commit -m "feat(csr): surface IsFromCsr on CertificateRowViewModel, gate PFX/key export"
```

---

## Task 10: Activity log message formatter for SignedFromCsr (TDD)

**Files:**
- Modify: `SelfCertForge.Core/Presentation/DashboardViewModel.cs` (specifically the kind→label mapping inside `ActivityRowViewModel`)
- Modify or extend: `SelfCertForge.Core.Tests/DashboardViewModelTests.cs`

- [ ] **Step 1: Find the existing kind→label mapping**

Grep for the existing labels (e.g., "forged-root", "forged-child"):

```bash
grep -rn "forged-child" SelfCertForge.Core
```

Identify where activity entry `Kind` strings are translated to display labels. Add `"SignedFromCsr"` → `"Signed from CSR"` (or equivalent) to that mapping.

- [ ] **Step 2: Add a focused test**

```csharp
[Fact]
public async Task SignedFromCsr_activity_entry_renders_friendly_label()
{
    var store = new FakeStore();   // existing test fake
    var log = new FakeActivityLog();
    await log.AppendAsync(new ActivityEntry("a", DateTimeOffset.UtcNow,
        "SignedFromCsr", "Signed certificate from CSR \"x.csr\" issued by Test CA.", "cert-id"));

    var vm = new DashboardViewModel(store, log);
    await vm.LoadAsync();

    Assert.Contains(vm.Activity, a => a.KindLabel == "Signed from CSR");
}
```

Reuse whatever fakes the existing `DashboardViewModelTests` already use; add this test alongside them.

- [ ] **Step 3: Run, implement, verify**

Run the test → see it fail. Add the mapping in the VM. Re-run → verify pass.

- [ ] **Step 4: Commit**

```bash
git add SelfCertForge.Core/Presentation/DashboardViewModel.cs \
        SelfCertForge.Core.Tests/DashboardViewModelTests.cs
git commit -m "feat(csr): render SignedFromCsr activity entries with friendly label"
```

---

## Task 11: App icon, FilePickerHelper macOS fix, MauiCsrFilePicker

**Files:**
- Modify: `SelfCertForge.App/Controls/IconPaths.cs`
- Modify: `SelfCertForge.App/Services/FilePickerHelper.cs`
- Create: `SelfCertForge.App/Services/MauiCsrFilePicker.cs`

- [ ] **Step 1: Add the Lucide `signature` geometry**

The Lucide `signature.svg` path data (verify against the actual icon — copy from `node_modules/lucide-static` or https://lucide.dev/icons/signature):

```csharp
// In SelfCertForge.App/Controls/IconPaths.cs
public static readonly Geometry Signature = G(
    "M20 19c-2.8 0-5-2.2-5-5s2.2-5 5-5M9 9l1 0M5 19c-2.8 0-3 -2.2 -3-5s2.2-5 5-5h11M3 3l18 18");
```

If the literal path differs, use the actual SVG `d` attribute from Lucide's `signature` icon. Don't guess — fetch the real source.

- [ ] **Step 2: Patch macOS UTI fallback in `FilePickerHelper`**

Open `SelfCertForge.App/Services/FilePickerHelper.cs` and locate the `#if MACCATALYST` branch that builds the UTType list. Modify so that if any extension fails to resolve via `UTType.CreateFromExtension`, the picker falls back to including `UTTypes.Data` (or `UTTypes.Item`) so unknown extensions like `.csr` are still selectable:

```csharp
#if MACCATALYST
var utis = new List<UTType>();
bool anyUnresolved = false;
foreach (var ext in extensions)
{
    var u = UTType.CreateFromExtension(ext);
    if (u is null) { anyUnresolved = true; continue; }
    utis.Add(u);
}
if (anyUnresolved) utis.Add(UTTypes.Data);
// ... existing usage of utis ...
#endif
```

The exact symbol path will be `UniformTypeIdentifiers.UTTypes.Data` — confirm import via the existing `using UniformTypeIdentifiers;` directive at the top of the file.

- [ ] **Step 3: Create `MauiCsrFilePicker`**

```csharp
// SelfCertForge.App/Services/MauiCsrFilePicker.cs
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Services;

public sealed class MauiCsrFilePicker : ICsrFilePicker
{
    public async Task<CsrFilePickResult?> PickCsrFileAsync(CancellationToken ct = default)
    {
        var path = await FilePickerHelper.PickFileAsync(
            new[] { "csr", "pem", "req", "txt" });
        if (string.IsNullOrEmpty(path)) return null;

        var contents = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return new CsrFilePickResult(path, contents);
    }
}
```

Adjust `FilePickerHelper.PickFileAsync` signature/return type to match the existing one — if it currently returns just a filename, leave the path-vs-filename mismatch to be resolved by the existing helper API.

- [ ] **Step 4: Build the App project**

Run: `dotnet build SelfCertForge.App/SelfCertForge.App.csproj`
Expected: BUILD SUCCEEDED on the current OS's TFM.

- [ ] **Step 5: Commit**

```bash
git add SelfCertForge.App/Controls/IconPaths.cs \
        SelfCertForge.App/Services/FilePickerHelper.cs \
        SelfCertForge.App/Services/MauiCsrFilePicker.cs
git commit -m "feat(csr): add Signature icon, macOS UTI fallback, MauiCsrFilePicker"
```

---

## Task 12: CreateFromCsrDialog XAML + code-behind + Host

**Files:**
- Create: `SelfCertForge.App/Dialogs/CreateFromCsrDialog.xaml`
- Create: `SelfCertForge.App/Dialogs/CreateFromCsrDialog.xaml.cs`
- Create: `SelfCertForge.App/Dialogs/CreateFromCsrDialogHost.cs`
- Create: `SelfCertForge.App/Converters/SanOriginToTagConverter.cs`

- [ ] **Step 1: Add the SAN origin converter**

```csharp
// SelfCertForge.App/Converters/SanOriginToTagConverter.cs
using System.Globalization;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Converters;

public sealed class SanOriginToTagConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is CsrSignedSanOrigin o && o == CsrSignedSanOrigin.FromCsr ? "From CSR" : "Added";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: Author the dialog XAML**

Pattern-match the existing `CreateSignedCertDialog.xaml`:
- Same overlay backdrop and centered 500px card
- Header: Lucide `Signature` icon (`controls:LucideIcon Glyph="{x:Static controls:IconPaths.Signature}"`) followed by `"Sign Certificate Signing Request"` title + `"Signed by {0}." StringFormat` muted line + filename muted line
- Read-only Subject DN field (Entry with `IsReadOnly="True"`)
- Read-only key info row (`"RSA {0} bits", PublicKeyBits`) and fingerprint
- Editable `ValidityDays` numeric entry (validation error styling, same as existing)
- SAN list: same look as existing dialog's SAN list, with a small "From CSR" / "Added" tag rendered via `SanOriginToTagConverter`, plus add-SAN row at the bottom
- KU + EKU sections: same toggles as existing dialog, all wrapped in a `Border` whose `IsEnabled` is bound to `!IsKeyUsageLocked` / `!IsEkuLocked`; show a muted "Locked by CSR" hint when locked
- Hash algorithm picker (Sha256/Sha384/Sha512) same as existing dialog
- Error banner (when `ErrorMessage` non-null)
- Footer with Cancel + Sign buttons (Sign bound to `SubmitCommand`)

Critical parity rule: **no platform-specific XAML.** Use only existing `Color*` and `Style` resources. Geometry icon must be the only icon source.

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:SelfCertForge.Core.Presentation;assembly=SelfCertForge.Core"
             xmlns:controls="clr-namespace:SelfCertForge.App.Controls"
             xmlns:conv="clr-namespace:SelfCertForge.App.Converters"
             x:Class="SelfCertForge.App.Dialogs.CreateFromCsrDialog"
             x:DataType="vm:CreateFromCsrDialogViewModel"
             BackgroundColor="#B2000000">
    <ContentPage.Resources>
        <conv:SanOriginToTagConverter x:Key="SanOriginToTag" />
    </ContentPage.Resources>
    <!-- TODO: full layout following CreateSignedCertDialog.xaml; see structure described above -->
</ContentPage>
```

Replace the TODO comment with the full layout. Time-box this step: copy `CreateSignedCertDialog.xaml`, rename, and swap the bindings — most styling is reusable verbatim. The structural list of sections above is the authoritative checklist.

- [ ] **Step 3: Add code-behind**

```csharp
// SelfCertForge.App/Dialogs/CreateFromCsrDialog.xaml.cs
using SelfCertForge.Core.Models;
using SelfCertForge.Core.Presentation;

namespace SelfCertForge.App.Dialogs;

public partial class CreateFromCsrDialog : ContentPage
{
    private readonly CreateFromCsrDialogViewModel _viewModel;
    internal CreateFromCsrDialogViewModel ViewModel => _viewModel;

    public CreateFromCsrDialog(CreateFromCsrDialogViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
        _viewModel.Created += OnClosed;
        _viewModel.CancelRequested += OnClosed;
    }

    public void PrepareForOpen(string signingAuthorityId, string signingAuthorityName,
        CsrSummary summary, string sourceCsrFilename)
        => _viewModel.Initialize(signingAuthorityId, signingAuthorityName, summary, sourceCsrFilename);

    private void OnClosed(object? sender, object? e)
        => MainThread.BeginInvokeOnMainThread(() => _ = Navigation.PopModalAsync(animated: false));

    private void OnKeyUsageTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Element el && el.BindingContext is CreateFromCsrDialogViewModel vm && !vm.IsKeyUsageLocked)
        {
            // tap-to-toggle via behavior on each row; keep handler minimal
        }
    }
    // Add tap handlers per checkbox if XAML uses TapGestureRecognizer instead of CheckBox bindings.
}
```

Match the tap-handler pattern from `CreateSignedCertDialog.xaml.cs` precisely. If existing dialog uses `TapGestureRecognizer` + handlers, replicate them. If it uses two-way binding on `CheckBox`, do the same.

- [ ] **Step 4: Add the host adapter**

```csharp
// SelfCertForge.App/Dialogs/CreateFromCsrDialogHost.cs
using SelfCertForge.Core.Abstractions;
using SelfCertForge.Core.Models;

namespace SelfCertForge.App.Dialogs;

public sealed class CreateFromCsrDialogHost : ICreateFromCsrDialog
{
    private readonly IServiceProvider _services;
    public CreateFromCsrDialogHost(IServiceProvider services) { _services = services; }

    public Task<StoredCertificate?> ShowAsync(
        string signingAuthorityId, string signingAuthorityName,
        CsrSummary csrSummary, string sourceCsrFilename, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<StoredCertificate?>(TaskCreationOptions.RunContinuationsAsynchronously);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var dialog = _services.GetRequiredService<CreateFromCsrDialog>();
            dialog.PrepareForOpen(signingAuthorityId, signingAuthorityName, csrSummary, sourceCsrFilename);

            void OnCreated(object? s, StoredCertificate c) { Cleanup(); tcs.TrySetResult(c); }
            void OnCancelled(object? s, EventArgs e) { Cleanup(); tcs.TrySetResult(null); }
            void Cleanup()
            {
                dialog.ViewModel.Created -= OnCreated;
                dialog.ViewModel.CancelRequested -= OnCancelled;
            }

            dialog.ViewModel.Created += OnCreated;
            dialog.ViewModel.CancelRequested += OnCancelled;

            var nav = Application.Current?.Windows[0]?.Page?.Navigation
                ?? throw new InvalidOperationException("No navigation context.");
            await nav.PushModalAsync(dialog, animated: false);
        });
        return tcs.Task;
    }
}
```

Mirror the existing `CreateSignedCertDialogHost` if it has a slightly different shape — match it. The pattern here is the spec.

- [ ] **Step 5: Build**

Run: `dotnet build SelfCertForge.App/SelfCertForge.App.csproj`
Expected: SUCCESS.

- [ ] **Step 6: Commit**

```bash
git add SelfCertForge.App/Converters/SanOriginToTagConverter.cs \
        SelfCertForge.App/Dialogs/CreateFromCsrDialog.xaml \
        SelfCertForge.App/Dialogs/CreateFromCsrDialog.xaml.cs \
        SelfCertForge.App/Dialogs/CreateFromCsrDialogHost.cs
git commit -m "feat(csr): add CreateFromCsr dialog, host adapter, and SAN origin converter"
```

---

## Task 13: Authorities + Certificates view XAML changes; DI wiring

**Files:**
- Modify: `SelfCertForge.App/Pages/AuthoritiesView.xaml`
- Modify: `SelfCertForge.App/Pages/CertificatesView.xaml`
- Modify: `SelfCertForge.App/MauiProgram.cs`

- [ ] **Step 1: Add the new button to `AuthoritiesView.xaml`**

Locate the existing "Create Signed Certificate" button block (line ~302 area where `CreateSignedCertCommand` is bound). Insert a sibling button immediately after it (or before — whichever reads more naturally given the surrounding layout) with:

```xml
<Border Style="{StaticResource ActionButtonBorder}" Margin="8,0,0,0">
    <Border.GestureRecognizers>
        <TapGestureRecognizer Command="{Binding CreateFromCsrCommand}" />
    </Border.GestureRecognizers>
    <HorizontalStackLayout Spacing="8" Padding="12,8">
        <controls:LucideIcon Glyph="{x:Static controls:IconPaths.Signature}"
                             HeightRequest="16" WidthRequest="16"
                             Stroke="{StaticResource ColorTextPrimary}" />
        <Label Text="Create From Certificate Signing Request"
               FontFamily="InterSemiBold" FontSize="14"
               TextColor="{StaticResource ColorTextPrimary}" />
    </HorizontalStackLayout>
</Border>
```

Reuse whichever style + `LucideIcon` control name the existing button uses — match it precisely. Look at the existing "Create Signed Certificate" button markup and clone the structure.

- [ ] **Step 2: Add "From CSR" badge + disabled tooltip to `CertificatesView.xaml`**

In the row template / detail panel of `CertificatesView.xaml`:

1. Next to the existing kind/trust badge area, add a small badge bound to `IsFromCsr` (visible when true) with text "From CSR":

```xml
<Border IsVisible="{Binding IsFromCsr}"
        BackgroundColor="{StaticResource ColorAccentMuted}"
        StrokeShape="RoundRectangle 6" Padding="6,2">
    <Label Text="From CSR" FontFamily="InterSemiBold" FontSize="11"
           TextColor="{StaticResource ColorTextPrimary}" />
</Border>
```

2. For the export-PFX and export-key menu items, ensure `IsEnabled` is bound to the row's `HasPrivateKey` (or the parent `CertificatesViewModel.SelectedRow.HasPrivateKey`). The `canExecute` predicates from Task 9 already prevent execution, but disabling the UI affordance prevents user confusion. Add a `ToolTipProperties.Text` for the disabled state explaining "Private key not stored — CSR-signed certs ship without one."

- [ ] **Step 3: Wire DI in `MauiProgram.cs`**

In the `MauiProgram.CreateMauiApp` method, add the new registrations. Locate the existing `AddSingleton<ICreateSignedCertDialog, CreateSignedCertDialogHost>()` registration and add adjacent lines:

```csharp
builder.Services.AddSingleton<ICsrFilePicker, MauiCsrFilePicker>();
builder.Services.AddSingleton<ICreateFromCsrDialog>(sp => new CreateFromCsrDialogHost(sp));
builder.Services.AddTransient<CreateFromCsrDialog>();
builder.Services.AddTransient<CreateFromCsrDialogViewModel>(sp => new CreateFromCsrDialogViewModel(
    sp.GetRequiredService<IForgeService>(),
    sp.GetRequiredService<IUserPreferencesStore>()));
```

Update the `AuthoritiesViewModel` singleton registration to pass the new dependencies (`ICreateFromCsrDialog`, `ICsrFilePicker`, `IConfirmationDialog`, `ICertificateWorkflowService`).

- [ ] **Step 4: Build the App on the current OS**

Run: `make build`
Expected: SUCCESS on both macCatalyst and Windows (CI matrix will verify the other OS).

- [ ] **Step 5: Commit**

```bash
git add SelfCertForge.App/Pages/AuthoritiesView.xaml \
        SelfCertForge.App/Pages/CertificatesView.xaml \
        SelfCertForge.App/MauiProgram.cs
git commit -m "feat(csr): wire CreateFromCsr button, From CSR badge, and DI registrations"
```

---

## Task 14: End-to-end smoke + cleanup

**Files:** none new

- [ ] **Step 1: Build and run on the current OS**

Run: `make run`

- [ ] **Step 2: Manual smoke — happy path**

1. Forge a root CA on the Authorities page.
2. Click the new "Create From Certificate Signing Request" button.
3. In another terminal generate a CSR:
   ```bash
   openssl req -new -newkey rsa:2048 -nodes -keyout /tmp/test.key \
     -out /tmp/test.csr -subj "/CN=device.example.local"
   ```
4. Pick `/tmp/test.csr`. Confirm the dialog opens with locked Subject + 2048 bits, editable validity, empty SAN list, and editable KU/EKU (since openssl's default CSR has none).
5. Add a SAN `device.example.local`, confirm it shows the "Added" tag.
6. Set Server Auth EKU, click Sign.
7. Confirm navigation to Certificates page and the new row shows the "From CSR" badge.
8. Confirm the Export menu disables "PFX" and "Key (PEM)" for that row, while DER and P7B remain enabled.
9. Confirm the Dashboard shows a recent activity entry "Signed from CSR".

- [ ] **Step 3: Manual smoke — failure path**

1. Pick `/tmp/test.key` (a private key, not a CSR) — expect the alert dialog "Invalid Certificate Signing Request" with the malformed message.
2. Generate an ECDSA CSR with openssl and pick it — expect "Only RSA public keys are supported".
3. Generate a 1024-bit RSA CSR — expect "smaller than the 2048-bit minimum".
4. Cancel the file picker — expect silent no-op.

- [ ] **Step 4: Run the full test suite one more time**

Run: `make test`
Expected: all green.

- [ ] **Step 5: Commit any tiny fixes uncovered by smoke testing**

```bash
git add -p
git commit -m "fix(csr): smoke-test follow-ups"
```

(Only commit if there are fixes; otherwise skip.)

- [ ] **Step 6: Open PR**

```bash
git push origin feat/csr-signing
gh pr create --base main --title "feat: sign external CSRs" \
  --body "Implements docs/superpowers/specs/2026-05-28-csr-signing-design.md per docs/superpowers/plans/2026-05-28-csr-signing.md."
```

---
