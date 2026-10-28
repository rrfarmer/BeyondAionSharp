"""Compare static HP/MP item-bonus fields for already matched equipment.

This is a read-only source audit, not a complete item or player stat calculation.
Alternate retail lists, inventory application and complex host modifiers are
reported separately and never promoted to a gameplay value.
"""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import armor_enchant as armor
import extract_curse_authorization_hp as auth

EXPECTED_PINS=(
    'ed518ff28c1af257f6f77e86691a28e7dac908db6a54f5e24df68a717f9ff57e',
    'dae844fb7b6837b0d898105e2466cb3fac557a6b6a4ada6c6d6e9f177745ce47',
    '11167024df7400bf6ff051f84bcf6ef1dffdec1949b33ff409f42e39e3e9716a',
    '7ce2457a34b8082826146804a1af8223e9e8f36d9e4c1a41603baea56f2d1470',
    'f10df47b68c161ba1d76aa1f4bd2853634a57e114dc61c50f63e89d2e657344f')
TAG=re.compile(r'bonus_attr(?:(\d+)|_([ab])(\d+))\Z')
VALUE=re.compile(r'([+-]?\d+)(%)?\Z')
FIELDS={'maxhp':'MAXHP','maxmp':'MAXMP'}

def retail_bonuses(node):
    result={'normal':{},'a':{},'b':{}}
    seen_tags=set()
    for child in node:
        match=TAG.fullmatch(child.tag)
        if not match:continue
        family='normal' if match.group(1) else match.group(2)
        number=int(match.group(1) or match.group(3))
        if not 1<=number<=(12 if family=='normal' else 4):continue
        if child.tag in seen_tags or child.attrib or len(child):raise ValueError('Ambiguous retail bonus field')
        seen_tags.add(child.tag)
        parts=(child.text or '').split()
        if not parts or parts[0].casefold() not in FIELDS:continue
        field=FIELDS[parts[0].casefold()]
        if len(parts)!=2 or not VALUE.fullmatch(parts[1]):raise ValueError('Unreviewed retail resource bonus')
        value=VALUE.fullmatch(parts[1]);assert value
        number=armor.integer(value.group(1),'retail bonus value')
        if field in result[family]:raise ValueError('Duplicate retail resource bonus in list')
        result[family][field]={'value':number,'percent':bool(value.group(2)),'tag':child.tag}
    return result

def host_bonuses(node):
    result={name:{'base':0,'bonus':0,'bonus_present':False,'complex':[]} for name in FIELDS.values()}
    lists=node.findall('modifiers')
    if len(lists)>1:raise ValueError('Duplicate host modifiers list')
    for child in lists[0] if lists else []:
        field=child.get('name')
        if field not in result:continue
        flag=child.get('bonus','false').casefold()
        if flag not in ('true','false'):raise ValueError('Unreviewed host bonus flag')
        if child.tag!='add' or len(child) or any(k not in ('name','value','bonus') for k in child.attrib):
            result[field]['complex'].append(ET.tostring(child,encoding='unicode').strip());continue
        raw=armor.integer(child.get('value','0'),'host resource modifier')
        key='bonus' if flag=='true' else 'base'
        result[field][key]+=raw
        if not -(2**31)<=result[field][key]<2**31:raise ValueError('Host resource modifiers overflow')
        if key=='bonus':result[field]['bonus_present']=True
    return result

