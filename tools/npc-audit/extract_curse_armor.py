"""Materialize reviewed 5.8 default-armor HP inputs outside the repository.

This is source data, not a claim about a player's equipment or item eligibility.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

import armor_enchant as armor

REPO = Path(__file__).resolve().parents[2]
CLASS_PERMITS = ('warrior', 'fighter', 'knight', 'scout', 'assassin', 'ranger', 'mage', 'wizard',
                 'elementalist', 'cleric', 'priest', 'chanter', 'engineer', 'rider', 'gunner', 'artist', 'bard')


def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def source_text(node, tag):
    matches = [x for x in node if isinstance(x.tag, str) and x.tag.casefold() == tag]
    if len(matches) > 1:
        raise ValueError('Duplicate retail ' + tag)
    if not matches:
        return None
    if matches[0].text is None or len(matches[0]) or matches[0].attrib:
        raise ValueError('Invalid retail ' + tag)
    return matches[0].text


def extract(bindings_path, binding_sha, retail_root, enchant_sha, host_items, output):
    bindings_path, host_items, output = map(Path, (bindings_path, host_items, output))
    if output.resolve().is_relative_to(REPO) or output.exists():
        raise ValueError('Output must be a new file outside the repository')
    if digest(bindings_path) != binding_sha:
        raise ValueError('Reviewed binding identity changed')
    binding = json.loads(bindings_path.read_bytes())
    if binding.get('result') != 'STATIC_DEFAULT_BINDING_COMPARISON':
        raise ValueError('Unsupported binding review')
    retail_items, selection = armor.selected_region_file(retail_root, 'item_armors.xml')
    retail_tables, table_selection = armor.selected_region_file(retail_root, 'item_enchanttable.xml')
    sources = (bindings_path, host_items, retail_items, retail_tables)
    before = {str(p.resolve()): digest(p) for p in sources}
    if binding.get('retail_sha256') != before[str(retail_items.resolve())] or binding.get('host_sha256') != before[str(host_items.resolve())]:
        raise ValueError('Selected item sources differ from reviewed binding')
    if before[str(retail_tables.resolve())] != enchant_sha:
        raise ValueError('Selected retail enchant source identity changed')
    wanted = {}
    for row in binding['items']:
        item_id = armor.integer(str(row['id']), 'item ID')
        name, group, host_max = row['name'], row['group'], row['host_max_enchant']
        if item_id in wanted or not isinstance(name, str) or not name or not name.isascii() or not isinstance(group, str):
            raise ValueError('Invalid or duplicate reviewed item')
        table = armor.default_retail_table(item_id)
        expected = group.replace('_glove', '_gloves') if group.endswith('_glove') else group
        if expected != table or table == 'shield':
            raise ValueError('Reviewed default table changed')
        if not isinstance(host_max, int) or not 0 <= host_max <= 15:
            raise ValueError('Unreviewed host item enchant limit')
        wanted[item_id] = (name, table, host_max)
    if not wanted:
        raise ValueError('Empty reviewed armor set')

    root = ET.parse(retail_tables).getroot()
    tables = []
    for name in sorted({v[1] for v in wanted.values()}):
        table = armor.unique_by(root, 'item_enchanttable', lambda x: x.findtext('name') == name)
        armor.unique_text(table, 'name', required=True)
        threshold = armor.integer(armor.unique_text(table, 'start_limitless', required=True), 'threshold')
        if not 15 < threshold <= 255:
            raise ValueError('Unreviewed default table threshold')
        tables.append({'name': name, 'threshold': threshold,
                       'hp_levels_1_to_15': [armor.retail_level(table, level, 'max_hp') for level in range(1, 16)]})

    found = {}
    stack = []
    for event, node in ET.iterparse(retail_items, events=('start', 'end')):
        if event == 'start':
            stack.append(node)
            continue
        if node.tag == 'item':
            raw_id = armor.unique_text(node, 'id')
            item_id = armor.integer(raw_id, 'retail item ID') if raw_id is not None else None
            if item_id in wanted:
                if item_id in found:
                    raise ValueError('Duplicate selected retail item ID')
                name, table, host_max = wanted[item_id]
                retail_name = armor.unique_text(node, 'name', required=True)
                if retail_name is None or not retail_name.isascii() or retail_name.casefold() != name.casefold():
                    raise ValueError('Selected item name changed')
                if any(isinstance(x.tag, str) and x.tag.casefold() == 'enchant_name' for x in node):
                    raise ValueError('Explicit enchant override requires separate review')
                hp_text = source_text(node, 'max_hp')
                cap_text = source_text(node, 'max_enchant_value')
                hp = 0 if hp_text is None else armor.integer(hp_text, 'retail armor max HP')
                cap = None if cap_text is None else armor.integer(cap_text, 'retail item enchant limit')
                if cap is not None and not 0 <= cap <= 255:
                    raise ValueError('Unreviewed retail item enchant limit')
                permits, present = [], 0
                for index, tag in enumerate(CLASS_PERMITS):
                    matches = [x for x in node if isinstance(x.tag, str) and x.tag.casefold() == tag]
                    if len(matches) > 1:
                        raise ValueError('Duplicate retail class permit')
                    if not matches:
                        permits.append(0)
                        continue
                    value = matches[0]
                    if value.text is None or len(value) or value.attrib:
                        raise ValueError('Invalid retail class permit')
                    level = armor.integer(value.text, 'retail class permit')
                    if not 0 <= level < 100:
                        raise ValueError('Retail class permit is outside the reviewed native range')
                    permits.append(level)
                    present |= 1 << index
                found[item_id] = {'id': item_id, 'name': name, 'table': table,
                                  'raw_max_hp': hp, 'max_hp_explicit': hp_text is not None,
                                  'host_max_enchant': host_max, 'retail_max_enchant': cap,
                                  'class_permits': permits, 'class_present_mask': present}
            if len(stack) > 1:
                stack[-2].remove(node)
            node.clear()
        stack.pop()
    if set(found) != set(wanted):
        raise ValueError('Reviewed items missing from selected retail source')
    if any(digest(p) != before[str(p.resolve())] for p in sources):
        raise ValueError('A source changed during extraction')
    result = {'version': 2, 'purpose': 'default-armor-hp-and-class-v2', 'region': 'USA',
              'provenance': {'bindings_sha256': binding_sha, 'retail_items_sha256': before[str(retail_items.resolve())],
                             'retail_enchant_sha256': before[str(retail_tables.resolve())],
                             'host_items_sha256': before[str(host_items.resolve())],
                             'retail_items_selection': selection, 'retail_enchant_selection': table_selection},
              'tables': tables, 'items': [found[key] for key in sorted(found)]}
    with output.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(result, stream, ensure_ascii=False, separators=(',', ':'))
        stream.write('\n')
    return {'items': len(found), 'tables': len(tables), 'sha256': digest(output)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bindings', type=Path, required=True)
    parser.add_argument('--binding-sha256', required=True)
    parser.add_argument('--retail-root', type=Path, required=True)
    parser.add_argument('--retail-enchant-sha256', required=True)
    parser.add_argument('--host-items', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(extract(args.bindings, args.binding_sha256, args.retail_root,
                             args.retail_enchant_sha256, args.host_items, args.output)))


if __name__ == '__main__':
    main()
