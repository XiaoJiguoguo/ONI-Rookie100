import sys,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'.tools/unitypy'))
from lesson_profiles import PROFILES
import UnityPy
mapping=json.loads((ROOT/'.tools/native-buildings.json').read_text())
tags=sorted({tag for _,props,_ in PROFILES.values() for tag in props}|{'ManualPressureDoor'})
missing=[tag for tag in tags if tag not in mapping]
if missing:raise ValueError(missing)
manifest={tag:mapping[tag] for tag in tags}
(ROOT/'Videos/native-buildings.json').write_text(json.dumps(manifest,indent=2)+'\n')
prefixes={v['anim'] for v in manifest.values()}
out=ROOT/'.tools/lesson-assets';out.mkdir(exist_ok=True)
root=Path('C:/STEAM/steamapps/common/OxygenNotIncluded/OxygenNotIncluded_Data')
found=set()
for file in ('sharedassets0.assets','sharedassets1.assets','sharedassets2.assets','resources.assets','StreamingAssets/expansion1_bundle'):
 env=UnityPy.load(str(root/file))
 for obj in env.objects:
  if obj.type.name not in ('TextAsset','Texture2D'):continue
  name=obj.peek_name() or ''
  if not any(name.startswith(p+'_') for p in prefixes):continue
  d=obj.read()
  if obj.type.name=='Texture2D':d.image.save(out/(name+'.png'))
  else:
   raw=d.m_Script
   if isinstance(raw,str):raw=raw.encode('utf8','surrogateescape')
   (out/(name+'.bytes')).write_bytes(raw)
  found.add(name)
for p in prefixes:
 if p+'_build' not in found or p+'_anim' not in found:raise ValueError('Missing native animation '+p)
print('Extracted native resources for',len(tags),'buildings')
