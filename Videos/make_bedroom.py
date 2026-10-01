import sys,struct,math,json,subprocess
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw,ImageEnhance,ImageFont
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'.tools/video-deps'))
import imageio_ffmpeg
ASSETS=ROOT/'.tools/lesson-assets'
class Reader:
    def __init__(self,data): self.data=data;self.pos=0
    def read(self,fmt):
        n=struct.calcsize('<'+fmt);v=struct.unpack_from('<'+fmt,self.data,self.pos);self.pos+=n
        return v[0] if len(v)==1 else v
    def string(self): n=self.read('i');v=self.data[self.pos:self.pos+n].decode('utf8');self.pos+=n;return v
    def table(self): return {self.read('i'):self.string() for _ in range(self.read('i'))}
TEXTURES={}
def build(prefix):
    r=Reader((ASSETS/(prefix+'_build.bytes')).read_bytes());assert r.read('4s')==b'BILD'
    version=r.read('i');count=r.read('i');r.read('i');r.string();symbols={}
    for _ in range(count):
        key=r.read('i')
        if version==10:r.read('i')
        r.read('i');r.read('i');frames=[]
        for _ in range(r.read('i')):
            start,duration,atlas=r.read('iii');cx,cy,w,h,u0,v0,u1,v1=r.read('8f')
            texture_key=(prefix,atlas)
            if texture_key not in TEXTURES:TEXTURES[texture_key]=Image.open(ASSETS/(prefix+'_'+str(atlas)+'.png')).convert('RGBA')
            texture=TEXTURES[texture_key]
            crop=texture.crop((round(u0*texture.width),round(v0*texture.height),round(u1*texture.width),round(v1*texture.height)))
            frames.append((start,duration,crop,(cx-w/2,cy-h/2,w,h)))
        symbols[key]=frames
    return symbols,r.table()
def animation(prefix):
    r=Reader((ASSETS/(prefix+'_anim.bytes')).read_bytes());assert r.read('4s')==b'ANIM';assert r.read('i')==5
    r.read('i');r.read('i');clips={}
    for _ in range(r.read('i')):
        name=r.string();r.read('i');fps=r.read('f');frames=[]
        for _ in range(r.read('i')):
            r.read('4f');els=[]
            for _ in range(r.read('i')):
                sym,idx,folder,flags=r.read('4i');colors=r.read('4f');matrix=r.read('7f');els.append((sym,idx,colors,matrix))
            frames.append(els)
        clips[name]=(fps,frames)
    return clips
class Renderer:
    def __init__(self,prefix,clips):self.symbols,self.names=build(prefix);self.clips=clips;self.cache={}
    def frame(self,clip,t,size=(300,360),origin=(150,310),scale=.8,equipment=None):
        fps,frames=self.clips[clip];idx=int(max(0,t)*fps)%len(frames);key=(clip,idx,size,origin,scale,equipment)
        if key in self.cache:return self.cache[key]
        out=Image.new('RGBA',size)
        for sym,index,color,m in reversed(frames[idx]):
            name=self.names.get(sym,'').lower()
            attached=(equipment=='gun' and name=='snapto_rgthand') or (equipment=='material' and name=='snapto_chest')
            if (name.startswith('snapto') and name not in ('snapto_headshape','snapto_mouth','snapto_eyes','snapto_hair')) or name in ('skirt','necklace'):
                if not attached:continue
            symbols=self.symbols.get(sym,[])
            entry=next((f for f in symbols if f[0]<=index<f[0]+f[1]),None)
            if attached:
                entry=gun_entry if equipment=='gun' else material_entry
            if entry is None:continue
            _,_,sprite,(bx,by,bw,bh)=entry
            if sprite.width==0 or sprite.height==0:continue
            a,b,c,d,tx,ty,_=m
            matrix=np.array([[a*scale*bw/sprite.width,c*scale*bh/sprite.height,origin[0]+scale*(a*bx+c*by+tx)],
                             [b*scale*bw/sprite.width,d*scale*bh/sprite.height,origin[1]+scale*(b*bx+d*by+ty)], [0,0,1.]])
            pts=matrix@np.array([[0,sprite.width,0,sprite.width],[0,0,sprite.height,sprite.height],[1,1,1,1]])
            x0,y0=np.floor(pts[:2].min(axis=1)).astype(int);x1,y1=np.ceil(pts[:2].max(axis=1)).astype(int)
            if x1<=x0 or y1<=y0:continue
            matrix[0,2]-=x0;matrix[1,2]-=y0
            try:inv=np.linalg.inv(matrix)
            except np.linalg.LinAlgError:continue
            patch=sprite.transform((int(x1-x0),int(y1-y0)),Image.Transform.AFFINE,tuple(inv[:2].flatten()),resample=Image.Resampling.BICUBIC)
            out.alpha_composite(patch,(int(x0),int(y0)))
        self.cache[key]=out;return out

