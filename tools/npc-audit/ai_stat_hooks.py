"""Identify direct host AI stat hooks without claiming a runtime result.

The scan is deliberately bounded: inherited implementations, dynamic spawn AI
overrides and conditions inside a hook remain unresolved. It never executes AI.
"""
from __future__ import annotations

import hashlib
from pathlib import Path
import re


def _mask_csharp(text: str) -> str:
    """Mask comments and quoted literals while preserving character offsets."""
    out = list(text)
    i = 0
    while i < len(text):
        start = i
        if text.startswith('//', i):
            i = text.find('\n', i)
            if i < 0:
                i = len(text)
        elif text.startswith('/*', i):
            end = text.find('*/', i + 2)
            i = len(text) if end < 0 else end + 2
        elif text[i] in ('"', "'"):
            quote = text[i]
            verbatim = quote == '"' and i > 0 and text[i - 1] == '@'
            i += 1
            while i < len(text):
                if verbatim and text.startswith('""', i):
                    i += 2
                elif not verbatim and text[i] == '\\':
                    i = min(len(text), i + 2)
                elif text[i] == quote:
                    i += 1
                    break
                else:
                    i += 1
        else:
            i += 1
            continue
        for j in range(start, i):
            if out[j] not in '\r\n':
                out[j] = ' '
    return ''.join(out)


def _closing_brace(text: str, opening: int) -> int:
    depth = 0
    for i in range(opening, len(text)):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return i
    raise ValueError('Unbalanced C# braces in AI source')


def scan(repo: Path) -> dict[str, list[dict]]:
    """Return directly declared hooks indexed by AIName, including source pins."""
    roots = [repo / 'src/Aion.GameServer/Ai', repo / 'src/Aion.GameServer/Handlers/AI']
    paths = sorted({p for root in roots if root.is_dir() for p in root.rglob('*.cs')})
    found: dict[str, list[dict]] = {}
    attr = re.compile(r'\[\s*(?:[\w.]+\.)?AIName\s*\(\s*"([^"\r\n]+)"\s*\)\s*\]')
    klass = re.compile(r'\bclass\s+(\w+)\b[^;{}]*\{')
    method = re.compile(r'\boverride\s+void\s+ModifyOwnerStat\s*\([^)]*\)\s*\{')
    for path in paths:
        raw = path.read_bytes()
        text = raw.decode('utf-8-sig')
        masked = _mask_csharp(text)
        digest = hashlib.sha256(raw).hexdigest()
        for match in attr.finditer(text):
            if masked[match.start()] != '[':  # attribute-like text in a comment/literal
                continue
            declaration = klass.search(masked, match.end())
            if declaration is None:
                continue
            next_attr = attr.search(text, match.end(), declaration.start())
            if next_attr is not None:
                continue
            opening = declaration.end() - 1
            closing = _closing_brace(masked, opening)
            candidates = []
            for m in method.finditer(masked, opening + 1, closing):
                prefix = masked[opening + 1:m.start()]
                if prefix.count('{') != prefix.count('}'):
                    continue  # nested type or local method, not the AI's own method
                body_end = _closing_brace(masked, m.end() - 1)
                body = masked[m.end() - 1:body_end + 1]
                stats = sorted(set(re.findall(r'\bStatEnum\.([A-Z_][A-Z_0-9]*)\b', body)))
                candidates.append({'class': declaration.group(1), 'file': path.relative_to(repo).as_posix(),
                                   'line': text.count('\n', 0, m.start()) + 1, 'sha256': digest,
                                   'referenced_stats': stats})
            if candidates:
                found.setdefault(match.group(1).lower(), []).extend(candidates)
        if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError('AI source changed during scan: ' + str(path))
    return found


def describe(ai_name: str | None, hooks: dict[str, list[dict]]) -> dict:
    matches = hooks.get((ai_name or '').lower(), [])
    return {'status': 'direct_override_found' if matches else 'no_direct_override_found',
            'handlers': matches,
            'scope': 'Static template AI only; runtime conditions, inheritance and spawn AI overrides are not established.'}
