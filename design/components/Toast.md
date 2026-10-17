# Toast

The reminder itself: a 368px glass card with Brady hanging from its top edge, a kind chip, a title, one line of body and up to three actions.

**You provide** `kind` (posture, move, eyes, water, stretch), `title`, the body as `children`, an optional `meta` line (interval or "52 min still"), and handlers `onDone`, `onSnooze`, `onSkip`, `onClose`. Skip only renders when `onSkip` is passed. Pass `mood: 'cheer'` with `actions: false` for the short confirmation after Done.

**Do**
- Keep the title to five words and the body to one sentence (see Voice in the brand book).
- Show one toast per reminder; stack at most two, newest at the bottom.
- Let it time out after 20 seconds; hovering pauses the timer.

**Don't**
- Put it on `glass`: toasts are always `glass-strong`, so text holds 4.5:1 over any wallpaper.
- Add a sound louder than a soft tick, or steal keyboard focus.
- Use it for anything but a reminder or its confirmation.
