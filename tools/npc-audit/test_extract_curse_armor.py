import json
import tempfile
import unittest
from pathlib import Path

import extract_curse_armor as extract


class CurseArmorExtractionTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix='aion-curse-armor-')
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.items = self.root / 'item_armors.xml'
        self.items.write_text('<items><item><id>110100420</id><name>robe</name><fighter>0</fighter></item>'
                              '<item><id>110100421</id><name>robe2</name><max_hp>0</max_hp>'
                              '<max_enchant_value>15</max_enchant_value><Fighter>33</Fighter></item></items>', encoding='utf-8')
        self.host = self.root / 'host.xml'
        self.host.write_text('<items/>', encoding='utf-8')
        rows = ''.join(f'<data><level>{level}</level><attr1>max_hp {level * 24}</attr1></data>'
                       for level in range(1, 16))
        (self.root / 'item_enchanttable.xml').write_text(
            '<tables><item_enchanttable><name>rb_torso</name><start_limitless>16</start_limitless>'
            f'<enchant_attr_list>{rows}</enchant_attr_list></item_enchanttable></tables>', encoding='utf-8')
        self.bindings = self.root / 'bindings.json'
        self.write_bindings()

    def write_bindings(self):
        value = {'result': 'STATIC_DEFAULT_BINDING_COMPARISON', 'retail_sha256': extract.digest(self.items),
                 'host_sha256': extract.digest(self.host), 'items': [
                     {'id': 110100420, 'name': 'robe', 'group': 'rb_torso', 'host_max_enchant': 15},
                     {'id': 110100421, 'name': 'robe2', 'group': 'rb_torso', 'host_max_enchant': 15}]}
        self.bindings.write_text(json.dumps(value), encoding='utf-8')

    def run_extract(self):
        enchant_sha = extract.digest(self.root / 'item_enchanttable.xml')
        return extract.extract(self.bindings, extract.digest(self.bindings), self.root, enchant_sha, self.host, self.root / 'out.json')

    def test_source_values_and_missing_vs_explicit_zero(self):
        result = self.run_extract()
        self.assertEqual((2, 1), (result['items'], result['tables']))
        source = json.loads((self.root / 'out.json').read_bytes())
        self.assertEqual([False, True], [x['max_hp_explicit'] for x in source['items']])
        self.assertEqual([None, 15], [x['retail_max_enchant'] for x in source['items']])
        self.assertEqual([0, 33], [x['class_permits'][1] for x in source['items']])
        self.assertEqual([2, 2], [x['class_present_mask'] for x in source['items']])
        self.assertEqual([0, 0], [x['class_permits'][0] for x in source['items']])
        self.assertEqual(360, source['tables'][0]['hp_levels_1_to_15'][-1])
        with self.assertRaisesRegex(ValueError, 'new file'):
            self.run_extract()

    def test_duplicate_item_is_rejected(self):
        text = self.items.read_text(encoding='utf-8').replace('</items>', '<item><id>110100420</id><name>robe</name></item></items>')
        self.items.write_text(text, encoding='utf-8')
        self.write_bindings()
        with self.assertRaisesRegex(ValueError, 'Duplicate selected'):
            self.run_extract()

    def test_explicit_table_override_and_source_drift_are_visible(self):
        text = self.items.read_text(encoding='utf-8').replace('<name>robe</name>', '<name>robe</name><enchant_name>other</enchant_name>')
        self.items.write_text(text, encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'differ from reviewed'):
            self.run_extract()
        self.write_bindings()
        with self.assertRaisesRegex(ValueError, 'override requires'):
            self.run_extract()

    def test_changed_enchant_source_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'enchant source identity changed'):
            extract.extract(self.bindings, extract.digest(self.bindings), self.root, '0' * 64, self.host, self.root / 'out.json')

    def test_case_variant_duplicate_class_permit_is_rejected(self):
        text = self.items.read_text(encoding='utf-8').replace('<fighter>0</fighter>', '<fighter>0</fighter><FIGHTER>1</FIGHTER>')
        self.items.write_text(text, encoding='utf-8')
        self.write_bindings()
        with self.assertRaisesRegex(ValueError, 'Duplicate retail class permit'):
            self.run_extract()


if __name__ == '__main__':
    unittest.main()
