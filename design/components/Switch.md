# Switch

An on/off settings row: the whole row is the hit target, label and description on the left, the track on the right, like Windows Settings.

**You provide** `label`, optional `description` (the schedule in plain words), `checked`, `onChange(checked)`, and `disabled`.

**Do**
- Group related rows in a glass settings card; rows divide themselves with a `line` hairline.
- Describe what happens in words: "Every 30 minutes while you type".

**Don't**
- Use it for actions that need confirmation, or for choosing between more than two options.
