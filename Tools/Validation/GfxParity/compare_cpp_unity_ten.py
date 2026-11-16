"""Compare unregistered original C++ and Unity captures; no image alignment."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import csv, hashlib, json

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / '.validation/angelica-unity-ten'
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
sheet = Image.new('RGB', (1024, 5 * 282), '#202020')
draw = ImageDraw.Draw(sheet)
rows = []
animation = []
for i in range(1, 11):
    t = i * 100
    paths = [OUT / f'cpp.frame-{i}.png', OUT / f'unity-{t:04d}.png']
    ims = [Image.open(p).convert('RGB') for p in paths]
    assert all(im.size == (512, 512) for im in ims)
    pixels = [list(im.getdata()) for im in ims]
    masks = [[max(p) > 12 for p in ps] for ps in pixels]
    counts = [sum(m) for m in masks]
    assert min(counts) > 0
    intersection = sum(a and b for a,b in zip(*masks))
    union = sum(a or b for a,b in zip(*masks))
    mae = sum(sum(abs(x-y) for x,y in zip(a,b)) for a,b in zip(*pixels)) / (512*512*3*255)
    row = dict(gfx_ms=t, cpp_pixels=counts[0], unity_pixels=counts[1], unity_cpp_area=counts[1]/counts[0], mask_iou=intersection/union, rgb_mae=mae)
    for label, mask in zip(('cpp','unity'), masks):
        xy = [(n%512,n//512) for n,m in enumerate(mask) if m]
        row[label+'_bbox'] = [min(x for x,y in xy),min(y for x,y in xy),max(x for x,y in xy)+1,max(y for x,y in xy)+1]
    rows.append(row)
    pair = Image.new('RGB',(1024,540),'#202020')
    pd = ImageDraw.Draw(pair)
    for j, im in enumerate(ims):
        x = ((i-1)%2)*512 + j*256
        y = ((i-1)//2)*282
        label = f'{"C++" if j==0 else "Unity"} | GFX {t} ms'
        sheet.paste(im.resize((256,256),Image.Resampling.LANCZOS),(x,y+26))
        draw.text((x+8,y+3),label,font=font,fill='white')
        pair.paste(im,(j*512,28)); pd.text((j*512+8,4),label,font=font,fill='white')
    pair.save(OUT / f'pair-{t:04d}.png')
    animation.append(pair)
sheet.save(OUT/'comparison-10-pairs.png')
animation[0].save(OUT/'comparison.gif',save_all=True,append_images=animation[1:],duration=150,loop=0)
(OUT/'metrics.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
with (OUT/'metrics.csv').open('w',newline='',encoding='utf-8') as f:
    writer=csv.DictWriter(f,fieldnames=rows[0].keys());writer.writeheader();writer.writerows(rows)
manifest={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in OUT.glob('*.png')}
(OUT/'image-sha256.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(rows,indent=2))
