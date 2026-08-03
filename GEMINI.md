<!-- AGENT_GROUND_START -->
@AGENTS.md

Use the Agent Ground repository contract and current source as authority.
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
