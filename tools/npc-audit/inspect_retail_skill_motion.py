"""Read-only source reconstruction of one NPC skill's 5.8 animation markers.

This reports source candidates, not observed retail runtime or a gameplay gate.
"""
import argparse
import hashlib
import io
import json
import math
from pathlib import Path
import re
import struct
import xml.etree.ElementTree as ET


def digest(data):
    return hashlib.sha256(data).hexdigest()


def checked(path, expected):
    data = Path(path).read_bytes()
    if digest(data) != expected:
        raise ValueError(f"Source hash differs: {path}")
    return data


def read_span(record):
    with open(record['path'], 'rb') as source:
        source.seek(record['byte_start'])
        data = source.read(record['byte_end_exclusive'] - record['byte_start'])
    if digest(data) != record['raw_sha256']:
        raise ValueError('NPC source span hash differs')
    return data


def single(root, name):
    rows = root.findall(name)
    if len(rows) > 1:
        raise ValueError(f'Duplicate {name}')
    return rows[0].text if rows else None


def marker_word(value):
    number = float(value)
    if not math.isfinite(number) or not 0 <= number <= 60:
        raise ValueError('Marker time outside reviewed range')
    number = struct.unpack('<f', struct.pack('<f', number))[0]
    number = struct.unpack('<f', struct.pack('<f', number * 1000))[0]
    return int(number) & 0xffff


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--manifest', required=True, type=Path)
    p.add_argument('--manifest-sha256', required=True)
    p.add_argument('--map', required=True, type=int)
    p.add_argument('--source-map', type=int,
                   help='Explicit source map when a reviewed cohort reuses its identical NPC profile')
    p.add_argument('--npc', required=True, type=int)
    p.add_argument('--skill', required=True, type=int)
    p.add_argument('--xml-root', required=True, type=Path)
    p.add_argument('--motion-evidence', required=True, type=Path,
                   help='Previously verified cohorts combat/motion-readiness.json')
    p.add_argument('--motion-evidence-sha256', required=True)
    args = p.parse_args()
    manifest = json.loads(checked(args.manifest, args.manifest_sha256))
    evidence = json.loads(checked(args.motion_evidence, args.motion_evidence_sha256))
    if manifest['region'] != evidence['source_context']['npc_region']:
        raise ValueError('Retail region differs')
    binary = [x for x in evidence['input_pins'] if Path(x['path']).name.lower() == 'npcsvr64_recover.exe']
    if len(binary) != 1:
        raise ValueError('Missing native NPC binary identity')
    checked(binary[0]['path'], binary[0]['sha256'])
    cohorts = [c for c in manifest['cohorts'] if c['map_id'] == args.map
               and any(m['npc_id'] == args.npc for m in c['members'])]
    if len(cohorts) != 1:
        raise ValueError('Expected exactly one selected map/NPC cohort')
    artifact = cohorts[0]['profile_artifact']
    profile_artifact = json.loads(checked(artifact['path'], artifact['sha256']))
    source_map = profile_artifact['map_id']
    if profile_artifact['region'] != manifest['region']:
        raise ValueError('Profile artifact map or region differs')
    if source_map != args.map and args.source_map != source_map:
        raise ValueError('Profile artifact map or region differs; explicit matching --source-map required')
    if source_map == args.map and args.source_map not in (None, source_map):
        raise ValueError('Explicit source map differs from profile artifact')
    rows = [row for row in profile_artifact['profiles'] if row['npc_id'] == args.npc]
    if len(rows) != 1:
        raise ValueError('Expected exactly one NPC profile')
    row = rows[0]
    members = [m for m in cohorts[0]['members'] if m['npc_id'] == args.npc]
    if len(members) != 1 or members[0]['source_record_sha256'] != row['source']['raw_sha256']:
        raise ValueError('Cohort NPC source identity differs')
    if source_map != args.map:
        origins = [c for c in manifest['cohorts'] if c['map_id'] == source_map
                   and any(m['npc_id'] == args.npc for m in c['members'])]
        if len(origins) != 1:
            raise ValueError('Explicit source-map NPC membership is missing or ambiguous')
        origin_asset = origins[0]['profile_artifact']
        origin = json.loads(checked(origin_asset['path'], origin_asset['sha256']))
        origin_rows = [candidate for candidate in origin['profiles'] if candidate['npc_id'] == args.npc]
        origin_members = [member for member in origins[0]['members'] if member['npc_id'] == args.npc]
        if (origin['map_id'] != source_map or origin['region'] != manifest['region']
                or origin['dataset_id'] != profile_artifact['dataset_id']
                or origin['output_manifest_sha256'] != profile_artifact['output_manifest_sha256']
                or len(origin_rows) != 1 or origin_rows[0] != row or len(origin_members) != 1
                or origin_members[0] != members[0]):
            raise ValueError('Explicit source-map profile does not match the selected NPC')
    npc = ET.fromstring(read_span(row['source']))
    bindings = [b for b in row['skill_bindings'] if b.get('supported48_id') == args.skill
                and b.get('status') == 'dual_join_agrees_static_only'
                and b.get('source_group_index') == 0]
    sources = [s for s in manifest['skill_sources'] if s['id'] == args.skill]
    if not bindings or len(sources) != 1 or int(single(npc, 'id')) != args.npc:
        raise ValueError('Unbound NPC skill or source identity')
    selected_skill = checked(sources[0]['path'], sources[0]['sha256'])
    origin = sources[0]['source']
    with open(origin['path'], 'rb') as stream:
        stream.seek(origin['byte_start'])
        original_skill = stream.read(origin['byte_end_exclusive'] - origin['byte_start'])
    if digest(original_skill) != sources[0]['sha256']:
        raise ValueError('Original skill record differs from selected source')
    skill = ET.fromstring(selected_skill)
    if int(single(skill, 'id')) != args.skill or any(b['name'] != single(skill, 'name') for b in bindings):
        raise ValueError('NPC and skill source disagree')
    entries = npc.findall('./skills/data')
    for binding in bindings:
        index = binding['source_entry_index']
        if not 0 <= index < len(entries) or single(entries[index], 'skill_name') != binding['name']:
            raise ValueError('NPC ordered skill binding differs')
        binding['selected_skill_level'] = single(entries[index], 'skill_level')
    equipment = npc.findall('visible_equipments')
    if len(equipment) > 1:
        raise ValueError('Duplicate equipment source')
    names = [single(equipment[0], slot) if equipment else None for slot in ('main', 'sub')]
    wanted = {x.casefold() for x in names if x}
    item_rows = {x: [] for x in wanted}
    item_sources = []
    if [x['relative'] for x in evidence['item_selection']] != [x['text'] for x in evidence['item_file_table']]:
        raise ValueError('Native item-file evidence differs')
    for selection in evidence['item_selection']:
        rel = selection['relative']
        if Path(rel).name != rel:
            raise ValueError('Invalid item source name')
        regional = args.xml_root / manifest['region'] / rel
        path = regional if regional.exists() else args.xml_root / rel
        raw = path.read_bytes()
        item_sources.append({'path': str(path), 'sha256': digest(raw)})
        for _, item in ET.iterparse(io.BytesIO(raw), events=('end',)):
            if item.tag == 'item':
                name = single(item, 'name')
                if name and name.casefold() in item_rows:
                    item_rows[name.casefold()].append({'name': name, 'id': int(single(item, 'id')),
                                                       'weapon_type': single(item, 'weapon_type')})
                item.clear()
    types = {x['text'].casefold(): x['index'] for x in evidence['weapon_type_table']}
    types['allwp'] = 18
    categories, items = [], []
    for name in names:
        if not name:
            categories.append(0)
            items.append(None)
            continue
        found = item_rows[name.casefold()]
        if len(found) != 1:
            raise ValueError(f'Visible item missing or ambiguous: {name}')
        item = found[0]
        category = 0 if not 100000000 <= item['id'] < 103100000 else types.get((item['weapon_type'] or '').casefold())
        if category is None:
            raise ValueError(f'Unknown weapon category: {name}')
        categories.append(category)
        items.append(item)
    suffixes = {0:'_noweapon',1:'_1hand',14:'_1hand',2:'_dagger',3:'_mace',4:'_2hand',
                15:'_2hand',16:'_2hand',17:'_2hand',5:'_polearm',6:'_staff',7:'_keyblade',
                8:'_harp',9:'_orb',10:'_book',11:'_1gun',12:'_cannon',13:'_bow',20:'_aqua'}
    primary, secondary = categories
    suffix = ({11:'_2gun',18:'_flame',19:'_earth',21:'_wind'}.get(primary, '_2weapon')
              if primary and secondary else suffixes.get(primary))
    if suffix is None:
        raise ValueError('Unreviewed weapon suffix')
    mesh, motion = single(npc, 'mesh'), single(skill, 'motion_name')
    mesh_origin = 'mesh'
    if mesh is None and not npc.findall('mesh'):
        # The pinned NPC server maps these two pc_type spellings to its LF
        # animation model. Do not infer the other PC model mappings here.
        appearances = npc.findall('appearance')
        skill_lists = npc.findall('skills')
        if len(appearances) == 1 and len(skill_lists) == 1:
            pc_type = single(appearances[0], 'pc_type')
            children = list(npc)
            if pc_type and pc_type.casefold() in ('lightf', 'light_f'):
                if binary[0]['sha256'] != 'c24e600107352268d377c9a7361e52d848e96af8deff61259e031b98b7130350':
                    raise ValueError('PC appearance mapping requires the reviewed NPC binary')
                if children.index(appearances[0]) >= children.index(skill_lists[0]):
                    raise ValueError('PC appearance follows NPC skill loading')
                mesh, mesh_origin = 'LF', 'appearance.pc_type'
    if not mesh or Path(mesh).name != mesh or not motion:
        raise ValueError('Missing or invalid model or skill motion')
    marker_path = args.xml_root / 'AnimationMarkers' / (mesh + '.xml')
    marker_raw = marker_path.read_bytes()
    marker_root = ET.fromstring(marker_raw)
    prefix = ('cfire' + suffix + '_' + motion + '_').casefold()
    matches = []
    for entry in marker_root:
        name = entry.get('name', '')
        if not name.casefold().startswith(prefix):
            continue
        variant = name[len(prefix):]
        if not re.fullmatch(r'[0-9]+', variant) or not 1 <= int(variant) <= 4:
            raise ValueError(f'Unreviewed motion variant: {name}')
        events = entry.findall('hitpoint') or entry.findall('shoot')
        if not events or events[0].get('when') is None:
            raise ValueError(f'Missing motion hit event: {name}')
        hit = events[0].get('when')
        matches.append({'name':name,'variant':int(variant),'event':events[0].tag,
                        'seconds':hit,'hit_word_ms':marker_word(hit),
                        'total_word_ms':marker_word(entry.get('animation_length', hit))})
    if not matches:
        raise ValueError('No reviewed motion marker; native fallback is not inferred')
    result = {'scope':'source reconstruction only; native runtime selection not observed',
              'map':args.map,'source_map':source_map,'npc':args.npc,'skill':args.skill,
              'npc_source_sha256':row['source']['raw_sha256'],
              'skill_source_sha256':sources[0]['sha256'],'manifest_sha256':args.manifest_sha256,
              'bindings':bindings,'mesh':mesh,'mesh_origin':mesh_origin,'motion':motion,'equipment':items,'categories':categories,
              'weapon_suffix':suffix,'marker_path':str(marker_path),'marker_sha256':digest(marker_raw),
              'markers':matches,'item_sources':item_sources,
              'limitations':['Does not establish active retail files, variant selection, projectile travel or cast timing.',
                             'Does not enable or change any combat or skill.']}
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
