"""Render distinct task storyboards using native art and localized sidecar subtitles.
Connections are instructional diagrams, not claims of a tested colony design.
"""
import sys,json,math,subprocess,argparse
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
# Reuse the verified native KAnim parser and accessorized duplicant renderer.
namespace={'__file__':str(ROOT/'Videos/make_bedroom.py')}
exec((ROOT/'Videos/make_bedroom.py').read_text(encoding='utf8').split('W,H=960,540')[0],namespace)
Renderer=namespace['Renderer'];animation=namespace['animation'];actor=namespace['actor'];tint=namespace['tint'];ease=namespace['ease']
ffmpeg=namespace['imageio_ffmpeg'].get_ffmpeg_exe()
from lesson_profiles import PROFILES
native=json.loads((ROOT/'Videos/native-buildings.json').read_text())
catalog=json.loads((ROOT/'Curriculum/catalog.v1.json').read_text(encoding='utf8'))
runtime={q['Id']:q for q in json.loads((ROOT/'quests.json').read_text(encoding='utf8'))['Quests']}
W,H=960,540;CELL=40;FPS=24;FLOOR=420
font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',22)
caption_font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
props={}
for tag,definition in native.items():
    clips=animation(definition['anim']);renderer=Renderer(definition['anim'],clips)
    clip='ui' if 'ui' in clips else 'off' if 'off' in clips else next(iter(clips))
    im=renderer.frame(clip,0,size=(1000,1000),origin=(500,700),scale=.7)
    box=im.getbbox()
    if not box:
        for candidate in ('ui#cap_left_default','off','on','place'):
            if candidate not in clips:continue
            im=renderer.frame(candidate,0,size=(1000,1000),origin=(500,700),scale=.7);box=im.getbbox()
            if box:break
    if not box:raise ValueError('Empty native prop: '+tag)
    im=im.crop(box)
    # Large machines use compact vignettes; printed dimensions remain authoritative.
    ratio=min(1,190/(definition['width']*CELL),170/(definition['height']*CELL))
    im=im.resize((max(1,round(definition['width']*CELL*ratio)),max(1,round(definition['height']*CELL*ratio))),Image.Resampling.LANCZOS)
    props[tag]=im
tile=props['Tile'].resize((42,42),Image.Resampling.LANCZOS)
base=Image.new('RGBA',(W,H),(73,147,211,255));grid=Image.new('RGBA',(W,H));gd=ImageDraw.Draw(grid)
for x in range(0,W,24):gd.line((x,0,x,H),fill=(212,235,248,14))
for y in range(0,H,24):gd.line((0,y,W,y),fill=(212,235,248,14))
base.alpha_composite(grid)
for x in range(52,932,CELL):base.alpha_composite(tile,(x,FLOOR))
def arrow(draw,a,b,color,t):
    draw.line((a,b),fill=(20,48,68),width=9);draw.line((a,b),fill=color,width=4)
    x,y=b;angle=math.atan2(b[1]-a[1],b[0]-a[0]);draw.polygon([(x,y),(x-13*math.cos(angle-.5),y-13*math.sin(angle-.5)),(x-13*math.cos(angle+.5),y-13*math.sin(angle+.5))],fill=color)
    phase=(t*.35)%1;px=a[0]+phase*(b[0]-a[0]);py=a[1]+phase*(b[1]-a[1]);draw.ellipse((px-4,py-4,px+4,py+4),fill='white')
def badge(draw,x,y,text):
    w=font.getlength(text);draw.rounded_rectangle((x-w/2-9,y-3,x+w/2+9,y+29),radius=5,fill=(23,43,62));draw.text((x-w/2,y),text,font=font,fill=(246,235,182))
def subtitles(im,t,film):
    cue=next(c for c in film['subtitles'] if c['start']<=t<c['end']);lines=[];line=''
    for ch in cue['zh']:
        if caption_font.getlength(line+ch)>865:lines.append(line);line=ch
        else:line+=ch
    if line:lines.append(line)
    overlay=Image.new('RGBA',(W,H));d=ImageDraw.Draw(overlay);top=H-16-30*len(lines)
    d.rounded_rectangle((28,top-8,W-28,H-7),radius=6,fill=(15,25,37,225))
    for i,line in enumerate(lines):d.text(((W-caption_font.getlength(line))/2,top+i*30),line,font=caption_font,fill='white')
    im.alpha_composite(overlay)
