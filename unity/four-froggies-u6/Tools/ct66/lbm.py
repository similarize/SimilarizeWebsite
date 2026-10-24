import struct, numpy as np
def rs(b,o):
    n=b[o];o+=1;return b[o:o+n].decode(),o+n
def parse(path):
    b=open(path,'rb').read();assert b[:4]==b'LBM1';o=4
    nm=b[o];o+=1;mats=[]
    for i in range(nm):
        name,o=rs(b,o);col=tuple(b[o:o+4]);gl,me,fl=b[o+4],b[o+5],b[o+6];o+=7;tex,o=rs(b,o)
        mats.append(dict(name=name,col=col,gloss=gl,metal=me,fl=fl,tex=tex))
    np_=b[o];o+=1;parts=[]
    for i in range(np_):
        name,o=rs(b,o);piv=struct.unpack_from('<3f',b,o);o+=12;nc=struct.unpack_from('<H',b,o)[0];o+=2;ch=[]
        for c in range(nc):
            m=b[o];o+=1;vc=struct.unpack_from('<I',b,o)[0];o+=4
            v=np.frombuffer(b,'<f4',vc*3,o).reshape(-1,3).copy();o+=vc*12
            n=np.frombuffer(b,'i1',vc*3,o).reshape(-1,3).astype(np.float32)/127;o+=vc*3
            uv=None
            if mats[m]['fl']&4:
                uv=np.frombuffer(b,'<f2',vc*2,o).reshape(-1,2).astype(np.float32);o+=vc*4
            ic=struct.unpack_from('<I',b,o)[0];o+=4;big=b[o];o+=1
            idx=np.frombuffer(b,'<u4' if big else '<u2',ic,o).astype(np.int64);o+=ic*(4 if big else 2)
            ch.append(dict(mat=m,v=v,n=n,uv=uv,idx=idx))
        parts.append(dict(name=name,piv=np.array(piv),ch=ch))
    assert o==len(b),(o,len(b))
    return mats,parts
def ws(s):
    e=s.encode();return bytes([len(e)])+e
def write(path,mats,parts):
    out=bytearray(b'LBM1');out.append(len(mats))
    for m in mats:
        out+=ws(m['name'])+bytes(m['col'])+bytes([m['gloss'],m['metal'],m['fl']])+ws(m['tex'])
    out.append(len(parts))
    for p in parts:
        out+=ws(p['name'])+struct.pack('<3f',*p['piv'])+struct.pack('<H',len(p['ch']))
        for c in p['ch']:
            v=c['v'].astype('<f4');out.append(c['mat']);out+=struct.pack('<I',len(v))+v.tobytes()
            n=c['n'];n=n/np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-9)
            out+=np.clip(np.round(n*127),-127,127).astype('i1').tobytes()
            if mats[c['mat']]['fl']&4: out+=c['uv'].astype('<f2').tobytes()
            idx=c['idx'];big=len(v)>65535
            out+=struct.pack('<I',len(idx))+bytes([1 if big else 0])+idx.astype('<u4' if big else '<u2').tobytes()
    open(path,'wb').write(bytes(out))
