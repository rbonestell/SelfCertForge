# SelfCertForge Design System

A dark-first, developer-focused design system for **SelfCertForge** — a desktop application for creating trusted root authorities and forging self-signed certificates locally.

> If it feels like a terminal evolved into a GUI with power tools, you nailed it.

---

## Overview

SelfCertForge is a **local-first developer tool**, not a cloud security dashboard. The design language is grounded in three ideas:

- **Precision** — cryptography, trust chains, exact bytes.
- **Industrial creation** — the *forge* metaphor: heat, strike, spark, artifact.
- **Local authority** — developers control their own trust store.

This system is **dark-first**, optimized for IDE-adjacent dev environments, and avoids generic security tropes (locks, globes, blue gradients). Orange is the heat of the forge — used sparingly for action and emphasis.

## Sources

This design system was generated from the SelfCertForge brand brief (color/type/component spec) and the official brand mark (`assets/icon.png`) provided by the team. No codebase or Figma file was provided; everything else was derived from the brief. See [Caveats](#caveats) below.

---

## Brand Principles

| Trait | Meaning |
|---|---|
| **Deliberate** | Nothing feels accidental. Every element is placed with intent. |
| **Technical** | Built for engineers. Show real values: thumbprints, serials, PEMs. |
| **Confident** | No fluff. No ambiguity. Direct verbs, precise nouns. |
| **Minimal** | No decoration that doesn't earn its place. |
| **Dark-first** | Optimized for IDE-adjacent environments. |

**Visual metaphor:** Forge → creation. Certificate → artifact. Strike → signing. Sparks → action / transformation. The brand mark literally depicts this — a hammer striking a certificate, sparks flying.

---

## Content Fundamentals

### Tone

Direct, technical, action-oriented. Address the developer as a peer, not as a customer. No marketing voice. No SaaS-isms.

### Voice rules

- **Use imperative verbs** for actions: *Forge Certificate*, *Create Trusted Root*, *Install in Local Trust Store*, *Export PEM*.
- **Use nouns precisely.** A "certificate" is not a "security asset." A "root authority" is not a "trust experience."
- **No hedging.** Don't say "would you like to" — say "Forge."
- **Prefer "you"** when addressing the user. Avoid "we" — there is no we; this is a local tool.
- **No emoji.** Ever. Not in UI, not in copy, not in error states.
- **Sentence case** for buttons, headings, and menu items. *Forge new certificate*, not *Forge New Certificate*.
- **Title Case** is reserved for proper nouns and product names (*Trust Store*, *Root Authority*).
- **Acronyms stay capitalized:** PEM, PFX, CN, SAN, CA.

### Microcopy patterns

| Pattern | Example |
|---|---|
| **Empty state** title | *No root authorities yet* |
| **Empty state** body  | *Create a local root authority to start forging trusted development certificates.* |
| **Empty state** CTA   | *Create Root Authority* |
| **Success toast**     | *Certificate forged.* (Past tense. One sentence. No exclamation.) |
| **Destructive confirm** | *Remove this root authority? Certificates issued by it will become untrusted.* |
| **Error**             | *Couldn't write to trust store. Run with elevated permissions.* |

### Words to avoid

> *Manage security assets* · *Optimize certificate workflows* · *Enable trust experiences* · *Seamless* · *Powerful* · *Robust* · *Solutions*

### Words we like

> *Forge* · *Strike* · *Sign* · *Issue* · *Trust* · *Install* · *Export* · *Thumbprint* · *Subject* · *SAN* · *Authority* · *Local*

---

## Visual Foundations

### Color

**Surfaces** stack in four levels of darkness, lightest = most elevated:

```
Forge Black    #0B0C10   page background
Charcoal       #12141A   app shell, nav rail
Panel          #1B1D25   cards, list rows
Elevated       #232631   modals, popovers, menus
```

**Forge accents** — orange is *action*, yellow is *attention*, ember red is *heat/danger accent*, hot core is *glow center*. Use orange ONLY on the primary CTA in any view; never as background fill.

**Text** is high-contrast on dark: `#F2F3F6` primary → `#B7BBC6` secondary → `#7C818E` muted → `#555A66` disabled. Never use pure white **except** as the foreground on the primary orange CTA — there, `#FFFFFF` is the rule for legibility (icons inside the CTA take the same white).

**Borders** are `#2F333D` (subtle, between surface levels) and `#4A4F5C` (strong, focused inputs). Borders, not shadows, are the primary separator on dark surfaces.

### Typography

- **UI:** Inter (Segoe UI / SF Pro / Roboto fallbacks). Tight letter-spacing on headings (`-0.01em` to `-0.02em`).
- **Mono:** JetBrains Mono (Cascadia Code / SF Mono fallbacks). Used **only** for: certificate CN/subject, SANs, thumbprints, serial numbers, PEM blocks, file paths, CLI output.
- **Never** use mono for headings, navigation, or body copy.

### Spacing

Multiples of 4. Card padding **16–24px**. Section spacing **24–32px**. Component gap **12–16px**.

### Control sizing (MAUI desktop)

| Control | `HeightRequest` | Font | Notes |
|---------|----------------|------|-------|
| Form input | `40` | `16` Inter/Mono | `Padding="12,0"` |
| Primary button | `42` | `16` InterSemiBold | `Padding="18,0"` |
| Secondary button | `42` | `16` InterSemiBold | `Padding="16,0"` |
| Segmented key-size selector | `36` | `15` JetBrainsMono | 3 segments, tap target |
| Card inline action strip | `36` | `16` InterSemiBold | e.g. "Create Signed Certificate" |

### Radii

`6 / 10 / 12 / 16 / 20 / 999`. Buttons = **12px**. Cards = **16px**. Modals = **20px**. Pills = **999px**.

### Backgrounds & imagery

- **No** background images. **No** patterns. **No** noise textures.
- Dark flat surfaces only. Hierarchy is communicated through surface elevation + border, never imagery.
- The one exception: **forge gradient** (`#FFB800 → #FF6A00 → #C0392B`) for hero sections, primary-CTA emphasis on empty states, and logo-related visuals. Never on body backgrounds, never behind text, never on small UI.

### Shadows & elevation

Borders do most of the elevation work. Shadows are subtle and only on floating surfaces (modals, popovers, dropdowns). The orange glow (`0 0 24px rgba(255,106,0,0.25)`) is reserved for the *moment of forging* — a brief visual highlight, never permanent.

### Animation

- Fast, deliberate, no bounce. Easing: `cubic-bezier(0.2, 0.8, 0.2, 1)`. Durations: 120–180ms for hover/state, 240–320ms for entrance.
- **Forge moment**: when a certificate is created, a brief spark/glow animation (~600ms) plays once. Everything else is restrained.
- No parallax. No scroll-triggered reveal. No looping decorative motion.

### Hover & press states

- **Hover** on primary buttons: lighten by ~6% (`#FF6A00 → #FF7F1A`).
- **Hover** on secondary buttons / list rows: surface goes one level up (`#1B1D25 → #232631`).
- **Press** on primary: darken (`#FF6A00 → #D94F00`). No scale transform.
- **Press** on secondary: same as hover, no shrink.
- **Focus** ring: 2px, `--forge-orange` with 40% alpha, offset 2px. Always visible on keyboard focus.

### Cards

`#1B1D25` background, `1px solid #2F333D` border, `16px` radius, `20px` padding. No shadow at rest. Hover state lifts to `#232631` background only when interactive.

### Transparency & blur

Used sparingly on overlays only. Modal backdrop: `rgba(11, 12, 16, 0.7)` with `backdrop-filter: blur(8px)`. Never on cards or panels.

### Imagery vibe (when used)

Cool-warm contrast: cool dark surfaces, warm orange accents. Never desaturated grayscale photography. If product imagery appears, it should have a forge-warm tint — see the brand mark for the reference palette.

---

## Iconography

### Brand mark

`assets/icon.png` — the official mark. A hammer striking a certificate with a burst of sparks. Painterly/illustrated style with warm orange-gold sparks, dark stone hammer head, parchment certificate. Use at 32px+ to preserve detail; below that the painterly treatment falls apart and you should consider a simplified version.

### UI icons

- **Style:** Outline-first, 1.5px stroke, minimal geometry. The brand mark is illustrated; the UI icons are *not* — they're crisp linework. This contrast is intentional: the mark carries the personality, the UI stays out of the way.
- **Library:** [Lucide Icons](https://lucide.dev) via CDN. Closest match to the spec's outline-first aesthetic. **Substitution flagged** — replace with a custom set if the brand develops one.
- **Sizes:** 16px (inline), 20px (default in toolbar/nav), 24px (section headers), 32px+ (empty states).
- **Color:** Default `--fg2`. Active/selected `--fg1`. Action accent `--forge-orange`.
- **No emoji. No unicode glyph icons** (✓, ★, ⚠) — use Lucide equivalents (`check`, `star`, `triangle-alert`).
- **No gradients inside icons** — only on the brand mark.

### Concept → icon mapping

| Concept | Lucide name |
|---|---|
| Root authority | `shield-check` / `badge-check` |
| Certificate | `file-badge` / `scroll-text` |
| Forge new | `hammer` / `sparkles` |
| Trust store | `key-round` / `database` |
| Export | `arrow-up-from-line` / `download` |
| Settings | `settings-2` |
| Warning | `triangle-alert` |
| Expired | `clock-alert` |
| Trusted | `shield-check` |

---

## File Index

```
README.md                  ← you are here
SKILL.md                   ← Agent Skill manifest (Claude Code compatible)
colors_and_type.css        ← all design tokens
assets/
  icon.png                 ← OFFICIAL brand mark (1024×1024 PNG, transparent)
  logo.svg                 ← horizontal lockup (placeholder — uses old mark)
  mark.svg                 ← square mark (placeholder, superseded by icon.png)
preview/                   ← design-system-tab cards
ui_kits/
  app/                     ← desktop app UI kit (JSX components + index.html)
```

---

## Caveats

- **No codebase, no Figma file** was attached. UI components and screen layouts were derived from the written brief. Real components, real screens, real interactions in the actual app may differ.
- **Logo lockup is provisional.** The real brand mark (`icon.png`) is in place; the *wordmark lockup* still uses my placeholder typography treatment ("SelfCert" + gradient "Forge"). Replace if you have an official lockup.
- **Old placeholder SVGs** (`logo.svg`, `mark.svg`) are kept for reference but no longer used in previews — `icon.png` is the canonical mark.
- **Fonts via Google Fonts CDN** (Inter, JetBrains Mono). If you want them bundled locally, drop the `.woff2` files into `fonts/` and update the `@import` in `colors_and_type.css`.
- **Lucide via CDN** as the icon library substitute — flagged. Switch when a custom set ships.

---

## Ask

Iterate this with us. Specifically helpful next inputs:

1. **Official wordmark / lockup** — I'm rendering "SelfCert**Forge**" in Inter with a gradient on "Forge"; if there's a real type treatment, send it.
2. **Screenshots or codebase access** to an existing SelfCertForge build, even pre-alpha — UI kit is currently inferred from the brief.
3. **Icon library decision** — keep Lucide, or commission a custom set that matches the painterly style of the mark?
4. **Any product copy** that exists (onboarding, error states, CLI help text) so we can tighten Content Fundamentals against real text.
