<!-- AGENT_GROUND_START -->
## Repository Ground

- Read `docs/architecture/README.md`, relevant ADRs under `docs/decisions/`, and `.agents/state/current.md` when present before substantial changes.
- Run Agent Ground `status` before trusting architecture notes. Stale notes are orientation only; current source and tests win.
- If `.codegraph/` exists, use CodeGraph before grep or manual source wandering for code structure and call paths.
- Keep boundaries cohesive and testable. Split by responsibility, not ceremony.
- Preserve unrelated work and generated/source boundaries.
- Update documentation when shipped behavior changes. State limitations plainly.
- Record durable project decisions in ADRs. Keep transient task state out of durable documentation.
<!-- AGENT_GROUND_END -->

<!-- QUEST_BOARD_START -->
## Shared Quest Board

This repository uses `.questboard/` as its shared coordination surface.

Before substantial work:

1. Run `python .questboard/quest.py brief --actor "<your name/tool>"`.
2. Claim the quest before editing its scope. If none exists, add one.
3. Treat an unexpired claim by someone else as occupied ground.

Before pausing or handing work to another person or agent:

1. Record what changed and the exact next action.
2. Use `handoff`, `move ... blocked/review`, or `finish` so the status is honest.
3. Commit `.questboard/quests/` changes with the code they describe.

The quest board is volatile coordination, not architecture documentation. Source, tests,
ADRs, and project documentation remain authoritative for durable technical facts.
<!-- QUEST_BOARD_END -->
