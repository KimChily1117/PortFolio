from pathlib import Path
import re,json,shutil,hashlib
r=Path('E:/task/ProjectDawnIntegration');src=Path('E:/task/C#/Project_Dawn/Assets');out=r/'UnityClient/Assets/Resources/DawnOriginal'
index={}
for p in src.rglob('*.meta'):
 m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
 if m:index[m[1]]=Path(str(p)[:-5])
existing={}
for p in (r/'UnityClient/Assets').rglob('*.meta'):
 m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig',errors='replace'),re.M)
 if m:existing[m[1]]=Path(str(p)[:-5])
records=[];seen=set()
def cp(p,dest=None):
 meta=Path(str(p)+'.meta');g=re.search(r'^guid: (\w+)',meta.read_text(encoding='utf-8-sig'),re.M)[1]
 if g in seen:return
 seen.add(g)
 dest=existing.get(g,dest or out/'FlowDependencies'/(g+p.suffix));dest.parent.mkdir(parents=True,exist_ok=True)
 if not dest.exists():shutil.copy2(p,dest);shutil.copy2(meta,str(dest)+'.meta')
 records.append(dict(source=str(p),destination=str(dest),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
 if p.suffix in ['.unity','.prefab','.anim','.controller','.mat','.asset','.renderTexture','.overrideController']:
  text=p.read_text(encoding='utf-8-sig',errors='replace')
  for dep in set(re.findall(r'guid: ([0-9a-f]{32})',text)):
   d=index.get(dep)
   if d and d.is_file() and d.suffix!='.cs':cp(d)
  # Custom original scripts reference original GameManager/network. Keep Unity UI/TMP, replace behavior with integration adapter.
  parts=re.split(r'(?=^--- !u!)',dest.read_text(encoding='utf-8-sig',errors='replace'),flags=re.M);removed=[];keep=[]
  for part in parts:
   match=re.search(r'^--- !u!114 &(\d+)',part)
   script=re.search(r'm_Script:.*guid: (\w+)',part)
   if match and script and script[1] in index and index[script[1]].suffix=='.cs':removed.append(match[1])
   else:keep.append(part)
  text=''.join(keep)
  for fid in removed:text=re.sub(r'^  - component: \{fileID: '+fid+r'\}\r?\n','',text,flags=re.M)
  dest.write_text(text,encoding='utf-8')
for name,new in [('Title(Login)','DawnLogin'),('Town','DawnTown'),('LoadingScene','DawnLoading')]:cp(src/'Scenes'/(name+'.unity'),r/'UnityClient/Assets/Scenes'/(new+'.unity'))
for p,new in [('Resources/Prefabs/UI/LoginPanel.prefab','LoginPanel'),('Resources/Prefabs/UI/PopUp/PartyPopUp.prefab','PartyPopUp')]:cp(src/p,out/(new+'.prefab'))
cp(src/'TextMesh Pro/Resources/TMP Settings.asset',r/'UnityClient/Assets/Resources/TMP Settings.asset')
cp(src/'Resources/Title BackGround.jpg',out/'TitleBackground.jpg')
for inc in (src/'TextMesh Pro/Shaders').glob('*.cginc'):
 shutil.copy2(inc,out/'FlowDependencies'/inc.name)
for name in ['CharacterSelect','seria_gate','bakal_ready']:
 matches=list((src/'Resources/Sounds').glob(name+'.*'))
 for p in matches:
  if p.suffix!='.meta':cp(p,out/'FlowSounds'/p.name)
(r/'Tools/flow-asset-provenance.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
print('Imported flow assets',len(records),'source bytes',sum(Path(x['source']).stat().st_size for x in records))
