# Quest Board

This directory is the project's shared notice board for humans and coding agents.
Quest cards under `quests/` are tracked Markdown. Each card contains a structured state
block plus a readable summary and handoff log.

## The short ritual

```text
arrive -> brief -> claim -> work -> handoff/review/block/finish
```

Run `python .questboard/quest.py --help` for commands. Useful examples:

```powershell
python .questboard/quest.py brief --actor "Pigeon/Codex"
python .questboard/quest.py add "Repair unit progress refresh" --next "Reproduce the stale scan path" --actor "Esmund/Cursor"
python .questboard/quest.py claim Q-... --actor "Esmund/Cursor"
python .questboard/quest.py handoff Q-... --actor "Esmund/Cursor" --next "Verify the progress tracker upload" --note "Refresh logic patched"
python .questboard/quest.py move Q-... review --actor "Pigeon/Codex" --next "Run Inventor smoke test"
python .questboard/quest.py finish Q-... --actor "Pigeon/Codex" --note "Smoke test passed"
```

## Ground rules

- One quest should have one concrete next action.
- Claim before changing code in its scope.
- Do not take an unexpired claim without an explicit `--force` decision.
- A handoff without a next action is just a diary entry. The CLI refuses it.
- Keep durable design facts elsewhere. This board records motion.
- If cards conflict in Git, resolve the overlap deliberately; two agents probably touched the same ground.

Set `QUESTBOARD_ACTOR` to avoid repeating `--actor`. Otherwise the CLI falls back to
the repository Git user, then the operating-system username.
