"""Summarize a previously generated read-only resource-bonus comparison."""
import argparse
import hashlib
import json
from pathlib import Path

def summarize(path,limit=10):
    if not 0<=limit<=1000:raise ValueError('Limit must be between 0 and 1000')
    path=Path(path);raw=path.read_bytes();data=json.loads(raw)
    if data.get('purpose')!='retail-item-resource-bonus-source-comparison-v1' or not data.get('read_only'):
        raise ValueError('Unsupported item bonus comparison')
    differences=[]
    for item in data['items']:
        for stat,field in item['fields'].items():
            if field['status']!='fields_differ':continue
            retail=field['normal']['value'] if field['normal'] is not None else 0
            host=field['host']['bonus'];differences.append({'id':item['id'],'name':item['name'],'kind':item['kind'],
                'stat':stat,'retail':retail,'host':host,'difference':retail-host})
    differences.sort(key=lambda x:(-abs(x['difference']),x['id'],x['stat']))
    return {'comparison_sha256':hashlib.sha256(raw).hexdigest(),'items':len(data['items']),'counts':data['counts'],
            'largest_field_differences':differences[:limit],
            'limitations':data['limitations']}

def main():
    p=argparse.ArgumentParser();p.add_argument('--input',required=True);p.add_argument('--limit',type=int,default=10)
    a=p.parse_args();print(json.dumps(summarize(a.input,a.limit),indent=2,ensure_ascii=False))
if __name__=='__main__':main()
