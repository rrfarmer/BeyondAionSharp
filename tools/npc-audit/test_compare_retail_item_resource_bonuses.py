import json
import tempfile
import unittest
from pathlib import Path
import compare_retail_item_resource_bonuses as subject
import extract_curse_authorization_hp as auth
import summarize_retail_item_resource_bonuses as summary

class ResourceBonusAuditTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.ids=self.root/'ids.json';self.host=self.root/'host.xml'
        self.arm=self.root/'item_armors.xml';self.acc=self.root/'item_accessories.xml';self.weapon=self.root/'item_weapons.xml'
        self.ids.write_text(json.dumps({'purpose':'retail-authorization-hp-bounded-v3','special_server_type':0,'items':[
            {'id':110100001,'name':'armor_a','kind':'armor','host_group':'RB_TORSO'},
            {'id':122000001,'name':'ring_a','kind':'other','host_group':'RING'},
            {'id':100000001,'name':'sword_a','kind':'weapon','host_group':'SWORD'}]}))
        self.host.write_text('<items><item_template id="110100001" cName="armor_a" item_group="RB_TORSO"><modifiers>'
            '<add name="MAXHP" value="100"/><add name="MAXHP" value="12" bonus="true"/>'
            '<add name="MAXMP" value="0" bonus="true"/></modifiers></item_template>'
            '<item_template id="122000001" cName="ring_a" item_group="RING"><modifiers><rate name="MAXHP" value="2" bonus="true"/></modifiers></item_template>'
            '<item_template id="100000001" cName="sword_a" item_group="SWORD"/></items>')
        self.arm.write_text('<items><item><id>110100001</id><name>armor_a</name><bonus_apply>equip</bonus_apply>'
            '<bonus_attr1>maxHp 12</bonus_attr1><bonus_attr2>maxMp 0</bonus_attr2></item></items>')
        self.acc.write_text('<items><item><id>122000001</id><name>ring_a</name><bonus_apply>equip</bonus_apply>'
            '<bonus_attr_a1>maxHp 50</bonus_attr_a1></item></items>')
        self.weapon.write_text('<items><item><id>100000001</id><name>sword_a</name><bonus_apply>inventory</bonus_apply>'
            '<bonus_attr1>maxHp 33</bonus_attr1></item></items>')
    def tearDown(self):self.tmp.cleanup()
    def run_audit(self,path):
        files=[self.ids,self.host,self.arm,self.acc,self.weapon]
        return subject.compare(self.root,self.ids,self.host,[auth.digest(p) for p in files],path)
    def test_preserves_base_bonus_explicit_zero_and_separate_lists(self):
        a=self.root/'a.json';b=self.root/'b.json';self.assertEqual(self.run_audit(a),self.run_audit(b));self.assertEqual(a.read_bytes(),b.read_bytes())
        rows={r['id']:r for r in json.loads(a.read_text())['items']};hp=rows[110100001]['fields']['MAXHP'];mp=rows[110100001]['fields']['MAXMP']
        self.assertEqual(hp['normal']['value'],12);self.assertEqual(hp['host']['base'],100);self.assertEqual(hp['host']['bonus'],12);self.assertEqual(hp['status'],'fields_match')
        self.assertEqual(mp['normal']['value'],0);self.assertTrue(mp['host']['bonus_present'])
        self.assertEqual(rows[122000001]['fields']['MAXHP']['status'],'alternate_retail_list')
        self.assertEqual(rows[100000001]['fields']['MAXHP']['status'],'unreviewed_application')
    def test_distinguishes_differences_percent_and_complex_host(self):
        self.arm.write_text(self.arm.read_text().replace('maxHp 12','maxHp 15%'))
        self.acc.write_text(self.acc.read_text().replace('<bonus_attr_a1>maxHp 50</bonus_attr_a1>',''))
        self.run_audit(self.root/'a.json');rows={r['id']:r for r in json.loads((self.root/'a.json').read_text())['items']}
        self.assertEqual(rows[110100001]['fields']['MAXHP']['status'],'retail_percent')
        self.assertEqual(rows[122000001]['fields']['MAXHP']['status'],'complex_host_modifier')
        self.arm.write_text(self.arm.read_text().replace('maxHp 15%','maxHp 15'));self.run_audit(self.root/'b.json')
        rows={r['id']:r for r in json.loads((self.root/'b.json').read_text())['items']};self.assertEqual(rows[110100001]['fields']['MAXHP']['status'],'fields_differ')
    def test_rejects_duplicate_resource_bonus_and_identity_drift(self):
        self.arm.write_text(self.arm.read_text().replace('</item>','<bonus_attr3>maxHp 9</bonus_attr3></item>'))
        with self.assertRaisesRegex(ValueError,'Duplicate retail resource'):self.run_audit(self.root/'a.json')
        self.arm.write_text(self.arm.read_text().replace('<bonus_attr3>maxHp 9</bonus_attr3>',''))
        self.host.write_text(self.host.read_text().replace('armor_a','different'))
        with self.assertRaisesRegex(ValueError,'Host item identity'):self.run_audit(self.root/'b.json')
    def test_rejects_changed_source_and_non_numeric_retail_values(self):
        with self.assertRaisesRegex(ValueError,'source identity changed'):
            subject.compare(self.root,self.ids,self.host,['0'*64]*5,self.root/'a.json')
        self.arm.write_text(self.arm.read_text().replace('maxHp 12','maxHp unknown'))
        with self.assertRaisesRegex(ValueError,'Unreviewed retail resource'):self.run_audit(self.root/'b.json')
    def test_summary_ranks_absolute_differences_and_excludes_unknowns(self):
        self.arm.write_text(self.arm.read_text().replace('maxHp 12','maxHp 42'))
        self.run_audit(self.root/'a.json');report=summary.summarize(self.root/'a.json',1)
        self.assertEqual(len(report['largest_field_differences']),1)
        self.assertEqual(report['largest_field_differences'][0]['difference'],30)
        with self.assertRaisesRegex(ValueError,'Limit'):summary.summarize(self.root/'a.json',1001)

if __name__=='__main__':unittest.main()
