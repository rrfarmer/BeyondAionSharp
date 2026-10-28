"""Package reviewed offline C# stat comparisons; never publish gameplay state."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import tempfile

REPO = Path(__file__).resolve().parents[2]
CONTEXT_ASSETS = ('identity_review', 'skill_context', 'combat_stats',
                  'equipment_response_context', 'status_context', 'spell_condition_context')


def digest(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON property: ' + key)
        result[key] = value
    return result


def loads(raw):
    return json.loads(raw, object_pairs_hook=unique_object,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError('Non-finite JSON: ' + value)))


def by_id(rows, label):
    result = {}
    for row in rows:
        npc = row['npc_id']
        if type(npc) is not int or npc <= 0 or npc in result:
            raise ValueError('Invalid/duplicate ' + label + ' NPC ID: ' + str(npc))
        result[npc] = row
    return result


def assemble(review_path: Path, output: Path) -> dict:
    review_path, output = review_path.resolve(), output.resolve()
    if output.is_relative_to(REPO) or output.exists():
        raise ValueError('Use a new output directory outside the checkout')
    review_raw = review_path.read_bytes()
    review = loads(review_raw)
    if review['version'] != 1 or review['region'] != 'USA':
        raise ValueError('Explicit USA source selection required')
    inputs = {review_path: digest(review_raw)}

    def asset(spec, *, parse_json=True):
        path = Path(spec['path'])
        if not path.is_absolute():
            path = review_path.parent / path
        path = path.resolve()
        if path.stat().st_size > 64 * 1024 * 1024:
            raise ValueError('Oversized stat input')
        raw = path.read_bytes()
        if digest(raw) != spec['sha256'] or any(spec[k] != len(raw) for k in ('bytes', 'byte_length') if k in spec):
            raise ValueError('Stat input hash/length changed: ' + str(path))
        if path in inputs and inputs[path] != spec['sha256']:
            raise ValueError('Conflicting stat input pins: ' + str(path))
        inputs[path] = digest(raw)
        return {**spec, 'path': str(path)}, loads(raw) if parse_json else None

    prepared, pairs, names = [], set(), set()
    dataset = manifest = None
    for group in review['cohorts']:
        name, map_id, ids = group['id'], group['map_id'], group['npc_ids']
        if (not isinstance(name, str) or not name.strip() or name in names or not ids
                or any(type(n) is not int or n <= 0 for n in ids) or len(ids) != len(set(ids))
                or type(map_id) is not int or map_id < 0 or not group.get('review_basis')):
            raise ValueError('Explicit unique reviewed statistics membership required')
        names.add(name)
        if any((map_id, npc) in pairs for npc in ids):
            raise ValueError('Overlapping statistics membership')
        pairs.update((map_id, npc) for npc in ids)
        _, source = asset(group['profiles'])
        _, comparison = asset(group['comparison'])
        if (comparison.get('statistics_only') is not True or comparison.get('activation_ready') is not False
                or comparison.get('context') != 'fresh-unowned-ordinary-kind1' or comparison.get('region') != 'USA'):
            raise ValueError('An inactive C# statistics-only comparison in the explicit ordinary USA context is required')
        if comparison['dataset_id'] != source['dataset_id'] or comparison['manifest_sha256'] != source['output_manifest_sha256']:
            raise ValueError('Source/calculator dataset differs')
        if dataset is not None and (dataset, manifest) != (source['dataset_id'], source['output_manifest_sha256']):
            raise ValueError('Mixed statistics source identities')
        dataset, manifest = source['dataset_id'], source['output_manifest_sha256']
        source_by_id, calculated = by_id(source['profiles'], 'source'), by_id(comparison['rows'], 'calculated')
        members = []
        for npc in ids:
            if npc not in source_by_id or npc not in calculated:
                raise ValueError('Missing source/calculated statistics member: ' + str(npc))
            row = calculated[npc]
            if (row['calculation'] != 'calculated_reference_under_explicit_context'
                    or not re.fullmatch('[0-9a-f]{64}', row.get('host_template_sha256', ''))):
                raise ValueError('Uncalculated statistics member: ' + str(npc))
            if row['source']['raw_record_sha256'] != source_by_id[npc]['source']['raw_sha256']:
                raise ValueError('Calculated/source record differs: ' + str(npc))
            members.append({'npc_id': npc, 'host_level': row['baseline48']['level'],
                            'host_template_sha256': row['host_template_sha256'],
                            'source_record_sha256': row['source']['raw_record_sha256']})
        prepared.append((group, {**source, 'profiles': [source_by_id[npc] for npc in sorted(ids)]}, members))
    if not prepared:
        raise ValueError('Empty statistics package')
    maps = review.get('supported_maps', sorted({g['map_id'] for g, _, _ in prepared if g['map_id'] > 0}))
    if (not maps or len(maps) > 2048 or any(type(m) is not int or m <= 0 for m in maps)
            or len(maps) != len(set(maps)) or any(g['map_id'] > 0 and g['map_id'] not in maps for g, _, _ in prepared)):
        raise ValueError('Explicit unique supported combat maps required')
    package = {'version': 1, 'mode': 'statistics-only-v1', 'region': 'USA', 'dataset_id': dataset,
               'output_manifest_sha256': manifest, 'supported_maps': maps}
    unknown = [k for k in review if k.endswith('_context') and k not in CONTEXT_ASSETS]
    if unknown:
        raise ValueError('Unrecognized context assets: ' + ', '.join(unknown))
    for name in CONTEXT_ASSETS:
        if name in review:
            package[name], _ = asset(review[name])
    if 'skill_sources' in review:
        package['skill_sources'] = [asset(spec, parse_json=False)[0] for spec in review['skill_sources']]
    # Required inputs are all admitted before any output directory is published.
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.' + output.name + '.staging-', dir=output.parent) as temp:
        stage = Path(temp).resolve()
        if stage.parent != output.parent:
            raise ValueError('Staging directory escaped output parent')

        def write(name, value):
            raw = (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode('utf-8')
            (stage / name).write_bytes(raw)
            return {'path': str(output / name), 'sha256': digest(raw), 'byte_length': len(raw)}

        package['cohorts'] = []
        for ordinal, (group, bounded, members) in enumerate(prepared):
            spec = write(f'profiles-{ordinal:03d}.json', bounded)
            package['cohorts'].append({'id': group['id'], 'map_id': group['map_id'], 'review_basis': group['review_basis'],
                                       'profile_artifact': spec, 'members': members})
        artifact = write('manifest.json', package)
        receipt = {'purpose': 'statistics only; no gameplay activation', 'review_sha256': digest(review_raw),
                   'inputs': [{'path': str(p), 'sha256': pin} for p, pin in inputs.items()],
                   'memberships': len(pairs), 'manifest': artifact, 'activation_performed': False}
        write('build-receipt.json', receipt)
        for path, pin in inputs.items():
            if digest(path.read_bytes()) != pin:
                raise ValueError('Input changed during packaging: ' + str(path))
        stage.rename(output)
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    print(json.dumps(assemble(args.review, args.out), ensure_ascii=False))
