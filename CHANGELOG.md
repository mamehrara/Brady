# Changelog

All notable changes to Brady are recorded here. Each build phase gets its own section and its own `phase-N` git tag.

## [0.1.0] - Phase 1: design system and toast - 2026-10-17

### Added
- .NET 10 solution: `Brady.Core` (no WPF), `Brady.App` (WPF) and `Brady.Core.Tests` (xUnit v3).
- Theme generated from `design/`:
  - `tools/gen-tokens.cs` turns tokens.json into light and dark color dictionaries plus spacing, radius and type metrics.
  - `tools/svg2xaml.cs` turns Brady and the icons into frozen DrawingImages and geometries.
  - Both have a `--check` mode for drift.
- Embedded Fredoka and Figtree fonts, with text styles that match the web reference's baselines.
- Controls ported from the web reference: Button (primary, glass, quiet; md and sm), Chip, Icon, and the toast close button.
- The toast:
  - a glass card with Brady hanging from the top edge, a kind chip, a title, a body, and Done / Snooze / Skip;
  - Done and Snooze replies, where Brady switches to cheer or sleepy;
  - a 20-second timeout that pauses on hover;
  - at most two toasts, stacked, newest at the bottom.
- Motion:
  - exact CSS cubic-bezier easing, a spring drop for Brady and a single damped sway;
  - a staggered content entrance;
  - an exit, and a FLIP stack reflow;
  - only opacity and transforms are animated;
  - a plain fade when Windows "Animation effects" is off.
- Toast host window: one long-lived window that never takes focus, isn't in Alt-Tab, is click-through outside the cards, and follows the monitor under the cursor.
- The app follows the Windows light/dark theme and the Transparency setting live.
- Developer flags: `--demo`, `--snapshot`, `--fps`, `--theme`, `--reduced-motion`, `--hardware`.
- Single instance, a lean generic host, and Serilog logs in `%LocalAppData%\Brady\logs`.

### Performance
- Software rendering by default. For small layered windows, the GPU path loaded about 80 MB of graphics driver and 50 threads with no smoothness gain.
- The idle working set is trimmed once toasts are gone (about 13 MB working set, 0% CPU at idle).
- Windows NLS instead of ICU.
- Cards are pre-rendered for two frames while still invisible. Text hinting is fixed to Animated, so text isn't re-rasterized once the motion settles.
