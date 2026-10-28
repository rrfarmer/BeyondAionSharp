import json
import tempfile
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET
import extract_curse_authorization_hp as auth
import extend_curse_authorization_hp as subject

class AuthorizationExpansionTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.arm=self.root/'item_armors.xml';self.weapon=self.root/'item_weapons.xml';self.table=self.root/'item_authorizetable.xml'
        self.parent=self.root/'parent.json';self.armor=self.root/'armor.json';self.weapons=self.root/'weapons.json'
        self.arm.write_text('<items><item><id>110100001</id><name>armor_a</name></item></items>')
        self.weapon.write_text('<items><item><id>100000001</id><name>sword_a</name><authorize_name>custom</authorize_name></item></items>')
        self.table.write_text('<tables>'+''.join('<item_authorizetable><name>'+n+'</name><start_limitless>2</start_limitless>'
            '<enchant_attr_list><data><level>1</level><attr1>Max_Hp 12</attr1></data></enchant_attr_list>'
            '<limitless_attr_list><data><index>0</index><attr_name>Max_Hp</attr_name><attr_value>3</attr_value></data></limitless_attr_list>'
            '</item_authorizetable>' for n in ['shield','rb_torso','custom'])+'</tables>')
        self.save_sources()
    def tearDown(self):self.tmp.cleanup()
    def save_sources(self):
        table=next(ET.parse(self.table).getroot().iter('item_authorizetable'))
        self.parent.write_text(json.dumps({'purpose':'retail-authorization-hp-bounded-v2','special_server_type':0,
            'provenance':{'input_sha256':{str(self.table.resolve()):auth.digest(self.table)}},
            'items':[{'id':115000001,'name':'shield_a','host_group':'SHIELD','table':'shield','explicit_table':False}],
            'tables':{'shield':auth.table_values(table)}}))
        self.armor.write_text(json.dumps({'purpose':'default-armor-hp-and-class-v2',
            'provenance':{'retail_items_sha256':auth.digest(self.arm)},'items':[{'id':110100001,'name':'armor_a','table':'rb_torso'}]}))
        self.weapons.write_text(json.dumps({'purpose':'retail-default-weapon-hp-base-v1',
            'provenance':{'retail_items_sha256':auth.digest(self.weapon)},'items':[{'id':100000001,'name':'sword_a','host_group':'SWORD','table':'sword'}]}))
    def run_extract(self,path):
        files=[self.parent,self.armor,self.weapons,self.arm,self.weapon,self.table]
        return subject.extract(self.root,self.parent,self.armor,self.weapons,[auth.digest(p) for p in files],path)
    def test_includes_exact_default_override_and_prior_source(self):
        a=self.root/'a.json';b=self.root/'b.json';self.assertEqual(self.run_extract(a),self.run_extract(b));self.assertEqual(a.read_bytes(),b.read_bytes())
        d=json.loads(a.read_text());self.assertEqual([x['kind'] for x in d['items']],['weapon','armor','other'])
        self.assertEqual([(x['table'],x['host_group']) for x in d['items']],[('custom','SWORD'),('rb_torso','RB_TORSO'),('shield','SHIELD')])
        self.assertEqual(auth.hp_at_level(d['tables']['custom'],4),21)
    def test_rejects_identity_drift_and_duplicate_id(self):
        self.weapon.write_text(self.weapon.read_text().replace('sword_a','different'));self.save_sources()
        with self.assertRaisesRegex(ValueError,'mismatched'):self.run_extract(self.root/'a.json')
        self.weapon.write_text(self.weapon.read_text().replace('different','sword_a'))
        self.arm.write_text(self.arm.read_text().replace('</items>','<item><id>110100001</id><name>armor_a</name></item></items>'));self.save_sources()
        with self.assertRaisesRegex(ValueError,'Duplicate or mismatched'):self.run_extract(self.root/'b.json')
    def test_missing_table_and_source_drift_are_visible(self):
        self.weapon.write_text(self.weapon.read_text().replace('custom','missing'));self.save_sources();p=self.root/'a.json'
        self.assertEqual(self.run_extract(p)['missing_tables'],['missing'])
        with self.assertRaisesRegex(ValueError,'source identity changed'):
            subject.extract(self.root,self.parent,self.armor,self.weapons,['0'*64]*6,self.root/'b.json')
    def test_prior_reviewed_table_change_is_rejected(self):
        p=json.loads(self.parent.read_text());p['tables']['shield']['levels']['1']['hp']=123
        self.parent.write_text(json.dumps(p))
        with self.assertRaisesRegex(ValueError,'Previously reviewed'):self.run_extract(self.root/'a.json')

if __name__=='__main__':unittest.main()