def ease(v):v=max(0,min(1,v));return v*v*(3-2*v)
def lerp(a,b,v):return a+(b-a)*ease(v)
def tint(image,color):
    im=Image.new('RGBA',image.size,color);im.putalpha(image.getchannel('A').point(lambda x:int(x*.6)));return im

idles=animation('anim_idles_default');loco=animation('anim_loco_new');construction=animation('anim_construction_default')
actor=Renderer('body_comp_default',{**idles,**loco,**construction})
# Apply the same symbol replacements used by the game's Accessorizer.
for prefix,mapping in (
    ('head_swap',{'snapto_headshape':'headshape_001','snapto_eyes':'eyes_001','snapto_mouth':'mouth_001'}),
    ('hair_swap',{'snapto_hair':'hair_001'}),
    ('body_swap',{'torso':'torso_001','arm_lower':'arm_lower_001','arm_upper':'arm_upper_001','leg_skin':'leg_skin_001'})):
    symbols,names=build(prefix);by_name={name:key for key,name in names.items()}
    for target,source in mapping.items():
        target_key=next(key for key,name in actor.names.items() if name==target)
        actor.symbols[target_key]=symbols[by_name[source]]
bed=Renderer('bedlg',animation('bedlg'));door=Renderer('door_manual',animation('door_manual'))
gun_symbols,gun_names=build('constructor_gun')
gun_entry=next(iter(gun_symbols.values()))[0]
material_symbols,material_names=build('sand_stone')
material_entry=material_symbols[next(k for k,n in material_names.items() if n=='sand_stone')][0]
# Review native body composition before encoding the complete scene.
actor.frame('idle_default',0).save(ROOT/'Videos/native-body-review.png')
bed.frame('off',0,size=(220,150),origin=(110,135),scale=.6).save(ROOT/'Videos/native-bed-review.png')
if '--inspect' in sys.argv:
    print('BUILD',actor.names)
    for key,name in actor.names.items():
        if name in ('leg','foot','leg_skin','pelvis'):
            print(name,[(f[0],f[2].size,f[3],f[2].getbbox()) for f in actor.symbols[key]][:4])
    for prefix in ('head_swap','head_master_swap','body_swap','hair_swap'):
        symbols,names=build(prefix);print(prefix,names)
    actor.frame('idle_default',0,size=(700,700),origin=(350,350),scale=1).save(ROOT/'Videos/body-wide.png')
    for sym,idx,c,m in actor.clips['idle_default'][1][0]:print('ELEMENT',sym,actor.names.get(sym),idx,'matrix',m,'color',c,'frames',[(f[0],f[1]) for f in actor.symbols.get(sym,[])][:4])
if '--review' in sys.argv or '--inspect' in sys.argv:sys.exit(0)
W,H=960,540;FPS=30;DURATION=36
floor=432
# Official tutorials use a blue blueprint field with a soft edge vignette.
base=Image.new('RGBA',(W,H));pixels=base.load()
for y in range(H):
    for x in range(W):
        edge=min(1,((x-W/2)/(W/2))**2*.55+((y-H/2)/(H/2))**2*.25)
        pixels[x,y]=(round(63+55*edge),round(138+28*edge),round(207+10*edge),255)
pattern=Image.new('RGBA',(W,H));d=ImageDraw.Draw(pattern)
for x in range(0,W,22):d.line((x,0,x,H),fill=(210,234,250,12),width=1)
for y in range(0,H,22):d.line((0,y,W,y),fill=(210,234,250,12),width=1)
for j in range(75):
    x=(j*137)%W;y=(j*83)%H
    d.line((x,y,x+28,y,x+28,y+38,x+54,y+38),fill=(205,232,247,15),width=1)
