# Countdown

A ring timer for guided breaks: twenty seconds of looking away, a fifteen-second stretch hold, a three-minute walk.

**You provide** `remaining` and `total` in seconds, `kind` (sets the ring tone) and an optional one-line `label`. Update `remaining` once a second; the ring eases between values.

**Do**
- Count down, never up, and end on Brady's `cheer` confirmation.
- Show it on the break screen or inside an expanded toast.

**Don't**
- Use it for the interval between reminders; that lives quietly in the tray.
- Flash or pulse the ring as time runs out.
