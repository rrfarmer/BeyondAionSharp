"""Inventory 4.8 shield inputs alongside selected 5.8 source records.

This is a read-only identity and operand comparison, not a combat simulator or
authorization to use a shield. Missing fields and duplicate IDs stay visible.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
import armor_enchant

REPO = Path(__file__).resolve().parents[2]


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def host_shields(path: Path) -> dict[int, list[dict]]:
    result: dict[int, list[dict]] = {}
    for _, element in ET.iterparse(path, events=('end',)):
        if element.tag != 'skill_template':
            continue
        shields = element.findall('./effects/shield')
        skill_id = int(element.attrib['skill_id'])
        result.setdefault(skill_id, []).append({
            'name': element.get('name'), 'group': element.get('group'),
            'stack': element.get('stack'),
            'shields': [dict(item.attrib) for item in shields],
        })
        element.clear()
    return {skill_id: records for skill_id, records in result.items()
            if any(record['shields'] for record in records)}


def retail_records(data: bytes, wanted: set[int]) -> dict[int, list[dict]]:
    result: dict[int, list[dict]] = {}
    text = data.decode('utf-16')
    for match in re.finditer(r'<skill_base>.*?</skill_base>', text, re.DOTALL):
        raw = match.group()
        identity = re.search(r'<id>\s*(\d+)\s*</id>', raw)
        if identity is None or int(identity.group(1)) not in wanted:
            continue
        skill_id = int(identity.group(1))
        try:
            node = ET.fromstring(raw)
        except ET.ParseError as error:
            result.setdefault(skill_id, []).append({'parse_error': str(error),
                'raw_sha256': digest(raw.encode('utf-16le'))})
            continue
        fields: dict[str, list[str]] = {}
        non_scalar_fields = []
        for child in node:
            if child.tag in ('id', 'name') or re.fullmatch(r'effect\d+_.+', child.tag):
                if len(child) or child.attrib:
                    non_scalar_fields.append({'tag': child.tag, 'xml': ET.tostring(child, encoding='unicode')})
                else:
                    fields.setdefault(child.tag, []).append(child.text or '')
        result.setdefault(skill_id, []).append({'raw_sha256': digest(raw.encode('utf-16le')),
                                                 'fields': fields, 'non_scalar_fields': non_scalar_fields})
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--retail-xml-root', type=Path, required=True,
                        help='5.8 Map/XML directory; selects USA override or base fallback')
    parser.add_argument('--host-skills', type=Path,
                        default=REPO / 'game-server/data/static_data/skills/skill_templates.xml')
    parser.add_argument('--out', type=Path, required=True,
                        help='new JSON file outside this checkout; never overwrites')
    args = parser.parse_args()
    output = args.out.resolve()
    if output.is_relative_to(REPO.resolve()):
        parser.error('Output must be outside the checkout')
    retail, selection = armor_enchant.selected_region_file(args.retail_xml_root, 'skill_base.xml')
    host_hash = digest(args.host_skills.read_bytes())
    host = host_shields(args.host_skills)
    if digest(args.host_skills.read_bytes()) != host_hash:
        raise RuntimeError('Host skills changed during comparison')
    retail_bytes = retail.read_bytes()
    records = retail_records(retail_bytes, set(host))
    rows = []
    for skill_id, versions in sorted(host.items()):
        selected = records.get(skill_id, [])
        rows.append({'skill_id': skill_id, 'host_records': versions,
                     'retail_records': selected,
                     'identity_status': ('missing-retail-id' if not selected else
                         'duplicate-id' if len(selected) != 1 or len(versions) != 1 else
                         'parse-error' if 'parse_error' in selected[0] else
                         'id-only-unverified-name-and-runtime-context')})
    payload = {
        'purpose': 'raw-input-inventory-not-retail-combat-admission',
        'retail_selection': selection, 'retail_file': str(retail.resolve()),
        'retail_sha256': digest(retail_bytes),
        'host_file': str(args.host_skills.resolve()), 'host_sha256': host_hash,
        'limits': ['ID is not proof of semantic identity or player access',
                   'missing fields are not defaulted', 'levels, shield state, timing, protection ordering and healing are not calculated'],
        'host_skill_count': len(host), 'rows': rows,
    }
    with output.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(payload, stream, indent=2, ensure_ascii=False)
        stream.write('\n')
    print(json.dumps({'host_skills': len(host), 'retail_id_matches': sum(bool(records.get(i)) for i in host),
                      'output': str(output)}, ensure_ascii=False))
    return 0


if __name__ == '__main__':
    sys.exit(main())
