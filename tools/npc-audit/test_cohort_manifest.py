import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from cohort_manifest import assemble


class CohortManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)

        def asset(name, value):
            path = self.base / name
            raw = json.dumps(value).encode()
            path.write_bytes(raw)
            return {'path': str(path), 'sha256': hashlib.sha256(raw).hexdigest(), 'byte_length': len(raw)}

        self.asset = asset
        record = '1' * 64
        evidence = asset('normal-evidence.json', {'fixture': 'packing-only, not native admission'})
        self.motions = asset('motions.json', {
            'source_context': {'npc_region': 'USA', 'dataset_id': 'a' * 64, 'manifest_sha256': 'b' * 64},
            'members': [{'map_id': 220010000, 'npc_id': 210649, 'source_record_sha256': record,
                         'source_motion_blockers': [], 'skill_motions': [],
                         'normal_motion': {'variant_count': 1, 'first_hit_ms': 300, 'second_hit_ms': None,
                                           'evidence_asset': evidence}}], 'spell_sources': {}})
        profiles = asset('profiles.json', {'version': 1, 'map_id': 220010000, 'dataset_id': 'a' * 64,
                         'output_manifest_sha256': 'b' * 64, 'profiles': [{'npc_id': 210649}]})
        comparison = asset('comparison.json', {'rows': [{'npc_id': 210649,
            'calculation': 'calculated_reference_under_explicit_context', 'host_template_sha256': '2' * 64,
            'source': {'raw_record_sha256': record}, 'host_ai': 'aggressive', 'baseline48': {'level': 2}}]})
        self.review = {'version': 1, 'region': 'USA', 'maps': asset('maps.json', [{'map_id': 220010000}]),
                       'motions': self.motions, 'cohorts': [{'id': 'packing-fixture', 'map_id': 220010000,
                       'domain': 'fresh-unowned-static-ground-kind1-v1', 'review_basis': {'fixture': True},
                       'npc_ids': [210649], 'profiles': profiles, 'comparison': comparison}]}

    def run_review(self, review=None):
        path = self.base / 'review.json'
        path.write_text(json.dumps(review or self.review), encoding='utf-8')
        return assemble(path, self.base / 'output')

    def test_assembly_is_not_activation_and_does_not_modify_inputs(self):
        before = {p: p.read_bytes() for p in self.base.iterdir()}
        result = self.run_review()
        self.assertFalse(result['enabled'])
        self.assertEqual(1, result['memberships'])
        manifest = json.loads(Path(result['manifest']['path']).read_text(encoding='utf-8'))
        normal = manifest['cohorts'][0]['members'][0]['normal_motion']
        self.assertIsNone(normal['second_hit_ms'])
        for path, raw in before.items():
            self.assertEqual(raw, path.read_bytes())
        with self.assertRaises(FileExistsError):
            self.run_review()

    def test_changed_reviewed_input_is_rejected_before_output_creation(self):
        Path(self.motions['path']).write_text('{}', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'hash differs'):
            self.run_review()
        self.assertFalse((self.base / 'output').exists())

    def test_overlapping_memberships_are_rejected(self):
        review = copy.deepcopy(self.review)
        extra = copy.deepcopy(review['cohorts'][0]); extra['id'] = 'overlapping'
        review['cohorts'].append(extra)
        with self.assertRaisesRegex(ValueError, 'Overlapping'):
            self.run_review(review)

    def test_unreviewed_owner_or_scaling_domain_is_not_assumed_ordinary(self):
        review = copy.deepcopy(self.review)
        review['cohorts'][0]['domain'] = 'owned-pet'
        with self.assertRaisesRegex(ValueError, 'supported owner'):
            self.run_review(review)


if __name__ == '__main__':
    unittest.main()
