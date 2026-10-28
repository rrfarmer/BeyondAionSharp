"""Read-only indexed NPC comparison; generated/private artifacts must stay outside the checkout."""
from __future__ import annotations
import argparse
import base64
import collections
from contextlib import closing
import csv
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import sys
import xml.etree.ElementTree as ET
from urllib.parse import quote

sys.dont_write_bytecode = True
REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools/client-extract'))
from extract_retail_npc_profiles import semantic_sha, sha_file, PILOT_IDS
import ai_stat_hooks

CONTEXT = 'fresh-unowned-ordinary-kind1'
MAX_LINE = 32 * 1024 * 1024
ARTIFACTS = {'coverage.csv', 'identity-blockers.csv', 'pilot-profiles.jsonl', 'profiles.jsonl',
             'provenance.json', 'semantic-diff.json', 'source-manifest.json', 'summary.json',
             'unsupported-skill-bindings.csv', 'validation.json'}


class AuditError(ValueError):
    pass


def need(condition, message):
    if not condition:
        raise AuditError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def loads(data):
    return json.loads(data, object_pairs_hook=unique_object,
                      parse_constant=lambda value: (_ for _ in ()).throw(AuditError('Non-finite JSON: ' + value)))


def write_json(path, value):
    # Exclusive publication: rerunning never overwrites an earlier report/receipt.
    with path.open('x', encoding='utf-8', newline='') as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2, allow_nan=False)
        handle.write('\n')


def external_destination(path, inputs=()):
    destination = path.resolve()
    need(not destination.is_relative_to(REPO), 'Generated/private audit output must be outside the checkout')
    for source in inputs:
        source = source.resolve()
        need(destination != source and not (source.is_dir() and destination.is_relative_to(source)),
             'Output overlaps a read-only input: ' + str(source))
    return destination


def stamp(path):
    s = path.stat()
    return {'path': str(path.resolve()), 'bytes': s.st_size, 'mtime_ns': s.st_mtime_ns}


