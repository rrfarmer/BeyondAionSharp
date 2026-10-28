"""Assemble explicitly reviewed cohort inputs. Never enables a server or invents evidence."""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
from pathlib import Path


def digest(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def read_asset(spec: dict, base: Path) -> tuple[Path, bytes]:
    path = Path(spec['path'])
    if not path.is_absolute():
        path = base / path
    path = path.resolve()
    if path.stat().st_size > 64 * 1024 * 1024:
        raise ValueError(f'Input exceeds64MB: {path}')
    raw = path.read_bytes()
    if digest(raw) != spec['sha256']:
        raise ValueError(f'Reviewed input hash differs: {path}')
    return path, raw


def write(path: Path, value: object) -> dict:
    raw = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')
    with path.open('xb') as stream:
        stream.write(raw)
    return {'path': str(path.resolve()), 'sha256': digest(raw), 'byte_length': len(raw)}


def assemble(review_path: Path, output: Path) -> dict:
    review_raw = review_path.read_bytes()
    review = json.loads(review_raw)
    if review['version'] != 1 or review['region'] != 'USA':
        raise ValueError('Only the explicitly selected USA v1 cohort source is supported')
    if not 1 <= len(review['cohorts']) <= 64:
        raise ValueError('Select1..64 explicitly reviewed cohorts')
    base = review_path.resolve().parent
    inputs = []

    def load(spec: dict):
        path, raw = read_asset(spec, base)
        inputs.append({'path': str(path), 'sha256': digest(raw), 'byte_length': len(raw)})
        return json.loads(raw)

    maps = load(review['maps'])
    motions = load(review['motions'])
    context = motions['source_context']
    if context['npc_region'] != review['region']:
        raise ValueError('Motion/profile region differs')
    by_motion = {(m['map_id'], m['npc_id']): m for m in motions['members']}
    if len(by_motion) != len(motions['members']):
        raise ValueError('Duplicate motion membership')
    cohorts = []
    selected = set()
    names = set()
    used_maps = set()
    # Validate all inputs before creating the output directory.
    prepared = []
    for item in review['cohorts']:
        name, map_id = item['id'], item['map_id']
        ids = item['npc_ids']
        if name in names or not name or not 1 <= len(ids) <= 1024 or len(set(ids)) != len(ids):
            raise ValueError('Duplicate/empty cohort name or membership')
        names.add(name)
        if any((map_id, npc) in selected for npc in ids):
            raise ValueError('Overlapping cohort membership')
        selected.update((map_id, npc) for npc in ids)
        used_maps.add(map_id)
        if not item.get('review_basis'):
            raise ValueError('An explicit review basis is required; calculator output is not an admission grant')
        if item.get('domain') != 'fresh-unowned-static-ground-kind1-v1':
            raise ValueError('Select an explicitly supported owner/scaling/behavior domain')
        profiles = load(item['profiles'])
        comparison = load(item['comparison'])
        if profiles['map_id'] != map_id or profiles['dataset_id'] != context['dataset_id'] or profiles['output_manifest_sha256'] != context['manifest_sha256']:
            raise ValueError('Profile source/map identity differs from reviewed motions')
        source = {p['npc_id']: p for p in profiles['profiles']}
        host = {r['npc_id']: r for r in comparison['rows']}
        members = []
        skill_ids = set()
        for npc in ids:
            raw, motion, old = source[npc], by_motion[map_id, npc], host[npc]
            if motion['source_motion_blockers']:
                raise ValueError(f'Unresolved source motion for{npc}: {motion["source_motion_blockers"]}')
            if old['calculation'] != 'calculated_reference_under_explicit_context' or not old.get('host_template_sha256'):
                raise ValueError(f'Missing current C# host calculation/graph identity for{npc}')
            if motion['source_record_sha256'] != old['source']['raw_record_sha256']:
                raise ValueError(f'Comparison/motion source identity differs for{npc}')
            normal = motion['normal_motion']
            read_asset(normal['evidence_asset'], base)
            if normal['variant_count'] != (1 if normal['second_hit_ms'] is None else 2):
                raise ValueError(f'Normal variant count differs for{npc}')
            skills = motion['skill_motions']
            if any(s.get('blockers') for s in skills):
                raise ValueError(f'Unresolved skill motion for{npc}')
            skill_ids.update(s['skill_id'] for s in skills)
            members.append({'npc_id': npc, 'source_record_sha256': motion['source_record_sha256'],
                            'host_template_sha256': old['host_template_sha256'],
                            'host_level': old['baseline48']['level'], 'host_ai': old['host_ai'],
                            'normal_motion': normal, 'skill_motions': skills})
        spells = []
        for skill in sorted(skill_ids):
            spec = motions['spell_sources'].get(str(skill))
            if spec is None:
                raise ValueError(f'No reviewed complete native spell source for{skill}')
            _, raw = read_asset(spec, base)
            spells.append({'skill_id': skill, 'sha256': digest(raw),
                           'utf16le_base64': base64.b64encode(raw).decode('ascii')})
        bounded = dict(profiles)
        bounded['profiles'] = [source[npc] for npc in sorted(ids)]
        bounded['scope'] = 'Explicit reviewed subset; full dataset/source identities retained; not an extraction replacement'
        prepared.append((item, bounded, members, spells))
    selected_maps = [m for m in maps if m['map_id'] in used_maps]
    if len(selected_maps) != len(used_maps) or len({m['map_id'] for m in selected_maps}) != len(selected_maps):
        raise ValueError('Missing/duplicate reviewed map definitions')
    output.mkdir(parents=True, exist_ok=False)
    artifacts = []
    for ordinal, (item, bounded, members, spells) in enumerate(prepared):
        artifact = write(output / f'profiles-{ordinal:02d}.json', bounded)
        artifacts.append(artifact)
        cohorts.append({'id': item['id'], 'map_id': item['map_id'], 'domain': item['domain'], 'review_basis': item['review_basis'],
                        'profile_artifact': {'path': artifact['path'], 'sha256': artifact['sha256']},
                        'members': members, 'spell_sources': spells})
    manifest = {'version': 1, 'region': review['region'], 'dataset_id': context['dataset_id'],
                'output_manifest_sha256': context['manifest_sha256'], 'maps': selected_maps, 'cohorts': cohorts}
    artifact = write(output / 'manifest.json', manifest)
    with (output / 'manifest.sha256').open('x', encoding='ascii', newline='') as stream:
        stream.write(artifact['sha256'] + '\n')
    receipt = {'version': 1, 'classification': 'reviewed-input assembly; runtime combat/loaded-map/SIM gates still required',
               'review': {'path': str(review_path.resolve()), 'sha256': digest(review_raw)},
               'inputs': inputs, 'profiles': artifacts, 'manifest': artifact,
               'memberships': len(selected), 'maps': sorted(used_maps), 'enabled': False}
    write(output / 'BUILD-RECEIPT.json', receipt)
    return receipt


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    result = assemble(args.review, args.out.resolve())
    print(json.dumps({'manifest': result['manifest'], 'memberships': result['memberships'], 'enabled': False}))


if __name__ == '__main__':
    main()
