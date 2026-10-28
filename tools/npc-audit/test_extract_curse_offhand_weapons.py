import json
import tempfile
import unittest
from pathlib import Path
import extract_curse_offhand_weapons as extract

class ExtractOffhandWeaponsTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.host=self.root/'host.xml';self.source=self.root/'item_weapons.xml'
        self.host.write_text('<items><item_template id="100000001" cName="Sword_A" item_group="SWORD" />'
            '<item_template id="100000002" cName="Sword_B" item_group="SWORD" />'
            '<item_template id="103000001" cName="Tool_A" item_group="SWORD" /></items>')
        self.source.write_text('<items><item><id>100000001</id><name>sword_a</name><equipment_slots>main_or_sub</equipment_slots></item>'
            '<item><id>100000002</id><name>sword_b</name><equipment_slots>main</equipment_slots></item>'
            '<item><id>103000001</id><name>tool_a</name><equipment_slots>main_or_sub</equipment_slots></item></items>')
    def tearDown(self):self.tmp.cleanup()
    def run_extract(self,out):return extract.extract(self.root,self.host,extract.sha(self.source),extract.sha(self.host),out)
    def test_exact_identity_selection_is_deterministic(self):
        a=self.root/'a.json';b=self.root/'b.json';one=self.run_extract(a);two=self.run_extract(b)
        self.assertEqual(one,two);self.assertEqual(a.read_bytes(),b.read_bytes())
        data=json.loads(a.read_bytes());self.assertEqual(data['items'],[{'id':100000001,'name':'sword_a','host_group':'SWORD'}])
        self.assertEqual(data['provenance']['retail_selection'],'USA missing; base fallback')
    def test_region_override_and_hash_are_required(self):
        regional=self.root/'USA';regional.mkdir();(regional/'item_weapons.xml').write_bytes(self.source.read_bytes())
        with self.assertRaisesRegex(ValueError,'identity changed'):
            extract.extract(self.root,self.host,'0'*64,extract.sha(self.host),self.root/'x.json')
        result=self.run_extract(self.root/'x.json');self.assertEqual(result['items'],1)
        self.assertEqual(json.loads((self.root/'x.json').read_bytes())['provenance']['retail_selection'],'USA override')
    def test_duplicate_source_id_and_overwrite_are_rejected(self):
        self.source.write_text('<items><item><id>100000001</id><name>sword_a</name><equipment_slots>main_or_sub</equipment_slots></item>'
            '<item><id>100000001</id><name>sword_a</name><equipment_slots>main_or_sub</equipment_slots></item></items>')
        with self.assertRaisesRegex(ValueError,'Duplicate retail weapon ID'):self.run_extract(self.root/'x.json')
        self.source.write_text('<items><item><id>100000001</id><name>sword_a</name><equipment_slots>main_or_sub</equipment_slots></item></items>')
        self.run_extract(self.root/'x.json')
        with self.assertRaisesRegex(ValueError,'new file'):self.run_extract(self.root/'x.json')
    def test_mismatched_name_is_not_accepted(self):
        self.source.write_text('<items><item><id>100000001</id><name>other</name><equipment_slots>main_or_sub</equipment_slots></item></items>')
        with self.assertRaisesRegex(ValueError,'No reviewed'):self.run_extract(self.root/'x.json')

if __name__=='__main__':unittest.main()
