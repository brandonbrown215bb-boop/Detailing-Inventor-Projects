# Esmund: start here

Quest Board is already installed in this repository. You do not need Codex, a plugin, an account, or a separate app. Cursor has a project rule that tells it to check the same board Pigeon's agents use.

The board answers five things:

1. What work exists?
2. Who has it right now?
3. What changed?
4. What is blocking it?
5. What is the next actual action?

## Sixty-second setup

Open a terminal in the repository root and run:

```bat
.questboard\questboard.cmd doctor
.questboard\questboard.cmd brief
```

The wrapper identifies you as `Esmund/Cursor`. To use another name for one command:

```bat
.questboard\questboard.cmd brief --actor "Esmund"
```

There is a ready quest called **First Quest Board handshake**. Claiming and returning it is the smoke test:

```bat
.questboard\questboard.cmd claim Q-20260803-213352-c21d
.questboard\questboard.cmd handoff Q-20260803-213352-c21d --next "Pigeon reviews Esmund's smoke-test note" --note "What made sense or what was annoying"
```

## The whole ritual

When starting:

```bat
.questboard\questboard.cmd brief
.questboard\questboard.cmd claim Q-...
```

When pausing but keeping the work:

```bat
.questboard\questboard.cmd note Q-... --note "What changed" --next "The exact next action"
```

When handing it to Pigeon or another agent:

```bat
.questboard\questboard.cmd handoff Q-... --note "What changed" --next "What the next person should do"
```

When implementation needs another set of eyes:

```bat
.questboard\questboard.cmd move Q-... review --note "What is ready" --next "What must be verified"
```

When blocked:

```bat
.questboard\questboard.cmd move Q-... blocked --blocker "The condition that must change" --next "What to do once it changes"
```

When finished:

```bat
.questboard\questboard.cmd finish Q-... --note "The evidence that it is done"
```

## What to tell Cursor

At the beginning of a session:

> Read the repository Quest Board, show me the open work, and claim the quest we choose before editing code.

Before stopping:

> Update the claimed Quest Board card with what changed and one exact next action. Include that card with the code when preparing the commit.

Cursor should receive this automatically from `.cursor/rules/quest-board.mdc`. The explicit prompt is here because agents are clever animals and clever animals still walk into screen doors.

## Git, without the incense

Quest cards live in `.questboard/quests/` and should travel in the same commit as the code they describe. The board does not pull or push Git automatically.

If Git feels unclear, ask Cursor:

> Show me the current branch, changed files, and the Quest Board card for this work. Do not commit or push until I approve the exact file list.

That keeps the ritual reviewable. It also avoids feeding an entire dirty worktree into one mystery commit.

## Boundaries

- An unexpired claim by someone else means stop and coordinate.
- `--force` is an emergency override, not a personality trait.
- Do not store permanent architecture or requirements in cards. Link the real document.
- This installation did not import, classify, commit, or alter the existing Inventor/WPF work. Create cards for active work deliberately.
