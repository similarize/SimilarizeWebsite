# ffrd66 (Ben 2026-10-24: "the Roadster's back looks incomplete / see-through and the wheels don't look right").
# Resources/LB/roadster52.bytes -> roadster66.bytes:
#  * every opaque chunk double-sided: the rear fascia / diffuser were wound inward (culled = see-through from behind)
#  * solid rear: a closing tail panel behind the diffuser + a full-width thin taillight bar
#  * dark smoked glass (was bronze) + black targa seams (removable roof panel look)
#  * new wheel: 0.67 m low-profile tyre (rounded shoulders), 10-spoke two-tone rim, brake disc; same materials / colours
import os,numpy as np,lbm
from build_ct66 import dbl,box
LB=os.path.join(os.path.dirname(os.path.abspath(__file__)),'../../Assets/Resources/LB/')
M,P=lbm.parse(LB+'roadster52.bytes')
names=[m['name'] for m in M];mi=names.index
part={p['name']:p for p in P}
body,wheel=part['body'],part['wheel']
M[mi('glass')]['col']=(16,20,26,222)
def add(p,mat,V,N,I): p['ch'].append(dict(mat=mi(mat),v=V,n=N,uv=None,idx=I))
# rear: closing panel just inside the tail (covers the open rear between diffuser and deck), thin full-width tail bar
allv=np.concatenate([c['v'] for c in body['ch']])
zr=allv[:,2].min()
add(body,'trim',*box([-0.86,0.30,zr+0.07],[0.86,0.56,zr+0.10]))
add(body,'paint',*box([-0.88,0.56,zr+0.07],[0.88,0.80,zr+0.10]))
add(body,'tail',*box([-0.93,0.795,zr+0.035],[0.93,0.815,zr+0.06]))
# targa seams: windscreen header + roll-hoop bar across the glass roof
for z,w in ((0.55,0.72),(-0.75,0.70)):
    gl=[c['v'] for c in body['ch'] if names[c['mat']]=='glass'][0]
    s=gl[(np.abs(gl[:,2]-z)<0.1)]
    y=s[np.abs(s[:,0])<0.25][:,1].max();w=min(w,np.abs(s[:,0]).max()-0.05)
    for k in range(6):   # follow the roof curve in 6 segments
        x0,x1=-w+2*w*k/6,-w+2*w*(k+1)/6;xm=(x0+x1)/2
        near=s[np.abs(s[:,0]-xm)<0.12];yy=near[:,1].max() if len(near) else y
        add(body,'trim',*box([x0,yy-0.012,z-0.035],[x1,yy+0.008,z+0.035]))
# new wheel (right wheel; outer face +x)
def ring(r0,r1,x0,x1,seg=48):
    """closed tyre-like torus band: outer radius r0 at x in [x0,x1] with rounded shoulders, inner radius r1"""
    prof=[(x0,r1),(x0,r0-0.03),(x0+0.012,r0-0.008),(x0+0.03,r0),(x1-0.03,r0),(x1-0.012,r0-0.008),(x1,r0-0.03),(x1,r1)]
    V=[];N=[];I=[];n=len(prof)
    for k in range(seg):
        a=2*np.pi*k/seg
        for x,r in prof: V.append([x,r*np.sin(a),r*np.cos(a)])
    V=np.array(V,np.float32)
    for k in range(seg):
        k2=(k+1)%seg
        for j in range(n):
            j2=(j+1)%n
            q=[k*n+j,k*n+j2,k2*n+j2,k2*n+j]
            I+=[q[0],q[1],q[2],q[0],q[2],q[3]]
    I=np.array(I).reshape(-1,3)
    # orient outward from the profile centroid
    cx=(x0+x1)/2;cr=(r0+r1)/2
    out=[]
    for t in I:
        p=V[t];nrm=np.cross(p[1]-p[0],p[2]-p[0]);c=p.mean(0);rad=np.hypot(c[1],c[2])
        d=np.array([c[0]-cx,(rad-cr)*c[1]/max(rad,1e-6),(rad-cr)*c[2]/max(rad,1e-6)])
        out.append(t if np.dot(nrm,d)>=0 else t[[0,2,1]])
    I=np.array(out)
    # smooth-ish normals: per vertex from profile direction
    for k in range(seg):
        a=2*np.pi*k/seg
        for j,(x,r) in enumerate(prof):
            d=np.array([x-cx,(r-cr)*np.sin(a),(r-cr)*np.cos(a)]);N.append(d/np.linalg.norm(d))
    return V,np.array(N,np.float32),I.reshape(-1)
def spokes(nsp,rin,rout,x,w,th):
    Vs=[];Ns=[];Is=[]
    for k in range(nsp):
        a=2*np.pi*k/nsp
        V,N,I=box([x-th,rin,-w/2],[x,rout,w/2])
        # taper: shrink tip width a little
        V[:,2]*=np.where(V[:,1]>rin+0.01,0.7,1.0)
        R=np.array([[1,0,0],[0,np.cos(a),-np.sin(a)],[0,np.sin(a),np.cos(a)]])
        V=V@R.T;N=N@R.T
        Is.append(I+sum(len(v) for v in Vs));Vs.append(V);Ns.append(N)
    return np.concatenate(Vs).astype(np.float32),np.concatenate(Ns).astype(np.float32),np.concatenate(Is)
from build_ct66 import disc
wheel['ch']=[]
add(wheel,'tyre',*ring(0.335,0.255,-0.15,0.15))
add(wheel,'rim',*ring(0.256,0.235,-0.13,0.135,48))
add(wheel,'rimface',*spokes(10,0.055,0.245,0.13,0.05,0.025))
V,N,I=disc(0.11,0.06,1);add(wheel,'rimface',V,N,I)
V,N,I=disc(0.112,0.035,1);add(wheel,'gold',V,N,I)
V,N,I=disc(0.02,0.2,1);add(wheel,'disc',V,N,I)
V,N,I=disc(-0.13,0.236,-1);add(wheel,'rim',V,N,I)
for p in P:
    for c in p['ch']:
        if not (M[c['mat']]['fl']&9): dbl(c)
lbm.write(LB+'roadster66.bytes',M,P)
print('tail z',round(float(zr),3),'tris',sum(len(c['idx'])//3 for p in P for c in p['ch']))
