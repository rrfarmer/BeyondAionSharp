import json
import tempfile
import unittest
from pathlib import Path
import extract_curse_authorization_hp as auth
import materialize_retail_item_resource_bonuses as subject

class MaterializeBonusTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.audit=self.root/'audit.json'
        self.document={'purpose':'retail-item-resource-bonus-source-comparison-v1','read_only':True,'items':[{
            'id':110100001,'name':'armor_a','host_group':'RB_TORSO','kind':'armor','retail_apply':'equip',
            'fields':{'MAXHP':{'normal':{'value':0,'percent':False,'tag':'bonus_attr1'},
                'alternates':{'a':{'value':5,'percent':False,'tag':'bonus_attr_a2'},'b':{'value':10,'percent':True,'tag':'bonus_attr_b1'}},'status':'alternate_retail_list'},
                'MAXMP':{'normal':None,'alternates':{},'status':'fields_match'}}}]}
        self.save()
    def tearDown(self):self.tmp.cleanup()
    def save(self):self.audit.write_text(json.dumps(self.document))
    def run_extract(self,path):return subject.materialize(self.audit,auth.digest(self.audit),path)
    def test_preserves_explicit_zero_percent_and_conditioned_lists(self):
        a=self.root/'a.json';b=self.root/'b.json';self.assertEqual(self.run_extract(a),self.run_extract(b));self.assertEqual(a.read_bytes(),b.read_bytes())
        row=json.loads(a.read_text())['items'][0]
        self.assertEqual([x and x['value'] for x in row['hp']['values']],[0,5,10])
        self.assertTrue(row['hp']['values'][2]['percent']);self.assertEqual(row['mp']['values'],[None,None,None])
    def test_rejects_malformed_tags_unknown_states_and_hash_drift(self):
        self.document['items'][0]['fields']['MAXHP']['alternates']['a']['tag']='bonus_attr_b1';self.save()
        with self.assertRaisesRegex(ValueError,'Invalid source bonus'):self.run_extract(self.root/'a.json')
        self.document['items'][0]['fields']['MAXHP']['alternates']['a']['tag']='bonus_attr_a1'
        self.document['items'][0]['fields']['MAXHP']['status']='unknown';self.save()
        with self.assertRaisesRegex(ValueError,'Unknown source comparison'):self.run_extract(self.root/'b.json')
        with self.assertRaisesRegex(ValueError,'identity changed'):subject.materialize(self.audit,'0'*64,self.root/'c.json')
    def test_rejects_duplicate_identity_and_output_overwrite(self):
        self.document['items'].append(self.document['items'][0]);self.save()
        with self.assertRaisesRegex(ValueError,'Invalid reviewed'):self.run_extract(self.root/'a.json')
        self.document['items'].pop();self.save();self.run_extract(self.root/'b.json')
        with self.assertRaisesRegex(ValueError,'new and outside'):self.run_extract(self.root/'b.json')

if __name__=='__main__':unittest.main()