base.alpha_composite(pattern)
d=ImageDraw.Draw(base)
tile=Renderer('floor_basic',animation('floor_basic'))
tile_image=tile.frame('ui',0,size=(400,400),origin=(200,200),scale=.7)
tile_image=tile_image.crop(tile_image.getbbox()).resize((46,46),Image.Resampling.LANCZOS)
for x in range(92,928,44):base.alpha_composite(tile_image,(x,floor))
bed_image=bed.frame('off',0,size=(420,420),origin=(210,360),scale=.6)
bed_image=bed_image.crop(bed_image.getbbox());bed_image=bed_image.resize((88,88),Image.Resampling.LANCZOS)
blue=tint(bed_image,(86,183,240,255))
door_clips=door.clips;door_clip='closed' if 'closed' in door_clips else 'off' if 'off' in door_clips else next(iter(door_clips))
door_image=door.frame(door_clip,0,size=(420,420),origin=(210,360),scale=.6)
door_image=door_image.crop(door_image.getbbox());door_image.thumbnail((44,88),Image.Resampling.LANCZOS)
material_icon=material_entry[2].copy();material_icon.thumbnail((32,29),Image.Resampling.LANCZOS)
copper_symbols,copper_names=build('copper')
copper_icon=copper_symbols[next(k for k,n in copper_names.items() if n=='copper')][0][2].copy();copper_icon.thumbnail((32,29),Image.Resampling.LANCZOS)
font_path=Path('C:/Windows/Fonts/segoeuib.ttf');font=ImageFont.truetype(str(font_path),22)
small=ImageFont.truetype(str(font_path),16)

