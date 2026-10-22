#!/usr/bin/env python3
"""Fails if en-US.json and zh-CN.json do not contain the same non-empty keys."""
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOCALIZATION = os.path.join(ROOT, "src", "VisualDiffLab.App", "Localization")

def load(name):
    with open(os.path.join(LOCALIZATION, f"{name}.json"), encoding="utf-8") as handle:
        data = json.load(handle)
    empty = [key for key, value in data.items() if not isinstance(value, str) or not value.strip()]
    if empty:
        print(f"{name}: empty values: {empty}", file=sys.stderr)
        sys.exit(1)
    return data

en = load("en-US")
zh = load("zh-CN")
missing_zh = sorted(set(en) - set(zh))
missing_en = sorted(set(zh) - set(en))
if missing_zh or missing_en:
    print(f"Localization key mismatch. Missing in zh-CN: {missing_zh}; missing in en-US: {missing_en}", file=sys.stderr)
    sys.exit(1)

print(f"Localization OK: {len(en)} keys in en-US and zh-CN")