def index(args):
    need(bool(args.identity_review) == bool(args.identity_review_sha256), 'Identity review path and SHA256 must be supplied together')
    root = args.dataset.resolve()
    target = external_destination(args.index, [root, args.ratio, *([args.inventory] if args.inventory else [])])
    need(not target.exists() and not Path(str(target) + '.receipt.json').exists(), 'Index already exists; choose a new filename')
    need(re.fullmatch('[0-9a-f]{64}', args.manifest_sha256) is not None, 'Explicit lowercase manifest SHA256 required')
    manifest_path = root / 'output-manifest.json'
    need(sha_file(manifest_path) == args.manifest_sha256, 'Output manifest SHA256 differs from caller pin')
    manifest = loads(manifest_path.read_bytes())
    need(len(manifest) == len(ARTIFACTS) and {r['file'] for r in manifest} == ARTIFACTS, 'Unexpected/duplicate extractor artifact set')
    before = {r['file']: stamp(root / r['file']) for r in manifest}
    for row in manifest:
        path = root / row['file']
        need(path.name == row['file'] and not path.is_symlink(), 'Artifact must be a plain dataset-relative file')
        need(path.stat().st_size == row['bytes'], 'Artifact size differs: ' + row['file'])
        need(sha_file(path) == row['sha256'], 'Artifact hash differs: ' + row['file'])
    provenance = loads((root / 'provenance.json').read_bytes())
    summary = loads((root / 'summary.json').read_bytes())
    validation = loads((root / 'validation.json').read_bytes())
    need(all(validation.get(k) is True for k in ('every_target_has_coverage_row', 'profile_ids_exactly_supported_and_identity_matched')),
         'Extractor validation gates are not passing')
    need(all(s['region'] == args.region for s in provenance['selection']), 'Requested region differs from selected extractor sources')
    ratio_bytes = args.ratio.read_bytes()
    need(0 < len(ratio_bytes) <= 1_048_576, 'Ratio table size exceeds supported bound')
    need(sha_file(args.ratio) == args.ratio_sha256, 'Ratio table SHA256 differs from caller pin')
    meta = {'version': 1, 'dataset': str(root), 'dataset_id': summary['dataset_id'], 'manifest_sha256': args.manifest_sha256,
            'region': args.region, 'provenance': provenance, 'source_manifest': loads((root / 'source-manifest.json').read_bytes()),
            'ratio_base64': base64.b64encode(ratio_bytes).decode('ascii'), 'ratio_sha256': args.ratio_sha256,
            'ratio_path': str(args.ratio.resolve()), 'ratio_selected_relative': args.ratio_relative,
            'ratio_selection_reason': args.ratio_reason, 'artifacts': before,
            'inventory': None if not args.inventory else {'path': str(args.inventory.resolve()), 'sha256': sha_file(args.inventory)},
            'map_scope': 'Historical inventory static_map_ids; selection aid only, not a current spawn/travel coverage claim',
            'extractor_sha256': sha_file(REPO / 'tools/client-extract/extract_retail_npc_profiles.py')}
    if args.identity_review:
        review = args.identity_review.read_bytes()
        need(0 < len(review) <= 1_048_576 and hashlib.sha256(review).hexdigest() == args.identity_review_sha256,
             'Identity review size/hash differs from caller pin')
        meta.update(identity_review_base64=base64.b64encode(review).decode('ascii'), identity_review_sha256=args.identity_review_sha256)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.open('xb').close()  # Reserve exclusively before SQLite opens its own file handle.
    with closing(sqlite3.connect(target)) as db, db:
        db.executescript('CREATE TABLE meta (key TEXT PRIMARY KEY,value TEXT NOT NULL);'
                         'CREATE TABLE coverage (npc_id INTEGER PRIMARY KEY, status TEXT, family TEXT, name48 TEXT, name58 TEXT);'
                         'CREATE TABLE profiles (npc_id INTEGER PRIMARY KEY, offset INTEGER, length INTEGER, sha256 TEXT);'
                         'CREATE TABLE maps (npc_id INTEGER,map_id INTEGER,PRIMARY KEY(npc_id,map_id));')
        with (root / 'coverage.csv').open(encoding='utf-8', newline='') as handle:
            for row in csv.DictReader(handle):
                db.execute('INSERT INTO coverage VALUES (?,?,?,?,?)',
                           (int(row['npc_id']), row['status'], row['family'], row['devnames48'], row['retail_devname']))
        if args.inventory:
            with args.inventory.open(encoding='utf-8-sig', newline='') as handle:
                for row in csv.DictReader(handle):
                    for map_id in set(re.findall(r'\b[0-9]{9}\b', row.get('static_map_ids', ''))):
                        db.execute('INSERT INTO maps VALUES (?,?)', (int(row['npc_id']), int(map_id)))
        count = 0
        with (root / 'profiles.jsonl').open('rb') as handle:
            while True:
                offset = handle.tell()
                line = handle.readline(MAX_LINE + 1)
                if not line:
                    break
                need(len(line) <= MAX_LINE and line.endswith(b'\n'), 'Truncated/oversized profile line')
                profile = loads(line)
                need(profile['dataset_id'] == summary['dataset_id'], 'Profile dataset identity differs')
                need(semantic_sha(profile['raw_record']) == profile['source']['semantic_sha256'],
                     'Ordered source-record semantic hash differs')
                coverage = db.execute('SELECT status,name48,name58 FROM coverage WHERE npc_id=?', (profile['npc_id'],)).fetchone()
                need(coverage and coverage[0] == 'exported_static_source_only' and coverage[2] == profile['devname'], 'Profile/coverage identity differs')
                need(profile['binding48']['devnames48'] == coverage[1].split(';'), '4.8 identity receipt differs from coverage')
                db.execute('INSERT INTO profiles VALUES (?,?,?,?)', (profile['npc_id'], offset, len(line), hashlib.sha256(line).hexdigest()))
                count += 1
        need(count == summary['exported_profiles'], 'Indexed profile count differs from extractor summary')
        need(db.execute('SELECT count(*) FROM coverage').fetchone()[0] == summary['target_count'], 'Coverage count differs')
        need(before == {r['file']: stamp(root / r['file']) for r in manifest}, 'An input changed during indexing')
        # Rehash the large stream after parsing as well; later random access verifies each selected line.
        need(sha_file(root / 'profiles.jsonl') == next(r['sha256'] for r in manifest if r['file'] == 'profiles.jsonl'),
             'Profile stream changed during indexing')
        need(sha_file(manifest_path) == args.manifest_sha256, 'Manifest changed during indexing')
        db.execute('INSERT INTO meta VALUES (?,?)', ('snapshot', json.dumps(meta, allow_nan=False)))
    write_json(Path(str(target) + '.receipt.json'), {'version': 1, 'index_sha256': sha_file(target),
               'manifest_sha256': args.manifest_sha256, 'dataset_id': summary['dataset_id'], 'profiles': count,
               'coverage': summary['target_count'], 'metadata': meta})
    print(json.dumps({'index': str(target), 'profiles': count, 'coverage': summary['target_count']}))