preview_zh='--subtitles' in sys.argv
lesson=json.loads((ROOT/'Videos/bedroom.lesson.json').read_text(encoding='utf8'))
caption_font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
def scene(t):
    im=base.copy();draw=ImageDraw.Draw(im)
    # Five storyboard shots with a continuous scene and a final room inspection.
    step=0 if t<6 else 1 if t<12 else 2 if t<20 else 3 if t<28 else 4
    for i in range(5):
        x=372+i*44;draw.rounded_rectangle((x,42,x+30,70),radius=5,fill=(211,189,130) if i==step else (81,78,88))
        draw.text((x+9,44),str(i+1),font=small,fill=(40,40,45) if i==step else (197,193,201))
    # Build preview is shown first; confirmation leaves stable blueprints.
    for i,x in enumerate((424,556,688)):
        complete=t>=17+i*.65
        if t>=2+i*.4:
            im.alpha_composite(bed_image if complete else blue,(int(x-bed_image.width/2),floor-bed_image.height))
    if t<6:
        cx=lerp(765,424,t/2.5);cy=363
        draw=ImageDraw.Draw(im);draw.polygon([(cx,cy),(cx+1,cy+23),(cx+7,cy+17),(cx+13,cy+27),(cx+18,cy+24),(cx+12,cy+14),(cx+23,cy+14)],fill='white',outline=(31,31,35))
        if 2.8<t<3.6:
            r=8+(t-2.8)*30;draw.ellipse((cx-r,cy-r,cx+r,cy+r),outline=(237,204,122),width=3)
    # Material stock remains visible, reinforcing actual colony preparation.
    draw=ImageDraw.Draw(im)
    for i in range(4):im.alpha_composite(material_icon,(128+i*17,floor-material_icon.height))
    if t>=20:im.alpha_composite(copper_icon,(741,floor-copper_icon.height))
    x=184;clip='idle_default';local=t;carrying=False
    if 6<=t<7:clip='pickup_pre' if t<6.2 else 'pickup_pst';local=t-(6 if t<6.2 else 6.2)
    elif 7<=t<11:x=lerp(184,372,(t-7)/4);clip='floor_floor_1_0_loop';local=t-7;carrying=True
    elif 11<=t<12:x=372;clip='place_pre' if t<11.2 else 'place_pst';local=t-(11 if t<11.2 else 11.2)
    elif 12<=t<17:
        x=372;clip='dig_fwd_pre' if t<12.334 else 'dig_fwd_loop';local=t-(12 if t<12.334 else 12.334)
        # Construction beam and contact spark are part of the shot, not the task state.
        draw.line((x+42,floor-44,432,floor-36),fill=(96,205,242),width=3)
        for j in range(5):
            a=t*7+j*1.256;draw.line((432,floor-36,432+math.cos(a)*12,floor-36+math.sin(a)*12),fill=(247,228,139),width=2)
    elif 17<=t<18:x=372;clip='dig_fwd_pst';local=t-17
    elif 18<=t<20:x=lerp(372,285,(t-18)/2);clip='floor_floor_1_0_loop';local=t-18
    elif t>=20:x=285
    person=actor.frame(clip,local,size=(220,230),origin=(110,205),scale=.34,equipment='gun' if 12<=t<18 else 'material' if carrying else None)
    if 18<=t<20:person=person.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    im.alpha_composite(person,(int(x)-110,floor-205))
    draw=ImageDraw.Draw(im)
    if 12<=t<17:
        draw.rounded_rectangle((384,288,465,297),radius=3,fill=(25,30,35));draw.rectangle((386,290,386+77*(t-12)/5,295),fill=(100,197,229))
    # Wall and door construction builds up without concealing the cot operation.
    if t>=20:
        count=min(17,int((t-20)*4)+1)
        for j in range(count):
            wx=136+j*44;im.alpha_composite(tile_image,(wx,256))
    if t>=24:
        for y in (300,344,388):im.alpha_composite(tile_image,(136,y))
    if t>=25:im.alpha_composite(door_image,(796,floor-door_image.height))
    if t>=26:
        for wx in (796,840):im.alpha_composite(tile_image,(wx,300))
        for wy in (344,388):im.alpha_composite(tile_image,(840,wy))
    if t>=28:
        overlay=Image.new('RGBA',(W,H));od=ImageDraw.Draw(overlay)
        od.rectangle((180,300,794,floor-1),fill=(73,165,114,48),outline=(143,214,145,230),width=3)
        im.alpha_composite(overlay);draw=ImageDraw.Draw(im)
        draw.rounded_rectangle((478,213,754,249),radius=7,fill=(37,58,47),outline=(146,208,151),width=2)
        draw.line((558,229,571,240,597,218),fill=(216,236,211),width=5)
        for bx in (424,556,688):draw.line((bx-9,321,bx-2,329,bx+12,309),fill=(199,230,167),width=4)
    # Measurements use the same 44-pixel cell grid as walls and the doorway.
    draw=ImageDraw.Draw(im)
    def badge(cx,cy,text):
        tw=font.getlength(text);draw.rounded_rectangle((cx-tw/2-12,cy-3,cx+tw/2+12,cy+30),radius=5,fill=(22,40,59))
        draw.text((cx-tw/2,cy),text,font=font,fill=(246,236,196))
    if t<6 or t>=20:
        draw.line((180,158,796,158),fill=(248,232,174),width=2)
        for px in (180,796):draw.line((px,151,px,166),fill=(248,232,174),width=2)
        for px in range(180,797,44):draw.line((px,155,px,161),fill=(248,232,174),width=1)
        badge(488,113,'14')
        draw.line((906,300,906,432),fill=(248,232,174),width=2)
        for py in (300,344,388,432):draw.line((899,py,913,py),fill=(248,232,174),width=2)
        badge(906,335,'3')
    if t>=28:badge(488,175,'14 x 3 = 42')
    if 3<=t<6:badge(556,240,'2 x 2')
    if 25<=t<28:badge(750,265,'1 x 2')
    # Subtle camera push is baked into the film; subtitles remain separate and localized.
    zoom=1+.055*ease((t-12)/5) if t<20 else 1+.055*(1-ease((t-20)/4))
    if zoom>1.001:
        cropw,croph=int(W/zoom),int(H/zoom);left=int((W-cropw)*.62);top=int((H-croph)*.67)
        im=im.crop((left,top,left+cropw,top+croph)).resize((W,H),Image.Resampling.LANCZOS)
    if preview_zh:
        text=next((c['zh'] for c in lesson['subtitles'] if c['start']<=t<c['end']),'')
        lines=[];line=''
        for ch in text:
            if caption_font.getlength(line+ch)>860:lines.append(line);line=ch
            else:line+=ch
        if line:lines.append(line)
        overlay=Image.new('RGBA',(W,H));od=ImageDraw.Draw(overlay)
        top=H-18-30*len(lines);od.rounded_rectangle((30,top-10,W-30,H-8),radius=7,fill=(16,24,35,220))
        for j,line in enumerate(lines):od.text(((W-caption_font.getlength(line))/2,top+j*30),line,font=caption_font,fill='white')
        im.alpha_composite(overlay)
    return im.convert('RGB')

if '--shots' in sys.argv:
    for t in (3,9,14,25,31):scene(t).save(ROOT/f'Videos/bedroom-shot-{t}.jpg',quality=90)
    sys.exit(0)
out=ROOT/('Videos/bedroom-preview-zh.mp4' if preview_zh else 'Videos/bedroom.mp4')
cmd=[imageio_ffmpeg.get_ffmpeg_exe(),'-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','-','-an','-c:v','libx264','-crf','23','-preset','slow','-pix_fmt','yuv420p','-movflags','+faststart',str(out)]
proc=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=subprocess.DEVNULL)
for i in range(FPS*DURATION):
    proc.stdin.write(scene(i/FPS).tobytes())
    if i%(FPS*6)==0:print(f'Encoded {i/FPS:.0f}/{DURATION}s',flush=True)
proc.stdin.close();assert proc.wait()==0
for t in (3,9,14,25,31):scene(t).save(ROOT/f'Videos/bedroom-shot-{t}.jpg',quality=90)
print('Created',out,out.stat().st_size,flush=True)
