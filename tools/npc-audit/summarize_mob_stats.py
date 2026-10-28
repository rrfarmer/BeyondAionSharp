"""Summarize an offline C# stats-package export. No gameplay or damage formulas.

The source census is an envelope, not proof that a spawn exists or is unowned.
All generated reports belong outside the checkout. Exit zero means report written,
never that the mob-stat rollout is complete.
"""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
ATTRIBUTES = ('str', 'vit', 'agi', 'dex', 'kno', 'will', 'hit_accuracy',
              'magical_hit_accuracy', 'critical', 'magical_critical', 'magical_skill_boost')
DEFENSES = ('physical_defend', 'magical_defend', 'magical_resist', 'dodge', 'parry',
            'block', 'magical_skill_boost_resist', 'physical_critical_reduce_rate',
            'magical_critical_reduce_rate', 'elemental_defend_water', 'elemental_defend_air',
            'elemental_defend_earth', 'elemental_defend_fire', 'elemental_defend_light',
            'elemental_defend_dark')
REQUIRED = ('max_hp',) + ATTRIBUTES + DEFENSES


def need(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        need(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def read_json(path, pin=None):
    raw = Path(path).read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    need(pin is None or pin == digest, 'Input hash differs: ' + str(path))
    return json.loads(raw, object_pairs_hook=unique_object,
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError(x))), digest


def by_id(rows, key, label):
    result = {}
    for row in rows:
        identity = key(row)
        need(identity not in result, 'Duplicate ' + label + ': ' + str(identity))
        result[identity] = row
    return result


def summarize(profiles, census, scope):
    need(profiles['version'] == 1 and profiles['offline_only'] is True
         and profiles['activation_ready'] is False, 'Expected inactive offline stats-package export')
    selected = by_id(profiles['rows'], lambda r: (r['map_id'], r['npc_id']), 'package membership')
    candidates = by_id(census, lambda r: r['npc_id'], 'census NPC')
    members = by_id(scope['members'], lambda r: r['npc_id'], 'scope NPC')
    maps = by_id(scope['maps'], lambda r: r['map_id'], 'scope map')
    need(len(scope['map_order']) == len(maps) and set(scope['map_order']) == set(maps),
         'Scope map order must include each map once')
    need(set(maps) == set(profiles['supported_maps']), 'Scope/package map sets differ; reconcile scope first')
    need(all(m == 0 or m in maps for m, _ in selected), 'Selected membership outside supported maps')
    need(profiles['summary']['memberships'] == len(selected)
         and profiles['summary']['unique_npcs'] == len({n for _, n in selected})
         and profiles['summary']['selected_maps'] == len(maps), 'Export summary counts differ')
    expected = set()
    for npc, member in members.items():
        if not member['combat_candidate']:
            continue
        need(npc in candidates and candidates[npc]['combat_candidate'], 'Combat member missing from census')
        need(set(member['map_ids']) == set(candidates[npc]['map_ids']), 'Scope/census NPC maps differ')
        for map_id in member['map_ids']:
            need(map_id in maps, 'NPC outside supported scope maps')
            expected.add((map_id, npc))
        if not member['map_ids'] and member['global_skill_supplement']:
            expected.add((0, npc))
    rows = []
    for map_id, npc in sorted(expected | set(selected)):
        candidate = candidates.get(npc, {})
        row = selected.get((map_id, npc))
        global_fallback = row is None and map_id != 0 and (0, npc) in selected
        if global_fallback:
            row = selected[(0, npc)]
        family = candidate.get('family', 'not_in_earlier_census')
        current = map_id in candidate.get('current_static_map_ids', [])
        record = dict(map_id=map_id, map_name=maps.get(map_id, {}).get('name', 'Global supplement'),
                      npc_id=npc, name48=(row or candidate).get('name48', ''), family=family,
                      membership_basis='current_static' if current else
                      'conditional_global_supplement' if map_id == 0 else 'historical_or_conditional',
                      expected_by_census=(map_id, npc) in expected,
                      package_status='selected_offline' if row else 'missing_profile',
                      package_lookup='global_fallback' if global_fallback else 'exact_membership',
                      runtime_verified=False, hp48=None, hp58=None, hp_status='unavailable',
                      normal_endpoint_min_field58=None, normal_endpoint_max_field58=None,
                      normal_interval58=None, normal_endpoint_order='unavailable', ordered_attacks=[],
                      normal_status='unavailable', normal_baseline_status='not_exported',
                      normal_before_target48=None, normal_before_target58=None, normal_comparisons=[],
                      attributes={}, defenses={}, evidence_blockers=[], special_skill_boundaries=[],
                      unassessed_skill_bindings=[])
        if row is None:
            reason = ('owned_or_scaled_context_requires_separate_source_projection'
                      if family == 'owned_summon_or_siege_fixed_stat_family' else
                      'no_selected_profile_for_expected_membership')
            record['evidence_blockers'].append(reason)
        else:
            source = row['source']
            record['source'] = source
            record['host_template_sha256'] = row['host_template_sha256']
            if candidate and source['raw_record_sha256'] != candidate['source_raw_sha256']:
                record['evidence_blockers'].append('census_and_selected_source_identity_differ')
            fields = by_id(row['fields'], lambda f: f['field'], 'stat field')
            for name in REQUIRED:
                if name not in fields:
                    record['evidence_blockers'].append('missing_exported_field:' + name)
            for name, field in fields.items():
                baseline, reference = field['baseline48'], field['reference58']
                status = ('baseline_unavailable' if baseline is None else
                          'matches' if baseline == reference else 'differs')
                need(field['status'] == status, 'Contradictory field status: ' + name)
            hp = fields.get('max_hp', {})
            record.update(hp48=hp.get('baseline48'), hp58=hp.get('reference58'),
                          hp_status=hp.get('status', 'unavailable'))
            record['attributes'] = {n: fields[n] for n in ATTRIBUTES if n in fields}
            record['defenses'] = {n: fields[n] for n in DEFENSES if n in fields}
            attacks = row['ordered_attacks']
            supported = row['normal_attack_correction_supported']
            need(bool(attacks) == supported, 'Normal support/list inconsistency: ' + str(npc))
            need([a['ordinal'] for a in attacks] == list(range(len(attacks))), 'Unordered attacks')
            minimum, maximum = row['normal_min_damage'], row['normal_max_damage']
            need(minimum >= 0 and maximum >= 0, 'Negative ordinary normal endpoint')
            # Native FUN_140df1660 and production RetailNpcNormalRange order the
            # two endpoints. Preserve authored order too; do not call it corrupt.
            record.update(normal_endpoint_min_field58=minimum, normal_endpoint_max_field58=maximum,
                          normal_interval58=sorted([minimum, maximum]),
                          normal_endpoint_order='reversed' if minimum > maximum else 'ordered',
                          ordered_attacks=attacks,
                          normal_status='calculated_pre_effect_pre_target' if supported else 'no_source_normal')
            if 'normal_before_target48' in row:
                baseline = row['normal_before_target48']
                reference = row['normal_before_target58']
                need(baseline['context'] == 'initialized-template-before-effects-ai-status-target'
                     and baseline['element'] == 'physical', 'Unknown normal baseline context')
                need(len(reference) == len(attacks), 'Calculated normal list differs')
                comparisons = []
                for attack, calculated in zip(attacks, reference):
                    need(all(calculated[k] == attack[k] for k in ('ordinal', 'type', 'weight', 'modifier')),
                         'Calculated normal entry identity differs')
                    if calculated['type'].lower() != 'physical':
                        status = 'attack_element_changed'
                    elif calculated['off'] is not None:
                        status = 'attack_record_shape_changed'
                    elif all(calculated['main'][k] == baseline[k] for k in ('minimum', 'maximum')):
                        status = 'matches'
                    else:
                        status = 'differs'
                    comparisons.append(dict(ordinal=attack['ordinal'], status=status))
                record.update(normal_baseline_status='calculated_before_effects_ai_status_target',
                              normal_before_target48=baseline, normal_before_target58=reference,
                              normal_comparisons=comparisons)
            # Skill mechanics do not block base HP/attributes or normal-attack accounting.
            record['special_skill_boundaries'] = sorted({
                f"{s['skill_id']}:{s['level']}:{s['primary_boundary']}"
                for s in row['declared_skills'] if s['primary_supported'] is False})
            record['unassessed_skill_bindings'] = [
                dict(skill_id=s['skill_id'], level=s['level'], source_bound=s['source_bound'])
                for s in row['declared_skills'] if s['primary_supported'] is None]
            if row['unresolved_source_skill_bindings']:
                record['special_skill_boundaries'].append('unresolved_source_skill_identity_bindings')
        rows.append(record)
    map_summaries = []
    for map_id in scope['map_order'] + [0]:
        these = [r for r in rows if r['map_id'] == map_id]
        selected_rows = [r for r in these if r['package_status'] == 'selected_offline']
        map_summaries.append(dict(map_id=map_id, name=maps.get(map_id, {}).get('name', 'Global supplement'), memberships=len(these),
            selected=len(selected_rows), missing=len(these)-len(selected_rows),
            hp=dict(collections.Counter(r['hp_status'] for r in these)),
            normal=dict(collections.Counter(r['normal_status'] for r in these)),
            normal_comparisons=dict(collections.Counter(c['status'] for r in these for c in r['normal_comparisons'])),
            attributes_differ=sum(any(f['status'] == 'differs' for f in r['attributes'].values()) for r in these),
            defenses_differ=sum(any(f['status'] == 'differs' for f in r['defenses'].values()) for r in these),
            evidence_blocked=sum(bool(r['evidence_blockers']) for r in these),
            special_skill_boundaries=sum(bool(r['special_skill_boundaries']) for r in these)))
    return dict(version=1, offline_only=True, activation_ready=False, runtime_verified=False,
                primary_skill_audit=profiles.get('primary_skill_audit', 'legacy-export-assessment'),
                scope_policy=scope['policy'], scope_limitations=scope['scope_limitations'],
                selected_memberships=len(selected), expected_memberships=len(expected),
                selected_unique_npcs=len({n for _, n in selected}), maps=map_summaries,
                normal_comparison='Where exported, compare calculated ordinary physical 4.8 and per-entry 5.8 '
                                  'main/off-hand base ranges before effects, AI, status and target mitigation. '
                                  'Element and record-shape changes are separate; older exports have no baseline. '
                                  'No final HP-loss or percentage comparison.',
                exception_counts=dict(collections.Counter(b for r in rows for b in r['evidence_blockers'])),
                rows=rows)


def write_report(args):
    receipt, receipt_pin = read_json(args.receipt)
    profiles, profile_pin = read_json(args.profiles, receipt['profiles_sha256'])
    need(receipt['offline_only'] is True and profiles['manifest_sha256'] == receipt['manifest_sha256']
         and profiles['templates48_sha256'] == receipt['templates48_sha256'], 'Receipt/report context differs')
    census, census_pin = read_json(args.census, args.census_sha256)
    scope, scope_pin = read_json(args.scope, args.scope_sha256)
    result = summarize(profiles, census, scope)
    out = args.out.resolve()
    need(not out.is_relative_to(REPO), 'Generated/private reports must be outside checkout')
    need(not out.exists(), 'Output already exists; choose a new directory')
    out.mkdir(parents=True)
    records = result.pop('rows')
    with (out / 'coverage.csv').open('x', encoding='utf-8', newline='') as stream:
        columns = list(records[0]) if records else []
        columns = list(dict.fromkeys(columns + ['source', 'host_template_sha256']))
        writer = csv.DictWriter(stream, columns)
        writer.writeheader()
        for row in records:
            writer.writerow({k: json.dumps(v, sort_keys=True) if isinstance(v, (dict, list)) else v
                             for k, v in row.items()})
    result['inputs'] = {str(p.resolve()): pin for p, pin in [
        (args.profiles, profile_pin), (args.receipt, receipt_pin),
        (args.census, census_pin), (args.scope, scope_pin)]}
    result['manifest_sha256'] = receipt['manifest_sha256']
    result['calculator_sha256'] = profiles['calculator_sha256']
    result['game_server_assembly_sha256'] = profiles['game_server_assembly_sha256']
    result['coverage_sha256'] = hashlib.sha256((out / 'coverage.csv').read_bytes()).hexdigest()
    result['summarizer_sha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    (out / 'summary.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    lines = ['# Offline mob-stat coverage', '',
             'Selected and calculated is not enabled or runtime verified. Special skills are separate from base stats.', '',
             '| Map | Selected | Missing | HP differs | Normal ranges | Evidence blockers | Special skill boundaries |',
             '|---|---:|---:|---:|---:|---:|---:|']
    for m in result['maps']:
        lines.append(f"| {m['name']} ({m['map_id']}) | {m['selected']} | {m['missing']} | "
                     f"{m['hp'].get('differs', 0)} | {m['normal'].get('calculated_pre_effect_pre_target', 0)} | "
                     f"{m['evidence_blocked']} | {m['special_skill_boundaries']} |")
    lines += ['', result['normal_comparison'], '', 'Scope limitations:', '']
    lines += ['- ' + text for text in result['scope_limitations']]
    (out / 'coverage.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('profiles', 'receipt', 'census', 'scope', 'out'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--census-sha256', required=True)
    parser.add_argument('--scope-sha256', required=True)
    args = parser.parse_args()
    try:
        result = write_report(args)
    except (ValueError, KeyError, OSError) as error:
        parser.exit(2, str(error) + '\n')
    print(json.dumps({k: result[k] for k in ('selected_memberships', 'selected_unique_npcs', 'exception_counts')}))


if __name__ == '__main__':
    main()
