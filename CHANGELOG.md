# Changelog

All notable changes to Brady are recorded here. Each build phase gets its own section and its own `phase-N` git tag.

## [0.5.0] - Phase 5: installer and GitHub Releases - 2026-10-17

The first installable release. (Phase 4, guided breaks and polish, comes next.)

### Added
- A per-user installer (Inno Setup 6):
  - It needs no admin prompt and no .NET install: it installs a self-contained win-x64 build into `%LocalAppData%\Programs\Brady`. The installer is about 45 MB and installs 146 MB.
  - The wizard is branded with Brady's app icon, canopy and the moss and bloom circles.
  - Tasks: "Start Brady when Windows starts" (checked) and "Create a desktop shortcut" (unchecked). The finish page has "Launch Brady now".
  - It uses the same HKCU Run entry as Settings, so the two never disagree.
  - A Start menu shortcut with Brady's AppUserModelID.
- Upgrades:
  - Setup closes a running Brady through its quit event and starts it again afterwards.
  - Settings and the startup choice are kept, including a choice changed in Settings since the last install.
- Uninstall:
  - It closes Brady and removes the startup entry (whoever created it), the shortcuts and the logs.
  - It asks whether to delete your settings; the default is to keep them.
- `build/build-installer.ps1`: tests, publish, then `artifacts/Brady-Setup-<version>.exe`. The version comes only from `Directory.Build.props`.
- A GitHub Actions release workflow: pushing a `v*` tag that matches the version builds the installer and attaches it to a GitHub Release.
- An uninstalled (dev or portable) Brady asks once, "Start with Windows?", with Yes and Not now.
- `tools/installer-art.cs` generates the wizard images from `design/`.

### Fixed
- Launching Brady again now brings Settings to the front instead of opening it behind other windows.

## [0.3.0] - Phase 3: Settings and per-reminder timing - 2026-10-17

### Added
- A Settings window on canopy with glass cards. It opens from the tray menu, the flyout, or by launching Brady again. The header shows Brady's head, "Brady" in `display` and "Today: N breaks taken". The title bar is tinted to match.
- One card per reminder:
  - an on/off switch;
  - an interval picker: preset buttons (Posture 20/30/45/60, Eyes 20/30/40, Move 30/45/50/60/90, Stretch 60/90/120, Water 30/45/60/90) plus Custom, with a 5–240 minute stepper. Move's is labelled "After sitting for";
  - snooze length (5/10/15/30 min);
  - guided-break length for Eyes (20/30 s) and Stretch (15/30/45 s);
  - a live status line ("Next in 12 min", "Off", "Paused");
  - Reset.
- A General card:
  - quiet hours, which may cross midnight;
  - "Count me as away after" (the idle threshold, default 5 min);
  - the gap between reminders (default 4 min);
  - theme (System, Light, Dark), switching live;
  - soft sound (stored; the sound itself arrives in phase 4);
  - Start with Windows, which reads and writes the real HKCU Run value `Brady` = `"…\Brady.exe" --background`.
- Reset to defaults per card, for the General card, and for everything, each with Undo in a small toast instead of a confirmation dialog.
- New controls: a switch row, a segmented choice row (chips stay non-clickable), a stepper, and tabular digits (Fredoka has no `tnum`, so digits are drawn in fixed-width cells).
- `--snapshot` also renders the Settings page.

### Changed
- Settings schema v2:
  - adds per-kind snooze and guided-break lengths, the idle threshold and the minimum gap;
  - v1 files migrate automatically;
  - odd hand-edited values snap to the nearest allowed choice.
- Settings changes reschedule only the affected kind. A new interval keeps the time already counted; if that already exceeds the new interval, the reminder comes one minute later, never immediately. A changed minimum gap applies at once.
- Focus rings appear for keyboard focus only, never after a mouse click.

### Tests
- 93 Core tests, adding:
  - an interval change mid-countdown;
  - an already-elapsed interval;
  - disable and re-enable;
  - per-kind snooze length;
  - custom quiet hours across midnight;
  - idle threshold changes both ways;
  - the minimum gap;
  - v1→v2 migration;
  - snapping odd values.

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