def scene(t,film,theme,tags,preview=False):
    im=base.copy();d=ImageDraw.Draw(im);step=min(len(film['steps'])-1,int(t//6));part=t%6
    for i in range(len(film['steps'])):
        x=480+(i-(len(film['steps'])-1)/2)*54
        d.rounded_rectangle((x-18,36,x+18,69),radius=5,fill=(224,204,145) if i==step else (53,81,108));d.text((x-7,39),str(i+1),font=font,fill=(28,45,60) if i==step else 'white')
    xs=[340+i*(450/max(1,len(tags)-1)) for i in range(len(tags))]
    if film['id']=='q01':xs=[128,380,620]
    if film['id']=='q01_dining':xs=[128,600]
    chosen=min(len(tags)-1,step*len(tags)//max(1,len(film['steps'])-1))
    overview=step==len(film['steps'])-1
    for i,(tag,x) in enumerate(zip(tags,xs)):
        p=props[tag];show=p if overview or step>0 or part>=3 else tint(p,(114,212,252,255))
        im.alpha_composite(show,(int(x-p.width/2),FLOOR-p.height))
        if i==chosen and not overview:d.rounded_rectangle((x-p.width/2-8,FLOOR-p.height-8,x+p.width/2+8,FLOOR+3),radius=5,outline=(248,230,168),width=2)
        if part>3 or overview:badge(d,x,FLOOR-p.height-46,f"{native[tag]['width']} x {native[tag]['height']}")
    if film['id']=='q01_dining' and step>=2:
        table=props['DiningTable']
        for px in (440,740):im.alpha_composite(table,(px-table.width//2,FLOOR-table.height))
    if film['id']=='q01':
        d.rectangle((82,FLOOR,174,FLOOR+50),fill=(48,120,198),outline=(195,229,248),width=2)
    # Shared staging of construction; subsequent scenes illustrate the task's system.
    clip='idle_default';actor_x=198;equipment=None
    if step==0 and 1<part<3:clip='floor_floor_1_0_loop';actor_x=198+40*ease((part-1)/2)
    if step==0 and 3<=part<5 and '建' in film['subtitles'][0]['zh']:clip='dig_fwd_loop';equipment='gun';actor_x=238
    if film['id']=='q01' and step==4:
        actor_x=620-390*ease(part/6);clip='floor_floor_1_0_loop'
    person=actor.frame(clip,t,size=(220,230),origin=(110,205),scale=.34,equipment=equipment)
    im.alpha_composite(person,(int(actor_x)-110,FLOOR-205))
    if film['id'] in ('q01','q01_dining') and step>=max(1,len(film['steps'])-3):
        right=812 if film['id']=='q01' else 852
        for x in range(252,853,CELL):im.alpha_composite(tile,(x,220))
        for y in range(260,FLOOR,CELL):
            if y<340:im.alpha_composite(tile,(212,y))
            im.alpha_composite(tile,(right,y))
        door=props['ManualPressureDoor'];im.alpha_composite(door,(212,FLOOR-80))
        if theme in ('room','research_room'):
            d.line((252,166,right,166),fill=(252,230,176),width=2);badge(d,552,119,'14 x 4 = 56' if film['id']=='q01' else '15 x 4 = 60')
            # Sanitation pump and dining research station stay outside the left door.
            if film['id']=='q01' and step==4:arrow(d,(620,195),(240,195),(234,222,151),t)
    if step>0:
        color=(250,216,93) if theme=='power' else (111,247,163) if theme=='automation' else (81,232,251)
        if theme in ('power','pipes','water','waste','oxygen','buffer','heat','automation'):
            for i in range(len(xs)-1):arrow(d,(xs[i]+20,184),(xs[i+1]-20,184),color,t+i)
            if theme=='heat':arrow(d,(xs[-1],213),(xs[0],213),(242,142,88),t)
        if theme=='oxygen':
            for i in range(12):
                x=380+(i*67)%450;y=280-((t*17+i*29)%120);d.ellipse((x,y,x+7,y+7),outline=(194,235,254),width=2)
        elif theme in ('water','waste'):
            d.rounded_rectangle((64,345,156,418),radius=5,fill=(54,133,221),outline=(205,232,248),width=2)
        elif theme=='farm':
            for x in xs:
                y=FLOOR-40;h=20+25*ease(min(1,t/12));d.line((x,y,x,y-h),fill=(126,214,108),width=4)
                d.ellipse((x-17,y-h+4,x,y-h+18),fill=(158,223,123));d.ellipse((x,y-h-5,x+17,y-h+10),fill=(158,223,123))
        elif theme in ('research','research_room','tools','budget','storage','food','settlement'):
            for i,x in enumerate(xs):
                d.rounded_rectangle((x-45,178,x+45,193),radius=4,fill=(30,57,77));d.rectangle((x-42,181,x-42+84*ease(part/5),190),fill=(181,221,172))
        elif theme in ('explore','nature'):
            arrow(d,(300,200),(790,200),(248,216,132),t)
            if theme=='nature':
                for x in (410,580,720):d.line((x,300,x,270),fill=(134,214,117),width=4);d.ellipse((x-12,253,x+12,276),fill=(157,225,131))
        elif theme in ('rocket','meteor'):
            for i in range(20):
                x=60+(i*137)%840;y=95+(i*43)%120;d.ellipse((x,y,x+2,y+2),fill=(237,243,251))
            if theme=='meteor':
                for i in range(4):
                    x=300+i*130;y=115+(t*30+i*40)%100;d.line((x-15,y-25,x,y),fill=(255,190,129),width=3)
            else:arrow(d,(870,200),(870,105),(243,209,123),t)
    if overview:
        d.rounded_rectangle((50,91,260,139),radius=6,fill=(26,51,69));d.text((78,98),'1 ... '+str(len(tags)),font=font,fill=(239,229,189))
    if preview:subtitles(im,t,film)
    return im.convert('RGB')
def definition(task):
    theme,tags,en=PROFILES[task['id']];zh=task['teaching']['steps'];english=en.split('|')
    if len(zh)!=len(english):raise ValueError('Untranslated steps '+task['id'])
    steps=[{'start':i*6,'zh':text if len(text)<=8 else text[:7]+'…','en':english[i] if len(english[i])<=18 else english[i][:15]+'...'} for i,text in enumerate(zh)]
    cues=[{'start':i*6,'end':(i+1)*6,'zh':text+'。','en':english[i]+'.'} for i,text in enumerate(zh)]
    steps.append({'start':len(zh)*6,'zh':'运行核对','en':'Check operation'})
    cues.append({'start':len(zh)*6,'end':(len(zh)+1)*6,'zh':'核对真实运行与资源供应；短片为示意，任务以存档检查或知识确认为准。','en':'Verify real operation and supplies. This is an illustration; completion uses colony checks or learning confirmation.'})
    if task['id']=='q01':
        cues[3]['zh']='围合公共厕所：示例内部14×4＝56格，手压泵留在房间外，出口经过洗手盆。'
        cues[3]['en']='Enclose a 14 x 4 interior: 56 cells. Keep the pitcher pump outside and route the exit past the wash basin.'
        cues[4]['zh']='观察同一复制人如厕后经过洗手盆；示意画面不能替代真实试运行。'
        cues[4]['en']='Observe the same duplicant using the toilet and then washing. The illustration does not replace an actual trial.'
    if task['id']=='q01_dining':
        cues[1]['zh']='先研究食物制备，解锁餐桌；研究台需要电力与泥土供应。'
        cues[1]['en']='Research Meal Preparation to unlock mess tables; supply power and dirt to the research station.'
        cues[3]['zh']='围合餐厅：示例内部15×4＝60格，不计墙砖；再用房间叠层核对。'
        cues[3]['en']='Enclose a 15 x 4 interior: 60 cells, excluding boundary tiles. Then verify the room overlay.'
    return {'version':1,'id':task['id'],'file':task['id']+'.mp4','duration':len(steps)*6,'width':W,'height':H,'steps':steps,'subtitles':cues,'visualTheme':theme,'nativeProps':tags}
def encode(film,preview=False):
    theme,tags,_=PROFILES[film['id']];out=ROOT/'Videos'/(film['id']+('-preview-zh' if preview else '')+'.mp4')
    cmd=[ffmpeg,'-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','-','-an','-c:v','libx264','-crf','23','-preset','fast','-pix_fmt','yuv420p','-movflags','+faststart',str(out)]
    proc=subprocess.Popen(cmd,stdin=subprocess.PIPE,stderr=subprocess.DEVNULL)
    for i in range(FPS*film['duration']):proc.stdin.write(scene(i/FPS,film,theme,tags,preview).tobytes())
    proc.stdin.close()
    if proc.wait():raise RuntimeError('Encoding failed: '+film['id'])
    print(f"{film['id']}: {film['duration']}s, {out.stat().st_size} bytes",flush=True)
parser=argparse.ArgumentParser();parser.add_argument('--ids',nargs='*');parser.add_argument('--preview',action='store_true');parser.add_argument('--shots',action='store_true');parser.add_argument('--metadata',action='store_true');args=parser.parse_args()
index=[]
for task in catalog['tasks']:
    if task['id']=='q01_bedroom':index.append({'id':task['id'],'definition':'bedroom.lesson.json','runtime':True});continue
    film=definition(task);index.append({'id':task['id'],'definition':task['id']+'.lesson.json','runtime':task['id'] in runtime})
    (ROOT/'Videos'/(task['id']+'.lesson.json')).write_text(json.dumps(film,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    if args.ids and task['id'] not in args.ids:continue
    if args.shots:
        for t in (3,film['duration']//2,film['duration']-3):scene(t,film,*PROFILES[task['id']][:2],True).save(ROOT/'.tools'/f"lesson-{task['id']}-{t}.jpg")
    elif not args.metadata:encode(film,args.preview)
(ROOT/'Videos/lessons.index.json').write_text(json.dumps({'version':1,'lessons':index},indent=2)+'\n')
