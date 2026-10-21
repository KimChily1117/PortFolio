"""Copy the existing Dawn HUD prefab and its GUID dependencies, plus original meteor clips.
No original file is changed. Keep a provenance manifest for every copied asset.
"""
from pathlib import Path
import re, shutil, json
root=Path(__file__).resolve().parents[1]
source=Path(r'E:\task\C#\Project_Dawn\Assets')
target=root/'UnityClient/Assets/Resources/DawnOriginal'
target.mkdir(parents=True,exist_ok=True)
index={}
for meta in source.rglob('*.meta'):
    match=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig',errors='replace'),re.M)
    if match:index[match[1]]=Path(str(meta)[:-5])
records={}
def copy_asset(asset,dest=None):
    if str(asset) in records:return records[str(asset)]['resource']
    meta=Path(str(asset)+'.meta')
    guid=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)[1]
    dest=dest or target/'Dependencies'/(guid+asset.suffix)
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(asset,dest);shutil.copy2(meta,str(dest)+'.meta')
    resource='DawnOriginal/'+str(dest.relative_to(target).with_suffix('')).replace('\\','/')
    records[str(asset)]=dict(destination=str(dest),resource=resource,guid=guid)
    if asset.suffix in ('.prefab','.controller','.anim','.mat'):
        for dep in set(re.findall(r'guid: ([0-9a-f]{32})',asset.read_text(encoding='utf-8-sig'))):
            if dep in index and index[dep].is_file() and index[dep].suffix!='.cs':copy_asset(index[dep])
    return resource
copy_asset(source/'Resources/Prefabs/UI/Scene/HUD.prefab',target/'HUD.prefab')
clips=[]
for name in ['Bakal_Skill_Meteor','Bakal_Skill_MeteorInside','bakal_skill_explosion_effect','Bakal_Skill_AreaExplosion']:
    asset=source/'Resources/Arts/Animation/Enemy'/(name+'.anim')
    text=asset.read_text(encoding='utf-8-sig')
    frames=re.findall(r'- time: ([\d.eE+-]+)\s+value: \{fileID: (-?\d+), guid: (\w+)',text)
    names=[]
    for t,fid,g in frames:
        meta=Path(str(index[g])+'.meta').read_text(encoding='utf-8-sig')
        table=meta.split('nameFileIdTable:',1)[-1]
        match=re.search(r'^      (.+): '+re.escape(fid)+r'$',table,re.M)
        names.append(match[1] if match else '')
    clips.append(dict(name=name,times=[float(t) for t,fid,g in frames],resources=[copy_asset(index[g]) for t,fid,g in frames],names=names))
(target/'meteor-clips.json').write_text(json.dumps(dict(clips=clips),indent=2),encoding='utf-8')
(root/'Tools/dawn-asset-provenance.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print('Copied',len(records),'original HUD/meteor assets')
