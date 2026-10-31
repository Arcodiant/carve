"""cairn-managed build hook: generates listing pages (tasks/bugs/batches/design/
changelog/requests index pages, any project-declared custom kind's index page,
plus the top-level milestones page) at `mkdocs build` time by scanning the
individual item files under docs/. No index file is ever committed to source —
see the cairn skill's references/artifacts.md for why.

Self-contained (stdlib only): mkdocs imports this file directly by path (the
`hooks:` key in mkdocs.yml), independent of how or whether the `cairn` Python
package itself is installed in whatever environment runs `mkdocs build`.

Mechanism: write the generated `index.md` / `milestones.md` files to disk in
`on_pre_build` (before mkdocs' own `get_files()` scans the docs directory, so
they're picked up like any other page with zero custom File-object plumbing),
then delete them in `on_post_build` so they never linger as stray on-disk files
between builds. `.gitignore` also excludes them defensively.
"""
from __future__ import annotations

import logging
import re
from pathlib import Path

log = logging.getLogger("mkdocs.cairn")

KIND_INFO = {
    "task": {"prefix": "T", "dir": "tasks", "label": "Tasks"},
    "bug": {"prefix": "B", "dir": "bugs", "label": "Bugs"},
    "batch": {"prefix": "BA", "dir": "batches", "label": "Batches"},
    "milestone": {"prefix": "M", "dir": "milestones", "label": "Milestones"},
    "design": {"prefix": "D", "dir": "design", "label": "Design docs"},
    "review": {"prefix": "RV", "dir": "reviews", "label": "Reviews"},
}

BOLD_FIELD_RE = re.compile(r"^\*\*([A-Za-z ]+):\*\*\s*(.*)$")

_GENERATED_PATHS: list[Path] = []  # populated in on_pre_build, cleaned in on_post_build


def _parse_item(path: Path):
    lines = path.read_text(encoding="utf-8").splitlines()
    item_id, title, fields = "", "", {}
    rest = []
    for i, line in enumerate(lines):
        if line.startswith("# "):
            heading = line[2:].strip()
            item_id, title = heading.split(" — ", 1) if " — " in heading else (heading, heading)
            rest = lines[i + 1:]
            break
    for line in rest:
        m = BOLD_FIELD_RE.match(line.strip())
        if m:
            fields[m.group(1).strip()] = m.group(2).strip()
        elif line.strip() == "":
            continue
        else:
            break
    return item_id, title, fields


def _numeric_sort_key(path: Path) -> tuple:
    """Sort T-2 before T-15 — string sort on the filename gets this backwards
    once ids reach two digits."""
    m = re.match(r"^([A-Z]+)-(\d+)", path.name)
    return (m.group(1), int(m.group(2))) if m else (path.name, 0)


def _table(rows: list[tuple[str, str, str]]) -> str:
    if not rows:
        return "*(none yet)*\n"
    out = "| ID | Status | Title |\n|---|---|---|\n"
    for item_id, status, title in rows:
        out += f"| {item_id} | {status} | {title} |\n"
    return out


def _kind_index_content(kind_dir: Path, prefix: str, label: str) -> str:
    rows = []
    for path in sorted(kind_dir.glob(f"{prefix}-*.md"), key=_numeric_sort_key):
        item_id, title, fields = _parse_item(path)
        if not item_id:
            continue
        rows.append((f"[{item_id}]({path.name})", fields.get("Status", ""), title))
    return f"# {label}\n\n{_table(rows)}"


def _milestones_content(milestones_dir: Path) -> str:
    rows = []
    for path in sorted(milestones_dir.glob("M-*.md"), key=_numeric_sort_key):
        item_id, title, fields = _parse_item(path)
        if not item_id:
            continue
        rows.append((f"[{item_id}](milestones/{path.name})", fields.get("Status", ""), title))
    return f"# Milestones\n\n{_table(rows)}"


def _version_sort_key(path: Path) -> tuple:
    """Sort changelog entries newest-first by semver, not filename string order —
    a plain string sort puts 0.10.0 before 0.2.0."""
    m = re.match(r"^(\d+)\.(\d+)\.(\d+)", path.name)
    return tuple(int(g) for g in m.groups()) if m else (0, 0, 0)


def _changelog_content(changelog_dir: Path) -> str:
    pending = changelog_dir / "UNRELEASED.md"
    rows = []
    # Listed only with entries in it: main keeps an empty one (the heading alone)
    # after every release, which isn't worth a row.
    if pending.exists() and any(line.startswith("- ")
                                for line in pending.read_text(encoding="utf-8").splitlines()):
        item_id, title, _ = _parse_item(pending)
        rows.append(f"- [{title or item_id}]({pending.name})")
    for path in sorted(changelog_dir.glob("*.md"), key=_version_sort_key, reverse=True):
        if path.name == pending.name:
            continue
        item_id, title, _ = _parse_item(path)
        if not item_id:
            continue
        rows.append(f"- [{item_id} — {title}]({path.name})" if title != item_id else f"- [{item_id}]({path.name})")
    body = "\n".join(rows) if rows else "*(none yet)*"
    return f"# Changelog\n\n{body}\n"


