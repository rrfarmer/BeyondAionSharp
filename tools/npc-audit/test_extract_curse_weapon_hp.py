import tempfile,unittest
from pathlib import Path
import extract_curse_weapon_hp as extract

class ExtractWeaponHpTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.host=self.root/'host.xml';self.weapons=self.root/'item_weapons.xml';self.enchants=self.root/'item_enchanttable.xml'
        self.host.write_text('<items><item_template id="100000001" cName="sword_a" item_group="SWORD" max_enchant="15" />'
            '<item_template id="100000002" cName="sword_b" item_group="SWORD" max_enchant="16" /></items>')
        self.weapons.write_text('<items><item><id>100000001</id><name>SWORD_A</name><equipment_slots>main_or_sub</equipment_slots></item>'
            '<item><id>100000002</id><name>sword_b</name><equipment_slots>main</equipment_slots></item></items>')
        rows=''.join('<data><level>'+str(n)+'</level></data>' for n in range(1,16))
        tables=''.join('<item_enchanttable><name>'+name+'</name><start_limitless>16</start_limitless>'
            '<enchant_attr_list>'+rows+'</enchant_attr_list></item_enchanttable>' for _,_,name in extract.RANGES)
        self.enchants.write_text('<item_enchanttables>'+tables+'</item_enchanttables>')
    def tearDown(self):self.tmp.cleanup()
    def run_extract(self,out):return extract.extract(self.root,self.host,extract.sha(self.weapons),extract.sha(self.enchants),extract.sha(self.host),out)
    def test_identity_caps_and_reproducibility(self):
        a=self.root/'a.json';b=self.root/'b.json';one=self.run_extract(a);two=self.run_extract(b)
        self.assertEqual(one,two);self.assertEqual(one['items'],1);self.assertEqual(a.read_bytes(),b.read_bytes())
    def test_nonzero_enchant_hp_fails_closed(self):
        self.enchants.write_text(self.enchants.read_text().replace('<level>1</level>','<level>1</level><attr1>max_hp 1</attr1>',1))
        with self.assertRaisesRegex(ValueError,'enchant HP'):self.run_extract(self.root/'x.json')
    def test_nonzero_raw_hp_fails_closed(self):
        self.weapons.write_text(self.weapons.read_text().replace('<name>SWORD_A</name>','<name>SWORD_A</name><max_hp>1</max_hp>'))
        with self.assertRaisesRegex(ValueError,'nonzero weapon base HP'):self.run_extract(self.root/'x.json')
    def test_overrides_and_name_mismatch_remain_unreviewed(self):
        self.weapons.write_text(self.weapons.read_text().replace('<name>SWORD_A</name>','<name>SWORD_A</name><enchant_name>special</enchant_name>'))
        with self.assertRaisesRegex(ValueError,'No reviewed'):self.run_extract(self.root/'x.json')
        self.weapons.write_text(self.weapons.read_text().replace('<name>SWORD_A</name><enchant_name>special</enchant_name>','<name>other</name>'))
        with self.assertRaisesRegex(ValueError,'No reviewed'):self.run_extract(self.root/'x.json')
    def test_changed_source_hash_is_rejected(self):
        with self.assertRaisesRegex(ValueError,'source identity changed'):
            extract.extract(self.root,self.host,'0'*64,extract.sha(self.enchants),extract.sha(self.host),self.root/'x.json')
if __name__=='__main__':unittest.main()
