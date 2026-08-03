#!/usr/bin/env python3
"""A Git-native notice board for humans and coding agents."""

from __future__ import annotations

import argparse
import getpass
import json
import os
import re
import shutil
import subprocess
import sys
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Iterable


TOOL_VERSION = "0.1.1"
SCHEMA_VERSION = 1
STATUSES = ("inbox", "ready", "active", "blocked", "review", "parked", "done")
STATE_RE = re.compile(r"<!-- questboard:state\s*\n(.*?)\n-->", re.DOTALL)
QUEST_BOARD_START = "<!-- QUEST_BOARD_START -->"
QUEST_BOARD_END = "<!-- QUEST_BOARD_END -->"
YAML_FRONTMATTER_RE = re.compile(r"\A---\s*\n(.*?)\n---\s*(?:\n|\Z)", re.DOTALL)

PROTOCOL = """## Shared Quest Board

This repository uses `.questboard/` as its shared coordination surface.

Before substantial work:

1. Run `python .questboard/quest.py brief --actor \"<your name/tool>\"`.
2. Claim the quest before editing its scope. If none exists, add one.
3. Treat an unexpired claim by someone else as occupied ground.

Before pausing or handing work to another person or agent:

1. Record what changed and the exact next action.
2. Use `handoff`, `move ... blocked/review`, or `finish` so the status is honest.
3. Commit `.questboard/quests/` changes with the code they describe.

The quest board is volatile coordination, not architecture documentation. Source, tests,
ADRs, and project documentation remain authoritative for durable technical facts.
"""

QUESTBOARD_README = """# Quest Board

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
"""

CURSOR_RULE = """---
description: Check and update the shared quest board before and after project work
globs:
alwaysApply: true
---

""" + PROTOCOL

ANTIGRAVITY_FRONTMATTER = """---
description: Shared quest board coordination protocol for agents and humans
alwaysApply: true
---
"""


class QuestBoardError(RuntimeError):
    pass


def utc_now() -> datetime:
    return datetime.now(timezone.utc).replace(microsecond=0)


def iso(value: datetime) -> str:
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_iso(value: str | None) -> datetime | None:
    if not value:
        return None
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def repository_root(path: str | Path) -> Path:
    root = Path(path).expanduser().resolve()
    if not root.exists() or not root.is_dir():
        raise QuestBoardError(f"Repository path does not exist: {root}")
    return root


def board_dir(root: Path) -> Path:
    return root / ".questboard"


def quests_dir(root: Path) -> Path:
    return board_dir(root) / "quests"


def config_path(root: Path) -> Path:
    return board_dir(root) / "config.json"


def default_actor(root: Path) -> str:
    if os.environ.get("QUESTBOARD_ACTOR", "").strip():
        return os.environ["QUESTBOARD_ACTOR"].strip()
    try:
        result = subprocess.run(
            ["git", "config", "user.name"],
            cwd=root,
            check=False,
            capture_output=True,
            text=True,
        )
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip()
    except OSError:
        pass
    return getpass.getuser()


def actor_for(args: argparse.Namespace, root: Path) -> str:
    return (getattr(args, "actor", None) or default_actor(root)).strip()


def read_config(root: Path) -> dict[str, Any]:
    path = config_path(root)
    if not path.exists():
        raise QuestBoardError("No quest board found. Run `init` first.")
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def marker_block() -> str:
    return f"{QUEST_BOARD_START}\n{PROTOCOL.rstrip()}\n{QUEST_BOARD_END}\n"


def upsert_marker(path: Path, block: str) -> str:
    if path.exists():
        content = path.read_text(encoding="utf-8")
    else:
        content = ""
    pattern = re.compile(
        re.escape(QUEST_BOARD_START) + r".*?" + re.escape(QUEST_BOARD_END) + r"\s*",
        re.DOTALL,
    )
    if pattern.search(content):
        updated = pattern.sub(block, content, count=1)
        action = "updated"
    else:
        prefix = content.rstrip()
        updated = (prefix + "\n\n" if prefix else "") + block
        action = "created" if not content else "appended"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(updated, encoding="utf-8")
    return action


def upsert_antigravity_rule(path: Path, block: str) -> str:
    existed = path.exists()
    content = path.read_text(encoding="utf-8") if existed else ""
    content = YAML_FRONTMATTER_RE.sub("", content, count=1)
    marker_pattern = re.compile(
        re.escape(QUEST_BOARD_START) + r".*?" + re.escape(QUEST_BOARD_END) + r"\s*",
        re.DOTALL,
    )
    remainder = marker_pattern.sub("", content, count=1).strip()
    updated = ANTIGRAVITY_FRONTMATTER.rstrip() + "\n\n" + block.rstrip() + "\n"
    if remainder:
        updated += "\n" + remainder + "\n"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(updated, encoding="utf-8")
    return "updated" if existed else "created"


