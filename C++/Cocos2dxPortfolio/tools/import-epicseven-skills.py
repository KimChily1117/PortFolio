"""Compile observed SEQANI actions and local CFX/Spine dependencies for presentation.

No filenames are treated as proof of current skill choreography. The complete
SEQANI inventory is reported; only explicitly checked actor/sequence pairs are
staged for playback. Raw action trees and provenance remain alongside schedules.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.deps/epicseven-assets'
RAW=BASE/'raw'
OUT=ROOT/'Resources/reference/skills'
TOOLS=BASE/'tools'
sys.path[:0]=[str(TOOLS/'deps'),str(TOOLS),str(TOOLS/'spine-conversion'),str(TOOLS/'spine-conversion/tools')]
import E7_Scsp2Json as v3
import scsp_to_json as dispatch
from convert_textures import convert as decode_texture
from PIL import Image
spec=importlib.util.spec_from_file_location('spine_adapter',ROOT/'tools/import-epicseven-spine.py')
adapter=importlib.util.module_from_spec(spec);spec.loader.exec_module(adapter)
os.environ['PYTHONPATH']=str(TOOLS/'deps')
SELECTED=[('c1017.s3','Achates','c1017','achates','c_acha_skill_03'),
          ('c1048.s3','Aramintha','c1048','araminta','c_araminta_skill_03'),
          ('c1028.s3','Clarissa','c1028','clarisa','c_clarisa_skill_03'),
          ('c1037.s3','Dominiel','c1037','dominiel','c_dominiel_skill_03')]

def resource(path):return path.relative_to(ROOT/'Resources').as_posix()
def number(value,default=0):
    try:return float(value)
    except (ValueError,TypeError):return default
def ms(value):return number(value)/1000
def duration(value):
    if isinstance(value,dict):return max([number(value.get('time',0))]+[duration(v) for k,v in value.items() if k!='_e7_flips'])
    if isinstance(value,list):return max([0]+[duration(v) for v in value])
    return 0
def save(path,data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
def reserve():
    if shutil.disk_usage(BASE).free<4*1024**3:raise RuntimeError('4 GiB reserve reached')
def texture(src):
    target=OUT/'assets'/src.relative_to(RAW).with_suffix('.png')
    if not target.exists():
        reserve();target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(decode_texture(src.read_bytes())[0])
    return target
def skeleton(src,visual=True):
    rel=src.relative_to(RAW);target=OUT/'assets'/rel.parent/src.stem
    target.mkdir(parents=True,exist_ok=True)
    decoded=target/(src.stem+'.source.json')
    if not decoded.exists():
        candidates=[BASE/'animated'/rel.parent/src.stem/(src.stem+'.json'),BASE/'native-3.8'/rel.parent/src.stem/(src.stem+'.json')]
        ready=next((p for p in candidates if p.exists()),None)
        if ready:shutil.copy2(ready,decoded)
        else:
            reserve();stage=BASE/'work/skill-conversion'/rel
            stage.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,stage)
            dispatch.convert(stage,decoded)
    original=json.loads(decoded.read_text())
    if not visual:return original
    output=target/(src.stem+'.cocos.json')
    save(output,adapter.adapt(original))
    atlas=src.with_suffix('.atlas')
    for _ in range(8):
        text=atlas.read_text(encoding='utf-8-sig');alias=text.strip()
        if '\n' not in alias and alias.endswith('.atlas'):atlas=atlas.parent/alias
        else:break
    lines=text.splitlines()
    for i,line in enumerate(lines):
        if not line.strip() or (i and lines[i-1].strip()):continue
        if i+1>=len(lines) or not lines[i+1].strip().startswith(('size:','format:')):continue
        source=atlas.parent/line.strip()
        png=texture(source);shutil.copy2(png,target/png.name)
        lines[i]=png.name
    atlas_out=target/(src.stem+'.atlas');atlas_out.write_text('\n'.join(lines),encoding='utf-8')
    return {'json':resource(output),'atlas':resource(atlas_out),'animations':{k:duration(v) for k,v in original['animations'].items()},'data':original}

class Assets:
    def __init__(self):self.effects={};self.warnings=[];self.cameras={}
    def effect(self,source):
        name=Path(source).stem
        if name in self.effects:return self.effects[name]
        path=RAW/'effect'/(name+'.cfx')
        if not path.exists():raise ValueError('Missing CFX '+str(path))
        raw=plistlib.loads(path.read_bytes());items=[]
        result={'source':path.relative_to(RAW).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'items':items,'duration':0}
        self.effects[name]=result
        for index,p in enumerate(raw.get('primitive',[])):
            record={'params':p,'delay':ms(p.get('delay',0)),'type':p.get('format',''),'source':p.get('source','')}
            try:
                if record['type']=='spine':
                    rig=skeleton(RAW/'effect'/(record['source']+'.scsp'))
                    ani=p.get('ani') or ('animation' if 'animation' in rig['animations'] else next(iter(rig['animations']),''))
                    if ani not in rig['animations']:raise ValueError('Missing CFX animation '+ani+' in effect/'+record['source']+'.scsp (CFX '+name+')')
                    record.update(json=rig['json'],atlas=rig['atlas'],animation=ani,duration=rig['animations'][ani])
                elif record['type']=='particle':
                    pt=RAW/'effect'/(record['source']+'.particle')
                    if not pt.exists():pt=RAW/'particle'/(record['source']+'.particle')
                    emitters=plistlib.loads(pt.read_bytes()).get('emitters',[]);record['emitters']=[]
                    for eidx,e in enumerate(emitters):
                        if e.get('disable'):continue
                        e=copy.deepcopy(e);tex=e.get('textureFileName')
                        if tex:
                            png=texture(RAW/tex);e['textureFileName']=resource(png)
                        # All original emitter fields remain. Cocos consumes its standard subset.
                        e.setdefault('rotationIsDir',False)
                        target=OUT/'assets/emitters'/(name+'_'+str(index)+'_'+str(eidx)+'.plist')
                        target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(plistlib.dumps(e))
                        record['emitters'].append({'path':resource(target),'delay':number(e.get('startDelay',0)),'source':e})
                        if e.get('colorBlends'):self.warnings.append('Particle custom colorBlends preserved but standard Cocos interpolation differs: '+str(pt.relative_to(RAW)))
                    record['duration']=max([0]+[max(0,number(e.get('duration',0)))+number(e.get('particleLifespan',0)) for e in emitters])
                else:
                    record['unsupported']='CFX primitive format '+record['type'];self.warnings.append(record['unsupported'])
            except Exception as ex:
                record['unsupported']=str(ex);self.warnings.append(str(ex))
            result['duration']=max(result['duration'],record['delay']+record.get('duration',0));items.append(record)
        return result
    def camera(self,source):
        name=Path(source).stem
        if name in self.cameras:return self.cameras[name]
        path=RAW/'camera'/(name+'.scsp')
        if not path.exists():raise ValueError('Missing cameraSCSP '+name)
        data=skeleton(path,False)
        value={'source':path.relative_to(RAW).as_posix(),'skeleton':data}
        self.cameras[name]=value
        return value

class Compiler:
    def __init__(self,model,assets,cut):
        self.model=model;self.assets=assets;self.cut=cut;self.events=[];self.warnings=[];self.errors=[];self.occurrences=[];self.ordinal=0;self.explicit_cut=False
    def emit(self,kind,at,span=0,params=None,evidence='SEQANI'):
        event={'type':kind,'time':round(max(0,at),6),'duration':round(max(0,span),6),'params':params or {},'evidence':evidence,'order':self.ordinal}
        self.ordinal+=1;self.events.append(event);return event
    def parallel(self,nodes,at,context=None):return max([0]+[self.walk(n,at,context) for n in nodes])
    def walk(self,node,at,context=None):
        kind=node.get('etty','ROOT');nodes=node.get('nodes',[]);params={k:v for k,v in node.items() if k not in ('nodes','etty')}
        delay=ms(node.get('delay',0));at+=delay;span=0
        if kind in ('ROOT','SPAWN'):return delay+self.parallel(nodes,at,context)
        if kind=='SEQ':
            elapsed=0
            for child in nodes:elapsed+=self.walk(child,at+elapsed,context)
            return 0 if node.get('without') else delay+elapsed
        if kind=='ANI':
            name=node.get('ani','');animation=self.model['animations'].get(name)
            if animation is None:self.errors.append('Missing model animation: '+name);return 0
            span=duration(animation);ctx={'animation':name,'start':at,'duration':span,'markers':animation.get('events',[])}
            self.emit('ANI',at,span,params);self.occurrences.append(ctx)
            self.parallel(nodes,at,ctx)
            if not self.explicit_cut:
                for marker in ctx['markers']:
                    if marker.get('name')=='cutin':self.emit('CUTIN',at+number(marker.get('time',0)),0,{'path':self.cut},'SCSP animation event cutin')
            return 0 if node.get('async') else delay+span
        if kind in ('ANI_POS','ANI_SCOPE'):
            if not context:self.errors.append(kind+' outside ANI');return 0
            start=0;end=context['duration']
            if kind=='ANI_SCOPE':
                for field in ['from','to']:
                    if node.get(field):
                        matches=[number(m.get('time',0)) for m in context['markers'] if m.get('name')==node[field]]
                        if not matches:self.errors.append('Missing '+context['animation']+' marker '+node[field]);return 0
                        if field=='from':start=matches[0]
                        else:end=matches[-1]
                at=context['start']+start;delay=0
            if at-context['start']>context['duration']+0.05:self.errors.append('Animation-relative action beyond currentclip: '+context['animation'])
            self.parallel(nodes,at,{**context,'scopeDuration':max(0,end-start)})
            return 0
        if kind=='DELAY':return 0 if node.get('without') or node.get('async') else delay+ms(node.get('time',0))
        if kind=='EFFECT' or kind=='BGSTAGE':
            try:
                effect=self.assets.effect(node.get('source',''));params['effect']=Path(node['source']).stem;span=effect['duration']
            except Exception as ex:self.errors.append(str(ex))
            self.emit(kind,at,span,params);self.parallel(nodes,at,context)
        elif kind=='SHAKE':
            if node.get('source'):
                try:self.assets.camera(node['source'])
                except Exception as ex:self.warnings.append(str(ex))
            span=max(0,ms(node.get('duration',0)));self.emit(kind,at,span,params)
        elif kind=='DARK' or kind=='COLOR_FADE':
            span=max(0,ms(node.get('duration',0)))+ms(node.get('fadeIn',0))+ms(node.get('fadeOut',0))
            self.emit(kind,at,span,params);span=0 # persistent visual action does not block the sequence
        elif kind in ('CAM','MOVE','LAYOUT','BY_TRANSFORM'):
            span=ms(node.get('time',0))
            if kind=='MOVE' and not span and context:span=context.get('scopeDuration',0)
            self.emit(kind,at,span,params)
            # CAM is nonblocking unless explicit block; MOVE follows scope/duration.
            if kind=='CAM' and not node.get('block'):span=0
        elif kind=='CUTIN':
            params['path']=self.cut;self.emit(kind,at,0,params)
        elif kind in ('STOP','RMCHILD','IDLE','BGSHOW','VISIBLE','FIRE'):
            self.emit(kind,at,0,params)
        elif kind=='BGHATCH':
            span=ms(node.get('time',0));self.emit(kind,at,span,params)
            self.warnings.append('BGHATCH uses shipped shader and textures; host speed-to-UV and screen geometry mapping remain unverified')
        elif kind=='ONEVENT':return 0
        else:
            self.emit('UNSUPPORTED',at,0,{'sourceAction':kind,**params});self.warnings.append('Unsupported action '+kind)
        return 0 if node.get('async') or node.get('without') else delay+span
    def compile(self,source):
        self.explicit_cut='"CUTIN"' in json.dumps(source)
        start=source.get('start',{});self.walk(start.get('action',{}),0)
        for registration in start.get('events',{}).get('nodes',[]):
            if registration.get('etty')!='ONEVENT':continue
            hits=0
            for occ in self.occurrences:
                if registration.get('ani') and registration['ani']!=occ['animation']:continue
                for marker in occ['markers']:
                    if marker.get('name')==registration.get('name'):
                        self.parallel(registration.get('nodes',[]),occ['start']+number(marker.get('time',0)),occ);hits+=1
            if not hits:self.errors.append('Unresolved ONEVENT '+registration.get('name',''))
        self.events.sort(key=lambda e:(e['time'],e['order']))
        end=max([0]+[e['time']+e['duration'] for e in self.events])
        self.warnings.extend(['Camera focus/location numbers, log/vel easing and movement adapter are not verified against the original runtime',
                              'Battle stage camera, BGSHOW and opponent layout are bound by host; source numeric mapping/easing remain unverified'])
        if not any(e['type']=='FIRE' for e in self.events):
            self.warnings.append('No source FIRE/hit timing recovered for this sequence; host gameplay applies impact at presentation completion')
        return {'duration':end,'events':self.events,'warnings':sorted(set(self.warnings+self.assets.warnings)),'errors':sorted(set(self.errors)),
                'semantics':'SEQ=sum;SPAWN/ROOT=parallel; ANI durations and markers from currentSCSP. Camera location/focus adapter and particle custom curves remain explicitly unverified.'}

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--self-test',action='store_true');args=ap.parse_args()
    if args.self_test:
        c=Compiler({'animations':{}},Assets(),'')
        assert abs(c.walk({'etty':'SEQ','nodes':[{'etty':'DELAY','time':100},{'etty':'DELAY','time':200}]},0)-0.3)<1e-9
        assert c.walk({'etty':'SPAWN','nodes':[{'etty':'DELAY','time':100},{'etty':'DELAY','time':200}]},0)==0.2
        print('SEQ/SPAWN time composition passed');return
    OUT.mkdir(parents=True,exist_ok=True);skills=[];inventory=[]
    shader_dir=OUT/'assets/hatch';shader_dir.mkdir(parents=True,exist_ok=True)
    for ext in ('vert','frag'):
        source=RAW/'system/program'/('sprite_hatch.'+ext)
        shutil.copy2(source,shader_dir/source.name)
        text=source.read_text().replace('CC_PMatrix','u_MVPMatrix').replace('CC_Texture0','u_texture')
        text=('uniform mat4 u_MVPMatrix;\n' if ext=='vert' else 'uniform sampler2D u_texture;\n')+text
        (shader_dir/('sprite_hatch.cocos.'+ext)).write_text(text)
    for n in range(1,4):shutil.copy2(RAW/'effect'/f'bg_hatch_{n}.png',shader_dir/f'bg_hatch_{n}.png')
    for src in sorted((RAW/'skillpt').glob('*.seqani')):
        inventory.append({'source':src.relative_to(RAW).as_posix(),'sha256':hashlib.sha256(src.read_bytes()).hexdigest(),
                          's3Candidate':bool(re.search(r'(?:skill_03|skill3)',src.stem)),'status':'not_mapped_to_current_actor','raw':json.loads(src.read_text())})
    for sid,name,hero,actor,sequence in SELECTED:
        assets=Assets();rig=skeleton(RAW/'model'/(actor+'.scsp'))
        seq=RAW/'skillpt'/(sequence+'.seqani');raw=json.loads(seq.read_text())
        cut='reference/epicseven/cut/fhd/sk_'+hero+'_3.webp'
        c=Compiler(rig['data'],assets,cut);timeline=c.compile(raw)
        timeline.update(schema=1,id=sid,source=seq.relative_to(RAW).as_posix(),sourceSha256=hashlib.sha256(seq.read_bytes()).hexdigest(),rawActions=raw,effects=assets.effects,cameras=assets.cameras)
        target=OUT/'timelines'/(sid+'.json');save(target,timeline)
        portrait=ROOT/'Resources/reference/epicseven/spine/portrait'/hero
        portrait_json=portrait/(hero+'.cocos.json');portrait_atlas=portrait/(hero+'.atlas')
        if not portrait_json.exists():
            try:
                p=skeleton(RAW/'portrait'/(hero+'.scsp'));portrait_json=ROOT/'Resources'/p['json'];portrait_atlas=ROOT/'Resources'/p['atlas']
            except Exception:pass
        entry={'id':sid,'name':name,'heroId':hero,'gender':'female','modelJson':rig['json'],'modelAtlas':rig['atlas'],
               'portraitJson':resource(portrait_json) if portrait_json.exists() else '', 'portraitAtlas':resource(portrait_atlas) if portrait_atlas.exists() else '',
               'cutPath':cut,'timelinePath':resource(target),'playable':not timeline['errors'],'limitations':timeline['warnings']+timeline['errors'],
               'duration':timeline['duration'],'sequenceSource':seq.relative_to(RAW).as_posix()}
        skills.append(entry)
        next(r for r in inventory if r['source']==entry['sequenceSource']).update(status='compiled' if entry['playable'] else 'source_incompatible',skillId=sid,errors=timeline['errors'])
        print(sid,'events',len(timeline['events']),'effects',len(assets.effects),'errors',timeline['errors'],flush=True)
    save(OUT/'catalog.json',{'schema':1,'initialSkillIds':[s[0] for s in SELECTED[:4]],'skills':skills,
                           'sourceCoverage':[{k:v for k,v in entry.items() if k!='raw'} for entry in inventory]})
    save(BASE/'cutscene-analysis/skill-presentation-coverage.json',{'schema':1,'sequenceCount':len(inventory),'compiled':len(skills),'entries':inventory})

if __name__=='__main__':main()