def _requests_content(requests_dir: Path) -> str:
    rows = []
    for path in sorted(requests_dir.glob("*.md")):
        item_id, title, _ = _parse_item(path)
        rows.append(f"- [{title or item_id or path.stem}]({path.name})")
    body = "\n".join(rows) if rows else "*(none yet)*"
    return f"# Requests\n\n{body}\n"


def _parse_custom_kinds(cairn_yml: Path) -> dict:
    """Read `.cairn.yml`'s optional `kinds:` block — a project's own doc types
    beyond cairn's core five, declared so `cairn reserve <kind>` can claim ids
    for them (see common.effective_kind_info / id-reservation.md). Duplicated
    from sync.py's `_parse_kinds_block` rather than imported: this file is
    self-contained by design (see module docstring) so it keeps working
    whether or not the `cairn` package is installed in the environment running
    `mkdocs build`.

    A custom kind has no status vocabulary (sync.py's parser doesn't record
    one either — see effective_kind_info's docstring on why kinds: stays
    reservation-only), so its listing below is a plain list, not a
    Status-columned table."""
    if not cairn_yml.exists():
        return {}
    text = cairn_yml.read_text(encoding="utf-8")
    m = re.search(r"^kinds:[ \t]*$", text, re.MULTILINE)
    if not m:
        return {}
    kinds: dict = {}
    current = None
    for line in text[m.end():].splitlines():
        if line.strip() == "":
            continue
        if re.match(r"^\S", line):  # dedented back to column 0 -- block ended
            break
        km = re.match(r"^  (\w[\w.-]*):\s*$", line)
        if km:
            current = km.group(1)
            kinds[current] = {}
            continue
        fm = re.match(r"^    (\w+):\s*(.+?)\s*$", line)
        if fm and current:
            kinds[current][fm.group(1)] = fm.group(2)
    return {name: info for name, info in kinds.items() if "prefix" in info and "dir" in info}


def _custom_kind_content(kind_dir: Path, prefix: str, label: str) -> str:
    rows = []
    for path in sorted(kind_dir.glob(f"{prefix}-*.md"), key=_numeric_sort_key):
        item_id, title, _ = _parse_item(path)
        if not item_id:
            continue
        rows.append(f"- [{item_id} — {title}]({path.name})" if title != item_id else f"- [{item_id}]({path.name})")
    body = "\n".join(rows) if rows else "*(none yet)*"
    return f"# {label}\n\n{body}\n"


def on_pre_build(config, **kwargs):
    global _GENERATED_PATHS
    _GENERATED_PATHS = []
    docs_dir = Path(config["docs_dir"])

    # Always (re)write — these paths are fully owned by this hook. A prior build
    # that crashed between on_pre_build and on_post_build can leave stale copies
    # on disk; regenerating unconditionally is correct (skipping would silently
    # serve stale content), and on_post_build below removes them again on success.
    for info in KIND_INFO.values():
        kind_dir = docs_dir / info["dir"]
        if not kind_dir.exists():
            continue
        target = kind_dir / "index.md"
        target.write_text(_kind_index_content(kind_dir, info["prefix"], info["label"]), encoding="utf-8")
        _GENERATED_PATHS.append(target)

    milestones_dir = docs_dir / "milestones"
    if milestones_dir.exists():
        target = docs_dir / "milestones.md"
        target.write_text(_milestones_content(milestones_dir), encoding="utf-8")
        _GENERATED_PATHS.append(target)

    changelog_dir = docs_dir / "changelog"
    if changelog_dir.exists():
        target = changelog_dir / "index.md"
        target.write_text(_changelog_content(changelog_dir), encoding="utf-8")
        _GENERATED_PATHS.append(target)

    requests_dir = docs_dir / "requests"
    if requests_dir.exists():
        target = requests_dir / "index.md"
        target.write_text(_requests_content(requests_dir), encoding="utf-8")
        _GENERATED_PATHS.append(target)

    for info in _parse_custom_kinds(docs_dir.parent / ".cairn.yml").values():
        kind_dir = docs_dir / info["dir"]
        if not kind_dir.exists():
            continue
        target = kind_dir / "index.md"
        label = info["dir"].replace("-", " ").replace("_", " ").title()
        target.write_text(_custom_kind_content(kind_dir, info["prefix"], label), encoding="utf-8")
        _GENERATED_PATHS.append(target)


def on_post_build(config, **kwargs):
    for path in _GENERATED_PATHS:
        try:
            path.unlink(missing_ok=True)
        except OSError:
            pass
    _GENERATED_PATHS.clear()
