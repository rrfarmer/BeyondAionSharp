"""Extract reviewed retail weapon HP percentage-base inputs, outside the repo.

Separate weapon bonuses, sockets, effect ownership and player state are not
included; these rows alone cannot enable a curse calculation.
"""
import argparse,hashlib,json
from pathlib import Path
import xml.etree.ElementTree as ET
import armor_enchant as armor

REPO=Path(__file__).resolve().parents[2]
RANGES=[(100000000,'SWORD','sword'),(100100000,'MACE','mace'),(100200000,'DAGGER','dagger'),
        (100500000,'ORB','orb'),(100600000,'SPELLBOOK','book'),(100900000,'GREATSWORD','2hsword'),
        (101300000,'POLEARM','polearm'),(101500000,'STAFF','staff'),(101700000,'BOW','bow'),
        (101800000,'GUN','gun'),(101900000,'CANNON','cannon'),(102000000,'HARP','harp'),(102100000,'KEYBLADE','keyblade')]
def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def default(item_id):
    found=[(group,table) for start,group,table in RANGES if start<=item_id<start+100000]
    return found[0] if len(found)==1 else None
def extract(root,host,retail_sha,enchant_sha,host_sha,output):
    host,output=Path(host),Path(output)
    if output.resolve().is_relative_to(REPO) or output.exists():raise ValueError('Output must be a new file outside the repository')
    source,selection=armor.selected_region_file(root,'item_weapons.xml')
    enchants,ench_selection=armor.selected_region_file(root,'item_enchanttable.xml')
    paths=(source,enchants,host);before={str(p.resolve()):sha(p) for p in paths}
    if [before[str(p.resolve())] for p in paths]!=[retail_sha,enchant_sha,host_sha]:raise ValueError('Selected source identity changed')
    tables=ET.parse(enchants).getroot()
    for _,_,table in RANGES:
        node=armor.unique_by(tables,'item_enchanttable',lambda n:n.findtext('name')==table)
        if armor.integer(armor.unique_text(node,'start_limitless',True),'threshold')<=15:
            raise ValueError('Unreviewed weapon enchant threshold')
        if any(armor.retail_level(node,n,'max_hp')!=0 for n in range(1,16)):
            raise ValueError('Weapon default enchant HP is not zero')
    host_rows={};stack=[]
    for event,node in ET.iterparse(host,events=('start','end')):
        if event=='start':stack.append(node);continue
        if node.tag=='item_template':
            raw=node.get('id');name=node.get('cName');group=node.get('item_group')
            if raw and name:
                ident=armor.integer(raw,'host item ID');expected=default(ident)
                if expected and group==expected[0] and node.get('enchant_name') is None:
                    cap=armor.integer(node.get('max_enchant','0'),'host enchant cap')
                    if 0<=cap<=15:
                        if ident in host_rows:raise ValueError('Duplicate host weapon ID')
                        if not name.isascii():raise ValueError('Non-ASCII weapon identity')
                        host_rows[ident]=(name,group,expected[1],cap)
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
                seen.add(ident);host_name,group,table,cap=host_rows[ident]
                name=armor.unique_text(node,'name',required=True);slots=armor.unique_text(node,'equipment_slots')
                raw_hp=armor.unique_text(node,'max_hp');override=node.find('enchant_name')
                if name and name.isascii() and name.casefold()==host_name.casefold() and slots in ('main','main_or_sub') and override is None:
                    hp=armor.integer(raw_hp,'weapon max HP') if raw_hp is not None else 0
                    if hp!=0:raise ValueError('Unreviewed nonzero weapon base HP')
                    rows[ident]={'id':ident,'name':name,'host_group':group,'table':table,'host_max_enchant':cap,'max_hp_explicit':raw_hp is not None}
            if len(stack)>1:stack[-2].remove(node)
            node.clear()
        stack.pop()
    if not rows:raise ValueError('No reviewed weapon HP inputs')
    if before!={str(p.resolve()):sha(p) for p in paths}:raise ValueError('Weapon source changed during extraction')
    data={'version':1,'purpose':'retail-default-weapon-hp-base-v1','region':'USA',
          'provenance':{'retail_items_sha256':retail_sha,'retail_enchant_sha256':enchant_sha,
                        'host_items_sha256':host_sha,'retail_selection':selection,'enchant_selection':ench_selection},
          'items':[rows[x] for x in sorted(rows)]}
    with output.open('x',encoding='utf-8',newline='\n') as f:json.dump(data,f,ensure_ascii=False,separators=(',',':'));f.write('\n')
    return {'items':len(rows),'sha256':sha(output)}
def main():
    p=argparse.ArgumentParser();p.add_argument('--retail-root',required=True);p.add_argument('--host-items',required=True)
    p.add_argument('--retail-sha256',required=True);p.add_argument('--enchant-sha256',required=True);p.add_argument('--host-sha256',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();print(json.dumps(extract(a.retail_root,a.host_items,a.retail_sha256,a.enchant_sha256,a.host_sha256,a.output)))
if __name__=='__main__':main()
