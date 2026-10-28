"""Offline identity checks; original review bytes stay in a pinned external fixture."""
import collections,dataclasses,hashlib,json,os,unittest
from pathlib import Path
import retail_npc_identity_bindings as B

PIN='f90650eda2f22bf9e04d81a02a716fc992cbad3a186edfcefc793b54aecd3ecf'

def read_private_review():
    path = os.environ.get('AION_NPC_IDENTITY_REVIEW_FIXTURE')
    if not path:
        raise unittest.SkipTest('Requires the pinned external identity review; see docs/mob-stat-comparison.md')
    raw = Path(path).read_bytes()
    if hashlib.sha256(raw).hexdigest() != PIN:
        raise AssertionError('External identity-review fixture hash differs')
    return raw


class ReviewedAbyssIdentityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.raw=read_private_review()
        cls.review=json.loads(cls.raw)
        cls.bindings=B.load_reviewed_abyss_bindings(cls.raw,expected_sha256=PIN)

    def args(self,row):
        span=row['retail_raw_record']
        fields={k:(v['retail58'],) for k,v in row['identity_field_comparisons'].items()}
        fields.update(id=(str(row['npc_id']),),name=(row['retail58_same_id_name'],),level=('65',))
        return dict(npc_id=row['npc_id'],client48_devnames=[row['client48_name']],current_level48=row['levels']['csharp48'],
                    source_file_sha256='e51dc42fd01b8f0cb2b2f1cd03fa15ec473202efb8079e7006f60119a68faee1',
                    source=dict(path=row['retail_source'],raw_sha256=span['raw_record_sha256'],byte_start=span['raw_record_start'],byte_end_exclusive=span['raw_record_end']),raw_fields=fields)

    def test_all_original_ninety_keep_their_ids_and_both_names(self):
        for row in self.review['rows']:
            with self.subTest(npc=row['npc_id']):
                answer=self.bindings.admit(**self.args(row)).metadata()
                self.assertEqual(answer['npc_id'],row['npc_id'])
                self.assertEqual(answer['client48_devname'],row['client48_name'])
                self.assertEqual(answer['retail58_devname'],row['retail58_same_id_name'])
                self.assertEqual(answer['raw_record_sha256'],row['retail_raw_record']['raw_record_sha256'])
                self.assertFalse(answer['activation_ready']);self.assertFalse(answer['effective_profile_verified'])
                self.assertFalse(answer['skill_compatibility_verified'])
        witness=self.bindings.admit(**self.args(self.review['rows'][0]))
        self.assertEqual((witness.npc_id,witness.client48_devname,witness.retail58_devname),
                         (self.review['rows'][0]['npc_id'],self.review['rows'][0]['client48_name'],self.review['rows'][0]['retail58_same_id_name']))

    def test_name_match_on_the_new_level75_id_is_rejected(self):
        for row in self.review['rows']:
            with self.subTest(npc=row['npc_id']):
                args=self.args(row);alternative=row['same_old_name_other_retail_records'][0]
                args['npc_id']=alternative['id'];args['raw_fields'].update(id=(str(alternative['id']),),name=(alternative['name'],),level=('75',))
                with self.assertRaises(B.IdentityBindingError):self.bindings.admit(**args)

    def test_every_consumed_identity_field_requires_one_actual_value(self):
        for row in self.review['rows']:
            for key in ('id','name','level','desc','race_type','tribe','npc_type','hpgauge_level','ai_name'):
                for mode in ('missing','duplicate','changed'):
                    with self.subTest(npc=row['npc_id'],field=key,mode=mode):
                        args=self.args(row)
                        if mode=='missing':del args['raw_fields'][key]
                        elif mode=='duplicate':args['raw_fields'][key]*=2
                        else:args['raw_fields'][key]=('wrong',)
                        with self.assertRaises(B.IdentityBindingError):self.bindings.admit(**args)

    def test_source_hash_span_and_context_are_independent_guards(self):
        row=self.review['rows'][0]
        changes=[('source_file_sha256','0'*64),('current_level48','64'),('client48_devnames',['different']),('npc_id',True)]
        for key,value in changes:
            with self.subTest(key=key):
                args=self.args(row);args[key]=value
                with self.assertRaises(B.IdentityBindingError):self.bindings.admit(**args)
        for key,value in [('path','npcs_abyss_monsters-other.xml'),('raw_sha256','0'*64),('byte_start',row['retail_raw_record']['raw_record_start']+2),('byte_end_exclusive',row['retail_raw_record']['raw_record_end']-2),('byte_start',True)]:
            with self.subTest(source=key):
                args=self.args(row);args['source'][key]=value
                with self.assertRaises(B.IdentityBindingError):self.bindings.admit(**args)
        args=self.args(row);args['raw_fields']['desc']=args['raw_fields']['desc'][0]
        with self.assertRaises(B.IdentityBindingError):self.bindings.admit(**args)

    def test_missing27_are_not_silently_alias_bound(self):
        missing=list(range(208001,208007))+list(range(298001,298022))
        self.assertEqual(len(missing),27)
        for i in missing:
            with self.subTest(npc=i):
                args=self.args(self.review['rows'][0]);args['npc_id']=i
                with self.assertRaises(B.IdentityBindingError):self.bindings.admit(**args)

    def test_level_differences_remain_explicit_profile_facts(self):
        pairs=collections.Counter()
        for row in self.review['rows']:
            metadata=self.bindings.admit(**self.args(row)).metadata()
            pairs[metadata['previous_level48']+'->'+metadata['retail_level58']]+=1
        self.assertEqual(pairs,{'65->65':3,'50->65':84,'55->65':1,'1->65':2})
        self.assertEqual(sum(n for pair,n in pairs.items() if pair!='65->65'),87)

    def test_review_is_an_independent_pin_not_its_own_expected_hash(self):
        changed=self.raw.replace(b'Ab1_1011_Boss_Li_1_No',b'Ab1_1011_Boss_Li_1_NX',1)
        self.assertNotEqual(changed,self.raw)
        for raw,pin in [(self.raw,'0'*64),(changed,PIN),(changed,hashlib.sha256(changed).hexdigest()),(b'',PIN)]:
            with self.subTest(pin=pin):
                with self.assertRaises(B.IdentityBindingError):B.load_reviewed_abyss_bindings(raw,expected_sha256=pin)

    def test_admitted_bindings_and_source_metadata_are_immutable(self):
        args=self.args(self.review['rows'][0]);first=self.bindings.admit(**args)
        with self.assertRaises(TypeError):self.bindings.entries[260000]=None
        with self.assertRaises(dataclasses.FrozenInstanceError):first.retail58_devname='changed'
        metadata=first.metadata();metadata['retail58_devname']='changed';metadata['activation_ready']=True
        self.assertEqual(first.metadata()['retail58_devname'],self.review['rows'][0]['retail58_same_id_name'])
        self.assertFalse(first.metadata()['activation_ready'])
        with self.assertRaises(B.IdentityBindingError):B.ReviewedAbyssBindings({},object())
        forged=dict(self.bindings.entries);forged[260000]=dataclasses.replace(forged[260000],retail58_devname='changed')
        with self.assertRaises(TypeError):dataclasses.replace(self.bindings,entries=B.MappingProxyType(forged))
        with self.assertRaises(AttributeError):self.bindings.entries=B.MappingProxyType(forged)

if __name__=='__main__':unittest.main()


class IdentityReviewAdmissionTests(unittest.TestCase):
    def test_missing_changed_and_self_signed_aliases_cannot_be_admitted(self):
        for raw, pin in [(b'', PIN), (b'{}', PIN), (b'{}', hashlib.sha256(b'{}').hexdigest()),
                         (b'x'*(1024*1024+1), PIN)]:
            with self.subTest(size=len(raw), pin=pin), self.assertRaises(B.IdentityBindingError):
                B.load_reviewed_abyss_bindings(raw, expected_sha256=pin)
