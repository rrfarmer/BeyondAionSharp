"""Extract bounded 5.8 item-authorization HP inputs for offline curse review.

The ordinary SpecialSvrType=0 context is explicit and hash-pinned. Unknown
tables and missing levels stay visible. This
does not produce complete player HP or enable an NPC curse in gameplay.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import armor_enchant as armor

REPO = Path(__file__).resolve().parents[2]
DEFAULTS = {'SHIELD':'shield', 'HEAD':'head', 'EARRING':'earring',
            'NECKLACE':'necklace', 'RING':'ring', 'BELT':'belt'}
REVIEWED_CONFIG_SHA256 = ('61bcdf692f72d3599e56814f03cc69f43e9a37c864e60309da8c7616e8c8fcdb',
                          '52c7ecf005355d98acd7988a4ef0ef7948d163ed4bdc7618b0772af3cbd78fda')

def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(1024*1024), b''): h.update(block)
    return h.hexdigest()

def table_values(row):
    threshold = armor.integer(armor.unique_text(row, 'start_limitless', True), 'authorization threshold')
    if not 1 <= threshold <= 256: raise ValueError('Unreviewed authorization threshold')
    lists = row.findall('enchant_attr_list')
    if len(lists) != 1: raise ValueError('Missing or duplicate authorization levels')
    levels = {}
    for entry in lists[0].findall('data'):
        level = armor.integer(armor.unique_text(entry, 'level', True), 'authorization level')
        if not 1 <= level <= 255 or level in levels: raise ValueError('Invalid or duplicate authorization level')
        attrs = {}
        for node in entry:
            if not node.tag.startswith('attr'): continue
            suffix = node.tag[4:]
            if not suffix.isascii() or not suffix.isdigit() or not 1 <= int(suffix) <= 10: continue
            if node.tag in attrs or node.attrib or len(node): raise ValueError('Ambiguous authorization attribute')
            attrs[node.tag] = node.text or ''
        hp = None
        for value in attrs.values():
            parts = value.split()
            if parts and parts[0].casefold() == 'max_hp':
                if len(parts) != 2 or hp is not None: raise ValueError('Ambiguous authorization HP')
                hp = armor.integer(parts[1], 'authorization HP')
        # The retail parser builds a set map; omitted Max_Hp is distinct from explicit zero.
        levels[level] = {'hp': hp if hp is not None else 0, 'explicit': hp is not None}
    limitless = row.findall('limitless_attr_list')
    if len(limitless) != 1: raise ValueError('Missing or duplicate limitless attributes')
    indices = set(); hp_increment = None; skipped_without_index = 0
    for entry in limitless[0].findall('data'):
        raw_index = armor.unique_text(entry, 'index')
        # Native loader ignores a row without an index, including any attr_name in it.
        if raw_index is None:
            skipped_without_index += 1
            continue
        index = armor.integer(raw_index, 'limitless attribute index')
        name = armor.unique_text(entry, 'attr_name', True)
        value = armor.integer(armor.unique_text(entry, 'attr_value', True), 'limitless attribute value')
        if index in indices or not name or not name.isascii(): raise ValueError('Ambiguous limitless attribute')
        indices.add(index)
        if name.casefold() == 'max_hp':
            if hp_increment is not None: raise ValueError('Duplicate limitless HP attribute')
            if any(entry.find(tag) is not None for tag in ('random_enchant','random_authorize','random_range','random_prob')):
                raise ValueError('Unreviewed random limitless HP')
            hp_increment = value
    return {'threshold':threshold, 'levels':{str(k):v for k,v in sorted(levels.items()) if k < threshold},
            'limitless_hp_per_level':hp_increment if hp_increment is not None else 0,
            'limitless_hp_explicit':hp_increment is not None,
            'limitless_rows_without_index':skipped_without_index}

def hp_at_level(table, level):
    """Return native int32 HP contribution, or None for a missing base level."""
    if not 0 <= level <= 255: raise ValueError('Unreviewed authorization level')
    if level == 0: return 0
    threshold = table['threshold']
    base_level = level if level < threshold else threshold - 1
    if base_level == 0: base = 0
    else:
        row = table['levels'].get(str(base_level))
        if row is None: return None
        base = row['hp']
    if level >= threshold: base += (level - threshold + 1) * table['limitless_hp_per_level']
    return ((base + 2**31) % 2**32) - 2**31

def extract(retail_root, reviewed_path, configs, expected_hashes, output):
    root, reviewed_path, output = Path(retail_root), Path(reviewed_path), Path(output)
    if output.resolve().is_relative_to(REPO) or output.exists(): raise ValueError('Output must be new and outside the repository')
    if len(configs) != 2: raise ValueError('Two selected config files are required')
    arm, arm_selection = armor.selected_region_file(root, 'item_armors.xml')
    acc, acc_selection = armor.selected_region_file(root, 'item_accessories.xml')
    table = root / 'item_authorizetable.xml' # native ordinary SpecialSvrType 0
    inputs = [reviewed_path, arm, acc, table, *map(Path, configs)]
    before = {str(p.resolve()):digest(p) for p in inputs}
    if len(expected_hashes) != len(inputs) or [before[str(p.resolve())] for p in inputs] != list(expected_hashes):
        raise ValueError('Selected authorization source identity changed')
    for config in configs:
        # Current reviewed native loader uses the absent key's zero PE default.
        config_root = ET.parse(config).getroot()
        if any(isinstance(n.tag,str) and n.tag.casefold() == 'specialsvrtype' for n in config_root.iter()):
            raise ValueError('Unreviewed special-server configuration')
    reviewed = json.loads(reviewed_path.read_text(encoding='utf-8'))
    if reviewed.get('purpose') != 'retail-other-equipment-hp-base-v1': raise ValueError('Unreviewed equipment identity source')
    p = reviewed['provenance']
    if p['retail_armors_sha256'] != digest(arm) or p['retail_accessories_sha256'] != digest(acc):
        raise ValueError('Equipment identities and selected items differ')
    identities = {}
    for row in reviewed['items']:
        ident, group = row['id'], row['host_group']
        if group not in DEFAULTS or ident in identities: raise ValueError('Invalid equipment identity')
        identities[ident] = row
    found = {}
    for path in (arm, acc):
        stack=[]
        for event, node in ET.iterparse(path, events=('start','end')):
            if event == 'start': stack.append(node); continue
            if node.tag == 'item':
                ident = armor.integer(armor.unique_text(node, 'id', True), 'retail item ID')
                if ident in identities:
                    expected = identities[ident]
                    name = armor.unique_text(node, 'name', True)
                    if ident in found or not name or name.casefold() != expected['name'].casefold():
                        raise ValueError('Duplicate or mismatched retail equipment identity')
                    nodes = node.findall('authorize_name')
                    if len(nodes) > 1 or any(n.attrib or len(n) or not n.text for n in nodes):
                        raise ValueError('Unreviewed authorization table override')
                    found[ident] = {'id':ident, 'name':name, 'host_group':expected['host_group'],
                                    'table':nodes[0].text if nodes else DEFAULTS[expected['host_group']],
                                    'explicit_table':bool(nodes)}
                if len(stack)>1: stack[-2].remove(node)
                node.clear()
            stack.pop()
    if len(found) != len(identities): raise ValueError('Missing reviewed retail equipment identities')
    wanted = {x['table'].casefold() for x in found.values()}
    tables = {}
    for row in ET.parse(table).getroot().iter('item_authorizetable'):
        name = armor.unique_text(row, 'name', True)
        if not name or name.casefold() not in wanted: continue
        key = name.casefold()
        if key in tables: raise ValueError('Duplicate authorization table name')
        tables[key] = table_values(row)
    missing = sorted(wanted - tables.keys())
    if before != {str(p.resolve()):digest(p) for p in inputs}: raise ValueError('Authorization source changed during extraction')
    result = {'version':2, 'purpose':'retail-authorization-hp-bounded-v2', 'special_server_type':0,
              'provenance':{'input_sha256':before, 'armor_selection':arm_selection,
                            'accessory_selection':acc_selection, 'table_selection':'ordinary base filename'},
              'tables':dict(sorted(tables.items())), 'missing_tables':missing,
              'items':[found[x] for x in sorted(found)]}
    with output.open('x', encoding='utf-8', newline='\n') as f:
        json.dump(result,f,ensure_ascii=False,separators=(',',':')); f.write('\n')
    return {'items':len(found), 'tables':len(tables), 'missing_tables':missing, 'sha256':digest(output)}

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--retail-root',required=True); p.add_argument('--reviewed-input',required=True)
    p.add_argument('--config',action='append',required=True); p.add_argument('--sha256',action='append',required=True)
    p.add_argument('--output',required=True); a=p.parse_args()
    if len(a.config)!=2: p.error('Two pinned selected config files are required')
    if tuple(a.sha256[-2:]) != REVIEWED_CONFIG_SHA256:
        p.error('Selected special-server configuration does not match the reviewed type-0 context')
    print(json.dumps(extract(a.retail_root,a.reviewed_input,a.config,a.sha256,a.output)))

if __name__=='__main__': main()