def open_index(path):
    path = path.resolve()
    receipt = loads(Path(str(path) + '.receipt.json').read_bytes())
    need(sha_file(path) == receipt['index_sha256'], 'Index hash differs; rebuild a new index')
    db = sqlite3.connect('file:' + quote(path.as_posix(), safe='/:') + '?mode=ro', uri=True)
    try:
        record = db.execute('SELECT value FROM meta WHERE key=?', ('snapshot',)).fetchone()
        need(record is not None, 'Index is incomplete; build a new index')
        meta = loads(record[0])
        need(meta == receipt['metadata'] and meta['version'] == 1, 'Index metadata differs from receipt')
        root = Path(meta['dataset'])
        for name, expected in meta['artifacts'].items():
            need(stamp(root / name) == expected, 'Dataset changed since indexing; build a new index: ' + name)
        need(sha_file(root / 'output-manifest.json') == meta['manifest_sha256'], 'Pinned manifest changed')
        for artifact in loads((root / 'output-manifest.json').read_bytes()):
            if artifact['file'] != 'profiles.jsonl':
                need(sha_file(root / artifact['file']) == artifact['sha256'], 'Dataset metadata hash changed: ' + artifact['file'])
        need(meta['provenance'] == loads((root / 'provenance.json').read_bytes()) and
             meta['source_manifest'] == loads((root / 'source-manifest.json').read_bytes()), 'Cached provenance differs from pinned metadata')
        return db, meta
    except BaseException:
        db.close()
        raise


def ensure_dll_window():
    need(not list((REPO / 'run/.sim-running').glob('*.json')), 'SIM owns the DLL window; wait until it finishes')
    if os.name == 'nt':
        output = subprocess.check_output(['tasklist', '/FI', 'IMAGENAME eq testhost.exe', '/FO', 'CSV', '/NH'], text=True)
        need('"testhost.exe"' not in output.lower(), 'A testhost owns the DLL window; wait until it finishes')


