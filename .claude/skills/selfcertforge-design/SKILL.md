---
name: selfcertforge-design
description: Use this skill to generate well-branded interfaces and assets for SelfCertForge, either for production or throwaway prototypes/mocks/etc. Contains essential design guidelines, colors, type, fonts, assets, and UI kit components for prototyping.
user-invocable: true
---

Read the README.md file within this skill, and explore the other available files.

Key files:
- `README.md` — brand context, content fundamentals, visual foundations, iconography rules
- `MAUI_HANDOFF.md` — **start here when building the .NET MAUI app**: token registration, JSX→XAML component map, project structure, quickstart prompt
- `colors_and_type.css` — all design tokens (colors, type scale, spacing, radii, shadows). Import this in any HTML artifact.
- `assets/icon.png` — official brand mark
- `preview/` — design-system specimen cards
- `ui_kits/app/` — desktop app **visual reference** (JSX components + interactive index.html). The target the MAUI app should look like.

If creating visual artifacts (slides, mocks, throwaway prototypes, etc), copy assets out and create static HTML files for the user to view. Always link `colors_and_type.css` and pull components from `ui_kits/app/` when relevant. If working on production code, copy tokens from `colors_and_type.css` and read the rules in README.md to become an expert in designing with this brand.

If the user invokes this skill without any other guidance, ask them what they want to build or design, ask some questions about audience and surface (desktop app, marketing, docs), and act as an expert designer who outputs HTML artifacts or production code, depending on the need.

Hard rules to remember:
- Dark-first only. Never invert.
- Orange (`--forge-orange #FF6A00`) is for ONE primary action per view. Never as background fill.
- Monospace ONLY for certificate technical data (CN, SANs, thumbprints, PEMs, paths, CLI). Never for headings or body.
- No emoji. Ever.
- No generic security iconography (locks, globes, shields-with-checkmarks-on-blue).
- **Trust applies to roots, never children.** Only root authorities have an `installed`/`uninstalled` trust-store state. Child certificates show their issuer + validity (`valid` / `expiring` / `expired` / `orphaned`) — never a trust toggle. The Trust Store page lists installed roots only. The JSX prototype mislabels this; the MAUI handoff has the correct model.
- Forge gradient (`#FFB800 → #FF6A00 → #C0392B`) only for: hero sections, primary CTA emphasis, empty states, logo. Never as page background, never behind text, never on small UI.
