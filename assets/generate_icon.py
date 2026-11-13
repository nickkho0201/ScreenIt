"""CONSTRUCT icon masters. Geometry traced from the supplied logo.png; no runtime fonts.
Run from repository root with Python 3 (standard library only).
16/20px omit alignment ticks and use pixel-aligned lettering for tray legibility.
"""
from pathlib import Path
import struct,zlib
root=Path('assets');root.mkdir(exist_ok=True)
# All coordinates on the approved 256-unit grid; lettering is outlined.
rects=[(42,41,63,105),(42,41,105,62),(151,41,214,62),(193,41,214,105),
       (42,151,63,214),(42,193,105,214),(193,151,214,214),(151,193,214,214),
       (77,89,179,168)]
ticks=[(125,34,131,70),(125,186,131,222),(34,125,68,131),(188,125,222,131)]
letters=[[(91,115),(113,115),(113,122),(98,122),(98,127),(113,127),(113,143),(90,143),(90,136),(106,136),(106,132),(91,132)],
         [(116,143),(134,143),(134,149),(116,149)],[(137,115),(144,115),(144,143),(137,143)],
         [(148,115),(170,115),(170,122),(162,122),(162,143),(155,143),(155,122),(148,122)]]
def path(points): return 'M'+' L'.join(f'{x},{y}' for x,y in points)+' Z'
svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256"><rect width="256" height="256" fill="#F04D32"/>'
svg+=''.join(f'<rect x="{a}" y="{b}" width="{c-a}" height="{d-b}" fill="#1A1A1A"/>' for a,b,c,d in rects+ticks)
svg+=''.join(f'<path d="{path(points)}" fill="#FBFEF8"/>' for points in letters)+'</svg>'
root.joinpath('screenit.svg').write_text(svg+'\n',encoding='utf8')
def inside(poly,x,y):
 hit=False
 for (a,b),(c,d) in zip(poly,poly[1:]+poly[:1]):
  if (b>y)!=(d>y) and x<(c-a)*(y-b)/(d-b)+a:hit=not hit
 return hit
def png(n):
 ss=4;rows=[]
 # Micro masters use crisp 1px letter stems and open frame, without fine ticks.
 micro=[(2,2,7,3),(2,2,3,7),(9,2,14,3),(13,2,14,7),(2,9,3,14),(2,13,7,14),(13,9,14,14),(9,13,14,14),(4,5,12,11)]
 pixels=['011101010111','010001010010','011101010010','000101010010','011101010010']
 for j in range(n):
  row=bytearray([0])
  for i in range(n):
   sums=[0,0,0,0]
   for v in range(ss):
    for u in range(ss):
     x=(i+(u+.5)/ss)*256/n;y=(j+(v+.5)/ss)*256/n
     black=any(a<=x<c and b<=y<d for a,b,c,d in rects+(ticks if n>=24 else []))
     white=any(inside(poly,x,y) for poly in letters)
     if n<=20:
      xx=x/16;yy=y/16
      black=any(a<=xx<c and b<=yy<d for a,b,c,d in micro)
      # Tiny S / I / T 3x5 glyphs in the central ink badge.
      gx=int((xx-4.5)*1.6);gy=int(yy-5.5)
      white=0<=gy<5 and 0<=gx<12 and pixels[gy][gx]=='1'
     color=(251,254,248,255) if white else (26,26,26,255) if black else (240,77,50,255)
     for k in range(4):sums[k]+=color[k]
   row.extend(round(c/(ss*ss)) for c in sums)
  rows.append(row)
 def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
 return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',n,n,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b'')
sizes=[16,20,24,32,48,64,128,256];frames=[png(n) for n in sizes];offset=6+16*len(sizes);entries=[]
for n,data in zip(sizes,frames):
 entries.append(struct.pack('<BBBBHHII',n%256,n%256,0,0,1,32,len(data),offset));offset+=len(data)
root.joinpath('ScreenIt.ico').write_bytes(struct.pack('<HHH',0,1,len(sizes))+b''.join(entries)+b''.join(frames))
folder=Path('artifacts/construct-icons');folder.mkdir(parents=True,exist_ok=True)
for n,data in zip(sizes,frames):folder.joinpath(f'icon-{n}.png').write_bytes(data)
