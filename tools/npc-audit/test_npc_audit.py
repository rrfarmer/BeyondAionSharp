import argparse
import contextlib
import csv
import io
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import npc_audit as audit
from extract_retail_npc_profiles import canonical, semantic_sha


class IndexTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='aion-npc-audit-test-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.dataset = self.root / 'dataset'; self.dataset.mkdir()
        raw = ['npc', [], [['id', [], ['1']], ['name', [], ['SyntheticNpc']]]]
        self.profile = {'npc_id': 1, 'dataset_id': 'a' * 64, 'devname': 'SyntheticNpc',
                        'binding48': {'devnames48': ['SyntheticNpc']}, 'raw_record': raw,
                        'source': {'semantic_sha256': semantic_sha(raw)},
                        'scalar_fields': {'zero': {'presence': 'explicit_zero', 'raw_values': ['0']},
                                          'missing': {'presence': 'absent', 'raw_values': []}}}
        for name in audit.ARTIFACTS:
            (self.dataset / name).write_bytes(b'[]\n')
        with (self.dataset / 'coverage.csv').open('w', encoding='utf-8', newline='') as f:
            writer = csv.writer(f); writer.writerow(['npc_id', 'status', 'family', 'devnames48', 'retail_devname'])
            writer.writerow([1, 'exported_static_source_only', 'ordinary', 'SyntheticNpc', 'SyntheticNpc'])
        (self.dataset / 'profiles.jsonl').write_bytes(canonical(self.profile) + b'\n')
        (self.dataset / 'provenance.json').write_bytes(canonical({'selection': [{'region': 'USA'}]}))
        (self.dataset / 'summary.json').write_bytes(canonical({'dataset_id': 'a' * 64, 'exported_profiles': 1, 'target_count': 1}))
        (self.dataset / 'validation.json').write_bytes(canonical({'every_target_has_coverage_row': True,
                    'profile_ids_exactly_supported_and_identity_matched': True}))
        self.ratio = self.root / 'ratio.xml'; self.ratio.write_bytes(b'<synthetic/>')
        self.target = self.root / 'profiles.sqlite'
        self.publish_manifest()

    def test_stat_commands_do_not_import_optional_equipment_experiments(self):
        import builtins
        original = builtins.__import__
        def guarded(name, *args, **kwargs):
            if name == 'armor_enchant':
                raise AssertionError('Stat command imported an optional equipment experiment')
            return original(name, *args, **kwargs)
        with patch.object(builtins, '__import__', guarded), patch.object(audit, 'stats_package') as run:
            self.assertEqual(0, audit.main(['stats-package', '--manifest', str(self.ratio),
                '--manifest-sha256', 'a'*64, '--out', str(self.root/'unused')]))
            run.assert_called_once()

    def test_package_audit_rejects_changed_source_before_build_or_output(self):
        args=argparse.Namespace(manifest=self.ratio,manifest_sha256='0'*64,templates=self.ratio,
                                out=self.root/'package-report',no_build=False)
        with patch.object(audit,'ensure_dll_window'),patch.object(audit.subprocess,'run') as process:
            with self.assertRaisesRegex(audit.AuditError,'supplied pin'):
                audit.stats_package(args)
            process.assert_not_called()
        self.assertFalse(args.out.exists())

    def test_package_audit_cannot_write_under_its_source_assets(self):
        args=argparse.Namespace(manifest=self.ratio,manifest_sha256=audit.sha_file(self.ratio),templates=self.ratio,
                                out=self.root/'report',no_build=False)
        with patch.object(audit,'ensure_dll_window'),patch.object(audit.subprocess,'run') as process:
            with self.assertRaisesRegex(audit.AuditError,'overlaps'):
                audit.stats_package(args)
            process.assert_not_called()
        self.assertFalse(args.out.exists())

    def publish_manifest(self):
        rows = [{'file': n, 'bytes': (self.dataset / n).stat().st_size, 'sha256': audit.sha_file(self.dataset / n)}
                for n in sorted(audit.ARTIFACTS)]
        (self.dataset / 'output-manifest.json').write_bytes(canonical(rows))

    def arguments(self):
        return argparse.Namespace(dataset=self.dataset, manifest_sha256=audit.sha_file(self.dataset / 'output-manifest.json'),
            index=self.target, region='USA', ratio=self.ratio, ratio_sha256=audit.sha_file(self.ratio),
            ratio_relative='pc_stat_ratio.xml', ratio_reason='synthetic_test_only', inventory=None, identity_review=None, identity_review_sha256=None)

    def build(self):
        with contextlib.redirect_stdout(io.StringIO()):
            audit.index(self.arguments())

    def test_index_keeps_offsets_and_zero_presence_without_modifying_inputs(self):
        before = {p.name: p.read_bytes() for p in self.dataset.iterdir()}
        self.build()
        db, meta = audit.open_index(self.target)
        self.addCleanup(db.close)
        offset, length, digest = db.execute('SELECT offset,length,sha256 FROM profiles').fetchone()
        data = (self.dataset / 'profiles.jsonl').read_bytes()[offset:offset+length]
        self.assertEqual('explicit_zero', audit.loads(data)['scalar_fields']['zero']['presence'])
        self.assertEqual('absent', audit.loads(data)['scalar_fields']['missing']['presence'])
        self.assertEqual(before, {p.name: p.read_bytes() for p in self.dataset.iterdir()})

    def test_pinned_manifest_and_artifact_corruption_rejected(self):
        args = self.arguments(); args.manifest_sha256 = '0' * 64
        with self.assertRaisesRegex(audit.AuditError, 'manifest SHA256'):
            audit.index(args)
        (self.dataset / 'profiles.jsonl').write_bytes(b'corruption')
        with self.assertRaisesRegex(audit.AuditError, 'size differs'):
            self.build()

    def test_ordered_source_semantic_corruption_rejected(self):
        self.profile['raw_record'][2].reverse()
        (self.dataset / 'profiles.jsonl').write_bytes(canonical(self.profile) + b'\n')
        self.publish_manifest()
        with self.assertRaisesRegex(audit.AuditError, 'semantic hash differs'):
            self.build()
        self.assertFalse(Path(str(self.target) + '.receipt.json').exists())

    def test_duplicate_json_and_nonfinite_values_rejected(self):
        for raw in ['{"id":1,"id":2}', '{"value":NaN}']:
            with self.assertRaises(audit.AuditError):
                audit.loads(raw)

    def test_index_tampering_rejected(self):
        self.build()
        with contextlib.closing(sqlite3.connect(self.target)) as db, db:
            db.execute("UPDATE coverage SET family='tampered'")
        with self.assertRaisesRegex(audit.AuditError, 'Index hash differs'):
            audit.open_index(self.target)

    def test_same_timestamp_metadata_tampering_rejected(self):
        self.build()
        path = self.dataset / 'summary.json'; stat = path.stat()
        path.write_bytes(path.read_bytes().replace(b'a'*64, b'b'*64))
        os.utime(path, ns=(stat.st_atime_ns, stat.st_mtime_ns))
        with self.assertRaisesRegex(audit.AuditError, 'metadata hash changed'):
            audit.open_index(self.target)

    def test_same_timestamp_selected_record_tampering_rejected_before_calculator(self):
        self.build()
        path = self.dataset / 'profiles.jsonl'; stat = path.stat()
        path.write_bytes(path.read_bytes().replace(b'"npc_id":1', b'"npc_id":2'))
        os.utime(path, ns=(stat.st_atime_ns, stat.st_mtime_ns))
        args = argparse.Namespace(index=self.target, npc=['1'], cohort=None, family=None, map=None,
            limit=None, offset=0, out=self.root/'report', templates=self.ratio, no_build=True)
        with patch.object(audit, 'ensure_dll_window'), patch.object(audit.subprocess, 'run') as calculator:
            with self.assertRaisesRegex(audit.AuditError, 'Selected profile bytes changed'):
                audit.compare(args)
            calculator.assert_not_called()
        self.assertFalse(args.out.exists())

    def test_outputs_cannot_overwrite_index_or_enter_checkout(self):
        self.build()
        with self.assertRaisesRegex(audit.AuditError, 'already exists'):
            self.build()
        with self.assertRaisesRegex(audit.AuditError, 'outside the checkout'):
            audit.external_destination(audit.REPO / 'audit-output.json')
        with self.assertRaisesRegex(audit.AuditError, 'overlaps'):
            audit.external_destination(self.dataset / 'report', [self.dataset])

    def test_duplicate_profile_and_coverage_identity_rejected(self):
        with (self.dataset / 'profiles.jsonl').open('ab') as f:
            f.write(canonical(self.profile) + b'\n')
        self.publish_manifest()
        with self.assertRaises(sqlite3.IntegrityError):
            self.build()

    def test_sim_ownership_prevents_build_and_calculation(self):
        marker = self.root/'repo/run/.sim-running/another-owner.json'
        marker.parent.mkdir(parents=True); marker.write_text('{}')
        with patch.object(audit, 'REPO', self.root/'repo'), patch.object(audit.subprocess, 'check_output') as process:
            with self.assertRaisesRegex(audit.AuditError, 'SIM owns'):
                audit.ensure_dll_window()
            process.assert_not_called()

    def test_partial_review_arguments_rejected(self):
        args = self.arguments();args.identity_review_sha256='0'*64
        with self.assertRaisesRegex(audit.AuditError,'supplied together'):
            audit.index(args)


