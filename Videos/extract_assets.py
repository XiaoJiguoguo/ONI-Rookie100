import sys
from pathlib import Path
sys.path.insert(0,str(Path('.tools/unitypy').resolve()))
import UnityPy
root=Path(r'C:\STEAM\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data')
prefixes=('body_comp_default','anim_construction_default','anim_loco_new','anim_idles_default','bedlg','door_manual','head_swap','head_master_swap','body_swap','hair_swap','anim_mouth_flap','constructor_gun','sand_stone','copper','floor_basic','tiles_solid')
out=Path('.tools/lesson-assets');out.mkdir(exist_ok=True)
for file in ('sharedassets0.assets','sharedassets2.assets','resources.assets'):
    env=UnityPy.load(str(root/file))
    for obj in env.objects:
        if obj.type.name not in ('TextAsset','Texture2D'):continue
        name=obj.peek_name()
        if not name or not any(name.startswith(p) for p in prefixes):continue
        d=obj.read()
        if obj.type.name=='TextAsset':
            raw=d.m_Script
            if isinstance(raw,str):raw=raw.encode('utf-8','surrogateescape')
            (out/(name+'.bytes')).write_bytes(raw)
            print('TEXT',name,len(raw))
        else:
            d.image.save(out/(name+'.png'));print('TEXTURE',name,d.m_Width,d.m_Height)
    del env
