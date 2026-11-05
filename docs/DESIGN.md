# Design system

A dense, data-first desktop tool with a dark purple identity (coral-to-pink accent) and a matching light
theme. WPF on .NET 8. Translucent "glass" panels sit on a gradient window background; there are no blur
effects (they are expensive in WPF and hurt table readability), depth comes from tone and 1 px borders.

## Implementation rules

- All colors, brushes and styles live in `Themes/Dark.xaml`, `Themes/Light.xaml` (tokens) and
  `Themes/Styles.xaml` (shared styles). No hard-coded colors, sizes or fonts in views.
- Brushes are bound with `DynamicResource` so the theme can change at runtime; constants use `StaticResource`.
- One implicit style (`TargetType`, no key) per control type; variants use `x:Key` plus `BasedOn`.
- The default theme follows the Windows setting; the user can toggle it (not persisted yet).
- Code-behind holds view logic only (focus, dialogs, opening links); business logic lives in Core.

## Color tokens

| Token | Dark | Light |
| --- | --- | --- |
| `WindowGradientBrush` | `#3A0F6B` → `#1B0B45` → `#120730` (diagonal) | `#F3ECFF` → `#F8F5FD` |
| `AccentGradientBrush` | `#FF9A5B` → `#FF4D8D` | same |
| `Sidebar` | `#66120730` | `#99FFFFFF` |
| `Surface` (cards, inputs) | `#26FFFFFF` | `#B3FFFFFF` |
| `SurfaceAlt` (tiles, header) | `#1FFFFFFF` | `#1F7C3AED` |
| `Border` | `#3DFFFFFF` | `#D4C6EE` |
| `Text` | `#F6F1FF` | `#25123F` |
| `TextMuted` | `#DDD2F7` | `#5F4F80` |
| `Primary` (icons, accents) | `#FFB4A8` | `#B01F55` |
| `OnPrimary` (text on the gradient) | `#1B0B2E` | `#1B0B2E` |
| `Keep` | `#4ADE80` | `#15803D` |
| `Trash` | `#FF8A8A` | `#B91C1C` |
| `Undecided` | `#FBBF24` | `#B45309` |
| `Focus` | `#FFD2A6` | `#6D28D9` |
| `Selection` | `#47FFFFFF` | `#337C3AED` |
| `Stripe` (alternate rows) | `#0FFFFFFF` | `#0D7C3AED` |

Two radial glow brushes (`GlowABrush`, `GlowBBrush`) are drawn as large ellipses behind the content.

Contrast (WCAG ratio, translucent surfaces composited over the gradient, measured with a script):
text on cards and tiles at least 4.9, muted text at least 4.8, `OnPrimary` on both gradient stops at
least 5.9, decision badges at least 4.9, icon colors at least 3.0.

Rules for meaning:
- Keep, Trash and Undecided are never conveyed by color alone: the badge contains the text (Koru, Sil,
  Kararsız).
- Red is reserved for the Trash decision and the destructive confirmation button. The window close
  button hover is neutral on purpose.
- Text on the accent gradient is always the dark `OnPrimary`; white fails 4.5:1 there.

## Typography

- Font: `Segoe UI Variable` (fallback `Segoe UI`). Numbers in tables use `Cascadia Mono` / `Consolas`
  for alignment. Icons: `Segoe Fluent Icons` glyphs (fallback `Segoe MDL2 Assets`). No font is bundled.
- Scale: page title 26 SemiBold, stat value 32 SemiBold, section title 16 SemiBold, body 13, secondary 12
  (the smallest text). Section labels are uppercase 12 SemiBold in `TextMuted`.
- Number columns right-aligned; long text uses `CharacterEllipsis` plus a tooltip.

## Shape, spacing and layout

- 4 px based spacing. Window margin 12, gap between panels 12, card padding 20, tile padding 16.
- Radius: sidebar 20, cards 16, tiles 12, buttons and inputs 10, filter chips 16 (pill), badges 10.
- No shadows. Separation by tone, `Border` and the glass surfaces.
- Layout: floating glass sidebar on the left (logo, grouped navigation, theme toggle), a glass top bar
  (breadcrumb, "local only" chip, window buttons), content below. Tables and cards stretch with the window.
- Minimum window 1024 × 640.

## Components

**Buttons.** Primary: accent gradient fill, dark text, one per screen. Secondary: `Surface` fill with a
border. Destructive (confirm move to Trash): `Trash` fill, only in the confirmation dialog. States: hover
8 % overlay, pressed 16 %, disabled 40 % opacity.

**Filter chips (`Segment`).** Pill radio buttons; the checked chip uses the accent gradient.

**Navigation (`Nav`).** Icon plus text rows, 44 px high, rounded; the active row uses `Selection` and a
`Primary` icon. Section labels group the items ("HAZIRLIK", "KARAR VE İŞLEM").

**Cards and tiles.** `Card` is a glass panel (radius 16); `Tile` is a nested lighter panel (radius 12).
`SafetyPanel` and `StepsPanel` are reusable cards for the guarantees and the workflow.

**DataGrid.** Transparent inside a card, header in `TextMuted`, horizontal lines only, rows 34 px,
striped rows, selection with `Selection`, virtualization on, decision column as an outlined badge.

**Dialogs.** Same window style as the main window; the title bar is colored to the theme through the DWM
API on Windows 11 and recent Windows 10 builds. The confirmation dialog's default (focused) button is
"Vazgeç".

**Title bar.** The main window uses `WindowChrome` with the glass top bar as drag area and custom
minimize, maximize and close buttons (`CaptionButton`, 44 × 32, glyphs from Segoe Fluent Icons).

**Feedback.** Determinate progress shows a count and, for the inventory, speed and time left;
cancelling tells the user the work resumes where it stopped. Errors say what happened and what to do.

**Motion.** Only 150 ms fades, disabled when Windows animations are off.

## Accessibility

- Everything works by keyboard; every interactive control has an `AutomationProperties.Name`
  (mandatory on icon-only buttons together with a tooltip); the focus ring is never removed.
- Shortcuts: Ctrl+A selects all rows, Esc closes dialogs, Enter activates the default button.
- Works with display scaling (125–200 %) without clipping.
- No emoji as icons; vector or font glyph icons only.

## Pre-delivery checklist

- [ ] Text at least 4.5:1 in both themes, icons at least 3:1
- [ ] Full keyboard navigation with a visible focus ring
- [ ] Decisions carry text, not only color
- [ ] No hard-coded colors or sizes in views
- [ ] No overflow at 125 % and 150 % scaling
- [ ] Tables stay smooth with 10,000 or more rows

## Not done yet

Persisting the theme choice, a high-contrast fallback (`SystemColors`), a status bar.