def compare(args):
    ensure_dll_window()
    db, meta = open_index(args.index)
    try:
        clauses, params = [], []
        if args.npc:
            wanted = sorted({int(x) for group in args.npc for x in group.split(',')})
            need(all(x > 0 for x in wanted), 'NPC IDs must be positive')
        else:
            if args.cohort:
                wanted = sorted(PILOT_IDS)
            else:
                if args.family:
                    clauses.append('family=?'); params.append(args.family)
                if args.map:
                    clauses.append('npc_id IN (SELECT npc_id FROM maps WHERE map_id=?)'); params.append(args.map)
                wanted = [r[0] for r in db.execute('SELECT npc_id FROM coverage WHERE ' + ' AND '.join(clauses) + ' ORDER BY npc_id', params)]
        matched = len(wanted)
        if args.limit is not None:
            need(1 <= args.limit <= 1024 and args.offset >= 0, 'Limit must be1..1024 and offset nonnegative')
            wanted = wanted[args.offset:args.offset + args.limit]
        else:
            need(args.offset == 0, 'An offset requires an explicit limit')
        need(0 < len(wanted) <= 1024, 'Select1..1024 NPCs. Narrow the query or use explicit --limit/--offset; no silent truncation')
        root = Path(meta['dataset'])
        out = external_destination(args.out, [root, args.index, args.templates])
        need(not out.exists(), 'Report directory already exists; choose a new directory')
        rows = []
        with (root / 'profiles.jsonl').open('rb') as handle:
            for npc in wanted:
                coverage = db.execute('SELECT status,family FROM coverage WHERE npc_id=?', (npc,)).fetchone()
                offset = db.execute('SELECT offset,length,sha256 FROM profiles WHERE npc_id=?', (npc,)).fetchone()
                profile = None
                if offset:
                    handle.seek(offset[0]); raw = handle.read(offset[1])
                    need(hashlib.sha256(raw).hexdigest() == offset[2], 'Selected profile bytes changed: ' + str(npc))
                    profile = loads(raw)
                    need(profile['npc_id'] == npc and profile['dataset_id'] == meta['dataset_id'], 'Selected record identity differs')
                rows.append({'npc_id': npc, 'coverage_status': coverage[0] if coverage else 'not_in_extractor_inventory',
                             'family': coverage[1] if coverage else None, 'profile': profile})
        out.mkdir(parents=True)
        request = {k: meta[k] for k in ('dataset_id', 'manifest_sha256', 'region', 'provenance', 'source_manifest',
                   'ratio_base64', 'ratio_sha256', 'ratio_path', 'ratio_selected_relative', 'ratio_selection_reason')}
        request.update({k: meta[k] for k in ('identity_review_base64','identity_review_sha256') if k in meta})
        request.update(protocol_version=1, context=CONTEXT, templates48=str(args.templates.resolve()),
                       templates48_sha256=sha_file(args.templates), npcs=rows,
                       statistics_only=getattr(args, 'stats_only', False))
        request_path = out / 'calculator-request.json'
        write_json(request_path, request)
        if not args.no_build:
            with (out / 'build.log').open('xb') as log:
                result = subprocess.run(['dotnet', 'build', str(REPO / 'tools/Aion.NpcAudit'), '--nologo'], cwd=REPO, stdout=log, stderr=subprocess.STDOUT)
            need(result.returncode == 0, 'Calculator build failed; see ' + str(out / 'build.log'))
        dll = REPO / 'tools/Aion.NpcAudit/bin/Debug/net10.0/Aion.NpcAudit.dll'
        need(dll.is_file(), 'Build the calculator first or omit --no-build')
        with (out / 'calculator.stderr.log').open('xb') as log:
            result = subprocess.run(['dotnet', str(dll), '--request', str(request_path)], cwd=REPO,
                                    stdout=subprocess.PIPE, stderr=log)
        need(result.returncode == 0, 'Calculator failed; see ' + str(out / 'calculator.stderr.log'))
        report = loads(result.stdout)
        need([r['npc_id'] for r in report['rows']] == wanted, 'Calculator omitted/reordered requested IDs')
        hooks = ai_stat_hooks.scan(REPO)
        for row in report['rows']:
            row['host_ai_stat_hook'] = ai_stat_hooks.describe(row.get('host_ai'), hooks)
        report['host_ai_stat_hook_scope'] = (
            'Static direct template-AI hooks are candidates only; runtime conditions, inherited hooks and spawn AI overrides are not evaluated.')
        report['host_ai_stat_hook_scanner_sha256'] = sha_file(REPO / 'tools/npc-audit/ai_stat_hooks.py')
        report.update(dataset_id=meta['dataset_id'], manifest_sha256=meta['manifest_sha256'], region=meta['region'],
                      index_path=str(args.index.resolve()), templates48_path=str(args.templates.resolve()),
                      templates48_sha256=request['templates48_sha256'], index_sha256=sha_file(args.index),
                      driver_sha256=sha_file(Path(__file__)), map_scope=meta['map_scope'], no_build=args.no_build)
        report['selection'] = {'matched': matched, 'returned': len(wanted), 'offset': args.offset, 'limit': args.limit,
                               'npc': args.npc, 'cohort': args.cohort, 'map': args.map, 'family': args.family,
                               'next_offset': args.offset + len(wanted) if args.offset + len(wanted) < matched else None}
        report['summary'] = {'npcs': len(wanted), 'calculable': sum(r['calculation'] != 'blocked' for r in report['rows']),
                             'blocked': sum(r['calculation'] == 'blocked' for r in report['rows']),
                             'direct_ai_stat_hook_candidates': sum(r['host_ai_stat_hook']['status'] == 'direct_override_found' for r in report['rows']),
                             'field_statuses': dict(collections.Counter(f['status'] for r in report['rows'] for f in r['fields']))}
        need(sha_file(args.templates) == request['templates48_sha256'], '4.8 templates changed during comparison')
        # Index and source continuity are checked again before report publication.
        check, after = open_index(args.index); check.close(); need(after == meta, 'Index changed during comparison')
        write_json(out / 'comparison.json', report)
        sources = {r['npc_id']: r for r in rows}
        write_json(out / 'blocked.json', [{'npc_id': r['npc_id'], 'name48': r['name48'], 'blockers': r['blockers'],
            'coverage_status': sources[r['npc_id']]['coverage_status'],
            'source': sources[r['npc_id']]['profile']['source'] if sources[r['npc_id']]['profile'] else None}
            for r in report['rows'] if r['calculation'] == 'blocked'])
        write_reports(out, report)
        print(json.dumps({'report': str(out / 'comparison.md'), **report['summary']}))
    finally:
        db.close()


