# MAUI Handoff — Building the SelfCertForge App

This file is for Claude Code (or any AI coding assistant) to translate the **`ui_kits/app/`** JSX reference into a real **.NET MAUI** application.

The JSX in `ui_kits/app/` is a **visual reference**, not production code. Recreate the same look and behavior in MAUI XAML + C#, using the design tokens from this skill.

---

## Trust model (corrects the JSX reference)

The JSX prototype mislabels child certificates with a `"trusted"` status pill and a "Trust Locally" button. **Do not carry that into MAUI.** The product's mental model is:

- **Trust applies to roots, never children.** A user installs a *root authority* into the OS trust store. Every certificate signed by that root is then trusted by chain.
- **Child certificates have no trust toggle.** Their pill should describe their relationship to a root: `Issued by <root name>` (with a `triangle-alert` warning variant if the issuing root is missing/uninstalled), plus a separate validity pill (`Expires in 30d`, `Expired`).
- **`TrustStorePage` lists root authorities only** — specifically the ones currently installed in the OS trust store. It is not a filtered view of all certs.
- **Neither dialog has an "Install in trust store" toggle.** Trust store installation is managed separately from certificate creation, on the Trust Store page.
- **Status pill kinds** to use: `installed` (root installed in trust store), `uninstalled` (root not in trust store), `valid` (child cert in date), `expiring` (child cert ≤ 30 days from expiry), `expired` (child cert past expiry), `orphaned` (child whose issuing root is gone).

When the JSX disagrees with this section, the handoff wins.

---

## What the target looks like

Open `ui_kits/app/index.html` in a browser. The window is divided as follows:

```
┌─────────────────────────────────────────────────────────────────┐
│ Native macOS title bar  ●●●  SelfCertForge                      │
├──────────┬──────────────────────────────────────────────────────┤
│ Sidebar  │  Content area                                        │
│  220px   │                                                      │
│          │  Routes:                                             │
│  • Dash  │   - Dashboard:     4 stat cards + activity log       │
│  • Auth  │   - Authorities:   cards list + "Create Root         │
│  • Certs │                    Certificate" button in header;    │
│  • Trust │                    per-card "Create Signed           │
│  • Sett  │                    Certificate" strip button         │
│          │   - Certificates:  list (left) + detail (right)      │
│          │   - Trust Store:   installed-roots cards list        │
│          │   - Settings:      empty state                       │
│          │                                                      │
│          │  Modal: ForgeDialog (centered, blurred backdrop)     │
│ [status] │   - Root mode:  invoked from Authorities header      │
│          │   - Child mode: invoked from Certificates header     │
└──────────┴──────────────────────────────────────────────────────┘
```

**No standalone Forge route.** The forge action is a modal (`ForgeDialog`) surfaced contextually:
- Authorities page → "Forge New Root Certificate" button → `ForgeDialog` in Root mode
- Certificates page → "Forge New Signed Certificate" button → `ForgeDialog` in Child mode

This keeps the nav at 5 items (Dashboard, Authorities, Certificates, Trust Store, Settings) and avoids a nav destination that is just a button wrapping a modal.

**Selected route** has a 2px orange bar on its left edge and orange icon. **Sidebar footer** shows a tiny "Trust store synced" status dot.

---

## Tokens to wire up first

In `App.xaml` ResourceDictionary, register every token from `colors_and_type.css`. The brief already gave you the MAUI version — paste it in:

```xml
<!-- Background -->
<Color x:Key="ColorBackground">#0B0C10</Color>
<Color x:Key="ColorSurface">#12141A</Color>
<Color x:Key="ColorPanel">#1B1D25</Color>
<Color x:Key="ColorPanelElevated">#232631</Color>
<!-- Text -->
<Color x:Key="ColorTextPrimary">#F2F3F6</Color>
<Color x:Key="ColorTextSecondary">#B7BBC6</Color>
<Color x:Key="ColorTextMuted">#7C818E</Color>
<!-- Accent -->
<Color x:Key="ColorAccentPrimary">#FF6A00</Color>
<Color x:Key="ColorAccentPrimaryHover">#FF7F1A</Color>
<Color x:Key="ColorAccentPrimaryPressed">#D94F00</Color>
<Color x:Key="ColorAccentSecondary">#FFB800</Color>
<Color x:Key="ColorAccentDanger">#C0392B</Color>
<!-- Borders -->
<Color x:Key="ColorBorderSubtle">#2F333D</Color>
<Color x:Key="ColorBorderStrong">#4A4F5C</Color>
<!-- Status -->
<Color x:Key="ColorSuccess">#38C172</Color>
<Color x:Key="ColorWarning">#FFB800</Color>
<Color x:Key="ColorDanger">#E5484D</Color>
<Color x:Key="ColorInfo">#6EA8FE</Color>

<LinearGradientBrush x:Key="ForgeGradient" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#FFB800" Offset="0.0" />
    <GradientStop Color="#FF6A00" Offset="0.55" />
    <GradientStop Color="#C0392B" Offset="1.0" />
</LinearGradientBrush>

<!-- Typography -->
<x:String x:Key="FontUi">Inter</x:String>
<x:String x:Key="FontMono">JetBrainsMono</x:String>

<!-- Radii -->
<x:Double x:Key="RadiusButton">12</x:Double>
<x:Double x:Key="RadiusCard">16</x:Double>
<x:Double x:Key="RadiusModal">20</x:Double>
```

Bundle Inter and JetBrains Mono as embedded fonts in your `MauiProgram.cs`:

```csharp
fonts.AddFont("Inter-Regular.ttf", "Inter");
fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
fonts.AddFont("Inter-Bold.ttf", "InterBold");
fonts.AddFont("JetBrainsMono-Regular.ttf", "JetBrainsMono");
```

