#!/usr/bin/env python3
import argparse
import json
import re
from pathlib import Path

PATTERNS = [
    ("high", "reflection-emit", re.compile(r"\bSystem\.Reflection\.Emit\b|\bDynamicMethod\b")),
    ("high", "dynamic-assembly-load", re.compile(r"\bAssembly\.Load(?:From|File|\s*\()")),
    ("medium", "activator-type", re.compile(r"\bActivator\.CreateInstance\s*\(")),
    ("medium", "expression-compile", re.compile(r"\.Compile\s*\(\s*\)")),
    ("medium", "type-by-name", re.compile(r"\bType\.GetType\s*\(")),
    ("medium", "dllimport", re.compile(r"\[\s*DllImport\s*\(")),
    ("review", "jsonserializer", re.compile(r"\bJsonSerializer\.(?:Serialize|Deserialize)\s*[<(]")),
    ("review", "dynamic-keyword", re.compile(r"\bdynamic\b")),
]


def main():
    ap = argparse.ArgumentParser(description="Heuristic source scan for Native AOT risk patterns.")
    ap.add_argument("repo")
    ap.add_argument("--output")
    args = ap.parse_args()
    root = Path(args.repo).resolve()
    findings = []
    for path in sorted(root.rglob("*.cs")):
        parts = {p.lower() for p in path.parts}
        if "obj" in parts or "bin" in parts:
            continue
        try:
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        except OSError:
            continue
        for idx, line in enumerate(lines, 1):
            for severity, rule, rx in PATTERNS:
                if rx.search(line):
                    findings.append({
                        "severity": severity,
                        "rule": rule,
                        "file": str(path.relative_to(root)),
                        "line": idx,
                        "text": line.strip()[:240],
                    })
    result = {
        "schema": "dot-net-native-aot/source-audit-v1",
        "repo": str(root),
        "finding_count": len(findings),
        "findings": findings,
        "note": "Heuristic only. Review findings; analyzer/native-publish evidence outranks pattern matching.",
    }
    text = json.dumps(result, indent=2, sort_keys=True) + "\n"
    if args.output:
        Path(args.output).write_text(text, encoding="utf-8")
    else:
        print(text, end="")


if __name__ == "__main__":
    main()
