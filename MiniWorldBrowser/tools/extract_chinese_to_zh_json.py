import os
import re
import json
from pathlib import Path

# Root of the repo (pass via argv or use current working dir)
try:
    import sys
    ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[1]
except Exception:
    ROOT = Path(__file__).resolve().parents[1]

ZH_JSON_PATH = ROOT / "Resources" / "i18n" / "zh-CN.json"

# File extensions to scan
EXTS = {".cs", ".js", ".ts", ".tsx", ".html", ".htm", ".xml", ".json"}

# Regex: capture quoted string content (basic), then check if contains CJK
QUOTE_RE = re.compile(r'"([^"\\]*(?:\\.[^"\\]*)*)"|\'([^\'\\]*(?:\\.[^\'\\]*)*)\'')
HAS_CJK_RE = re.compile(r'[\u3400-\u4dbf\u4e00-\u9fff\uF900-\uFAFF]')  # basic CJK ranges

def find_strings_with_chinese(path: Path):
    items = []
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return items
    lines = text.splitlines()
    for i, line in enumerate(lines, start=1):
        for m in QUOTE_RE.finditer(line):
            val = m.group(1) if m.group(1) is not None else m.group(2)
            if not val:
                continue
            if HAS_CJK_RE.search(val):
                # normalize whitespace
                norm = val.strip()
                if norm:
                    items.append((i, norm))
    return items

def sanitize_key_component(s: str) -> str:
    # Replace path separators and disallowed chars with dots
    s = s.replace("\\", "/")
    s = re.sub(r'[^A-Za-z0-9._/-]+', '_', s)
    s = s.replace("/", ".")
    return s

def main():
    if not ZH_JSON_PATH.exists():
        print(f"[error] zh-CN.json not found: {ZH_JSON_PATH}")
        return 1
    try:
        with ZH_JSON_PATH.open("r", encoding="utf-8") as f:
            zh = json.load(f)
    except Exception as e:
        print(f"[error] failed to load zh-CN.json: {e}")
        return 1

    raw = zh.get("raw")
    if not isinstance(raw, dict):
        raw = {}
        zh["raw"] = raw

    added = 0
    scanned_files = 0
    for dirpath, _, filenames in os.walk(ROOT):
        for fn in filenames:
            p = Path(dirpath) / fn
            if p.suffix.lower() not in EXTS:
                continue
            # skip generated or resource binaries
            if "bin" in p.parts or "obj" in p.parts:
                continue
            scanned_files += 1
            rel = p.relative_to(ROOT)
            key_base = sanitize_key_component(str(rel))
            for lineno, text in find_strings_with_chinese(p):
                key = f"{key_base}.L{lineno}"
                # Do not overwrite existing keys
                if key not in raw:
                    raw[key] = text
                    added += 1

    # Write back
    tmp_path = ZH_JSON_PATH.with_suffix(".tmp.json")
    with tmp_path.open("w", encoding="utf-8") as f:
        json.dump(zh, f, ensure_ascii=False, indent=2)
        f.write("\n")
    tmp_path.replace(ZH_JSON_PATH)

    print(f"[done] scanned={scanned_files}, added={added}, zh_path={ZH_JSON_PATH}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