def write_reports(out, report):
    with (out / 'comparison.csv').open('x', encoding='utf-8', newline='') as handle:
        writer = csv.writer(handle, lineterminator='\n')
        writer.writerow(['npc_id', 'name48', 'calculation', 'field', 'baseline48', 'reference58', 'status', 'source_presence', 'rule',
                         'host_ai', 'host_ai_stat_hook_status', 'host_ai_referenced_stats'])
        for row in report['rows']:
            hook = row.get('host_ai_stat_hook', {})
            stats = sorted({stat for handler in hook.get('handlers', []) for stat in handler['referenced_stats']})
            ai_columns = [row.get('host_ai'), hook.get('status'), ','.join(stats)]
            if not row['fields']:
                writer.writerow([row['npc_id'],row['name48'],row['calculation'],'',None,None,'blocked',None,
                                 json.dumps(row['blockers'],ensure_ascii=False), *ai_columns])
            for field in row['fields']:
                writer.writerow([row['npc_id'], row['name48'], row['calculation'], *[field.get(k) for k in ('field', 'baseline48', 'reference58', 'status', 'source_presence', 'rule')], *ai_columns])
    text = '# Read-only mob comparison\n\n' + report['scope'] + '\n\n'
    text += f"Region: {report['region']}; NPCs: {report['summary']['npcs']}; calculable: {report['summary']['calculable']}; blocked: {report['summary']['blocked']}.\n\n"
    text += 'Selection: ' + json.dumps(report['selection']) + '\n\n'
    text += 'No gameplay activation or corrections are performed. Differences are findings, not automatic fix approval. Full ordered attacks/skills, presence, source receipts and blockers are in JSON.\n\n'
    text += '| NPC | 4.8 name | HP48 | HP58 | HP result | Normal damage58 | Calculation |\n|---|---|---:|---:|---|---|---|\n'
    for row in report['rows']:
        fields = {f['field']: f for f in row['fields']}; hp = fields.get('max_hp', {})
        lo = fields.get('normal_min_damage', {}).get('reference58'); hi = fields.get('normal_max_damage', {}).get('reference58')
        text += f"| {row['npc_id']} | {str(row['name48'] or '').replace('|', '/')} | {hp.get('baseline48', '')} | {hp.get('reference58', '')} | {hp.get('status', '')} | {lo}–{hi} | {row['calculation']} |\n"
    for row in report['rows']:
        if row['blockers']:
            text += '\nNPC ' + str(row['npc_id']) + ' blockers:\n\n' + '\n'.join('- ' + json.dumps(i, ensure_ascii=False) for i in row['blockers']) + '\n'
    flagged = [row for row in report['rows'] if row.get('host_ai_stat_hook', {}).get('status') == 'direct_override_found']
    if flagged:
        text += '\nTemplate AI stat-hook candidates (conditions and actual spawn AI are not evaluated):\n\n'
        for row in flagged:
            for hook in row['host_ai_stat_hook']['handlers']:
                stats = ', '.join(hook['referenced_stats']) or 'indirect or unclassified stats'
                text += f"- NPC {row['npc_id']}: {row['host_ai']} references {stats} in {hook['file']}:{hook['line']}.\n"
    text += '\nNormal damage58 is the calculated pre-effect range, not target HP loss. Legacy physical/magical attack stats are deliberately not compared to that range. Path geometry, final damage, effects, assistance, owned/difficulty scaling and running-host feature flags require separate checks.\n'
    text += 'The AI scan only finds directly declared hooks in scanned source. Inherited hooks, runtime conditions and spawn overrides remain unverified even when no direct hook is listed.\n'
    with (out / 'comparison.md').open('x', encoding='utf-8', newline='') as handle:
        handle.write(text)


