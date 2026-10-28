"""Materialize source-only HP/MP bonus lists from the pinned offline audit.

This does not activate item effects or prove item eligibility, fusion, player
state, item revision or a complete curse calculation.
"""
import argparse
import json
from pathlib import Path
import re
import extract_curse_authorization_hp as auth

REVIEWED_AUDIT_SHA='72607d020e1b7a511631eca0692bfc7a3b9ccbd32dced9fe0e7cde16b813f4b2'
TAG={'normal':re.compile(r'bonus_attr(?:[1-9]|1[0-2])\Z'),
     'a':re.compile(r'bonus_attr_a[1-4]\Z'),'b':re.compile(r'bonus_attr_b[1-4]\Z')}
STATUSES={'fields_match','fields_differ','alternate_retail_list','complex_host_modifier','retail_percent','unreviewed_application'}

def convert_field(field):
    status=field['status']
    if status not in STATUSES:raise ValueError('Unknown source comparison status')
    alternates=field['alternates']
    if not isinstance(alternates,dict) or set(alternates)-{'a','b'}:raise ValueError('Unknown alternate bonus list')
    values=[]
    for group,entry in [('normal',field['normal']),('a',alternates.get('a')),('b',alternates.get('b'))]:
        if entry is None:values.append(None);continue
        if set(entry)!={'value','percent','tag'} or not isinstance(entry['value'],int) or isinstance(entry['value'],bool) \
            or not -(2**31)<=entry['value']<2**31 or not isinstance(entry['percent'],bool) \
            or not isinstance(entry['tag'],str) or not TAG[group].fullmatch(entry['tag']):
            raise ValueError('Invalid source bonus operand')
        values.append({'value':entry['value'],'percent':entry['percent'],'tag':entry['tag']})
    return {'values':values,'comparison_status':status}

def materialize(audit_path,expected_sha,output):
    audit_path,output=Path(audit_path),Path(output)
    if output.resolve().is_relative_to(auth.REPO) or output.exists():raise ValueError('Output must be new and outside the repository')
    before=auth.digest(audit_path)
    if before!=expected_sha:raise ValueError('Item bonus audit identity changed')
    audit=json.loads(audit_path.read_text(encoding='utf-8'))
    if audit.get('purpose')!='retail-item-resource-bonus-source-comparison-v1' or not audit.get('read_only'):
        raise ValueError('Unreviewed item bonus audit')
    rows={}
    for item in audit['items']:
        ident=item['id'];name=item['name'];group=item['host_group'];kind=item['kind'];mode=item['retail_apply']
        if not isinstance(ident,int) or ident<=0 or ident in rows or not name or not name.isascii() \
            or not group or not group.isascii() or kind not in ('armor','weapon','other') or mode not in ('equip','inventory',None):
            raise ValueError('Invalid reviewed item identity')
        rows[ident]={'id':ident,'name':name,'host_group':group,'kind':kind,'retail_apply':mode,
                     'hp':convert_field(item['fields']['MAXHP']),'mp':convert_field(item['fields']['MAXMP'])}
    if not rows:raise ValueError('No reviewed bonus identities')
    if auth.digest(audit_path)!=before:raise ValueError('Item bonus audit changed during materialization')
    result={'version':1,'purpose':'retail-item-resource-bonus-source-v1','audit_sha256':before,
            'items':[rows[x] for x in sorted(rows)]}
    with output.open('x',encoding='utf-8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,separators=(',',':'));f.write('\n')
    return {'items':len(rows),'sha256':auth.digest(output)}

def main():
    p=argparse.ArgumentParser();p.add_argument('--audit',required=True);p.add_argument('--output',required=True);a=p.parse_args()
    print(json.dumps(materialize(a.audit,REVIEWED_AUDIT_SHA,a.output)))
if __name__=='__main__':main()