def compare(root,reviewed_path,host_path,expected_hashes,output):
    root,reviewed_path,host_path,output=map(Path,(root,reviewed_path,host_path,output))
    if output.resolve().is_relative_to(auth.REPO) or output.exists():raise ValueError('Output must be new and outside the repository')
    selected=[armor.selected_region_file(root,n) for n in ('item_armors.xml','item_accessories.xml','item_weapons.xml')]
    files=[reviewed_path,host_path,*[x[0] for x in selected]]
    before={str(p.resolve()):auth.digest(p) for p in files}
    if len(expected_hashes)!=len(files) or [before[str(p.resolve())] for p in files]!=list(expected_hashes):
        raise ValueError('Selected item bonus source identity changed')
    data=json.loads(reviewed_path.read_text(encoding='utf-8'))
    if data.get('purpose')!='retail-authorization-hp-bounded-v3' or data.get('special_server_type')!=0:
        raise ValueError('Unreviewed item identities')
    identities={}
    for row in data['items']:
        if row['id'] in identities:raise ValueError('Duplicate reviewed item identity')
        identities[row['id']]=row
    host={};stack=[]
    for event,node in ET.iterparse(host_path,events=('start','end')):
        if event=='start':stack.append(node);continue
        if node.tag=='item_template':
            raw=node.get('id');ident=armor.integer(raw,'host item ID') if raw else None
            if ident in identities:
                identity=identities[ident];name=node.get('cName');group=node.get('item_group')
                if ident in host or not name or name.casefold()!=identity['name'].casefold() or group!=identity['host_group']:
                    raise ValueError('Host item identity changed')
                host[ident]=host_bonuses(node)
            if len(stack)>1:stack[-2].remove(node)
            node.clear()
        stack.pop()
    if len(host)!=len(identities):raise ValueError('Missing reviewed host item')
    found={};counts=Counter();stack=[]
    for file,_selection in selected:
        for event,node in ET.iterparse(file,events=('start','end')):
            if event=='start':stack.append(node);continue
            if node.tag=='item':
                raw=armor.unique_text(node,'id',True);ident=armor.integer(raw,'retail item ID')
                if ident in identities:
                    identity=identities[ident];name=armor.unique_text(node,'name',True)
                    if ident in found or not name or name.casefold()!=identity['name'].casefold():
                        raise ValueError('Duplicate or changed retail item identity')
                    mode=armor.unique_text(node,'bonus_apply');retail=retail_bonuses(node);fields={}
                    for field in FIELDS.values():
                        normal=retail['normal'].get(field);alternates={k:retail[k][field] for k in ('a','b') if field in retail[k]}
                        h=host[ident][field]
                        if mode!='equip':status='unreviewed_application'
                        elif alternates:status='alternate_retail_list'
                        elif h['complex']:status='complex_host_modifier'
                        elif normal is not None and normal['percent']:status='retail_percent'
                        else:status='fields_match' if (normal['value'] if normal else 0)==h['bonus'] else 'fields_differ'
                        counts[(field,status)]+=1
                        fields[field]={'normal':normal,'alternates':alternates,'host':h,'status':status}
                    found[ident]={'id':ident,'name':name,'kind':identity['kind'],'host_group':identity['host_group'],
                                  'retail_apply':mode,'fields':fields}
                if len(stack)>1:stack[-2].remove(node)
                node.clear()
            stack.pop()
    if len(found)!=len(identities):raise ValueError('Missing reviewed retail item')
    if before!={str(p.resolve()):auth.digest(p) for p in files}:raise ValueError('Item bonus sources changed during audit')
    result={'version':1,'purpose':'retail-item-resource-bonus-source-comparison-v1','read_only':True,
            'limitations':'Field comparison only; alternate lists, eligibility, sockets, effects, item sets and actual player state remain separate.',
            'source_sha256':before,'selection':{file.name:selection for file,selection in selected},
            'counts':{f'{field}:{status}':n for (field,status),n in sorted(counts.items())},
            'items':[found[x] for x in sorted(found)]}
    with output.open('x',encoding='utf-8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,separators=(',',':'));f.write('\n')
    return {'items':len(found),'counts':result['counts'],'sha256':auth.digest(output)}

def main():
    p=argparse.ArgumentParser();p.add_argument('--retail-root',required=True);p.add_argument('--reviewed-input',required=True)
    p.add_argument('--host-items',required=True);p.add_argument('--output',required=True);a=p.parse_args()
    print(json.dumps(compare(a.retail_root,a.reviewed_input,a.host_items,EXPECTED_PINS,a.output)))
if __name__=='__main__':main()
