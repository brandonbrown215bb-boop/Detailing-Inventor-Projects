#!/usr/bin/env python3
"""
Read DOCUMENT_CONFIG_JSON from 391Z Inventor IAM files (same COM approach as Pigeon).

Stdout: JSON { ok, surfaces: [{ iamPath, config }], errors: [{ iamPath, error }] }
Stderr: progress logs only.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

try:
    import win32com.client
except ImportError as exc:
    print(
        json.dumps(
            {
                "ok": False,
                "error": f"pywin32 is required: {exc}. Install with: pip install pywin32",
                "surfaces": [],
                "errors": [],
            }
        ),
        flush=True,
    )
    raise SystemExit(1)

CONFIG_ATTR = "DOCUMENT_CONFIG_JSON"
MOM_SET = "MOM_DATA"
CLOSE_WAIT_SECONDS = 0.5
DOCUMENT_READY_TIMEOUT_SECONDS = 30.0
DOCUMENT_READY_POLL_SECONDS = 0.5
ATTRIBUTE_SCAN_RETRIES = 5
ATTRIBUTE_SCAN_RETRY_DELAY_SECONDS = 0.5


def wait_for_document_ready(doc) -> bool:
    elapsed = 0.0
    while elapsed < DOCUMENT_READY_TIMEOUT_SECONDS:
        try:
            _ = doc.FullFileName
            _ = doc.AttributeSets.Count
            return True
        except Exception:
            time.sleep(DOCUMENT_READY_POLL_SECONDS)
            elapsed += DOCUMENT_READY_POLL_SECONDS
    return False


def normalize_attribute_value(raw_value, attr_name: str) -> str:
    if raw_value is None:
        raise ValueError(f"Attribute '{attr_name}' has no value.")
    if isinstance(raw_value, bytes):
        return raw_value.decode("utf-8")
    if isinstance(raw_value, str):
        return raw_value
    return str(raw_value)


def read_config_from_document(doc, iam_path: str) -> dict:
    last_error = None
    for attempt in range(1, ATTRIBUTE_SCAN_RETRIES + 1):
        # Same as Ce3 ICG/Framer: read from MOM_DATA attribute set first.
        try:
            attr_sets = doc.AttributeSets
            if attr_sets.NameIsUsed(MOM_SET):
                mom_set = attr_sets[MOM_SET]
                if mom_set.NameIsUsed(CONFIG_ATTR):
                    att = mom_set[CONFIG_ATTR]
                    text_value = normalize_attribute_value(att.Value, CONFIG_ATTR)
                    config = json.loads(text_value)
                    if not isinstance(config, dict) or "configuration" not in config:
                        raise ValueError("DOCUMENT_CONFIG_JSON missing configuration block")
                    return config
                last_error = f"MOM_DATA exists but {CONFIG_ATTR} is missing"
            else:
                last_error = f"{MOM_SET} attribute set missing"
        except Exception as exc:
            last_error = str(exc)

        # Fallback: scan all attribute sets (legacy IAMs).
        for attrib_set in doc.AttributeSets:
            for att in attrib_set:
                try:
                    attr_name = str(att.Name)
                except Exception:
                    continue
                if attr_name != CONFIG_ATTR:
                    continue
                text_value = normalize_attribute_value(att.Value, CONFIG_ATTR)
                config = json.loads(text_value)
                if not isinstance(config, dict) or "configuration" not in config:
                    raise ValueError("DOCUMENT_CONFIG_JSON missing configuration block")
                return config
        if attempt < ATTRIBUTE_SCAN_RETRIES:
            time.sleep(ATTRIBUTE_SCAN_RETRY_DELAY_SECONDS)
    detail = last_error or "attribute not found"
    raise ValueError(f"{CONFIG_ATTR} not readable from {iam_path} ({detail})")


def read_config_from_iam(inv, iam_path: str) -> dict:
    iam_path = str(Path(iam_path).resolve())
    doc = None
    try:
        doc = inv.Documents.Open(iam_path, True)
        if not wait_for_document_ready(doc):
            raise RuntimeError("Document did not become ready in time")
        return read_config_from_document(doc, iam_path)
    finally:
        if doc is not None:
            try:
                doc.Close()
            except Exception:
                pass
            time.sleep(CLOSE_WAIT_SECONDS)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Read DOCUMENT_CONFIG_JSON from IAM files via Inventor.")
    parser.add_argument("iam_paths", nargs="*", help="IAM file paths")
    parser.add_argument("--paths-file", help="UTF-8 text file with one IAM path per line")
    parser.add_argument("--visible", action="store_true", help="Show Inventor UI (default: hidden)")
    return parser.parse_args()


def load_paths(args: argparse.Namespace) -> list[str]:
    paths: list[str] = [str(Path(p).resolve()) for p in args.iam_paths]
    if args.paths_file:
        text = Path(args.paths_file).read_text(encoding="utf-8")
        for line in text.splitlines():
            line = line.strip()
            if line:
                paths.append(str(Path(line).resolve()))
    # preserve order, drop duplicates
    seen: set[str] = set()
    unique: list[str] = []
    for p in paths:
        if p in seen:
            continue
        seen.add(p)
        unique.append(p)
    return unique


def skid_hint_from_path(iam_path: str) -> str:
    for part in reversed(Path(iam_path).parts):
        if part.lower().startswith("skid "):
            return part
    return ""


def emit_progress(**payload) -> None:
    payload.setdefault("type", "progress")
    print(json.dumps(payload), file=sys.stderr, flush=True)


def main() -> int:
    args = parse_args()
    iam_paths = load_paths(args)
    if not iam_paths:
        print(json.dumps({"ok": False, "error": "No IAM paths provided", "surfaces": [], "errors": []}), flush=True)
        return 1

    inv = None
    surfaces: list[dict] = []
    errors: list[dict] = []

    try:
        inv = win32com.client.Dispatch("Inventor.Application")
        inv.Visible = bool(args.visible)
        inv.SilentOperation = True

        total = len(iam_paths)
        emit_progress(phase="starting", current=0, total=total, message=f"Opening Inventor for {total} assemblies…")

        for index, iam_path in enumerate(iam_paths, start=1):
            surface_name = Path(iam_path).stem
            skid = skid_hint_from_path(iam_path)
            emit_progress(
                phase="reading",
                current=index,
                total=total,
                iamPath=iam_path,
                skid=skid,
                surface=surface_name,
                message=f"Reading {skid + ': ' if skid else ''}{surface_name} ({index}/{total})",
            )
            if not Path(iam_path).is_file():
                errors.append({"iamPath": iam_path, "error": "File not found"})
                continue
            try:
                config = read_config_from_iam(inv, iam_path)
                surfaces.append({"iamPath": iam_path, "config": config})
            except Exception as exc:
                errors.append({"iamPath": iam_path, "error": str(exc)})
    except Exception as exc:
        print(
            json.dumps(
                {
                    "ok": False,
                    "error": f"Inventor COM failed: {exc}",
                    "surfaces": surfaces,
                    "errors": errors,
                }
            ),
            flush=True,
        )
        return 1
    finally:
        if inv is not None:
            try:
                inv.Quit()
            except Exception:
                pass

    print(
        json.dumps(
            {
                "ok": len(surfaces) > 0,
                "surfaces": surfaces,
                "errors": errors,
            }
        ),
        flush=True,
    )
    return 0 if surfaces else 1


if __name__ == "__main__":
    raise SystemExit(main())
