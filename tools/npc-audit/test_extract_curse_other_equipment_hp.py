import json,tempfile,unittest
from pathlib import Path
import extract_curse_other_equipment_hp as extract

class ExtractOtherEquipmentTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.host=self.root/'host.xml';self.arm=self.root/'item_armors.xml';self.acc=self.root/'item_accessories.xml';self.enchant=self.root/'item_enchanttable.xml'
        self.host.write_text('<items><item_template id="115000001" cName="shield_a" item_group="SHIELD" max_enchant="15" />'
            '<item_template id="122000001" cName="ring_a" item_group="RING" /></items>')
        self.arm.write_text('<items><item><id>115000001</id><name>shield_a</name><equipment_slots>sub</equipment_slots></item></items>')
        self.acc.write_text('<items><item><id>122000001</id><name>ring_a</name><equipment_slots>right_or_left_finger</equipment_slots></item></items>')
        rows=''.join('<data><level>'+str(n)+'</level></data>' for n in range(1,16))
        self.enchant.write_text('<item_enchanttables><item_enchanttable><name>shield</name><start_limitless>16</start_limitless>'
            '<enchant_attr_list>'+rows+'</enchant_attr_list></item_enchanttable></item_enchanttables>')
    def tearDown(self):self.tmp.cleanup()
    def run_extract(self,p):return extract.extract(self.root,self.host,extract.sha(self.arm),extract.sha(self.acc),extract.sha(self.enchant),extract.sha(self.host),p)
    def test_six_family_selection_and_deterministic_output(self):
        a=self.root/'a.json';b=self.root/'b.json';self.assertEqual(self.run_extract(a),self.run_extract(b));self.assertEqual(a.read_bytes(),b.read_bytes())
        self.assertEqual([(x['host_group'],x['max_enchant_level']) for x in json.loads(a.read_bytes())['items']],[('SHIELD',15),('RING',0)])
    def test_nonzero_base_or_enchant_is_rejected(self):
        self.arm.write_text(self.arm.read_text().replace('<name>shield_a</name>','<name>shield_a</name><max_hp>1</max_hp>'))
        with self.assertRaisesRegex(ValueError,'nonzero raw'):self.run_extract(self.root/'a.json')
        self.arm.write_text(self.arm.read_text().replace('<max_hp>1</max_hp>',''))
        self.enchant.write_text(self.enchant.read_text().replace('<level>1</level>','<level>1</level><attr1>max_hp 1</attr1>',1))
        with self.assertRaisesRegex(ValueError,'enchant HP'):self.run_extract(self.root/'a.json')
    def test_mismatched_slot_or_identity_stays_out(self):
        self.acc.write_text(self.acc.read_text().replace('right_or_left_finger','neck'))
        result=self.run_extract(self.root/'a.json');self.assertEqual(result['items'],1)
        self.arm.write_text(self.arm.read_text().replace('shield_a','other'))
        with self.assertRaisesRegex(ValueError,'No reviewed'):self.run_extract(self.root/'b.json')
    def test_override_is_excluded_and_hash_mismatch_rejected(self):
        self.arm.write_text(self.arm.read_text().replace('<name>shield_a</name>','<name>shield_a</name><enchant_name>special</enchant_name>'))
        self.assertEqual(self.run_extract(self.root/'a.json')['items'],1)
        with self.assertRaisesRegex(ValueError,'source identity changed'):
            extract.extract(self.root,self.host,'0'*64,extract.sha(self.acc),extract.sha(self.enchant),extract.sha(self.host),self.root/'b.json')
if __name__=='__main__':unittest.main()
