"""Descriptive real-asset render comparison; metrics are not parity certification."""
import json
from pathlib import Path
from PIL import Image, ImageDraw
root=Path(__file__).resolve().parents[3]/'.validation/gfx-parity'
times=[0,100,250,500,750,1000,1250,1500,1750,1999,2001,2250]
rows=[]
def stats(pixels,width):
 coords=[(i%width,i//width) for i,p in enumerate(pixels) if max(p)>12.75]
 n=len(pixels)
 return {'litPixels':len(coords),'meanRgb':[sum(p[c] for p in pixels)/n/255 for c in range(3)],
 'bbox':None if not coords else [min(p[0] for p in coords),min(p[1] for p in coords),max(p[0] for p in coords),max(p[1] for p in coords)],
 'centroid':None if not coords else [sum(p[c] for p in coords)/len(coords) for c in range(2)]}
for t in times:
 gi=Image.open(root/f'real-godot-{t:04d}.png').convert('RGB');ui=Image.open(root/f'reference-fresh-{t:04d}.png').convert('RGB')
 assert gi.size==ui.size
 g=list(gi.get_flattened_data());u=list(ui.get_flattened_data());gm=[max(p)>12.75 for p in g];um=[max(p)>12.75 for p in u]
 rows.append({'gfxTimeMs':t,'godot':stats(g,gi.width),'unity':stats(u,ui.width),
 'maeRgb':sum(sum(abs(a-b) for a,b in zip(p,q)) for p,q in zip(g,u))/(len(g)*3*255),
 'maskIoU':sum(a and b for a,b in zip(gm,um))/max(1,sum(a or b for a,b in zip(gm,um)))})
result={'scope':'one original user archive GFX, all 10 elements; descriptive render metrics only',
 'timeDomain':'manual effect milliseconds, not wall-clock/video time',
 'maskThreshold':0.05,'frames':rows}
(root/'fix-pass11-real-comparison.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
# Four representative pairs, no registration or pixel alteration of either render.
canvas=Image.new('RGB',(708,4*422),(24,24,24));d=ImageDraw.Draw(canvas)
for row,t in enumerate([250,750,1500,2001]):
 for col,(prefix,label) in enumerate([('real-godot','Godot'),('reference-fresh','Unity')]):
  d.text((col*354+8,row*422+5),f'{label}  GFX time {t} ms',fill='white')
  canvas.paste(Image.open(root/f'{prefix}-{t:04d}.png').convert('RGB'),(col*354,row*422+24))
canvas.save(root/'fix-pass11-real-contact.png')
print(json.dumps({'frames':len(rows),'meanMAE':sum(r['maeRgb'] for r in rows)/len(rows),
 'meanMaskIoU':sum(r['maskIoU'] for r in rows)/len(rows)},indent=2))

canvas=Image.new('RGB',(708,10*220),(24,24,24));d=ImageDraw.Draw(canvas);elements=[]
for i in range(10):
 row={'elementIndex':i}
 for j,engine in enumerate(['godot','unity']):
  im=Image.open(root/f'real-{engine}-element-{i:02d}.png').convert('RGB')
  row[engine]=stats(list(im.get_flattened_data()),im.width)
  im.thumbnail((177,197));canvas.paste(im,(j*354,i*220+20));d.text((j*354,i*220),f'{engine} element {i+1}',fill='white')
 elements.append(row)
canvas.save(root/'fix-pass11-elements-contact.png')
(root/'fix-pass11-elements-bounds.json').write_text(json.dumps(elements,indent=2),encoding='utf-8')
