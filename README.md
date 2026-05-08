<p align="center">
 	<img src="og.png" width="60%" height="60%" alt="SelfCertForge" />
</p>
<p align="center">
	Generate and manage local certificates. Export PEM, PFX, DER, or P7B formats.
</p>

# SelfCertForge

A .NET MAUI desktop app for generating and managing self-signed certificates and root certificate authorities. Useful for local development, home labs, and internal tooling.

Download the appropriate installer for your operating system:

[![Download for macOS][1]][2] [![Download for Windows x64][3]][4] [![Download for Windows ARM][5]][6]

## Screenshots

<p align="center">
	<img src="assets/screenshot-create-root.png" width="45%" alt="Create Root CA">
	<img src="assets/screenshot-root-authorities.png" width="45%" alt="Root Authorities List">
</p>

<p align="center">
	<img src="assets/screenshot-sign-cert.png" width="45%" alt="Created Signed Certificate">
	<img src="assets/screenshot-cert-details.png" width="45%" alt="Signed Certificate Details">
</p>


## Features

- Create a Root CA (certificate + private key)
- Generate and sign child certificates
- Export as PEM, PFX, or separate files
- Editable standard X509 fields; Subject, Subject Alternative Names, Key Usage, etc.
## Platforms

- macOS
- Windows

## Development Environment Prerequisites

- .NET 10 SDK with MAUI workload

## Build & Test Commands

```bash
make build    # build the app
make run      # launch the app
make clean    # clean build output
make rebuild  # clean + build
make test     # run tests
```

[1]: assets/download-macos.png
[2]: https://github.com/rbonestell/SelfCertForge/releases/latest/download/SelfCertForge-osx-Setup.pkg "Download for macOS"
[3]: assets/download-windows-x64.png
[4]: https://github.com/rbonestell/SelfCertForge/releases/latest/download/SelfCertForge-win-x64-Setup.exe "Download for Windows x64"
[5]: assets/download-windows-arm.png
[6]: https://github.com/rbonestell/SelfCertForge/releases/latest/download/SelfCertForge-win-arm64-Setup.exe "Download for Windows ARM"
