"""Read CocosStudio CSB FlatBuffers using the shipped CSParseBinary.fbs layout.

Read-only input; writes a compact, reproducible UI evidence report. No game code
is executed. Options are polymorphic tables: NodeTree.options.data points to the
class-specific options, whose first field is WidgetOptions (Node uses it directly).
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[1]

class CSB:
    def __init__(self, path):
        self.data = path.read_bytes()
        self.nodes = []
    def num(self, pos, fmt):
        size = struct.calcsize(fmt)
        if pos < 0 or pos + size > len(self.data):
            raise ValueError(f'out-of-range read at {pos}')
        return struct.unpack_from('<' + fmt, self.data, pos)[0]
    def ref(self, pos):
        return pos + self.num(pos, 'I') if pos else 0
    def field(self, table, index):
        if not table: return 0
        vt = table - self.num(table, 'i')
        if 4 + index * 2 >= self.num(vt, 'H'): return 0
        off = self.num(vt + 4 + index * 2, 'H')
        return table + off if off else 0
    def string(self, pos):
        if not pos: return ''
        start = self.ref(pos)
        size = self.num(start, 'I')
        if start + 4 + size > len(self.data): raise ValueError('invalid string')
        return self.data[start + 4:start + 4 + size].decode('utf-8')
    def vec(self, pos):
        if not pos: return []
        start = self.ref(pos)
        count = self.num(start, 'I')
        if count > 100000: raise ValueError('oversized vector')
        return [start + 4 + i * 4 for i in range(count)]
    def pair(self, table, index):
        pos = self.field(table, index)
        return [round(self.num(pos + i * 4, 'f'), 3) for i in range(2)] if pos else None
    def node(self, table, parent='', depth=0):
        if depth > 100: raise ValueError('node nesting limit')
        cls = self.string(self.field(table, 0))
        options = self.ref(self.field(table, 2))
        specific = self.ref(self.field(options, 0))
        widget = specific if cls == 'Node' else self.ref(self.field(specific, 0))
        name = self.string(self.field(widget, 0))
        path = parent + '/' + name
        row = dict(path=path, classname=cls, name=name,
                   position=self.pair(widget, 7), size=self.pair(widget, 11),
                   anchor=self.pair(widget, 9), scale=self.pair(widget, 8))
        for label, index, fmt, default in [('visible',4,'?',True), ('alpha',5,'B',255), ('touch',15,'?',False)]:
            p = self.field(widget, index)
            row[label] = self.num(p,fmt) if p else default
        resource_fields = {'Sprite':[1], 'ImageView':[1], 'Button':[1,2,3,4],
                           'Text':[1], 'TextBMFont':[1], 'ProjectNode':[]}
        resources = []
        for field in resource_fields.get(cls, []):
            res = self.ref(self.field(specific,field))
            if res:
                resource = self.string(self.field(res,0))
                if resource: resources.append(resource)
        if resources: row['resources'] = resources
        text_field = {'Text':4, 'Button':5, 'TextBMFont':2}.get(cls)
        if text_field is not None:
            row['text'] = self.string(self.field(specific,text_field))
        self.nodes.append(row)
        for p in self.vec(self.field(table,1)): self.node(self.ref(p),path,depth+1)
    def read(self):
        root = self.num(0,'I')
        version = self.string(self.field(root,0))
        self.node(self.ref(self.field(root,3)))
        return dict(version=version,node_count=len(self.nodes),nodes=self.nodes)

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--source',type=Path,default=ROOT/'.deps/epicseven-assets/raw')
    ap.add_argument('--output',type=Path,default=ROOT/'.deps/epicseven-assets/lobby-analysis/layouts.json')
    ap.add_argument('--import-ui',action='store_true',help='Decode verified PNGs and atlas regions into local frontend assets')
    args=ap.parse_args()
    files=['wnd/lobby.csb','wnd/lobby_ui.csb','wnd/lobby_ui_renew.csb',
           'wnd/battle_stage_list.csb','wnd/battle_ready.csb','wnd/battle_ready_renew.csb',
           'wnd/hero_detail_base.csb','wnd/hero_detail_info.csb','wnd/unit_list.csb',
           'story_layouts/story_tm_ma_1.csb']
    result={'schema':'cocos2d-x/cocos/editor-support/cocostudio/fbs-files/CSParseBinary.fbs', 'files':[]}
    for rel in files:
        path=args.source/rel
        if not path.exists(): continue
        item={'source':rel,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
        try: item.update(CSB(path).read())
        except (ValueError,struct.error,UnicodeDecodeError) as error: item['error']=str(error)
        result['files'].append(item)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    if args.import_ui:
        from PIL import Image
        dest=ROOT/'Resources/reference/epicseven/frontend'
        dest.mkdir(parents=True,exist_ok=True)
        selected=['img/lobby_pub_mockup.png','worldmap/worldmap_paper.png',
                  'img/_btn_green.png','img/_btn_gold.png','img/_btn_small_black.png',
                  'img/main_hud_btn_hero.png','img/main_hud_btn_battle.png','img/main_hud_btn_archieve.png',
                  'img/cm_icon_star.png']
        receipts=[]
        for rel in selected:
            source=args.source/rel
            with Image.open(source) as im:
                im.load()
                im.convert('RGBA').save(dest/source.name)
                receipts.append(dict(source=rel,output=source.name,size=list(im.size),sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
        atlas=args.source/'worldmap/ritania.atlas'
        lines=atlas.read_text().splitlines()
        regions={}
        for i,line in enumerate(lines):
            if line.startswith('ritania/'):
                props=dict(x.strip().split(': ',1) for x in lines[i+1:i+7])
                regions[line]=props
        with Image.open(args.source/'worldmap/ritania.png') as sheet:
            for i in range(1,11):
                key='ritania/landmap_'+str(i+2)
                if key not in regions: continue
                p=regions[key]; x,y=map(int,p['xy'].split(','));w,h=map(int,p['size'].split(','))
                rotated=p['rotate']=='true'
                im=sheet.crop((x,y,x+(h if rotated else w),y+(w if rotated else h)))
                if rotated: im=im.transpose(Image.Transpose.ROTATE_270)
                name='region-'+str(i)+'.png'; im.save(dest/name)
                receipts.append(dict(source='worldmap/ritania.png',atlas_region=key,output=name,atlas_fields=p,
                                     sha256=hashlib.sha256((args.source/'worldmap/ritania.png').read_bytes()).hexdigest(),
                                     atlas_sha256=hashlib.sha256(atlas.read_bytes()).hexdigest()))
        catalog=ROOT/'Resources/reference/epicseven/catalog.json'
        if catalog.exists():
            ids={item.get('name') for item in json.loads(catalog.read_text(encoding='utf-8'))['items'] if '/portrait/' in item.get('path','')}
            for hero in sorted(ids):
                source=args.source/'face'/(hero+'_s.png')
                if source.exists():
                    with Image.open(source) as im:
                        im.convert('RGBA').save(dest/(hero+'_face.png'))
                    receipts.append(dict(source=source.relative_to(args.source).as_posix(),output=hero+'_face.png',sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
        (dest/'sources.json').write_text(json.dumps(receipts,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps([{k:v for k,v in f.items() if k!='nodes'} for f in result['files']],ensure_ascii=False))
    if any('error' in f for f in result['files']): raise SystemExit(1)

if __name__=='__main__': main()
