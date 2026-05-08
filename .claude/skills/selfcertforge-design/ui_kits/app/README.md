# SelfCertForge — Desktop App UI Kit

A React/JSX recreation of the SelfCertForge desktop application UI. Click-through prototype, not production code.

## Files

- `index.html` — interactive demo (sidebar + certificate list + detail pane + forge dialog)
- `Shell.jsx` — app chrome: title bar, sidebar nav, content area
- `CertificateList.jsx` — list view with status pills and search
- `CertificateDetail.jsx` — detail pane with Identity / SAN / Validity / Thumbprints / Export
- `ForgeDialog.jsx` — modal for forging a new certificate
- `EmptyState.jsx` — empty state for no-authorities
- `primitives.jsx` — Button, Pill, Field, IconButton, Card

## Screens covered

1. **Dashboard** — recent activity + summary
2. **Root Authorities** — list, with empty state
3. **Certificates** — main list + selected detail
4. **Forge New** — modal flow with form
5. **Trust Store** — read-only list of installed certs

## Conventions

- All visual tokens come from `../../colors_and_type.css` — never hard-code hex.
- Mono font ONLY for technical fields (CN, SAN, thumbprint, serial, PEM, paths).
- Lucide icons via CDN, 1.6 stroke weight.
- Orange used for one primary action per view.