def info(args):
    db, meta = open_index(args.index)
    try:
        print(json.dumps({'dataset_id': meta['dataset_id'], 'region': meta['region'], 'map_scope': meta['map_scope'],
                          'coverage': dict(db.execute('SELECT status,count(*) FROM coverage GROUP BY status')),
                          'families': dict(db.execute('SELECT family,count(*) FROM coverage GROUP BY family'))}, indent=2))
    finally:
        db.close()


def stats_package(args):
    """One production-parser pass over an admitted statistics package; no server or actors."""
    ensure_dll_window()
    need(sha_file(args.manifest) == args.manifest_sha256, 'Statistics package differs from supplied pin')
    out = external_destination(args.out, [args.manifest.parent, args.templates])
    need(not out.exists(), 'Report directory already exists; choose a new directory')
    out.mkdir(parents=True)
    template_pin = sha_file(args.templates)
    if not args.no_build:
        with (out / 'build.log').open('xb') as log:
            result = subprocess.run(['dotnet', 'build', str(REPO / 'tools/Aion.NpcAudit'), '--nologo'],
                                    cwd=REPO, stdout=log, stderr=subprocess.STDOUT)
        need(result.returncode == 0, 'Calculator build failed; see ' + str(out / 'build.log'))
    dll = REPO / 'tools/Aion.NpcAudit/bin/Debug/net10.0/Aion.NpcAudit.dll'
    need(dll.is_file(), 'Build the calculator first or omit --no-build')
    with (out / 'calculator.stderr.log').open('xb') as log:
        result = subprocess.run(['dotnet', str(dll), '--stats-package', str(args.manifest.resolve()),
                                 args.manifest_sha256, str(args.templates.resolve()), str(out)],
                                cwd=REPO, stdout=subprocess.PIPE, stderr=log)
    need(result.returncode == 0, 'Package calculator failed; see ' + str(out / 'calculator.stderr.log'))
    need(sha_file(args.manifest) == args.manifest_sha256 and sha_file(args.templates) == template_pin,
         'Source input changed during audit')
    summary = loads(result.stdout)
    write_json(out / 'receipt.json', {'manifest_sha256': args.manifest_sha256,
        'templates48_sha256': template_pin, 'driver_sha256': sha_file(Path(__file__)),
        'profiles_sha256': sha_file(out / 'profiles.json'), 'csv_sha256': sha_file(out / 'profiles.csv'),
        'summary': summary, 'offline_only': True, 'no_build': args.no_build})
    print(json.dumps({'report': str(out / 'profiles.json'), **summary}))


def curse_scenario(args):
    """Project supplied native-stage numbers without creating actors or enabling effects."""
    dll = REPO / 'tools/Aion.NpcAudit/bin/Debug/net10.0/Aion.NpcAudit.dll'
    need(dll.is_file(), 'Build tools/Aion.NpcAudit first; this command never builds')
    values = [args.percentage_base, args.unscaled_total, args.existing_percent, args.added_percent,
              args.old_maximum, args.current]
    result = subprocess.run(['dotnet', str(dll), '--curse-scenario', args.resource,
                             *map(str, values)], cwd=REPO, capture_output=True, text=True)
    need(result.returncode == 0, 'Calculator failed: ' + result.stderr.strip())
    report = loads(result.stdout)
    report['calculator_sha256'] = sha_file(dll)
    print(json.dumps(report, indent=2))


