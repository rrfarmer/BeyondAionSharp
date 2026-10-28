import json
import tempfile
import unittest
from pathlib import Path
import extract_curse_authorization_hp as subject

class AuthorizationExtractionTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.root=Path(self.temp.name)
        self.arm=self.root/'item_armors.xml'; self.acc=self.root/'item_accessories.xml'
        self.table=self.root/'item_authorizetable.xml'; self.reviewed=self.root/'reviewed.json'
        self.configs=[self.root/'config.xml',self.root/'common.xml']
        for c in self.configs:c.write_text('<config/>')
        self.arm.write_text('<items><item><id>115000001</id><name>shield_a</name><authorize_name>special</authorize_name></item></items>')
        self.acc.write_text('<items><item><id>122000001</id><name>ring_a</name></item></items>')
        self.table.write_text('<tables>'
            '<item_authorizetable><name>special</name><start_limitless>3</start_limitless><enchant_attr_list>'
            '<data><level>1</level><attr1>Max_Hp 25</attr1><attr2>Max_Hp_O 500</attr2></data><data><level>2</level><attr1>Max_Hp 0</attr1></data>'
            '<data><level>3</level><attr1>Max_Hp 99</attr1></data></enchant_attr_list>'
            '<limitless_attr_list><data><index>0</index><attr_name>Max_Hp</attr_name><attr_value>7</attr_value></data>'
            '<data><index>1</index><attr_name>Max_Hp_O</attr_name><attr_value>500</attr_value></data></limitless_attr_list></item_authorizetable>'
            '<item_authorizetable><name>ring</name><start_limitless>3</start_limitless><enchant_attr_list>'
            '<data><level>1</level><attr1>block 5</attr1></data></enchant_attr_list><limitless_attr_list>'
            '<data><attr_name>Max_Hp</attr_name><attr_value>500</attr_value></data></limitless_attr_list></item_authorizetable></tables>')
        self.save_reviewed()
    def tearDown(self):self.temp.cleanup()
    def save_reviewed(self):
        self.reviewed.write_text(json.dumps({'purpose':'retail-other-equipment-hp-base-v1',
            'provenance':{'retail_armors_sha256':subject.digest(self.arm),'retail_accessories_sha256':subject.digest(self.acc)},
            'items':[{'id':115000001,'name':'shield_a','host_group':'SHIELD'},
                     {'id':122000001,'name':'ring_a','host_group':'RING'}]}))
    def run_extract(self,path):
        files=[self.reviewed,self.arm,self.acc,self.table,*self.configs]
        return subject.extract(self.root,self.reviewed,self.configs,[subject.digest(p) for p in files],path)
    def test_override_default_and_explicit_zero_are_distinct(self):
        a=self.root/'a.json'; b=self.root/'b.json'
        self.assertEqual(self.run_extract(a),self.run_extract(b));self.assertEqual(a.read_bytes(),b.read_bytes())
        d=json.loads(a.read_text());self.assertEqual(d['items'][0]['table'],'special');self.assertTrue(d['items'][0]['explicit_table'])
        self.assertEqual(d['items'][1]['table'],'ring');self.assertFalse(d['items'][1]['explicit_table'])
        self.assertEqual(d['tables']['special']['levels']['1'],{'hp':25,'explicit':True})
        self.assertEqual(d['tables']['special']['levels']['2'],{'hp':0,'explicit':True})
        self.assertEqual(d['tables']['ring']['levels']['1'],{'hp':0,'explicit':False})
        self.assertEqual(d['tables']['ring']['limitless_rows_without_index'],1)
        self.assertEqual(d['tables']['ring']['limitless_hp_per_level'],0)
        self.assertNotIn('2',d['tables']['ring']['levels']);self.assertNotIn('3',d['tables']['special']['levels'])
        self.assertEqual([subject.hp_at_level(d['tables']['special'],n) for n in (0,1,2,3,4)],[0,25,0,7,14])
        self.assertIsNone(subject.hp_at_level(d['tables']['ring'],3))
        self.assertEqual(subject.hp_at_level({'threshold':1,'levels':{},'limitless_hp_per_level':2},3),6)
        self.assertEqual(subject.hp_at_level({'threshold':1,'levels':{},'limitless_hp_per_level':2147483647},2),-2)
    def test_missing_table_remains_visible(self):
        self.acc.write_text(self.acc.read_text().replace('</name>','</name><authorize_name>missing</authorize_name>'))
        self.save_reviewed();p=self.root/'a.json';self.assertEqual(self.run_extract(p)['missing_tables'],['missing'])
    def test_rejects_duplicate_levels_and_source_drift(self):
        self.table.write_text(self.table.read_text().replace('<data><level>3</level>', '<data><level>1</level>'))
        with self.assertRaisesRegex(ValueError,'duplicate authorization level'):self.run_extract(self.root/'a.json')
        with self.assertRaisesRegex(ValueError,'source identity changed'):
            subject.extract(self.root,self.reviewed,self.configs,['0'*64]*6,self.root/'b.json')
    def test_rejects_special_context_and_ambiguous_override(self):
        self.configs[0].write_text('<config><SpecialSvrType>1</SpecialSvrType></config>')
        with self.assertRaisesRegex(ValueError,'special-server'):self.run_extract(self.root/'a.json')
        self.configs[0].write_text('<config/>')
        self.arm.write_text(self.arm.read_text().replace('</authorize_name>','</authorize_name><authorize_name>ring</authorize_name>'))
        self.save_reviewed()
        with self.assertRaisesRegex(ValueError,'override'):self.run_extract(self.root/'b.json')

if __name__=='__main__':unittest.main()
