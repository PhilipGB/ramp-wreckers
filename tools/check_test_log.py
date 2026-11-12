#!/usr/bin/env python3
"""Fail when a Godot runtime test log contains unexpected managed/unmanaged errors."""
import sys
from pathlib import Path

BAD = (
    "Handle is not initialized",
    "Leaked unsafe reference",
    "System.InvalidOperationException",
    "Unhandled test exception",
    "ERROR: System.",
)

def main() -> int:
    if len(sys.argv) != 2:
        print("usage: check_test_log.py <log>")
        return 2
    path = Path(sys.argv[1])
    if not path.is_file():
        print(f"FAIL: missing test log {path}")
        return 1
    text = path.read_text(encoding="utf-8", errors="replace")
    hits = [line for line in text.splitlines() if any(marker in line for marker in BAD)]
    if hits:
        print(f"FAIL: unexpected runtime errors in {path}:")
        for line in hits[:20]:
            print("  " + line)
        return 1
    print(f"OK: no unexpected managed/unmanaged exceptions in {path}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
