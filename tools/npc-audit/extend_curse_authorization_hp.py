"""Extend the pinned ordinary 5.8 authorization-HP source to armor and weapons.

This only materializes input components; it does not enable a curse or establish
the complete player HP base. A missing table or level remains visible.
"""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import armor_enchant as armor
import extract_curse_authorization_hp as auth
import extract_curse_weapon_hp as weapons

REVIEWED_PINS=(
    '9f42bc42da798233843fedfbeb6ca50eb3c03a14639b7dd1100f21e6c7bbea7c',
    '72fa5bf580b1ace67426d6a15081531a75b7760962d601de59eed6fdb62a28ff',
    '09c3411e58241d3b51b82d676841e4f2b09d29fffeb2114eeae7e115c8f6a53a',
    '11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a',
    'f10df47b68c161ba1d76aa1f4bd2853634a57e114dc61c50f63e89d2e657344f',
    'a2e8001d83768b815239eabae960c5be1d896f82e3fa8a6a7029fe9674b482ac')

def extract(root,parent_path,armor_path,weapon_path,expected_hashes,output):
    root,parent_path,armor_path,weapon_path,output=map(Path,(root,parent_path,armor_path,weapon_path,output))
    if output.resolve().is_relative_to(auth.REPO) or output.exists():raise ValueError('Output must be new and outside the repository')
    item_arm,arm_selection=armor.selected_region_file(root,'item_armors.xml')
    item_weapon,weapon_selection=armor.selected_region_file(root,'item_weapons.xml')
    table_path=root/'item_authorizetable.xml'
    inputs=[parent_path,armor_path,weapon_path,item_arm,item_weapon,table_path]
    before={str(p.resolve()):auth.digest(p) for p in inputs}
    if len(expected_hashes)!=len(inputs) or [before[str(p.resolve())] for p in inputs]!=list(expected_hashes):
        raise ValueError('Selected authorization expansion source identity changed')
    parent=json.loads(parent_path.read_text(encoding='utf-8'))
    arm=json.loads(armor_path.read_text(encoding='utf-8'));weapon=json.loads(weapon_path.read_text(encoding='utf-8'))
    if parent.get('purpose')!='retail-authorization-hp-bounded-v2' or parent.get('special_server_type')!=0:
        raise ValueError('Unreviewed authorization parent')
    if arm.get('purpose')!='default-armor-hp-and-class-v2' or weapon.get('purpose')!='retail-default-weapon-hp-base-v1':
        raise ValueError('Unreviewed armor or weapon identity source')
    if arm['provenance']['retail_items_sha256']!=auth.digest(item_arm) or weapon['provenance']['retail_items_sha256']!=auth.digest(item_weapon):
        raise ValueError('Equipment identities and selected items differ')
    if parent['provenance']['input_sha256'].get(str(table_path.resolve()))!=auth.digest(table_path):
        raise ValueError('Authorization table differs from parent context')
    rows={}
    for row in parent['items']:
        if row['id'] in rows:raise ValueError('Duplicate parent item')
        rows[row['id']]={**row,'kind':'other'}
    expected={}
    for row in arm['items']:
        ident=row['id']; table=armor.default_retail_table(ident)
        if table!=row['table'] or ident in rows or ident in expected:raise ValueError('Invalid armor authorization default')
        group=table.upper();group=group[:-1] if group.endswith('_GLOVES') else group
        expected[ident]=(row['name'],group,table,'armor')
    for row in weapon['items']:
        ident=row['id']; default=weapons.default(ident)
        if default!=(row['host_group'],row['table']) or ident in rows or ident in expected:
            raise ValueError('Invalid weapon authorization default')
        expected[ident]=(row['name'],row['host_group'],row['table'],'weapon')
    remaining=set(expected)
    for path,kind in ((item_arm,'armor'),(item_weapon,'weapon')):
        stack=[]
        for event,node in ET.iterparse(path,events=('start','end')):
            if event=='start':stack.append(node);continue
            if node.tag=='item':
                raw=armor.unique_text(node,'id',True);ident=armor.integer(raw,'retail item ID')
                if ident in expected:
                    name,group,default,expected_kind=expected[ident]
                    selected=armor.unique_text(node,'name',True)
                    if ident not in remaining or kind!=expected_kind or not selected or selected.casefold()!=name.casefold():
                        raise ValueError('Duplicate or mismatched retail equipment identity')
                    overrides=node.findall('authorize_name')
                    if len(overrides)>1 or any(n.attrib or len(n) or not n.text for n in overrides):
                        raise ValueError('Unreviewed authorization table override')
                    rows[ident]={'id':ident,'name':selected,'host_group':group,
                                 'table':overrides[0].text if overrides else default,
                                 'explicit_table':bool(overrides),'kind':kind}
                    remaining.remove(ident)
                if len(stack)>1:stack[-2].remove(node)
                node.clear()
            stack.pop()
    if remaining:raise ValueError('Missing reviewed retail equipment identities')
    wanted={x['table'].casefold() for x in rows.values()};tables={}
    for row in ET.parse(table_path).getroot().iter('item_authorizetable'):
        name=armor.unique_text(row,'name',True)
        if not name or name.casefold() not in wanted:continue
        if name.casefold() in tables:raise ValueError('Duplicate authorization table name')
        tables[name.casefold()]=auth.table_values(row)
    missing=sorted(wanted-tables.keys())
    for name,old in parent['tables'].items():
        if name not in tables or tables[name]!=old:raise ValueError('Previously reviewed authorization table changed')
    if before!={str(p.resolve()):auth.digest(p) for p in inputs}:raise ValueError('Authorization sources changed during extraction')
    data={'version':3,'purpose':'retail-authorization-hp-bounded-v3','special_server_type':0,
          'provenance':{'input_sha256':before,'parent_provenance':parent['provenance'],
                        'armor_selection':arm_selection,'weapon_selection':weapon_selection,'table_selection':'ordinary base filename'},
          'tables':dict(sorted(tables.items())),'missing_tables':missing,'items':[rows[x] for x in sorted(rows)]}
    with output.open('x',encoding='utf-8',newline='\n') as f:json.dump(data,f,ensure_ascii=False,separators=(',',':'));f.write('\n')
    return {'items':len(rows),'armor':len(arm['items']),'weapon':len(weapon['items']),
            'other':len(parent['items']),'tables':len(tables),'missing_tables':missing,'sha256':auth.digest(output)}

def main():
    p=argparse.ArgumentParser();p.add_argument('--retail-root',required=True);p.add_argument('--parent',required=True)
    p.add_argument('--armor',required=True);p.add_argument('--weapons',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();print(json.dumps(extract(a.retail_root,a.parent,a.armor,a.weapons,REVIEWED_PINS,a.output)))
if __name__=='__main__':main()
