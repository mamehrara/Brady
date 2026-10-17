# Button

Three variants for every action Brady offers: `primary` for the one thing to do now, `glass` for the alternative, `quiet` for the way out.

**You provide** `children` (a verb), optional `variant` (`primary`, `glass`, `quiet`; default `glass`), `size` (`md` 32px, `sm` 28px for toasts), `icon` (an Icon name), and any button attributes.

**Do**
- One `primary` per surface. In a toast it is always Done.
- Write labels as verbs or outcomes: "Done", "Snooze 10 min", "Start break".

**Don't**
- Use "OK", "Got it", or an icon-only button without `aria-label`.
- Put a `glass` button on a solid fill; it needs something behind it to blur.