def ensure_gitignore(root: Path) -> None:
    path = root / ".gitignore"
    line = ".questboard/BOARD.local.md"
    content = path.read_text(encoding="utf-8") if path.exists() else ""
    if line not in {item.strip() for item in content.splitlines()}:
        path.write_text(content.rstrip() + ("\n" if content.strip() else "") + line + "\n", encoding="utf-8")


def install_cli(root: Path, *, upgrade: bool) -> str:
    target = board_dir(root) / "quest.py"
    source = Path(__file__).resolve()
    if target.exists() and not upgrade:
        return "preserved"
    try:
        if target.exists() and source.samefile(target):
            return "current"
    except OSError:
        pass
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    return "upgraded" if upgrade else "installed"


def command_init(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    qdir = quests_dir(root)
    qdir.mkdir(parents=True, exist_ok=True)
    config = config_path(root)
    if not config.exists():
        write_json(
            config,
            {
                "schema_version": SCHEMA_VERSION,
                "tool_version": TOOL_VERSION,
                "project": root.name,
                "claim_ttl_hours": args.claim_hours,
                "statuses": list(STATUSES),
            },
        )
    elif args.upgrade:
        existing_config = json.loads(config.read_text(encoding="utf-8"))
        existing_config["tool_version"] = TOOL_VERSION
        write_json(config, existing_config)
    readme = board_dir(root) / "README.md"
    if not readme.exists() or args.upgrade:
        readme.write_text(QUESTBOARD_README, encoding="utf-8")
    cli_action = install_cli(root, upgrade=args.upgrade)
    agents_action = upsert_marker(root / "AGENTS.md", marker_block())
    gemini_action = upsert_marker(root / "GEMINI.md", marker_block())
    antigravity = root / ".agents" / "rules" / "quest-board.md"
    antigravity_action = upsert_antigravity_rule(antigravity, marker_block())
    cursor = root / ".cursor" / "rules" / "quest-board.mdc"
    cursor.parent.mkdir(parents=True, exist_ok=True)
    cursor.write_text(CURSOR_RULE, encoding="utf-8")
    ensure_gitignore(root)
    print(f"Quest Board {TOOL_VERSION} initialized in {root}")
    print(f"  CLI: {cli_action}")
    print(f"  AGENTS.md: {agents_action}")
    print(f"  GEMINI.md: {gemini_action}")
    print(f"  Antigravity rule: {antigravity_action}")
    print("  Cursor rule: updated")
    return 0


def make_id(now: datetime | None = None) -> str:
    value = now or utc_now()
    return f"Q-{value.strftime('%Y%m%d-%H%M%S')}-{uuid.uuid4().hex[:4]}"


def quest_path(root: Path, quest_id: str) -> Path:
    return quests_dir(root) / f"{quest_id}.md"


def render_quest(state: dict[str, Any]) -> str:
    history = state.get("history") or []
    lines = [
        "<!-- questboard:state",
        json.dumps(state, indent=2),
        "-->",
        "",
        f"# {state['title']}",
        "",
        f"- **ID:** `{state['id']}`",
        f"- **Status:** `{state['status']}`",
        f"- **Priority:** `{state['priority']}`",
        f"- **Owner:** {state.get('owner') or 'Unclaimed'}",
        f"- **Updated:** {state['updated_at']}",
        "",
        "## Next action",
        "",
        state.get("next_action") or "No next action recorded.",
        "",
        "## Context",
        "",
        state.get("context") or "No additional context.",
        "",
        "## Blockers",
        "",
        state.get("blocker") or "None recorded.",
        "",
        "## Handoff log",
        "",
    ]
    if history:
        for item in reversed(history):
            note = item.get("note") or "No note."
            lines.append(f"- {item['at']} - **{item['actor']}** - `{item['event']}`: {note}")
    else:
        lines.append("- No updates yet.")
    return "\n".join(lines) + "\n"


def write_quest(root: Path, state: dict[str, Any]) -> None:
    path = quest_path(root, state["id"])
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(".md.tmp")
    temporary.write_text(render_quest(state), encoding="utf-8")
    temporary.replace(path)


def load_quest_file(path: Path) -> dict[str, Any]:
    content = path.read_text(encoding="utf-8")
    match = STATE_RE.search(content)
    if not match:
        raise QuestBoardError(f"Missing questboard state block: {path}")
    try:
        state = json.loads(match.group(1))
    except json.JSONDecodeError as error:
        raise QuestBoardError(f"Invalid questboard state in {path}: {error}") from error
    return state


def load_quest(root: Path, quest_id: str) -> dict[str, Any]:
    path = quest_path(root, quest_id)
    if not path.exists():
        candidates = list(quests_dir(root).glob(f"{quest_id}*.md"))
        if len(candidates) == 1:
            path = candidates[0]
        else:
            raise QuestBoardError(f"Quest not found: {quest_id}")
    return load_quest_file(path)


def all_quests(root: Path) -> list[dict[str, Any]]:
    directory = quests_dir(root)
    if not directory.exists():
        read_config(root)
        return []
    quests = [load_quest_file(path) for path in sorted(directory.glob("Q-*.md"))]
    return sorted(quests, key=lambda q: (STATUSES.index(q.get("status", "inbox")), q.get("priority", "normal"), q["id"]))


def append_history(state: dict[str, Any], actor: str, event: str, note: str | None) -> None:
    state.setdefault("history", []).append(
        {"at": iso(utc_now()), "actor": actor, "event": event, "note": (note or "").strip()}
    )


def command_add(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    read_config(root)
    actor = actor_for(args, root)
    now = utc_now()
    quest_id = make_id(now)
    state: dict[str, Any] = {
        "schema_version": SCHEMA_VERSION,
        "id": quest_id,
        "title": args.title.strip(),
        "status": args.status,
        "priority": args.priority,
        "owner": None,
        "claim_expires_at": None,
        "next_action": args.next.strip(),
        "context": (args.context or "").strip(),
        "blocker": None,
        "created_at": iso(now),
        "updated_at": iso(now),
        "history": [],
    }
    append_history(state, actor, "created", args.note or f"Created as {args.status}.")
    write_quest(root, state)
    print(f"Added {quest_id}: {state['title']}")
    return 0


def claim_is_live(state: dict[str, Any]) -> bool:
    expiry = parse_iso(state.get("claim_expires_at"))
    return bool(state.get("owner") and expiry and expiry > utc_now())


def command_claim(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    config = read_config(root)
    actor = actor_for(args, root)
    state = load_quest(root, args.quest_id)
    current_owner = state.get("owner")
    if current_owner and current_owner != actor and claim_is_live(state) and not args.force:
        raise QuestBoardError(
            f"{state['id']} is claimed by {current_owner} until {state['claim_expires_at']}. "
            "Use --force only after deliberate coordination."
        )
    hours = args.hours if args.hours is not None else int(config.get("claim_ttl_hours", 24))
    state["owner"] = actor
    state["claim_expires_at"] = iso(utc_now() + timedelta(hours=hours))
    state["status"] = "active"
    state["blocker"] = None
    state["updated_at"] = iso(utc_now())
    append_history(state, actor, "claimed", args.note or f"Claimed for {hours} hours.")
    write_quest(root, state)
    print(f"Claimed {state['id']} as {actor} until {state['claim_expires_at']}")
    return 0


def ensure_actor_can_update(state: dict[str, Any], actor: str, force: bool) -> None:
    owner = state.get("owner")
    if owner and owner != actor and claim_is_live(state) and not force:
        raise QuestBoardError(
            f"{state['id']} is claimed by {owner} until {state['claim_expires_at']}. "
            "Use --force only after deliberate coordination."
        )


def command_move(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    read_config(root)
    actor = actor_for(args, root)
    state = load_quest(root, args.quest_id)
    ensure_actor_can_update(state, actor, args.force)
    if args.status == "blocked" and not (args.blocker or state.get("blocker")):
        raise QuestBoardError("A blocked quest needs --blocker with the thing that must change.")
    if args.next:
        state["next_action"] = args.next.strip()
    if args.status != "done" and not state.get("next_action"):
        raise QuestBoardError("An open quest needs one concrete next action.")
    state["status"] = args.status
    state["blocker"] = args.blocker.strip() if args.blocker else (state.get("blocker") if args.status == "blocked" else None)
    if args.status in ("ready", "inbox", "review", "parked", "done"):
        state["owner"] = None
        state["claim_expires_at"] = None
    elif args.status == "active":
        state["owner"] = state.get("owner") or actor
    state["updated_at"] = iso(utc_now())
    append_history(state, actor, f"moved-to-{args.status}", args.note)
    write_quest(root, state)
    print(f"Moved {state['id']} to {args.status}")
    return 0


def command_handoff(args: argparse.Namespace) -> int:
    if not args.next.strip():
        raise QuestBoardError("A handoff needs --next with the next concrete action.")
    args.status = args.status or "ready"
    args.note = args.note or "Work handed off."
    return command_move(args)


def command_finish(args: argparse.Namespace) -> int:
    args.status = "done"
    args.next = "Complete."
    args.blocker = None
    args.note = args.note or "Quest completed."
    return command_move(args)


def command_note(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    read_config(root)
    actor = actor_for(args, root)
    state = load_quest(root, args.quest_id)
    ensure_actor_can_update(state, actor, args.force)
    if args.next:
        state["next_action"] = args.next.strip()
    state["updated_at"] = iso(utc_now())
    append_history(state, actor, "note", args.note)
    write_quest(root, state)
    print(f"Updated {state['id']}")
    return 0


def format_quest_line(state: dict[str, Any]) -> str:
    owner = state.get("owner") or "unclaimed"
    return f"{state['id']}  [{state['status']:<7}] [{state['priority']:<6}] {state['title']} - {owner}"


def filter_quests(quests: Iterable[dict[str, Any]], statuses: list[str] | None) -> list[dict[str, Any]]:
    return [quest for quest in quests if not statuses or quest.get("status") in statuses]


def command_list(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    read_config(root)
    quests = filter_quests(all_quests(root), args.status)
    if args.json:
        print(json.dumps(quests, indent=2))
    elif not quests:
        print("No matching quests.")
    else:
        for quest in quests:
            print(format_quest_line(quest))
    return 0


def command_brief(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    read_config(root)
    actor = actor_for(args, root)
    quests = all_quests(root)
    visible = [quest for quest in quests if quest.get("status") != "done"]
    print(f"Quest Board - {root.name} - arriving as {actor}")
    if not visible:
        print("\nNo open quests. Add one before substantial work.")
        return 0
    for status in ("active", "review", "ready", "blocked", "inbox", "parked"):
        group = [quest for quest in visible if quest.get("status") == status]
        if not group:
            continue
        print(f"\n{status.upper()}")
        for quest in group:
            print(f"  {format_quest_line(quest)}")
            print(f"    Next: {quest.get('next_action') or 'MISSING'}")
            if status == "blocked":
                print(f"    Blocker: {quest.get('blocker') or 'MISSING'}")
    print("\nClaim before editing. Leave one exact next action before you go.")
    return 0


def validate_quest(state: dict[str, Any], path: Path) -> list[str]:
    issues: list[str] = []
    required = ("schema_version", "id", "title", "status", "priority", "created_at", "updated_at")
    for key in required:
        if key not in state:
            issues.append(f"{path}: missing {key}")
    if state.get("status") not in STATUSES:
        issues.append(f"{path}: invalid status {state.get('status')!r}")
    if state.get("status") != "done" and not state.get("next_action"):
        issues.append(f"{path}: open quest has no next action")
    if state.get("status") == "blocked" and not state.get("blocker"):
        issues.append(f"{path}: blocked quest has no blocker")
    if state.get("owner") and not state.get("claim_expires_at"):
        issues.append(f"{path}: owner has no claim expiry")
    if state.get("id") and path.stem != state["id"]:
        issues.append(f"{path}: filename does not match id {state['id']}")
    return issues


def command_doctor(args: argparse.Namespace) -> int:
    root = repository_root(args.repo)
    config = read_config(root)
    issues: list[str] = []
    if config.get("schema_version") != SCHEMA_VERSION:
        issues.append(f"config: unsupported schema version {config.get('schema_version')}")
    seen: set[str] = set()
    directory = quests_dir(root)
    for path in sorted(directory.glob("*.md")):
        try:
            state = load_quest_file(path)
            issues.extend(validate_quest(state, path))
            if state.get("id") in seen:
                issues.append(f"{path}: duplicate id {state.get('id')}")
            seen.add(state.get("id"))
        except (QuestBoardError, OSError) as error:
            issues.append(str(error))
    required_adapters = [
        root / "AGENTS.md",
        root / "GEMINI.md",
        root / ".agents" / "rules" / "quest-board.md",
        root / ".cursor" / "rules" / "quest-board.mdc",
    ]
    for path in required_adapters:
        if not path.exists():
            issues.append(f"missing adapter: {path.relative_to(root)}")
    antigravity_path = root / ".agents" / "rules" / "quest-board.md"
    if antigravity_path.exists():
        antigravity_content = antigravity_path.read_text(encoding="utf-8")
        frontmatter = YAML_FRONTMATTER_RE.match(antigravity_content)
        if not frontmatter:
            issues.append("Antigravity adapter is missing YAML frontmatter")
        elif not re.search(r"(?m)^alwaysApply:\s*true\s*$", frontmatter.group(1)):
            issues.append("Antigravity adapter frontmatter must declare alwaysApply: true")
    if issues:
        print("Quest Board doctor found issues:")
        for issue in issues:
            print(f"  - {issue}")
        return 1
    print(f"Quest Board {TOOL_VERSION} is healthy ({len(seen)} quest cards).")
    return 0


def add_common(parser: argparse.ArgumentParser, *, actor: bool = False, force: bool = False) -> None:
    parser.add_argument("--repo", default=".", help="Repository root (default: current directory)")
    if actor:
        parser.add_argument("--actor", help="Human/tool identity; defaults to QUESTBOARD_ACTOR or Git user")
    if force:
        parser.add_argument("--force", action="store_true", help="Override another live claim deliberately")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", action="version", version=f"Quest Board {TOOL_VERSION}")
    sub = parser.add_subparsers(dest="command", required=True)

    init_parser = sub.add_parser("init", help="Install or refresh Quest Board in a repository")
    add_common(init_parser)
    init_parser.add_argument("--claim-hours", type=int, default=24, help="Default claim duration")
    init_parser.add_argument("--upgrade", action="store_true", help="Refresh managed protocol and CLI files")
    init_parser.set_defaults(func=command_init)

    add_parser = sub.add_parser("add", help="Add a quest with one concrete next action")
    add_common(add_parser, actor=True)
    add_parser.add_argument("title")
    add_parser.add_argument("--next", required=True, help="The next concrete action")
    add_parser.add_argument("--context")
    add_parser.add_argument("--note")
    add_parser.add_argument("--status", choices=STATUSES, default="ready")
    add_parser.add_argument("--priority", choices=("low", "normal", "high", "urgent"), default="normal")
    add_parser.set_defaults(func=command_add)

    claim_parser = sub.add_parser("claim", help="Claim a quest before changing its scope")
    add_common(claim_parser, actor=True, force=True)
    claim_parser.add_argument("quest_id")
    claim_parser.add_argument("--hours", type=int)
    claim_parser.add_argument("--note")
    claim_parser.set_defaults(func=command_claim)

    move_parser = sub.add_parser("move", help="Move a quest to another state")
    add_common(move_parser, actor=True, force=True)
    move_parser.add_argument("quest_id")
    move_parser.add_argument("status", choices=STATUSES)
    move_parser.add_argument("--next")
    move_parser.add_argument("--blocker")
    move_parser.add_argument("--note")
    move_parser.set_defaults(func=command_move)

    handoff_parser = sub.add_parser("handoff", help="Release a quest with an explicit next action")
    add_common(handoff_parser, actor=True, force=True)
    handoff_parser.add_argument("quest_id")
    handoff_parser.add_argument("--next", required=True)
    handoff_parser.add_argument("--note")
    handoff_parser.add_argument("--status", choices=("ready", "review", "blocked"), default="ready")
    handoff_parser.add_argument("--blocker", help="Required when handing off as blocked")
    handoff_parser.set_defaults(func=command_handoff)

    finish_parser = sub.add_parser("finish", help="Mark a quest done and release its claim")
    add_common(finish_parser, actor=True, force=True)
    finish_parser.add_argument("quest_id")
    finish_parser.add_argument("--note")
    finish_parser.set_defaults(func=command_finish)

    note_parser = sub.add_parser("note", help="Add a progress note without changing status")
    add_common(note_parser, actor=True, force=True)
    note_parser.add_argument("quest_id")
    note_parser.add_argument("--note", required=True)
    note_parser.add_argument("--next")
    note_parser.set_defaults(func=command_note)

    list_parser = sub.add_parser("list", help="List quest cards")
    add_common(list_parser)
    list_parser.add_argument("--status", action="append", choices=STATUSES)
    list_parser.add_argument("--json", action="store_true")
    list_parser.set_defaults(func=command_list)

    brief_parser = sub.add_parser("brief", help="Show the board and next actions at session start")
    add_common(brief_parser, actor=True)
    brief_parser.set_defaults(func=command_brief)

    doctor_parser = sub.add_parser("doctor", help="Validate cards and agent adapters")
    add_common(doctor_parser)
    doctor_parser.set_defaults(func=command_doctor)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.func(args))
    except (QuestBoardError, OSError, json.JSONDecodeError) as error:
        print(f"quest-board: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
