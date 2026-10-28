"""Extract class-permit inputs for previously matched 5.8 equipment.

The result is source data, not proof that an item is equipped or usable.
Generated item data must remain outside the repository.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

import armor_enchant as armor
from extract_curse_armor import CLASS_PERMITS, source_text

REPO = Path(__file__).resolve().parents[2]
BONUS_SHA = 'e5afc36026295acd35d91e6d57e139a915b73c2b827882dcba057f4cbb8ba6a6'
SOURCE_SHAS = {
    'item_armors.xml': '11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a',
    'item_weapons.xml': 'f10df47b68c161ba1d76aa1f4bd2853634a57e114dc61c50f63e89d2e657344f',
    'item_accessories.xml': '7ce2457a34b8082826146804a1af8223e9e8f36d9e4c1a41603baea56f2d1470',
}


def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def permits(node):
    values, present = [], 0
    for index, name in enumerate(CLASS_PERMITS):
        text = source_text(node, name)
        if text is None:
            values.append(0)
            continue
        value = armor.integer(text, 'retail class permit')
        if not 0 <= value < 100:
            raise ValueError('Unreviewed retail class permit')
        values.append(value)
        present |= 1 << index
    return values, present


def selected_file(row):
    kind, group = row['kind'], row['host_group']
    if kind == 'armor':
        return 'item_armors.xml'
    if kind == 'weapon':
        return 'item_weapons.xml'
    if kind == 'other' and group in ('SHIELD', 'HEAD'):
        return 'item_armors.xml'
    if kind == 'other' and group in ('EARRING', 'NECKLACE', 'RING', 'BELT'):
        return 'item_accessories.xml'
    raise ValueError('Unreviewed item family')


def extract(retail_root, bonus_source, output):
    bonus_source, output = Path(bonus_source), Path(output)
    if output.resolve().is_relative_to(REPO) or output.exists():
        raise ValueError('Output must be a new file outside the repository')
    if digest(bonus_source) != BONUS_SHA:
        raise ValueError('Matched bonus source identity changed')
    source = json.loads(bonus_source.read_bytes())
    if source.get('version') != 1 or source.get('purpose') != 'retail-item-resource-bonus-source-v1':
        raise ValueError('Unsupported bonus source')
    wanted = {}
    for row in source['items']:
        ident, name, group = row['id'], row['name'], row['host_group']
        if not isinstance(ident, int) or ident <= 0 or ident in wanted or not isinstance(name, str) or not name or not name.isascii():
            raise ValueError('Invalid matched item identity')
        wanted[ident] = (name, group, row['kind'], selected_file(row))
    if len(wanted) != 54153:
        raise ValueError('Matched item coverage changed')
    paths, selections = {}, {}
    for name, expected in SOURCE_SHAS.items():
        path, selection = armor.selected_region_file(retail_root, name)
        if digest(path) != expected:
            raise ValueError('Selected retail item source changed: ' + name)
        paths[name], selections[name] = path, selection
    found = {}
    for filename, path in paths.items():
        stack = []
        for event, node in ET.iterparse(path, events=('start', 'end')):
            if event == 'start':
                stack.append(node)
                continue
            if node.tag == 'item':
                raw = armor.unique_text(node, 'id')
                ident = armor.integer(raw, 'retail item ID') if raw is not None else None
                if ident in wanted and wanted[ident][3] == filename:
                    if ident in found:
                        raise ValueError('Duplicate selected retail item ID')
                    name, group, kind, _ = wanted[ident]
                    actual = armor.unique_text(node, 'name', required=True)
                    if not actual or not actual.isascii() or actual.casefold() != name.casefold():
                        raise ValueError('Retail item identity changed')
                    levels, present = permits(node)
                    found[ident] = {'id': ident, 'name': name, 'host_group': group, 'kind': kind,
                                    'class_permits': levels, 'class_present_mask': present}
                if len(stack) > 1:
                    stack[-2].remove(node)
                node.clear()
            stack.pop()
    if set(found) != set(wanted):
        raise ValueError('Matched items missing from selected retail source')
    if digest(bonus_source) != BONUS_SHA or any(digest(paths[n]) != h for n, h in SOURCE_SHAS.items()):
        raise ValueError('A source changed during extraction')
    data = {'version': 1, 'purpose': 'retail-matched-item-class-permits-v1', 'region': 'USA',
            'bonus_source_sha256': BONUS_SHA, 'retail_source_sha256': SOURCE_SHAS,
            'selection': selections, 'items': [found[i] for i in sorted(found)]}
    with output.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(data, stream, ensure_ascii=True, separators=(',', ':'))
        stream.write('\n')
    return {'items': len(found), 'sha256': digest(output)}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--retail-root', type=Path, required=True)
    p.add_argument('--bonus-source', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    print(json.dumps(extract(args.retail_root, args.bonus_source, args.output)))


if __name__ == '__main__':
    main()
