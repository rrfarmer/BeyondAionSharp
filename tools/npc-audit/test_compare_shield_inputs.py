import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import compare_shield_inputs as comparison


class ShieldInputsTests(unittest.TestCase):
    def test_duplicates_missing_fields_and_region_selection_are_visible(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            host = root / 'host.xml'
            host.write_text('''<skills>
              <skill_template skill_id="1" name="shield"><effects><shield value="42"/></effects></skill_template>
              <skill_template skill_id="1" name="other"><effects><stun/></effects></skill_template>
              <skill_template skill_id="2" name="missing"><effects><shield/></effects></skill_template>
              <skill_template skill_id="3" name="no shield"><effects><stun/></effects></skill_template>
              </skills>''', encoding='utf-8')
            xml_root = root / 'retail'; xml_root.mkdir()
            (xml_root / 'skill_base.xml').write_text('<skills/>', encoding='utf-16')
            regional = xml_root / 'USA'; regional.mkdir()
            regional_file = regional / 'skill_base.xml'
            regional_file.write_text('''<skills>
              <skill_base><id>1</id><name>first</name><effect1_type>Shield</effect1_type><effect1_reserved2 x="1">4</effect1_reserved2></skill_base>
              <skill_base><id>1</id><name>second</name><effect1_type>Shield</effect1_type></skill_base>
              </skills>''', encoding='utf-16')
            output = root / 'result.json'
            command = [sys.executable, str(Path(comparison.__file__)), '--host-skills', str(host),
                       '--retail-xml-root', str(xml_root), '--out', str(output)]
            completed = subprocess.run(command, capture_output=True, text=True, check=True)
            self.assertEqual(2, json.loads(completed.stdout)['host_skills'])
            result = json.loads(output.read_text(encoding='utf-8'))
            self.assertEqual('USA override', result['retail_selection'])
            self.assertEqual(comparison.digest(regional_file.read_bytes()), result['retail_sha256'])
            rows = {row['skill_id']: row for row in result['rows']}
            self.assertEqual('duplicate-id', rows[1]['identity_status'])
            self.assertEqual(2, len(rows[1]['host_records']))
            self.assertEqual(2, len(rows[1]['retail_records']))
            self.assertNotIn('effect1_reserved1', rows[1]['retail_records'][0]['fields'])
            self.assertNotIn('effect1_reserved2', rows[1]['retail_records'][0]['fields'])
            self.assertEqual('effect1_reserved2', rows[1]['retail_records'][0]['non_scalar_fields'][0]['tag'])
            self.assertEqual('missing-retail-id', rows[2]['identity_status'])
            self.assertNotIn(3, rows)
            before = output.read_bytes()
            self.assertNotEqual(0, subprocess.run(command, capture_output=True).returncode)
            self.assertEqual(before, output.read_bytes())


if __name__ == '__main__':
    unittest.main()
