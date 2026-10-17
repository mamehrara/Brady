Brady is a Windows tray app that nudges people who sit at a PC all day to fix their posture, walk, rest their eyes, drink water and stretch. The nudges come from Brady, a three-toed sloth who hangs from the top edge of every toast. The joke carries the brand: the slowest animal alive is the one telling you to move. Everything about Brady is gentle, warm and a little self-aware, and nothing ever nags.

## Voice

Brady speaks in the first person, to "you", in plain sentence case.

- Titles are five words or fewer and set in `title`. Body copy is one sentence, about 90 characters at most, in `body`.
- Say what to do, physically and specifically: "Shoulders back, feet flat, screen at eye level." Not "Remember to maintain good posture."
- Brady's humor is about being a sloth, never about the user: "Even I'd move by now." Never "You've been lazy."
- No guilt, no streak-shaming, no "you missed 4 breaks". Missed reminders are counted quietly in the tray, never announced.
- No exclamation marks, no emoji. Status lives in icons and chips, not in 🧘.
- Button labels are verbs or outcomes: "Done", "Snooze 10 min", "Skip", "Start break". Never "OK" or "Got it".

Real copy, by kind:

| Kind | Title | Body |
| --- | --- | --- |
| Posture | Sit tall for a sec | Shoulders back, feet flat, screen at eye level. |
| Move | Even I'd move by now | You've been sitting a while. Take a three-minute walk. |
| Eyes | Look far away | Twenty seconds on something across the room. I'll count. |
| Water | Sip break | A glass of water, then back to it. |
| Stretch | Reach for the branches | Arms up, slow breath, hold for fifteen seconds. |
| After Done | Nice. See you in 45. | (Brady switches to the `cheer` mood.) |
| After Snooze | Okay. I'll hang here. | Back in 10 minutes. (Brady switches to `sleepy`.) |

## Color

- Brady's own windows (Settings, the break screen) sit on `canopy`. Toasts sit on the user's wallpaper, so they always use `glass-strong`, never `glass`.
- `moss` is the brand: primary buttons (`on-moss` text), checked switches, the focus ring. Hover is `moss-hover`.
- Each reminder kind has one tone, used for its chip and its countdown ring: Posture `moss`, Move `bloom`, Eyes and Water `sky`, Stretch `fur`. Pair every tone with its `-soft` tint for backgrounds, with the tone itself as the text on it. Eyes and Water share `sky`, so a chip always shows its icon and word, never color alone.
- Text is `ink` or `ink-muted` on `canopy`, `surface-solid`, `glass` or `glass-strong`, in both themes. Every listed pair holds 4.5:1, and the tones hold 4.5:1 on `glass-strong` even over a pure white or pure black wallpaper.
- `brady-*` colors are the mascot's own and never appear in UI. They stay the same in light and dark.
- There is no error red. Brady has nothing to alarm anyone about. `bloom` is a sunrise, not a warning.

## Glass

- A glass surface is three things together: a `glass` or `glass-strong` fill, `backdrop-filter: blur(var(--blur-glass)) saturate(140%)`, and a 1px `glass-edge` border, lifted with `shadow-glass`.
- Use `blur-soft` for small glass pieces like glass buttons.
- When Windows "Transparency effects" is off, the app window is unfocused, or `prefers-reduced-transparency` is set, swap every glass fill for `surface-solid` and drop the blur.
- No gradients anywhere. The glass and the wallpaper behind it are the only depth.

## Type

- Fredoka (`display` family) is Brady's voice: `display` for the break screen headline, `title` for toast titles, `numeral` for countdowns with tabular figures.
- Figtree (`sans` family) carries everything else: `headline` for setting names, `body` for running text, `label` for buttons, `caption` for chips and meta lines.
- Both are variable fonts in `fonts/`. Segoe UI Variable is the fallback, so a missing font still looks native on Windows 11.

## Shape, space and motion

- Corners are soft: `radius-lg` for toasts and cards, `radius-md` for buttons, `radius-pill` for chips and switches.
- Space in the `space-*` steps only. A toast is padded with `space-4`; buttons sit `space-2` apart.
- Motion is slow and springy, like a sloth swinging, never bouncy-fast. Toasts slide up 12px and fade in over 320ms, ease-out. Brady sways once (±4°, 900ms) when a toast arrives, then stays still. Honor `prefers-reduced-motion` and the Windows "Animation effects" setting by cutting to a plain fade.
- Focus is a solid 2px `focus-ring` outline with a 2px offset on every control.

## Brady, the mascot

- Brady appears in three moods: `awake` (a new reminder), `cheer` (after Done), `sleepy` (after Snooze, and in the tray while paused). Use the `Mascot` component or the files in the Mascot group.
- In a toast, Brady hangs from the top edge at the left, claws over the edge. Never put Brady inside a box, behind text, or at the bottom of a card.
- The `head` pose (the Logos group's `brady-mark.svg`) is for the tray icon, the app avatar and the Settings header.
- Never redraw, recolor, stretch or rotate Brady beyond the single arrival sway. Never give him new outfits or props.

## Iconography

- One small outlined set, drawn for Brady at 24px: posture, move, eyes, water, stretch, snooze, check, close. 1.75px stroke, round caps and joins.
- In code, use the `Icon` component (it inherits `color`). The SVG files in the Icons group are drawn in `ink` (#1c2620) for use in light contexts.
- Show icons at 14px in chips, 16px in buttons and 24px elsewhere. Pair an icon with a word unless the control has an `aria-label`.

## Building it on Windows

- Toasts open bottom-right, 16px above the taskbar, stacked upward with `space-3` between them, at most two at once. They are borderless, topmost, and never take keyboard focus from what the user is doing.
- A toast waits 20 seconds, then slides away and counts as missed. Hovering pauses that timer.
- Brady stays silent during fullscreen apps, presentations and Windows Focus sessions, and resumes afterward without catching up on missed nudges.
- In WinUI 3, map `glass-strong` to a DesktopAcrylicController tint at the same color and opacity; in WPF or an embedded web view, use the CSS in `components/bundle.css` as is.