@unittest.skipUnless(os.environ.get('NPC_AUDIT_PRIVATE_INDEX') and os.environ.get('NPC_AUDIT_EXPECTED'),
                     'Requires external indexed extraction and retained25 acceptance fixture; no raw retail data is checked in')
class PrivateCalculatorTests(unittest.TestCase):
    def run_comparison(self, selector, root):
        result = subprocess.run([sys.executable, '-B', str(Path(audit.__file__)), 'compare',
            '--index', os.environ['NPC_AUDIT_PRIVATE_INDEX'], '--out', str(root), '--no-build', *selector],
            cwd=audit.REPO, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return audit.loads((root / 'comparison.json').read_bytes())

    def test_sematariux_comparison_surfaces_direct_ai_stat_hook(self):
        with tempfile.TemporaryDirectory(prefix='aion-npc-audit-ai-') as directory:
            root = Path(directory) / 'report'
            report = self.run_comparison(['--npc', '216520'], root)
            row = report['rows'][0]
            self.assertEqual('sematariux', row['host_ai'])
            self.assertEqual('direct_override_found', row['host_ai_stat_hook']['status'])
            handlers = row['host_ai_stat_hook']['handlers']
            self.assertEqual(1, len(handlers))
            self.assertIn('MAXHP', handlers[0]['referenced_stats'])
            self.assertIn('PHYSICAL_ATTACK', handlers[0]['referenced_stats'])
            self.assertIn('SematariuxAI.cs', (root / 'comparison.md').read_text(encoding='utf-8'))
            self.assertIn('conditions and actual spawn AI are not evaluated', (root / 'comparison.md').read_text(encoding='utf-8'))
            with (root / 'comparison.csv').open(encoding='utf-8', newline='') as stream:
                row = next(csv.DictReader(stream))
            self.assertEqual('direct_override_found', row['host_ai_stat_hook_status'])
            self.assertIn('MAXHP', row['host_ai_referenced_stats'])

    def test_all25_known_hp_damage_attributes_timing_order_and_repeatability(self):
        expected = audit.loads(Path(os.environ['NPC_AUDIT_EXPECTED']).read_bytes())
        with tempfile.TemporaryDirectory(prefix='aion-npc-audit-private-') as directory:
            root = Path(directory)
            a = self.run_comparison(['--cohort', 'three-keys'], root / 'a')
            b = self.run_comparison(['--cohort', 'three-keys'], root / 'b')
            self.assertEqual(a, b)
            self.assertEqual((root/'a/comparison.csv').read_bytes(), (root/'b/comparison.csv').read_bytes())
            self.assertEqual(25, a['summary']['calculable']); self.assertEqual(0, a['summary']['blocked'])
            rows = {row['npc_id']: row for row in a['rows']}; directions = []
            for original in expected['npcs']:
                row = rows[original['npc_id']]; fields = {f['field']: f for f in row['fields']}; e = original['expected']
                self.assertEqual(original['old_hp'], fields['max_hp']['baseline48'])
                for field, key in [('max_hp','hp'), ('level','level'), ('normal_min_damage','normal_min'),
                                   ('normal_max_damage','normal_max'), ('attack_delay_ms','delay_ms'), ('hp_regen','hp_regen')]:
                    self.assertEqual(e[key], fields[field]['reference58'], (row['npc_id'],field))
                self.assertEqual(e['element'], row['attacks58'][0]['type'])
                self.assertEqual(e['ordered_skill_count'], len(row['skills58']))
                self.assertEqual(list(range(len(row['skills58']))), [s['ordinal'] for s in row['skills58']])
                self.assertEqual(0, fields['magical_defend']['reference58'])
                self.assertGreater(fields['magical_defend']['baseline48'],0)  # Production loader filled the raw zero.
                self.assertEqual(100,fields['str']['baseline48'])
                self.assertTrue(all(fields[k]['reference58']==0 for k in ['str','vit','agi','dex','kno','will']))
                self.assertFalse(row['activation_ready'])
                self.assertEqual(original['source_record_sha256'],row['source']['raw_record_sha256'])
                directions.append((e['hp'] > original['old_hp']) - (e['hp'] < original['old_hp']))
            self.assertEqual({-1:10,0:14,1:1}, dict(audit.collections.Counter(directions)))
            skills = [s for row in rows.values() for s in row['skills58']]
            self.assertEqual(59, len(skills)); self.assertEqual(5, sum(not s['count_present'] for s in skills))
            self.assertEqual(45, sum(not s['delay_present'] for s in skills))

    def test_nonpilot_ids_and_missing_identities_remain_explicit(self):
        with tempfile.TemporaryDirectory(prefix='aion-npc-audit-general-') as directory:
            report = self.run_comparison(['--npc','200000,203752,212040,260000,999999'], Path(directory)/'report')
            self.assertEqual([200000,203752,212040,260000,999999], [r['npc_id'] for r in report['rows']])
            rows = {row['npc_id']:row for row in report['rows']}
            self.assertEqual('blocked', rows[999999]['calculation'])
            self.assertTrue(rows[999999]['blockers'])
            if rows[260000]['calculation']=='blocked':self.assertTrue(rows[260000]['blockers'])
            else:self.assertTrue(rows[260000]['identity_binding']['reviewed_same_id'])
            self.assertTrue(any(rows[n]['calculation'] != 'blocked' for n in (200000,203752,212040)))
            with (Path(directory)/'report/comparison.csv').open(encoding='utf-8') as f:
                self.assertEqual(set(rows),{int(r['npc_id']) for r in csv.DictReader(f)})
            db,meta=audit.open_index(Path(os.environ['NPC_AUDIT_PRIVATE_INDEX']));db.close()
            if meta.get('identity_review_base64'):
                self.assertTrue(rows[260000]['identity_binding']['reviewed_same_id'])

    def test_large_family_batch_is_explicit_and_has_next_page(self):
        with tempfile.TemporaryDirectory(prefix='aion-npc-audit-page-') as directory:
            report = self.run_comparison(['--family','ordinary_or_scripted_mob_candidate','--limit','3','--offset','1'],Path(directory)/'report')
            self.assertGreater(report['selection']['matched'],1024)
            self.assertEqual(3,report['selection']['returned'])
            self.assertEqual(4,report['selection']['next_offset'])


if __name__ == '__main__':
    unittest.main()
