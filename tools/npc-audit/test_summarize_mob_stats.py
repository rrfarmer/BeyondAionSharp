import copy
import hashlib
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest

from summarize_mob_stats import REQUIRED, read_json, summarize, write_report


class MobStatCoverageTests(unittest.TestCase):
    def setUp(self):
        self.census = [dict(npc_id=1, combat_candidate=True, map_ids=[220010000],
                            current_static_map_ids=[220010000], name48='test mob',
                            family='ordinary_or_scripted_mob_candidate', source_raw_sha256='a'*64)]
        self.scope = dict(policy='fixture scope', scope_limitations=['Conditional memberships are not spawns.'],
                          map_order=[220010000], maps=[dict(map_id=220010000, name='Ishalgen')],
                          members=[dict(npc_id=1, combat_candidate=True, map_ids=[220010000],
                                        global_skill_supplement=False)])
        self.row = dict(map_id=220010000, npc_id=1, name48='test mob',
                        source=dict(raw_record_sha256='a'*64), host_template_sha256='b'*64,
                        fields=[dict(field=f, baseline48=0, reference58=0,
                                     status='matches', source_presence='ExplicitZero') for f in REQUIRED],
                        ordered_attacks=[dict(ordinal=0, type='magical_Fire', weight=1000, modifier=100)],
                        normal_min_damage=7, normal_max_damage=9, normal_attack_correction_supported=True,
                        declared_skills=[], unresolved_source_skill_bindings=[])
        self.profiles = dict(version=1, offline_only=True, activation_ready=False,
                             supported_maps=[220010000], rows=[self.row],
                             summary=dict(memberships=1, unique_npcs=1, selected_maps=1),
                             manifest_sha256='c'*64, templates48_sha256='d'*64,
                             calculator_sha256='e'*64, game_server_assembly_sha256='f'*64)

    def report(self):
        return summarize(self.profiles, self.census, self.scope)

    def test_zero_preserved_and_attack_range_not_claimed_as_comparison(self):
        report = self.report()
        row = report['rows'][0]
        self.assertEqual((0, 0, 'matches'), (row['hp48'], row['hp58'], row['hp_status']))
        self.assertEqual('ExplicitZero', row['attributes']['str']['source_presence'])
        self.assertEqual('not_exported', row['normal_baseline_status'])
        self.assertEqual([7, 9], row['normal_interval58'])
        self.assertEqual('magical_Fire', row['ordered_attacks'][0]['type'])
        self.assertFalse(report['runtime_verified'])
        self.assertFalse(report['activation_ready'])

    def test_special_skills_do_not_block_base_stats(self):
        self.row['declared_skills'] = [dict(skill_id=16596, level=1,
                                          primary_supported=False, primary_boundary='complex-curse')]
        row = self.report()['rows'][0]
        self.assertEqual(['16596:1:complex-curse'], row['special_skill_boundaries'])
        self.assertEqual([], row['evidence_blockers'])
        self.assertEqual('selected_offline', row['package_status'])

    def test_unassessed_primary_skills_are_reported_without_inventing_a_failure(self):
        self.profiles['primary_skill_audit'] = 'not-performed-outside-mob-stat-scope'
        self.row['declared_skills'] = [dict(skill_id=16596, level=1, source_bound=True,
                                          primary_supported=None, primary_boundary='not-assessed-by-mob-stat-audit')]
        report = self.report()
        row = report['rows'][0]
        self.assertEqual([], row['special_skill_boundaries'])
        self.assertEqual([dict(skill_id=16596, level=1, source_bound=True)], row['unassessed_skill_bindings'])
        self.assertEqual(self.profiles['primary_skill_audit'], report['primary_skill_audit'])
        self.assertEqual([], row['evidence_blockers'])

    def test_missing_owned_and_ordinary_profiles_remain_separate(self):
        self.profiles['rows'] = []
        self.profiles['summary'].update(memberships=0, unique_npcs=0)
        row = self.report()['rows'][0]
        self.assertEqual(['no_selected_profile_for_expected_membership'], row['evidence_blockers'])
        self.census[0]['family'] = 'owned_summon_or_siege_fixed_stat_family'
        row = self.report()['rows'][0]
        self.assertEqual(['owned_or_scaled_context_requires_separate_source_projection'], row['evidence_blockers'])
        self.assertEqual('unavailable', row['hp_status'])

    def test_source_drift_and_missing_fields_are_visible(self):
        self.row['source']['raw_record_sha256'] = 'x'*64
        self.row['fields'] = [f for f in self.row['fields'] if f['field'] != 'magical_defend']
        self.assertEqual(['census_and_selected_source_identity_differ',
                          'missing_exported_field:magical_defend'], self.report()['rows'][0]['evidence_blockers'])

    def test_duplicates_contradictions_and_summary_drift_fail(self):
        for mutation in ('membership', 'field', 'status', 'summary', 'map_order'):
            with self.subTest(mutation=mutation):
                profiles, scope = copy.deepcopy(self.profiles), copy.deepcopy(self.scope)
                if mutation == 'membership':
                    profiles['rows'].append(copy.deepcopy(self.row))
                elif mutation == 'field':
                    profiles['rows'][0]['fields'].append(copy.deepcopy(self.row['fields'][0]))
                elif mutation == 'status':
                    profiles['rows'][0]['fields'][0]['status'] = 'differs'
                elif mutation == 'summary':
                    profiles['summary']['memberships'] = 2
                else:
                    scope['map_order'].append(220010000)
                with self.assertRaises(ValueError):
                    summarize(profiles, self.census, scope)

    def test_multiple_normals_stay_ordered_and_absent_normal_is_not_a_match(self):
        self.row['ordered_attacks'].append(dict(ordinal=1, type='physical', weight=100, modifier=80))
        self.assertEqual(['magical_Fire', 'physical'], [a['type'] for a in self.report()['rows'][0]['ordered_attacks']])
        self.row['ordered_attacks'] = []
        self.row['normal_attack_correction_supported'] = False
        self.assertEqual('no_source_normal', self.report()['rows'][0]['normal_status'])
        self.row['normal_attack_correction_supported'] = True
        with self.assertRaisesRegex(ValueError, 'inconsistency'):
            self.report()

    def test_reversed_native_endpoints_preserved_with_ordered_interval(self):
        self.row['normal_min_damage'], self.row['normal_max_damage'] = 5466, 3345
        row = self.report()['rows'][0]
        self.assertEqual([3345, 5466], row['normal_interval58'])
        self.assertEqual((5466, 3345), (row['normal_endpoint_min_field58'], row['normal_endpoint_max_field58']))
        self.assertEqual('reversed', row['normal_endpoint_order'])
        self.assertEqual([], row['evidence_blockers'])

    def test_global_catalog_fallback_does_not_invent_a_spawn(self):
        self.row['map_id'] = 0
        report = self.report()
        local = next(r for r in report['rows'] if r['map_id'] == 220010000)
        supplement = next(r for r in report['rows'] if r['map_id'] == 0)
        self.assertEqual('global_fallback', local['package_lookup'])
        self.assertEqual('current_static', local['membership_basis'])
        self.assertEqual('conditional_global_supplement', supplement['membership_basis'])
        self.assertEqual(1, report['selected_memberships'])

    def test_calculated_baseline_compares_same_stage_without_masking_elements_or_dual(self):
        self.row['normal_before_target48'] = dict(context='initialized-template-before-effects-ai-status-target',
                                                  element='physical', minimum=8.8, maximum=13.2)
        calculated = dict(ordinal=0, type='physical', weight=1000, modifier=100,
                          main=dict(minimum=7, maximum=9), off=None)
        self.row['ordered_attacks'][0]['type'] = 'physical'
        self.row['normal_before_target58'] = [calculated]
        self.assertEqual('differs', self.report()['rows'][0]['normal_comparisons'][0]['status'])
        calculated['main'] = dict(minimum=8.8, maximum=13.2)
        self.assertEqual('matches', self.report()['rows'][0]['normal_comparisons'][0]['status'])
        calculated['off'] = dict(minimum=7, maximum=9)
        self.assertEqual('attack_record_shape_changed', self.report()['rows'][0]['normal_comparisons'][0]['status'])
        calculated['type'] = self.row['ordered_attacks'][0]['type'] = 'magical_Fire'
        self.assertEqual('attack_element_changed', self.report()['rows'][0]['normal_comparisons'][0]['status'])
        calculated['ordinal'] = 2
        with self.assertRaisesRegex(ValueError, 'identity differs'):
            self.report()

    def test_pinned_inputs_and_new_external_output(self):
        # The first CSV membership lacks a profile; later selected rows still
        # need the explicit unassessed-skill column.
        self.census.append(dict(self.census[0], npc_id=2))
        self.scope['members'].append(dict(self.scope['members'][0], npc_id=2))
        self.row['npc_id'] = 2
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            def asset(name, value):
                path = base / name
                raw = json.dumps(value).encode()
                path.write_bytes(raw)
                return path, hashlib.sha256(raw).hexdigest()
            profiles, pin = asset('profiles.json', self.profiles)
            receipt, _ = asset('receipt.json', dict(offline_only=True, profiles_sha256=pin,
                               manifest_sha256='c'*64, templates48_sha256='d'*64))
            census, census_pin = asset('census.json', self.census)
            scope, scope_pin = asset('scope.json', self.scope)
            args = SimpleNamespace(profiles=profiles, receipt=receipt, census=census,
                                   census_sha256=census_pin, scope=scope, scope_sha256=scope_pin,
                                   out=base/'report')
            report = write_report(args)
            self.assertEqual(1, report['selected_memberships'])
            self.assertTrue((args.out/'coverage.csv').is_file())
            self.assertIn('unassessed_skill_bindings', (args.out/'coverage.csv').read_text().splitlines()[0])
            with self.assertRaisesRegex(ValueError, 'already exists'):
                write_report(args)
            profiles.write_text('{}', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'hash differs'):
                write_report(args)
            path = base/'duplicate.json'
            path.write_text('{"id":1,"id":2}', encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Duplicate JSON'):
                read_json(path)


if __name__ == '__main__':
    unittest.main()