def curse_class_bases(args):
    """Compare class base inputs without creating a player or changing gameplay."""
    ensure_dll_window()
    dll = REPO / 'tools/Aion.NpcAudit/bin/Debug/net10.0/Aion.NpcAudit.dll'
    need(dll.is_file(), 'Build tools/Aion.NpcAudit first; this command never builds')
    game_dll = dll.parent / 'Aion.GameServer.dll'
    need(game_dll.is_file(), 'The GameServer calculator assembly is missing')
    result = subprocess.run(['dotnet', str(dll), '--curse-class-bases', args.player_class, str(args.level)],
                            cwd=REPO, capture_output=True, text=True)
    need(result.returncode == 0, 'Calculator failed: ' + result.stderr.strip())
    report = loads(result.stdout)
    report['calculator_sha256'] = sha_file(dll)
    report['game_server_sha256'] = sha_file(game_dll)
    print(json.dumps(report, indent=2))


def armor_enchant_comparison(args):
    import armor_enchant  # Optional equipment experiment; never needed by stat commands.
    """Compare an armor item's selected enchant tables without changing gear or gameplay."""
    ensure_dll_window()
    dll = REPO / 'tools/Aion.NpcAudit/bin/Debug/net10.0/Aion.NpcAudit.dll'
    need(dll.is_file(), 'Build tools/Aion.NpcAudit first; this command never builds')
    game_dll = dll.parent / 'Aion.GameServer.dll'
    need(game_dll.is_file(), 'The GameServer calculator assembly is missing')
    calculator_sha, game_sha = sha_file(dll), sha_file(game_dll)
    retail_items, _ = armor_enchant.selected_region_file(args.retail_root, 'item_armors.xml')
    retail_tables, _ = armor_enchant.selected_region_file(args.retail_root, 'item_enchanttable.xml')
    paths = [retail_items, retail_tables, args.host_items, args.host_tables]
    before = {str(path.resolve()): sha_file(path) for path in paths}
    selected = armor_enchant.prepare(args.item, args.level, args.retail_root, args.host_items, args.host_tables, args.stat)
    need([p.resolve() for p in selected['source_paths']] == [p.resolve() for p in paths], 'Source selection changed')
    scenario = '--armor-hp-enchant-scenario' if args.stat == 'max-hp' else '--armor-enchant-scenario'
    result = subprocess.run(['dotnet', str(dll), scenario, *map(str, selected['inputs'])],
                            cwd=REPO, capture_output=True, text=True)
    need(result.returncode == 0, 'Calculator failed: ' + result.stderr.strip())
    need(all(sha_file(path) == before[str(path.resolve())] for path in paths), 'A source file changed during comparison')
    need(sha_file(dll) == calculator_sha and sha_file(game_dll) == game_sha, 'Calculator changed during comparison')
    report = loads(result.stdout)
    report.update({k: v for k, v in selected.items() if k not in ('source_paths', 'inputs')})
    report['selected_operands'] = selected['inputs']
    report['sources_sha256'] = before
    report['calculator_sha256'] = calculator_sha
    report['game_server_assembly_sha256'] = game_sha
    report['scope'] = (f'{args.stat} enchant contribution only. The supplied item is not equipped or changed; '
                       'actual selection, eligibility, gear bonuses, armor revision and final player stats are not established.')
    print(json.dumps(report, indent=2))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('index', help='Validate a complete existing extractor generation and create an external offset index once')
    p.add_argument('--dataset', type=Path, required=True); p.add_argument('--manifest-sha256', required=True)
    p.add_argument('--index', type=Path, required=True); p.add_argument('--region', required=True)
    p.add_argument('--ratio', type=Path, required=True); p.add_argument('--ratio-sha256', required=True)
    p.add_argument('--ratio-relative', required=True); p.add_argument('--ratio-reason', required=True)
    p.add_argument('--inventory', type=Path); p.set_defaults(run=index)
    p.add_argument('--identity-review', type=Path, help='Optional existing pinned90 retained-ID review; never enables name guessing')
    p.add_argument('--identity-review-sha256')
    p = sub.add_parser('compare', help='Read selected records and call existing C# profile calculations; writes reports only')
    p.add_argument('--index', type=Path, required=True); p.add_argument('--out', type=Path, required=True)
    group = p.add_mutually_exclusive_group(required=True)
    group.add_argument('--npc', action='append', help='One/comma-separated IDs; repeatable')
    group.add_argument('--cohort', choices=['three-keys']); group.add_argument('--map', type=int); group.add_argument('--family')
    p.add_argument('--templates', type=Path, default=REPO / 'game-server/data/static_data/npcs/npc_templates.xml')
    p.add_argument('--no-build', action='store_true', help='Use already compiled DLLs; hashes are recorded. Does not certify current source was compiled.')
    p.add_argument('--stats-only', action='store_true', help='Project base stats without admitting skill bindings or movement behavior; complete raw skills remain in JSON.')
    p.add_argument('--limit', type=int, help='Explicit batch size1..1024; report records total matches and next offset')
    p.add_argument('--offset', type=int, default=0)
    p.set_defaults(run=compare)
    p = sub.add_parser('info', help='Show indexed coverage and family names without running the calculator')
    p.add_argument('--index', type=Path, required=True); p.set_defaults(run=info)
    p = sub.add_parser('stats-package', help='Calculate every admitted statistics cohort in one offline pass; no actors or activation')
    p.add_argument('--manifest', type=Path, required=True); p.add_argument('--manifest-sha256', required=True)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--templates', type=Path, default=REPO / 'game-server/data/static_data/npcs/npc_templates.xml')
    p.add_argument('--no-build', action='store_true')
    p.set_defaults(run=stats_package)
    p = sub.add_parser('curse-scenario', help='Offline 5.8 curse arithmetic for explicitly supplied native-stage player inputs')
    p.add_argument('--resource', required=True, choices=['hp', 'mp'])
    p.add_argument('--percentage-base', required=True, type=int, help='Retail percentage-scaled base, including applicable inherent armor')
    p.add_argument('--unscaled-total', required=True, type=int, help='All other native contributions before percentage adjustment')
    p.add_argument('--existing-percent', required=True, type=int, help='Aggregate existing retail percent modifier')
    p.add_argument('--added-percent', required=True, type=int, help='Curse percentage; use -35 for the reviewed sources')
    p.add_argument('--old-maximum', required=True, type=int); p.add_argument('--current', required=True, type=int)
    p.set_defaults(run=curse_scenario)
    p = sub.add_parser('curse-class-bases', help='Compare 4.8 class bases with the 5.8 curse percentage-base calculation')
    p.add_argument('--class', dest='player_class', required=True, help='Named 4.8 class or all')
    p.add_argument('--level', type=int, required=True, help='4.8 player level, 1 through 65')
    p.set_defaults(run=curse_class_bases)
    p = sub.add_parser('armor-enchant', help='Compare an armor item\'s 4.8 and 5.8 enchant contribution')
    p.add_argument('--item', required=True, type=int); p.add_argument('--level', required=True, type=int)
    p.add_argument('--stat', choices=('physical-defense', 'max-hp'), default='physical-defense')
    p.add_argument('--retail-root', type=Path, default=Path(r'D:\Aion58ServerTesting\Server\Map\XML'))
    p.add_argument('--host-items', type=Path, default=REPO / 'game-server/data/static_data/items/item_templates.xml')
    p.add_argument('--host-tables', type=Path, default=REPO / 'game-server/data/static_data/enchants/enchant_templates.xml')
    p.set_defaults(run=armor_enchant_comparison)
    args = parser.parse_args(argv)
    try:
        args.run(args)
        return 0
    except (AuditError, OSError, ValueError, KeyError, ET.ParseError, sqlite3.Error, subprocess.SubprocessError) as error:
        print(type(error).__name__ + ': ' + str(error), file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
