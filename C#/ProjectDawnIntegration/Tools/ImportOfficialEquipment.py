"""Public, unauthenticated DNF equipment-dictionary name/image provenance.

Uses the same read-only paginated endpoint exposed by the official page.
No API key, account credentials, or game process access. Saves only selected
usable images; optional name catalog pagination saves metadata, not all images.
"""
from pathlib import Path
import argparse, csv, hashlib, io, json, time
import requests
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Research'/'equipment-web'
UNITY=ROOT/'UnityClient'/'Assets'/'Resources'/'DawnEquipment'
PAGE='https://df.nexon.com/guide/equipment'
FETCH=PAGE+'/fetch'
CDN='https://bbscdn.df.nexon.com/data7/showroom/static/icon/'

def digest(data):return hashlib.sha256(data).hexdigest()
def save(name,data):
    (OUT/name).write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')

def session():
    s=requests.Session()
    s.get(PAGE,timeout=30).raise_for_status()
    s.headers.update({'Referer':PAGE,'X-Requested-With':'XMLHttpRequest','Accept':'application/json'})
    return s

def fetch(s,page=0,keyword='',category='all'):
    params=dict(page=page,category=category,rarity='all',minLev=0,maxLev=0,hashtags='',keyword=keyword)
    r=s.get(FETCH,params=params,timeout=30);r.raise_for_status()
    data=r.json()
    if not data.get('success'):raise RuntimeError('Dictionary response unsuccessful')
    return data,r.url,digest(r.content)

def selected(s):
    data,url,response_hash=fetch(s,keyword='근원을 삼킨')
    save('selected-query-response.json',data)
    selected=[]
    for index,(kind,label) in enumerate([('katana','도'),('lswd','대검'),('sswd','소검'),('club','둔기'),('beamswd','광검')]):
        row=next(r for r in data['body']['list'] if r['itemName']=='근원을 삼킨 '+label)
        image_url=CDN+row['imageUrl']
        result=s.get(image_url,timeout=30);result.raise_for_status()
        im=Image.open(io.BytesIO(result.content)).convert('RGBA')
        name=f'official-origin-{kind}.png'
        (OUT/'Images'/name).write_bytes(result.content)
        im.save(UNITY/name)
        local=[]
        for p in (ROOT/'Research'/'equipment-names'/'Icons').glob(f'dnf-icon-{kind}-*.png'):
            original=Image.open(p).convert('RGBA')
            if original.size==im.size and original.tobytes()==im.tobytes():
                local.append(dict(path=str(p),sha256=digest(p.read_bytes()),comparison='exact RGBA pixel equality'))
        selected.append(dict(integrationTemplateId=92001+index,name=row['itemName'],kind=kind,
            officialDictionaryItemId=row['itemId'],officialLevel=row['level'],officialRarity=row['rarity']['nameKor'],
            sourcePage=PAGE+'?mode=equip&id='+row['itemId'],queryUrl=url,queryResponseSha256=response_hash,
            imageUrl=image_url,downloadSha256=digest(result.content),dimensions=list(im.size),
            resourcePath='DawnEquipment/'+Path(name).stem,unityFile=str(UNITY/name),
            localExtractedExactMatches=local,statsSource='ProjectDawn demo rules; not original DNF values'))
    save('selected-equipment.json',dict(schemaVersion=1,source='Official DNF public equipment dictionary',items=selected,
        limits=['Official dictionary itemId is a website identifier, not claimed as original game numeric item ID.',
                'Stats remain custom ProjectDawn data; model/animation/character weapon-layer mapping is not recovered.']))
    canvas=Image.new('RGB',(640,220),'#17202d');draw=ImageDraw.Draw(canvas)
    font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',16)
    for i,row in enumerate(selected):
        im=Image.open(row['unityFile']).convert('RGBA').resize((84,84),Image.Resampling.NEAREST)
        canvas.paste(im,(i*128+22,20),im)
        draw.text((i*128+10,120),'근원을 삼킨',font=font,fill='#e7b349')
        draw.text((i*128+40,145),row['name'].split()[-1],font=font,fill='white')
        draw.text((i*128+30,175),str(row['integrationTemplateId']),font=font,fill='#a9b6ca')
    canvas.save(OUT/'selected-equipment.png')
    print(json.dumps(dict(selected=len(selected),mapping=str(OUT/'selected-equipment.json')),ensure_ascii=True),flush=True)

def names(s,max_pages,category):
    all_rows=[];seen=set();pages=[];complete=False;reason='page limit reached'
    catalog_file='equipment-name-catalog-'+category+'.json'
    for number in range(max_pages):
        try:
            response,url,response_hash=fetch(s,page=number,category=category)
        except Exception as exc:
            reason=type(exc).__name__+': '+str(exc);break
        body=response['body'];rows=body['list']
        if rows and all(r['itemId'] in seen for r in rows):
            reason='Repeated page detected; stopped without claiming complete coverage';break
        pages.append(dict(page=number,url=url,responseSha256=response_hash,count=len(rows),hasNext=body['hasNext']))
        for row in rows:
            if row['itemId'] not in seen:
                seen.add(row['itemId']);all_rows.append(row)
        if not body['hasNext']:
            complete=True;reason='Official endpoint returned hasNext=false';break
        if number%25==0:
            save(catalog_file,dict(schemaVersion=1,complete=False,reason='Pagination in progress',filters=dict(category=category,rarity='all',minLev=0,maxLev=0,hashtags='',keyword=''),count=len(all_rows),pages=pages,items=all_rows))
            print('Names page',number,'records',len(all_rows),flush=True)
        time.sleep(.35)
    result=dict(schemaVersion=1,sourcePage=PAGE,complete=complete,reason=reason,
        filters=dict(category=category,rarity='all',minLev=0,maxLev=0,hashtags='',keyword=''),
        scope='All pages exposed by the ordinary equipment (not set/enchant) dictionary list under these filters.',
        count=len(all_rows),pages=pages,items=all_rows,
        limits=['Website catalog coverage only, not all installed Script.pvf records.',
                'Hidden/retired/non-equipment entries absent from public endpoint are unknown.',
                'No full icon download; imageUrl is recorded with its name as published.'])
    save(catalog_file,result)
    with (OUT/('equipment-name-catalog-'+category+'.csv')).open('w',encoding='utf-8-sig',newline='') as f:
        fields=['itemId','itemName','itemType','itemDetailType','level','imageUrl'];writer=csv.DictWriter(f,fieldnames=fields)
        writer.writeheader();writer.writerows({k:r.get(k) for k in fields} for r in all_rows)
    print(json.dumps(dict(names=len(all_rows),pages=len(pages),complete=complete,reason=reason),ensure_ascii=True),flush=True)

def legacy(s):
    items=[]
    for template_id,name,asset in [(1,'창성의 구원자 - 소검','official-savior-sswd'),
                                   (100,'오만에 가득찬 눈','official-pride-coat'),
                                   (101,'나태함을 가진 발','official-sloth-shoes')]:
        data,url,response_hash=fetch(s,keyword=name)
        row=next(r for r in data['body']['list'] if r['itemName']==name)
        image_url=CDN+row['imageUrl'];r=s.get(image_url,timeout=30);r.raise_for_status()
        im=Image.open(io.BytesIO(r.content)).convert('RGBA')
        (OUT/'Images'/(asset+'.png')).write_bytes(r.content);im.save(UNITY/(asset+'.png'))
        items.append(dict(integrationTemplateId=template_id,name=name,officialDictionaryItemId=row['itemId'],
                          sourcePage=PAGE+'?mode=equip&id='+row['itemId'],queryUrl=url,
                          queryResponseSha256=response_hash,imageUrl=image_url,downloadSha256=digest(r.content),
                          dimensions=list(im.size),resourcePath='DawnEquipment/'+asset,unityFile=str(UNITY/(asset+'.png')),
                          statsSource='Existing ProjectDawn demo data; not original DNF values'))
    save('existing-equipment.json',dict(schemaVersion=1,source='Official DNF public equipment dictionary',items=items))
    print('Existing equipment verified:',len(items),flush=True)

if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('--names',action='store_true');a.add_argument('--legacy',action='store_true');a.add_argument('--max-pages',type=int,default=3000);a.add_argument('--category',default='10_1')
    args=a.parse_args();OUT.mkdir(parents=True,exist_ok=True);(OUT/'Images').mkdir(exist_ok=True);UNITY.mkdir(exist_ok=True)
    s=session()
    if args.names:names(s,args.max_pages,args.category)
    elif args.legacy:legacy(s)
    else:selected(s)
