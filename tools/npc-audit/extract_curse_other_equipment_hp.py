"""Extract reviewed zero raw/default-enchant HP inputs for ordinary equipment.

This does not materialize tempering, sockets, bonus attributes, item effects or
complete player state; consumers must keep those independent inputs visible.
"""
import argparse,hashlib,json
from pathlib import Path
import xml.etree.ElementTree as ET
import armor_enchant as armor

REPO=Path(__file__).resolve().parents[2]
# start, exclusive end, host group, source slot token, selected file, allowed enchant level
RANGES=[(115000000,115100000,'SHIELD','sub','item_armors.xml',15),
        (125000000,125400000,'HEAD','head','item_armors.xml',0),
        (120000000,120100000,'EARRING','right_or_left_ear','item_accessories.xml',0),
        (121000000,121100000,'NECKLACE','neck','item_accessories.xml',0),
        (122000000,122100000,'RING','right_or_left_finger','item_accessories.xml',0),
        (123000000,123100000,'BELT','waist','item_accessories.xml',0)]
def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def classify(item_id):
    matches=[x for x in RANGES if x[0]<=item_id<x[1]]
    return matches[0] if len(matches)==1 else None
def extract(root,host,armor_sha,accessory_sha,enchant_sha,host_sha,output):
    host,output=Path(host),Path(output)
    if output.resolve().is_relative_to(REPO) or output.exists():raise ValueError('Output must be a new file outside the repository')
    arm,arm_sel=armor.selected_region_file(root,'item_armors.xml')
    accessory,acc_sel=armor.selected_region_file(root,'item_accessories.xml')
    enchant,enc_sel=armor.selected_region_file(root,'item_enchanttable.xml')
    paths=(arm,accessory,enchant,host);before={str(p.resolve()):sha(p) for p in paths}
    if [before[str(p.resolve())] for p in paths]!=[armor_sha,accessory_sha,enchant_sha,host_sha]:raise ValueError('Selected source identity changed')
    table=armor.unique_by(ET.parse(enchant).getroot(),'item_enchanttable',lambda n:n.findtext('name')=='shield')
    if armor.integer(armor.unique_text(table,'start_limitless',True),'threshold')<=15:raise ValueError('Unreviewed shield enchant threshold')
    if any(armor.retail_level(table,n,'max_hp')!=0 for n in range(1,16)):raise ValueError('Shield default enchant HP is not zero')
    host_rows={};stack=[]
    for event,node in ET.iterparse(host,events=('start','end')):
        if event=='start':stack.append(node);continue
        if node.tag=='item_template':
            raw=node.get('id');name=node.get('cName');group=node.get('item_group')
            if raw and name:
                ident=armor.integer(raw,'host item ID');entry=classify(ident)
                if entry and group==entry[2] and node.get('enchant_name') is None:
                    cap=armor.integer(node.get('max_enchant','0'),'host enchant cap')
                    if 0<=cap<=255 and (group!='SHIELD' or cap<=15):
                        if ident in host_rows:raise ValueError('Duplicate host item ID')
                        if not name.isascii():raise ValueError('Non-ASCII item identity')
                        host_rows[ident]=(name,entry,cap)
            if len(stack)>1:stack[-2].remove(node)
            node.clear()
        stack.pop()
    rows={};seen=set()
    for path in (arm,accessory):
        stack=[]
        for event,node in ET.iterparse(path,events=('start','end')):
            if event=='start':stack.append(node);continue
            if node.tag=='item':
                raw=armor.unique_text(node,'id');ident=armor.integer(raw,'retail item ID') if raw else None
                if ident in host_rows:
                    if ident in seen:raise ValueError('Duplicate retail item ID')
                    seen.add(ident);host_name,entry,cap=host_rows[ident]
                    name=armor.unique_text(node,'name',required=True);slots=armor.unique_text(node,'equipment_slots')
                    hp=armor.unique_text(node,'max_hp');override=node.find('enchant_name')
                    if name and name.isascii() and name.casefold()==host_name.casefold() and slots==entry[3] and path.name==entry[4] and override is None:
                        value=armor.integer(hp,'item max HP') if hp is not None else 0
                        if value!=0:raise ValueError('Unreviewed nonzero raw item HP')
                        rows[ident]={'id':ident,'name':name,'host_group':entry[2],'host_max_enchant':cap,
                                     'max_enchant_level':entry[5],'max_hp_explicit':hp is not None}
                if len(stack)>1:stack[-2].remove(node)
                node.clear()
            stack.pop()
    if not rows:raise ValueError('No reviewed equipment HP inputs')
    if before!={str(p.resolve()):sha(p) for p in paths}:raise ValueError('Item source changed during extraction')
    data={'version':1,'purpose':'retail-other-equipment-hp-base-v1','region':'USA',
          'provenance':{'retail_armors_sha256':armor_sha,'retail_accessories_sha256':accessory_sha,'retail_enchant_sha256':enchant_sha,
                        'host_items_sha256':host_sha,'armor_selection':arm_sel,'accessory_selection':acc_sel,'enchant_selection':enc_sel},
          'items':[rows[x] for x in sorted(rows)]}
    with output.open('x',encoding='utf-8',newline='\n') as f:json.dump(data,f,ensure_ascii=False,separators=(',',':'));f.write('\n')
    return {'items':len(rows),'sha256':sha(output)}
def main():
    p=argparse.ArgumentParser();p.add_argument('--retail-root',required=True);p.add_argument('--host-items',required=True)
    p.add_argument('--armor-sha256',required=True);p.add_argument('--accessory-sha256',required=True);p.add_argument('--enchant-sha256',required=True);p.add_argument('--host-sha256',required=True);p.add_argument('--output',required=True)
    a=p.parse_args();print(json.dumps(extract(a.retail_root,a.host_items,a.armor_sha256,a.accessory_sha256,a.enchant_sha256,a.host_sha256,a.output)))
if __name__=='__main__':main()
