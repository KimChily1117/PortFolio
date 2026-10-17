"""Read original battle CSB fields and extract verified UI/background PNG assets.

Inputs are read-only. The atlas is split by declared regions, with rotation and
trim offsets restored. Scene placement is supplied by our offline renderer, not
claimed to be the original Lua stage layout.
"""
import argparse
import hashlib
import json
from pathlib import Path
import runpy

ROOT = Path(__file__).resolve().parents[1]
CSB = runpy.run_path(str(ROOT / 'tools/parse-lobby-resource.py'))['CSB']

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def regions(path):
    result = {}
    current = None
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        if line and not line.startswith(' ') and ':' not in line:
            current = line
        elif current and line.startswith('  ') and ':' in line:
            key, value = line.strip().split(':', 1)
            result.setdefault(current, {})[key] = value.strip()
    return {k:v for k,v in result.items() if 'xy' in v and 'size' in v}

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--source', type=Path, default=ROOT/'.deps/epicseven-assets')
    ap.add_argument('--evidence-only', action='store_true')
    args = ap.parse_args()
    raw = args.source/'raw'
    analysis = args.source/'battle-analysis'
    analysis.mkdir(parents=True, exist_ok=True)
    layouts = []
    paths = set()
    for name in ['inbattle_hud','inbattle_hud_0','inbattle_order','inbattle_topbar',
                 'inbattle_target_guide','inbattle_state','inbattle_detail','skill_icon']:
        file = raw/'wnd'/(name+'.csb')
        data = CSB(file).read()
        layouts.append(dict(source='wnd/'+file.name,sha256=digest(file),**data))
        for node in data['nodes']:
            paths.update(p for p in node.get('resources',[]) if p.endswith('.png'))
    (analysis/'layouts.json').write_text(json.dumps(layouts,ensure_ascii=False,indent=2),encoding='utf-8')
    if args.evidence_only:
        print(json.dumps([{'source':x['source'],'nodes':x['node_count']}for x in layouts]));return
    from PIL import Image
    dest = ROOT/'Resources/reference/epicseven/battleui'
    paths.update('img/'+n for n in ['game_hud_bar_hp.png','game_hud_bar_hp_red.png','game_hud_bar_bg.png',
        'game_hud_target1.png','game_hud_target2.png','cm_hero_cirfrm2.png','cm_hero_cirbg2.png',
        'game_hud_timebg1.png','game_hud_timebg2.png'])
    paths.update(p.relative_to(raw).as_posix() for p in (raw/'skill').glob('sk_c*.png'))
    paths.update(p.relative_to(raw).as_posix() for p in (raw/'face').glob('*_s.png'))
    receipts = []
    for rel in sorted(paths):
        source=raw/rel
        if not source.is_file(): continue
        target=dest/rel; target.parent.mkdir(parents=True,exist_ok=True)
        with Image.open(source) as im:
            im.load(); im.convert('RGBA').save(target)
            receipts.append(dict(source=rel,sha256=digest(source),size=list(im.size),output=target.relative_to(dest).as_posix()))
    for group in ['forest','battle','nworld1']:
        atlas=raw/'bg'/(group+'.atlas')
        page=args.source/'converted/bg'/(group+'.png')
        with Image.open(page) as sheet:
            sheet.load()
            for key,fields in regions(atlas).items():
                x,y=map(int,fields['xy'].split(','));w,h=map(int,fields['size'].split(','))
                ow,oh=map(int,fields.get('orig',fields['size']).split(','));ox,oy=map(int,fields.get('offset','0,0').split(','))
                rotate=fields.get('rotate')=='true'
                tile=sheet.crop((x,y,x+(h if rotate else w),y+(w if rotate else h)))
                if rotate: tile=tile.transpose(Image.Transpose.ROTATE_270)
                restored=Image.new('RGBA',(ow,oh));restored.paste(tile,(ox,oh-h-oy))
                target=dest/'backgrounds'/group/(key+'.png');target.parent.mkdir(parents=True,exist_ok=True);restored.save(target)
                receipts.append(dict(source='bg/'+group+'.sct',sha256=digest(raw/'bg'/(group+'.sct')),
                                     atlas_sha256=digest(atlas),region=key,fields=fields,output=target.relative_to(dest).as_posix()))
    (dest/'sources.json').write_text(json.dumps(receipts,ensure_ascii=False,indent=2),encoding='utf-8')
    hero_icons=[]
    for hero,name in [('c1017','Achates'),('c1048','Aramintha'),('c1028','Clarissa'),('c1037','Dominiel')]:
        icons=['reference/epicseven/battleui/skill/sk_'+hero+'_'+str(i)+'.png' for i in range(1,4)]
        face='reference/epicseven/battleui/face/'+hero+'_s.png'
        if not all((ROOT/'Resources'/p).is_file() for p in icons+[face]):
            raise ValueError('Missing verified party icon: '+hero)
        hero_icons.append(dict(hero_id=hero,name=name,face=face,skills=icons,
                               evidence='Exact hero ID and skill ordinal in original PNG path; decoded successfully'))
    (dest/'hero-icons.json').write_text(json.dumps(hero_icons,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'layouts':len(layouts),'nodes':sum(x['node_count']for x in layouts),'decoded_images':len(receipts)},ensure_ascii=False))

if __name__=='__main__':main()
