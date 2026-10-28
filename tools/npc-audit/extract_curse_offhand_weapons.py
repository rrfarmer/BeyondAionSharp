"""Extract reviewed 5.8/4.8 weapon identities for the retail off-hand stat gate.

The resulting identities prove neither a complete equipment calculation nor a
player's live equipment state. The output is written outside the repository.
"""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET
import armor_enchant as armor

REPO=Path(__file__).resolve().parents[2]
GROUPS={'SWORD','DAGGER','MACE','ORB','SPELLBOOK','GREATSWORD','POLEARM','STAFF','BOW','GUN','CANNON','HARP','KEYBLADE'}

def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def extract(root,host,retail_sha,host_sha,output):
    host,output=Path(host),Path(output)
    if output.resolve().is_relative_to(REPO) or output.exists():raise ValueError('Output must be a new file outside the repository')
    source,selection=armor.selected_region_file(root,'item_weapons.xml')
    before={str(p.resolve()):sha(p) for p in (source,host)}
    if before[str(source.resolve())]!=retail_sha or before[str(host.resolve())]!=host_sha:
        raise ValueError('Selected weapon source identity changed')
    host_rows={};stack=[]
    for event,node in ET.iterparse(host,events=('start','end')):
        if event=='start':stack.append(node);continue
        if node.tag=='item_template':
            raw=node.get('id');name=node.get('cName');group=node.get('item_group')
            if raw and name and group in GROUPS:
                ident=armor.integer(raw,'host item ID')
                if 100000000<=ident<103000000:
                    if ident in host_rows:raise ValueError('Duplicate host weapon ID')
                    if not name.isascii():raise ValueError('Non-ASCII weapon identity')
                    host_rows[ident]=(name,group)
            if len(stack)>1:stack[-2].remove(node)
            node.clear()
        stack.pop()
    rows={};seen=set();stack=[]
    for event,node in ET.iterparse(source,events=('start','end')):
        if event=='start':stack.append(node);continue
        if node.tag=='item':
            raw=armor.unique_text(node,'id');ident=armor.integer(raw,'retail item ID') if raw else None
            if ident in host_rows:
                if ident in seen:raise ValueError('Duplicate retail weapon ID')
                seen.add(ident);host_name,group=host_rows[ident]
                name=armor.unique_text(node,'name',required=True)
                slots=armor.unique_text(node,'equipment_slots')
                if name and name.isascii() and name.casefold()==host_name.casefold() and slots=='main_or_sub':
                    rows[ident]={'id':ident,'name':name,'host_group':group}
            if len(stack)>1:stack[-2].remove(node)
            node.clear()
        stack.pop()
    if not rows:raise ValueError('No reviewed off-hand identities')
    if before!={str(p.resolve()):sha(p) for p in (source,host)}:raise ValueError('Weapon source changed during extraction')
    data={'version':1,'purpose':'retail-offhand-weapon-identity-v1','region':'USA',
          'provenance':{'retail_items_sha256':retail_sha,'host_items_sha256':host_sha,'retail_selection':selection},
          'items':[rows[x] for x in sorted(rows)]}
    with output.open('x',encoding='utf-8',newline='\n') as f:json.dump(data,f,ensure_ascii=False,separators=(',',':'));f.write('\n')
    return {'items':len(rows),'sha256':sha(output)}

def main():
    p=argparse.ArgumentParser();p.add_argument('--retail-root',required=True);p.add_argument('--host-items',required=True)
    p.add_argument('--retail-sha256',required=True);p.add_argument('--host-sha256',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();print(json.dumps(extract(a.retail_root,a.host_items,a.retail_sha256,a.host_sha256,a.output)))

if __name__=='__main__':main()