Download `.ttf` files from the [Inter](https://fonts.google.com/specimen/Inter) and [JetBrains Mono](https://fonts.google.com/specimen/JetBrains+Mono) Google Fonts pages.

---

## Type scale (FontSize values)

Desktop-legible scale, viewed at arm's length. Always use these — never invent a size.

| Token          | px | MAUI `FontSize` | Use for |
|----------------|----|-----------------|---------|
| `--fs-display` | 44 | `44`  | Hero numbers, splash, marketing surfaces |
| `--fs-h1`      | 32 | `32`  | Page titles |
| `--fs-h2`      | 24 | `24`  | Section headers, large content placeholders |
| `--fs-h3`      | 22 | `22`  | Modal titles |
| `--fs-card`    | 19 | `19`  | Card / list-row primary name |
| `--fs-body`    | 16 | `16`  | Body copy, nav items, button labels, input entry text, mono technical data |
| `--fs-small`   | 15 | `15`  | Form field labels, secondary captions, muted helper text |
| `--fs-micro`   | 13 | `13`  | Footer status dot label, micro hints, all-caps eyebrow |

Top-bar brand and primary CTA both use `--fs-body` (16). Sidebar nav rows and sidebar brand both use `--fs-body` (16). Use `--fs-h1` (32) for content-area page titles.

### Input & button sizing

| Element | HeightRequest | FontSize | Notes |
|---------|--------------|----------|-------|
| Form input (`Entry` inside `Border`) | `40` | `16` | `Padding="12,0"` |
| Primary / secondary button | `42` | `16` | `Padding="18,0"` primary, `"16,0"` secondary |
| Key-size segmented selector | `36` | `15` | 3-segment tap target |
| Inline action strip (e.g. "Create Signed Certificate" in card) | `36` | `16` | `StrokeShape="RoundRectangle 8"` |

---

## Component map: JSX → MAUI

| JSX file | What it is | MAUI translation |
|---|---|---|
| `primitives.jsx` `Button` | 4 variants (primary / secondary / ghost / danger) | `Button` styles in `Resources/Styles/Buttons.xaml`. One style per variant via `Style.TargetType="Button"` with `x:Key` |
| `primitives.jsx` `Pill` | Status badge with colored dot | Custom `ContentView` named `StatusPill` with `Kind` BindableProperty; uses a `Border` + `Ellipse` + `Label` |
| `primitives.jsx` `Field` | Label + value, optional mono | Reusable `Grid` with two `Label`s; mono variant flips `FontFamily` to `JetBrainsMono` |
| `primitives.jsx` `Card` | Bordered panel with hover/selected states | `Border` with `StrokeShape="RoundRectangle 16"`, `Background` from `ColorPanel`. Hover via `VisualStateManager` |
| `primitives.jsx` `Input` | Text field with optional icon and focus glow | Custom `ContentView` wrapping `Entry` inside a `Border`; toggle border color on focus |
| `Shell.jsx` `Sidebar` | Nav rail with logo, items, status footer | `Grid` columns="220,*" at the page level. Sidebar is a `VerticalStackLayout` with custom `NavItem` ContentViews |
| `Shell.jsx` `TitleBar` | Top bar with traffic lights + Forge CTA | On Windows: hide system chrome and draw your own with a `Grid`; on macOS use `WindowExtensions` to set transparent title bar |
| `CertificateList.jsx` | Search + list with selection | `CollectionView` with `ItemTemplate`. Search uses an `Entry` bound to a filtered `ObservableCollection` |
| `CertificateDetail.jsx` | Multi-section detail page | `ScrollView` containing `VerticalStackLayout` of section `Grid`s |
| `ForgeDialog.jsx` | Modal with form fields | Two independent `CommunityToolkit.Maui` `Popup` classes: **`CreateRootDialog`** and **`CreateSignedCertDialog`**. No standalone Forge page. Root dialog collects: Common Name (CN, required), Email, Organization, Organizational Unit, City/Locality, State/Province, Country (2-letter), Validity (days), Key size. Signed cert dialog collects: Name, Common Name, SANs (comma-separated), Validity, Key size. **No "Install in trust store" toggle in either dialog** — that decision is deferred to the Trust Store page. |
| `EmptyState.jsx` | Centered icon + title + body + CTA | Custom `ContentView` taking 4 BindableProperties |
| `Dashboard.jsx` | 4-up stat grid + activity list | `Grid` with `RowDefinitions="Auto,Auto,*"`, stats in a 4-column `Grid`, activity in a `CollectionView` |

---

## Visual rules to follow strictly

1. **Surface stack:** page = `ColorBackground` (#0B0C10), sidebar = `ColorSurface`, cards = `ColorPanel`, modals = `ColorPanelElevated`.
2. **One** primary orange button per view. All other actions are `secondary` (border-only) or `ghost` (text-only).
3. **Monospace ONLY** for: CN, SAN entries, thumbprints, serials, PEM blocks, file paths, CLI output. Headings and body text use Inter.
4. **Card spec:** `Border` with `Stroke="{StaticResource ColorBorderSubtle}"`, `StrokeThickness="1"`, `StrokeShape="RoundRectangle 16"`, `Padding="20"`, `Background="{StaticResource ColorPanel}"`. No drop shadow at rest.
5. **Selected list row:** orange border + 12% orange-tinted background + faint orange glow.
6. **Status pill:** rounded-pill `Border` with 12%-alpha background of the status color, 28%-alpha border, and a 6×6 dot of the solid status color. The `installed` (root in trust store) dot has a soft glow (`Shadow` effect, color = `ColorSuccess`, radius 6). Other kinds (`uninstalled`, `valid`, `expiring`, `expired`, `orphaned`) have no glow.
7. **Focus state on inputs:** border becomes `ColorAccentPrimary`, plus an outer 3px halo (`Border` wrapping with `Stroke=ColorAccentPrimary, Opacity=0.18`).
8. **No emoji. No locks. No globes.** The brand mark (`assets/icon.png`) is the only illustrated element.

---

## Suggested project structure

```
SelfCertForge/
├── App.xaml                       # ResourceDictionary with all tokens
├── MauiProgram.cs                 # Font registration
├── AppShell.xaml                  # Top-level Shell with FlyoutBehavior=Disabled
├── Resources/
│   ├── Fonts/                     # Inter + JetBrains Mono .ttf
│   ├── Images/
│   │   └── icon.png               # from this skill's assets/
│   └── Styles/
│       ├── Buttons.xaml
│       ├── Labels.xaml
│       └── Cards.xaml
├── Controls/                      # Reusable views
│   ├── StatusPill.xaml
│   ├── NavItem.xaml
│   ├── EmptyState.xaml
│   └── FieldRow.xaml
├── Views/                         # Pages
│   ├── DashboardPage.xaml
│   ├── CertificatesPage.xaml
│   ├── AuthoritiesPage.xaml
│   ├── TrustStorePage.xaml
│   └── SettingsPage.xaml
├── Dialogs/
│   └── ForgeDialog.xaml
└── ViewModels/
    ├── CertificateViewModel.cs
    └── ...
```

---

## Quickstart prompt for Claude Code

Paste this into Claude Code in your project root:

> I'm using the **selfcertforge-design** skill in `~/.claude/skills/selfcertforge-design/`. Read `SKILL.md` and `MAUI_HANDOFF.md` from that folder, then study `ui_kits/app/index.html` (and the surrounding JSX files) as the visual target.
>
> My project is a **.NET MAUI** desktop app at the current working directory. Update it to match the `ui_kits/app/index.html` reference, screen by screen:
>
> 1. First, register every token from the skill's `colors_and_type.css` into `App.xaml` as a ResourceDictionary (use the MAUI snippet in `MAUI_HANDOFF.md`). Bundle Inter + JetBrains Mono as embedded fonts.
> 2. Build the app shell — `AppShell.xaml` with the 220px sidebar, top title bar, and content area. Use `assets/icon.png` from the skill in the sidebar header.
> 3. Build the reusable controls in this order: `StatusPill`, `NavItem`, `Card`, `FieldRow`, `EmptyState`. Match the JSX `primitives.jsx` exactly for colors, spacing, radii.
> 4. Build pages in this order: `CertificatesPage` (the most important — list + detail + "Forge New Signed Certificate" header button), `DashboardPage`, `AuthoritiesPage` ("Forge New Root Certificate" header button), `TrustStorePage`, `SettingsPage`. There is no standalone Forge page — forge is a modal action surfaced from Authorities and Certificates.
> 5. Build the `ForgeDialog` modal last. It is opened from both pages; `ForgeMode.Root` shows the "Install in trust store" toggle, `ForgeMode.Child` hides it and shows the issuer picker instead.
>
> **Strict rules:**
> - Pull every color from the ResourceDictionary. Never hardcode hex.
> - Monospace font ONLY for CN, SANs, thumbprints, serial, PEM, paths, CLI output. Inter for everything else.
> - One primary orange button per view; all other actions are secondary/ghost.
> - No emoji, no lock/globe icons. Use Lucide-equivalent geometry where you need icons (or a Lucide MAUI port if available).
>
> Show me your plan first, then execute one section at a time, and let me review after each page.

---

## Lucide icons in MAUI

Two options:

1. **`Lucide.Maui`** — there are community NuGet packages exposing Lucide as a font. Search NuGet for "Lucide MAUI" and add the most active one. Usage: `<Label FontFamily="LucideIcons" Text="&#xE001;" />`.
2. **SVG paths embedded as `Path` data** — copy the SVG path string from [lucide.dev](https://lucide.dev) for each icon you need, render via `<Path Data="..." Stroke="{StaticResource ColorTextSecondary}" StrokeThickness="1.6" />`.

Option 2 is more code but keeps you off third-party dependencies. Recommended icons to start with: `hammer`, `shield-check`, `scroll-text`, `key-round`, `settings-2`, `triangle-alert`, `arrow-up-from-line`, `search`, `x`, `copy`, `layout-dashboard`.
