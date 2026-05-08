<p align="center">
 	<img src="og.png" width="60%" height="60%" alt="SelfCertForge" />
</p>
<p align="center">
	Generate and manage Root CA certificates. Generate and sign child certificates. Export PEM, PFX, etc.
</p>

# SelfCertForge

A .NET MAUI desktop app for generating and managing self-signed certificates and root certificate authorities. Useful for local development, home labs, and internal tooling.

## Features

- Create a Root CA (certificate + private key)
- Generate and sign child certificates
- Export as PEM, PFX, or separate files
- Editable standard X509 fields; Subject, Subject Alternative Names, Key Usage, etc.
## Platforms

- macOS
- Windows

## Prerequisites

- .NET 10 SDK with MAUI workload

## Build & Test Commands

```bash
make build    # build the app
make run      # launch the app
make clean    # clean build output
make rebuild  # clean + build
make test     # run tests
```
