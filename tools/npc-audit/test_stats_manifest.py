import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import stats_manifest as M


class StatsManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='aion-stat-package-test-')
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.output = self.base / 'packages' / 'new'
        self.review_path = self.base / 'review.json'
        self.source = dict(dataset_id='a'*64, output_manifest_sha256='b'*64,
                           profiles=[dict(npc_id=1, source=dict(raw_sha256='c'*64),
                                          raw_record=['npc', [], []], ordered_lists=dict(skills=['retained']))])
        self.comparison = dict(dataset_id='a'*64, manifest_sha256='b'*64, region='USA',
                               statistics_only=True, activation_ready=False, context='fresh-unowned-ordinary-kind1',
                               rows=[dict(npc_id=1, calculation='calculated_reference_under_explicit_context',
                                          host_template_sha256='d'*64, baseline48=dict(level=7),
                                          source=dict(raw_record_sha256='c'*64))])
        self.review = dict(version=1, region='USA', supported_maps=[220010000], cohorts=[dict(
            id='Ishalgen', map_id=220010000, npc_ids=[1], review_basis='synthetic offline fixture',
            profiles=self.asset('source.json', self.source), comparison=self.asset('comparison.json', self.comparison))])

    def asset(self, name, value):
        raw = json.dumps(value).encode()
        (self.base/name).write_bytes(raw)
        return dict(path=name, sha256=hashlib.sha256(raw).hexdigest(), byte_length=len(raw))

    def assemble(self):
        self.review_path.write_text(json.dumps(self.review))
        return M.assemble(self.review_path, self.output)

    def test_relative_assets_rebased_and_all_contexts_preserved_without_activation(self):
        for key in M.CONTEXT_ASSETS:
            self.review[key] = self.asset(key+'.json', dict(fixture=key))
        raw = '<skill/>'.encode('utf-16-le')
        (self.base/'skill.xml').write_bytes(raw)
        self.review['skill_sources'] = [dict(id=7, path='skill.xml', sha256=M.digest(raw))]
        receipt = self.assemble()
        manifest = json.loads(Path(receipt['manifest']['path']).read_bytes())
        self.assertFalse(receipt['activation_performed'])
        self.assertEqual(1, receipt['memberships'])
        for key in M.CONTEXT_ASSETS:
            self.assertEqual(str(self.base/(key+'.json')), manifest[key]['path'])
        self.assertEqual(str(self.base/'skill.xml'), manifest['skill_sources'][0]['path'])
        spec = manifest['cohorts'][0]['profile_artifact']
        self.assertEqual(self.source, json.loads(Path(spec['path']).read_bytes()))
        self.assertEqual(spec['sha256'], M.digest(Path(spec['path']).read_bytes()))
        self.assertFalse(list(self.output.parent.glob('*.staging-*')))

    def test_invalid_or_duplicate_memberships_and_maps_never_publish(self):
        original = copy.deepcopy(self.review)
        cases = [lambda r: r['cohorts'].append(copy.deepcopy(r['cohorts'][0])),
                 lambda r: r['cohorts'][0].update(npc_ids=[1, 1]),
                 lambda r: r['cohorts'][0].update(npc_ids=[True]),
                 lambda r: r['cohorts'][0].update(map_id=-1),
                 lambda r: r.update(supported_maps=[220010000, 220010000]),
                 lambda r: r.update(supported_maps=[220030000])]
        for mutation in cases:
            self.review = copy.deepcopy(original); mutation(self.review)
            with self.subTest(review=self.review), self.assertRaises(ValueError): self.assemble()
            self.assertFalse(self.output.exists())

    def test_duplicate_source_or_calculated_rows_are_not_overwritten(self):
        for key, document, rows in [('profiles', self.source, 'profiles'), ('comparison', self.comparison, 'rows')]:
            with self.subTest(key=key):
                changed = copy.deepcopy(document); changed[rows].append(copy.deepcopy(changed[rows][0]))
                old = self.review['cohorts'][0][key]
                self.review['cohorts'][0][key] = self.asset('duplicate.json', changed)
                with self.assertRaisesRegex(ValueError, 'duplicate'): self.assemble()
                self.assertFalse(self.output.exists())
                self.review['cohorts'][0][key] = old

    def test_required_ordinary_context_and_source_identity_fail_closed(self):
        changes = [dict(statistics_only=False), dict(activation_ready=True), dict(region='EU'),
                   dict(context='owned'), dict(dataset_id='e'*64)]
        for change in changes:
            changed = dict(self.comparison, **change)
            self.review['cohorts'][0]['comparison'] = self.asset('bad-comparison.json', changed)
            with self.subTest(change=change), self.assertRaises(ValueError): self.assemble()
            self.assertFalse(self.output.exists())

    def test_changed_hash_and_duplicate_json_are_visible(self):
        (self.base/'source.json').write_text('{}')
        with self.assertRaisesRegex(ValueError, 'hash'): self.assemble()
        self.assertFalse(self.output.exists())
        with self.assertRaisesRegex(ValueError, 'Duplicate JSON'): M.loads('{"version":1,"version":2}')
        with self.assertRaisesRegex(ValueError, 'Non-finite'): M.loads('{"value":NaN}')

    def test_input_mutation_before_publication_cleans_the_staging_directory(self):
        original = Path.write_bytes
        def mutate(path, raw):
            result = original(path, raw)
            if path.name == 'manifest.json': original(self.base/'source.json', b'{}')
            return result
        with patch.object(Path, 'write_bytes', mutate), self.assertRaisesRegex(ValueError, 'changed during packaging'):
            self.assemble()
        self.assertFalse(self.output.exists())
        self.assertFalse(list(self.output.parent.iterdir()))

    def test_unknown_context_and_output_in_checkout_are_rejected(self):
        self.review['unreviewed_context'] = {}
        with self.assertRaisesRegex(ValueError, 'Unrecognized'): self.assemble()
        self.review.pop('unreviewed_context')
        self.output = M.REPO/'run'/'never-write-stat-package'
        with self.assertRaisesRegex(ValueError, 'outside the checkout'): self.assemble()
        self.assertFalse(self.output.exists())


if __name__ == '__main__': unittest.main()
