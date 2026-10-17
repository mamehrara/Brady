# Changelog

All notable changes to Brady are recorded here. Each build phase gets its own section and its own `phase-N` git tag.

## [0.2.0] - Phase 2: reminder engine and tray - 2026-10-17

### Added
- Reminder engine in `Brady.Core`, a pure, clock-free scheduler with a thin async driver:
  - Each kind fires after its interval of active time. Defaults: Posture 30, Move 50, Eyes 20, Stretch 90 (on); Water 60 (off).
  - Away detection uses no polling: idle for 5 minutes (checked exactly when it could happen), lock, or sleep. Sitting-based kinds reset; Water only pauses. Unlocking or waking counts as being back.
  - It never nags: a minimum 4-minute gap, and one reminder at a time by priority (Move > Stretch > Posture > Eyes > Water) while the others wait.
  - Snooze re-fires after the snooze length. Done, Skip, dismiss and missed restart the interval. Missed reminders are counted silently.
  - Quiet conditions (fullscreen, presentation, Windows quiet time, quiet hours 22:00 to 08:00 across midnight, paused) drop due reminders without catching up afterwards.
- Windows integration: GetLastInputInfo, SHQueryUserNotificationState, and lock/unlock, sleep/wake and clock-change events.
- Real reminders: copy rotates per kind, the meta line says "Every 30 min", "20-20-20" or "52 min still", and Snooze names its length.
- Tray icon, which switches to sleepy while paused:
  - The tooltip says what's next and refreshes only when its text changes.
  - Left click opens a glass flyout: coming up, Take a break now, Pause or Resume.
  - Right click opens a glass menu: pause for 30 minutes, 1 hour or until tomorrow; Resume; Settings (phase 3); Quit.
- Settings schema v1 in `%AppData%\Brady\settings.json`:
  - migrations by version;
  - hand-edited values clamped (comments and trailing commas are fine);
  - unreadable files set aside;
  - atomic, debounced saves;
  - a pause survives a restart.
- `--fast` (real reminders in seconds), `--quit` (clean exit from another launch) and `--background`.
- `--snapshot` also renders the tray menu and flyout.
- 82 Core tests on a fake clock.

### Fixed
- Brady could stay alive after exiting if WPF shut down while a menu popup was still closing. Quit now waits for the menu to close, and a watchdog ends the process a few seconds after exit no matter what.
- A startup failure now logs and exits instead of leaving an invisible process.

### Performance
- Tray mode idles at about 11 MB working set (3 MB private) and 0% CPU.
- The tray icon is created without Windows Efficiency Mode, which would throttle toast animations.

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
