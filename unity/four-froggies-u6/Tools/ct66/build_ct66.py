# ffct66 (Ben 2026-10-24): Cybertruck visual overhaul, built from Resources/LB/cybertruck52.bytes -> cybertruck66.bytes
#  * every opaque chunk double-sided (no see-through / culled faces from any angle), inner tyre face capped, darker side glass
#  * lower front fascia raked back + nose top lowered: long sloped hood to a sharp low nose (less blocky)
#  * thin full-width white light bar across the nose
# Arch sizing + camber are done at runtime (Vehicles/CyberArch66.cs) so the wells can open up when the wheels grow.
import sys,os,numpy as np,lbm
LB=os.path.join(os.path.dirname(__file__),'../../Assets/Resources/LB/')
M,P=lbm.parse(LB+'cybertruck52.bytes')
def dbl(c):
    v,n,t=c['v'],c['n'],c['idx'].reshape(-1,3);k=len(v)
    c['v']=np.concatenate([v,v]);c['n']=np.concatenate([n,-n])
    if c['uv'] is not None:c['uv']=np.concatenate([c['uv'],c['uv']])
    c['idx']=np.concatenate([t,t[:,[0,2,1]]+k]).reshape(-1)
def box(lo,hi):
    lo,hi=np.array(lo,float),np.array(hi,float);V=[];N=[];I=[]
    for ax in range(3):
        for s in (0,1):
            nn=np.zeros(3);nn[ax]=1 if s else -1
            a,b=[i for i in range(3) if i!=ax]
            q=[]
            for ua,ub in ((0,0),(1,0),(1,1),(0,1)):
                p=np.zeros(3);p[ax]=hi[ax] if s else lo[ax];p[a]=(lo,hi)[ua][a];p[b]=(lo,hi)[ub][b];q.append(p)
            base=len(V);V+=q;N+=[nn]*4
            tri=[[0,1,2],[0,2,3]]
            for tr in tri:
                pts=[q[i] for i in tr]
                if np.dot(np.cross(pts[1]-pts[0],pts[2]-pts[0]),nn)<0: tr=[tr[0],tr[2],tr[1]]
                I+=[base+i for i in tr]
    return np.array(V,np.float32),np.array(N,np.float32),np.array(I)
def disc(x,r,nx,seg=32):
    V=[[x,0,0]]+[[x,r*np.sin(a),r*np.cos(a)] for a in np.linspace(0,2*np.pi,seg,endpoint=False)]
    I=[]
    for i in range(seg):
        tr=[0,1+i,1+(i+1)%seg];p=np.array([V[j] for j in tr])
        if np.dot(np.cross(p[1]-p[0],p[2]-p[0]),[nx,0,0])<0: tr=[tr[0],tr[2],tr[1]]
        I+=tr
    return np.array(V,np.float32),np.array([[nx,0,0]]*len(V),np.float32),np.array(I)
body=[p for p in P if p['name']=='body'][0];wheel=[p for p in P if p['name']=='wheel'][0]
names=[m['name'] for m in M]
# --- nose reshape (body chunks, not the cabin / seats) ---
Z0,ZN=2.3,2.86
for c in body['ch']:
    nm=names[c['mat']]
    if nm in('seat','cabin'):continue
    v=c['v']
    # lower fascia rakes back: at y 0.42 the front is ~0.3 m behind the nose tip, none above y 1.0
    m=(v[:,2]>Z0)&(v[:,1]<1.0)
    zmax=ZN-np.clip(1.0-v[m,1],0,1)*0.5
    v[m,2]=Z0+(v[m,2]-Z0)*(zmax-Z0)/(ZN-Z0)
    # hood top falls to a sharp low nose
    m=(v[:,2]>1.45)&(v[:,1]>1.0)
    f=1-0.45*np.clip((v[m,2]-1.45)/1.4,0,1)
    v[m,1]=1.0+(v[m,1]-1.0)*f
    c['v']=v
    if nm in('glass',):M[c['mat']]['col']=(13,15,18,215)
# --- light bar: thin emissive strip on the nose tip ---
allv=np.concatenate([c['v'] for c in body['ch'] if names[c['mat']] not in('seat','cabin')])
s=allv[(allv[:,1]>0.95)&(allv[:,1]<1.15)]
zf=s[:,2].max()
V,N,I=box([-0.97,1.012,zf-0.03],[0.97,1.034,zf+0.006])
li=names.index('light')
body['ch'].append(dict(mat=li,v=V,n=N,uv=None,idx=I))
# --- inner tyre cap (wheel part is the right wheel: hub cap on +x, inner face -x) ---
V,N,I=disc(-0.143,0.44,-1)
wheel['ch'].append(dict(mat=names.index('tire'),v=V,n=N,uv=None,idx=I))
for p in (body,wheel):
    for c in p['ch']:
        if not (M[c['mat']]['fl']&9): dbl(c)   # opaque, non-emissive: double-sided
lbm.write(LB+'cybertruck66.bytes',M,P)
print('nose z',round(float(zf),3),'tris',sum(len(c['idx'])//3 for p in P for c in p['ch']))
