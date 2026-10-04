from pathlib import Path
import struct,zlib,math
root=Path('assets');root.mkdir(exist_ok=True)
svg='''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect x="2" y="2" width="60" height="60" rx="14" fill="#4255c5"/><path d="M26 16H16V26 M38 16H48V26 M16 38V48H26 M48 38V48H38" fill="none" stroke="white" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/><circle cx="34" cy="30" r="7" fill="white"/></svg>'''
root.joinpath('screenit.svg').write_text(svg+'\n',encoding='utf8')
def png(n):
    rows=[];ss=4
    seg=[(26,16,16,16),(16,16,16,26),(38,16,48,16),(48,16,48,26),(16,38,16,48),(16,48,26,48),(48,38,48,48),(48,48,38,48)]
    def distance(x,y,a,b,c,d):
        t=max(0,min(1,((x-a)*(c-a)+(y-b)*(d-b))/((c-a)**2+(d-b)**2)))
        return math.hypot(x-a-t*(c-a),y-b-t*(d-b))
    for j in range(n):
        row=bytearray([0])
        for i in range(n):
            sums=[0,0,0,0]
            for v in range(ss):
                for u in range(ss):
                    x=(i+(u+.5)/ss)*64/n;y=(j+(v+.5)/ss)*64/n
                    inside=2<=x<=62 and 2<=y<=62 and math.hypot(max(16-x,0,x-48),max(16-y,0,y-48))<=14
                    white=inside and (math.hypot(x-34,y-30)<=7 or any(distance(x,y,*s)<=2.5 for s in seg))
                    col=(255,255,255,255) if white else (66,85,197,255) if inside else (0,0,0,0)
                    for k in range(4):sums[k]+=col[k]
            row.extend(round(v/(ss*ss)) for v in sums)
        rows.append(row)
    def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',n,n,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b'')
sizes=[16,20,24,32,48,64,128,256];frames=[png(n) for n in sizes];offset=6+16*len(sizes);entries=[]
for n,data in zip(sizes,frames):
    entries.append(struct.pack('<BBBBHHII',n%256,n%256,0,0,1,32,len(data),offset));offset+=len(data)
root.joinpath('ScreenIt.ico').write_bytes(struct.pack('<HHH',0,1,len(sizes))+b''.join(entries)+b''.join(frames))
Path('artifacts').mkdir(exist_ok=True);Path('artifacts/icon-preview.png').write_bytes(frames[-1])
