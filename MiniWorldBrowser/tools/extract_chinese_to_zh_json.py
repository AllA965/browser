import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
ZH_JSON_PATH = ROOT / "Resources" / "i18n" / "zh-CN.json"

# File extensions to scan
EXTS = {".cs", ".js", ".ts", ".tsx", ".html", ".htm", ".xml", ".json"}
SKIP_DIRS = {"bin", "obj", ".git", ".idea", ".vscode", ".trae"}
SKIP_REL_PREFIXES = ("Resources/i18n/",)

# Regex: capture quoted string content (basic), then check if contains CJK
QUOTE_RE = re.compile(r'"([^"\\]*(?:\\.[^"\\]*)*)"|\'([^\'\\]*(?:\\.[^\'\\]*)*)\'')
HAS_CJK_RE = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff\uF900-\uFAFF]")  # basic CJK ranges
RAW_ENTRY_RE = re.compile(r'^\s*"(?P<key>[^"\\]+)"\s*:\s*"(?P<val>(?:\\.|[^"\\])*)"\s*,?\s*$')
RAW_BLOCK_START_RE = re.compile(r'"raw"\s*:\s*\{')


def sanitize_key_component(s: str) -> str:
    # Replace path separators and disallowed chars with dots
    s = s.replace("\\", "/")
    s = re.sub(r"[^A-Za-z0-9._/-]+", "_", s)
    s = s.replace("/", ".")
    return s


def find_strings_with_chinese(path: Path):
    items = []
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return items

    for lineno, line in enumerate(text.splitlines(), start=1):
        occ = 0
        for m in QUOTE_RE.finditer(line):
            val = m.group(1) if m.group(1) is not None else m.group(2)
            if not val:
                continue
            norm = val.strip()
            if norm and HAS_CJK_RE.search(norm):
                occ += 1
                items.append((lineno, occ, norm))
    return items


def make_key(key_base: str, lineno: int, occ: int) -> str:
    if occ <= 1:
        return f"{key_base}.L{lineno}"
    return f"{key_base}.L{lineno}.N{occ}"


def find_raw_object_bounds(text: str):
    m = RAW_BLOCK_START_RE.search(text)
    if not m:
        return None

    start_brace = m.end() - 1  # points at "{"
    depth = 0
    in_str = False
    esc = False
    for idx in range(start_brace, len(text)):
        ch = text[idx]
        if in_str:
            if esc:
                esc = False
            elif ch == "\\":
                esc = True
            elif ch == '"':
                in_str = False
            continue

        if ch == '"':
            in_str = True
        elif ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return start_brace, idx
    return None


def parse_existing_raw_keys(raw_inner: str):
    keys = set()
    for line in raw_inner.splitlines():
        m = RAW_ENTRY_RE.match(line)
        if m:
            keys.add(m.group("key"))
    return keys


def should_skip_file(path: Path):
    lower_parts = {p.lower() for p in path.parts}
    if SKIP_DIRS.intersection(lower_parts):
        return True
    rel = path.relative_to(ROOT).as_posix()
    for prefix in SKIP_REL_PREFIXES:
        if rel.startswith(prefix):
            return True
    return False


def collect_missing_entries(existing_keys):
    additions = {}
    scanned_files = 0

    for dirpath, dirnames, filenames in os.walk(ROOT):
        # prune directories early
        dirnames[:] = [d for d in dirnames if d.lower() not in SKIP_DIRS]

        for fn in filenames:
            p = Path(dirpath) / fn
            if p.suffix.lower() not in EXTS:
                continue
            if should_skip_file(p):
                continue

            scanned_files += 1
            rel = p.relative_to(ROOT).as_posix()
            key_base = sanitize_key_component(rel)

            for lineno, occ, text in find_strings_with_chinese(p):
                key = make_key(key_base, lineno, occ)
                if key in existing_keys or key in additions:
                    continue
                additions[key] = text

    return scanned_files, additions


def append_entries_to_raw(text: str, raw_bounds, additions):
    if not additions:
        return text

    start, end = raw_bounds
    raw_inner = text[start + 1 : end]
    lines = raw_inner.splitlines()

    # ensure existing last entry ends with comma before appending
    last_non_empty = -1
    for i in range(len(lines) - 1, -1, -1):
        if lines[i].strip():
            last_non_empty = i
            break

    if last_non_empty >= 0 and not lines[last_non_empty].rstrip().endswith(","):
        lines[last_non_empty] = lines[last_non_empty].rstrip() + ","

    new_lines = []
    items = list(additions.items())
    for i, (key, value) in enumerate(items):
        suffix = "," if i < len(items) - 1 else ""
        escaped = json.dumps(value, ensure_ascii=False)
        new_lines.append(f'    "{key}": {escaped}{suffix}')

    if lines and lines[-1].strip() == "":
        lines = lines[:-1]
    lines.extend(new_lines)

    new_inner = "\n" + "\n".join(lines) + "\n  "
    return text[: start + 1] + new_inner + text[end:]


def main():
    if not ZH_JSON_PATH.exists():
        print(f"[error] zh-CN.json not found: {ZH_JSON_PATH}")
        return 1

    text = ZH_JSON_PATH.read_text(encoding="utf-8", errors="ignore")
    raw_bounds = find_raw_object_bounds(text)
    if not raw_bounds:
        print(f"[error] 'raw' object not found in {ZH_JSON_PATH}")
        return 1

    raw_inner = text[raw_bounds[0] + 1 : raw_bounds[1]]
    existing_keys = parse_existing_raw_keys(raw_inner)
    scanned_files, additions = collect_missing_entries(existing_keys)

    if not additions:
        print(f"[done] scanned={scanned_files}, added=0, zh_path={ZH_JSON_PATH}")
        return 0

    updated = append_entries_to_raw(text, raw_bounds, additions)
    tmp_path = ZH_JSON_PATH.with_suffix(".tmp.json")
    tmp_path.write_text(updated, encoding="utf-8")
    tmp_path.replace(ZH_JSON_PATH)

    print(f"[done] scanned={scanned_files}, added={len(additions)}, zh_path={ZH_JSON_PATH}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
