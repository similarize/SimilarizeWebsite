using System.IO;
using UnityEngine;

// REAL ROOM first-person frog hands: two skinned meshes (hands.bin "RRS1": a procedural frog forelimb built in Blender
// with a skin modifier + subdivision - forearm, palm, four slender fingers with round adhesive toe pads - and 14 bones,
// weights by distance to the bone segments). Procedural poses: idle sway + walk bob, the intro "look at my hands"
// (fingers flex while they turn real), reach / poke (light switch), hold + throw (duck), resting on the armrests.
public static class Hands
{
    static Transform rootR, rootL;
    static Transform[] bR, bL;
    static Quaternion[] restR, restL;
    static Material matR, matL;
    class CpuSkin { public Mesh mesh; public Vector3[] pos, nrm, outP, outN; public BoneWeight[] bw; public Matrix4x4[] bind, m; public Transform[] bones; public Transform root; }
    static CpuSkin skinR, skinL;
    static bool loggedView;
    static void Skin(CpuSkin k)
    {
        if (k == null) return;
        Matrix4x4 w2r = k.root.worldToLocalMatrix;
        for (int b = 0; b < k.bones.Length; b++) k.m[b] = w2r * k.bones[b].localToWorldMatrix * k.bind[b];
        for (int i = 0; i < k.pos.Length; i++)
        {
            BoneWeight w = k.bw[i]; Vector3 v = k.pos[i], n = k.nrm[i];
            Matrix4x4 a = k.m[w.boneIndex0], b1 = k.m[w.boneIndex1], c = k.m[w.boneIndex2];
            k.outP[i] = a.MultiplyPoint3x4(v) * w.weight0 + b1.MultiplyPoint3x4(v) * w.weight1 + c.MultiplyPoint3x4(v) * w.weight2;
            k.outN[i] = a.MultiplyVector(n) * w.weight0 + b1.MultiplyVector(n) * w.weight1 + c.MultiplyVector(n) * w.weight2;
        }
        k.mesh.vertices = k.outP; k.mesh.normals = k.outN;
    }
    static string[] names;
    public static float introPose, walk;
    static float morph = 1f;
    public static Vector3 WaveOrigin = Vector3.zero;
    public static Vector3 HoldPoint { get { return rootR != null ? rootR.TransformPoint(new Vector3(0.01f, -0.05f, 0.105f)) : Vector3.zero; } }
    public static Quaternion HoldRot { get { return rootR != null ? rootR.rotation * Quaternion.Euler(-10f, 200f, 0f) : Quaternion.identity; } }
    static Transform eye;

    public static void Build(byte[] data, Transform eyeT, Texture2D skin, Color frogColor)
    {
        eye = eyeT;
        var r = new BinaryReader(new MemoryStream(data));
        r.ReadBytes(4);
        int nb = r.ReadInt32(), nm = r.ReadInt32();
        names = new string[nb];
        var par = new int[nb]; var head = new Vector3[nb]; var tail = new Vector3[nb];
        for (int i = 0; i < nb; i++)
        {
            int l = r.ReadByte(); names[i] = System.Text.Encoding.UTF8.GetString(r.ReadBytes(l));
            par[i] = r.ReadInt32();
            head[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            tail[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
        var sh = Shader.Find("FF/RRSkin");
        Color toon = frogColor.linear;
        for (int side = 0; side < 2 && side < nm; side++)
        {
            int nv = r.ReadInt32(), nt = r.ReadInt32();
            var pos = new Vector3[nv]; var nrm = new Vector3[nv];
            for (int i = 0; i < nv; i++) pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            for (int i = 0; i < nv; i++) nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var idx = new byte[nv * 3]; for (int i = 0; i < idx.Length; i++) idx[i] = r.ReadByte();
            var w = new float[nv * 3]; for (int i = 0; i < w.Length; i++) w[i] = r.ReadSingle();
            var tri = new int[nt * 3];
            for (int i = 0; i < tri.Length; i++) tri[i] = nv < 65536 ? r.ReadUInt16() : (int)r.ReadUInt32();
            float mx = side == 1 ? -1f : 1f;
            // bones (mirrored for the left hand): local +z along the bone, x horizontal, so curling = +x rotation
            var go = new GameObject(side == 0 ? "FrogHandR" : "FrogHandL"); go.layer = RealRoom.Layer;
            var root = go.transform; root.SetParent(eyeT, false);
            var bones = new Transform[nb];
            for (int i = 0; i < nb; i++)
            {
                var t = new GameObject(names[i]).transform;
                Vector3 h = new Vector3(head[i].x * mx, head[i].y, head[i].z), tl = new Vector3(tail[i].x * mx, tail[i].y, tail[i].z);
                Vector3 d = tl - h; if (d.sqrMagnitude < 1e-8) d = Vector3.forward;
                t.SetParent(par[i] >= 0 ? bones[par[i]] : root, false);
                t.position = root.TransformPoint(h);
                t.rotation = root.rotation * Quaternion.LookRotation(d.normalized, Vector3.up);
                bones[i] = t;
            }
            var bind = new Matrix4x4[nb];
            for (int i = 0; i < nb; i++) bind[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            var bw = new BoneWeight[nv];
            var col = new Color[nv];
            var rest = new System.Collections.Generic.List<Vector3>(pos);
            var restN = new System.Collections.Generic.List<Vector3>(nrm);
            for (int i = 0; i < nv; i++)
            {
                bw[i] = new BoneWeight { boneIndex0 = idx[i * 3], weight0 = w[i * 3], boneIndex1 = idx[i * 3 + 1], weight1 = w[i * 3 + 1], boneIndex2 = idx[i * 3 + 2], weight2 = w[i * 3 + 2] };
                // toe pads = near the tip of each last finger bone
                float pad = 0f;
                for (int b = 0; b < nb; b++)
                    if (names[b].EndsWith("_2"))
                    {
                        Vector3 tp = new Vector3(tail[b].x * mx, tail[b].y, tail[b].z);
                        pad = Mathf.Max(pad, Mathf.Clamp01(1f - ((pos[i] - tp).magnitude - 0.006f) / 0.008f));
                    }
                col[i] = new Color(1, 1, 1, pad);
                rest[i] = new Vector3(pos[i].x * mx, pos[i].y, pos[i].z);    // same skin pattern on both hands
                restN[i] = new Vector3(nrm[i].x * mx, nrm[i].y, nrm[i].z);
            }
            var mesh = new Mesh { name = go.name };
            mesh.vertices = pos; mesh.normals = nrm; mesh.triangles = tri; mesh.colors = col;
            mesh.SetUVs(2, rest); mesh.SetUVs(3, restN);
            mesh.RecalculateBounds();
            // ffu18c: skinned on the CPU into a plain MeshRenderer (the SkinnedMeshRenderer hands never showed up in the
            // WebGL build - no other SkinnedMeshRenderer in the game; 2 x 3k verts x 3 bones per frame is cheap)
            mesh.MarkDynamic();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var smr = go.AddComponent<MeshRenderer>();
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false;
            smr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var m = new Material(sh); m.name = go.name;
            if (!sh.isSupported) Debug.LogWarning("RealRoom: FF/RRSkin not supported here");
            if (skin != null) m.SetTexture("_SkinTex", skin);
            m.SetColor("_Toon", new Color(toon.r, toon.g, toon.b, 1f));
            smr.sharedMaterial = m;
            var ck = new CpuSkin { mesh = mesh, pos = pos, nrm = nrm, bw = bw, bind = bind, bones = bones, root = root, outP = new Vector3[nv], outN = new Vector3[nv], m = new Matrix4x4[nb] };
            if (side == 0) skinR = ck; else skinL = ck;
            var restQ = new Quaternion[nb]; for (int i = 0; i < nb; i++) restQ[i] = bones[i].localRotation;
            if (side == 0) { rootR = root; bR = bones; restR = restQ; matR = m; } else { rootL = root; bL = bones; restL = restQ; matL = m; }
        }
        Debug.Log("RealRoom: frog hands " + nb + " bones");
    }

    public static void SetMorph(float k)
    {
        morph = k;
        if (matR != null) matR.SetFloat("_Morph", k);
        if (matL != null) matL.SetFloat("_Morph", k);
        if (rootR != null) WaveOrigin = (rootR.position + rootL.position) * 0.5f;
    }

    public static void SetLights(Vector3 lamp, Vector3 tv)
    {
        if (matR == null) return;
        matR.SetVector("_LampPos", lamp); matL.SetVector("_LampPos", lamp);
        matR.SetVector("_TvPos", tv); matL.SetVector("_TvPos", tv);
    }

    // pose of one hand in eye space: position, euler, finger curl (0 open .. 1 fist), index finger extra (-1 pointing)
    struct Pose { public Vector3 p, e; public float curl, index; }
    static Pose Lerp(Pose a, Pose b, float t) { return new Pose { p = Vector3.Lerp(a.p, b.p, t), e = new Vector3(Mathf.LerpAngle(a.e.x, b.e.x, t), Mathf.LerpAngle(a.e.y, b.e.y, t), Mathf.LerpAngle(a.e.z, b.e.z, t)), curl = Mathf.Lerp(a.curl, b.curl, t), index = Mathf.Lerp(a.index, b.index, t) }; }
    static Pose curR, curL;
    static bool init;

    public static void Tick(float dt, bool holding, float charge, ref float throwT, ref float pokeT, bool sitting, bool play)
    {
        if (rootR == null) return;
        float t = Time.time;
        float bob = Mathf.Sin(walk) * 0.010f, bob2 = Mathf.Abs(Mathf.Cos(walk)) * 0.006f;
        float sway = Mathf.Sin(t * 1.3f) * 0.004f;
        // idle: low in the corners, fingers relaxed
        var idleR = new Pose { p = new Vector3(0.17f, -0.17f + bob + sway, 0.32f), e = new Vector3(38f, -12f, -20f), curl = 0.3f };
        // intro: both hands raised in front of the chest, backs up, looking down at them
        var introR = new Pose { p = new Vector3(0.085f, -0.20f, 0.30f), e = new Vector3(18f, -22f, -12f), curl = 0.12f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * 2.2f)) };
        var holdR = new Pose { p = new Vector3(0.13f, -0.17f + bob, 0.33f), e = new Vector3(20f, -18f, -70f), curl = 0.55f };
        var sitR = new Pose { p = new Vector3(0.24f, -0.33f, 0.24f), e = new Vector3(55f, -6f, -10f), curl = 0.35f };
        Pose tR = sitting ? sitR : holding ? holdR : idleR;
        tR = Lerp(tR, introR, introPose);
        if (holding && charge > 0f) tR.p += new Vector3(0.03f, 0.05f, -0.10f) * charge;   // wind up
        if (throwT >= 0f)
        {
            throwT += dt;
            float k = Mathf.Sin(Mathf.Clamp01(throwT / 0.35f) * Mathf.PI);
            tR.p += new Vector3(-0.03f, 0.06f, 0.16f) * k; tR.e += new Vector3(-30f, 0f, 0f) * k; tR.curl = Mathf.Lerp(tR.curl, 0.05f, k);
            if (throwT > 0.4f) throwT = -1f;
        }
        if (pokeT >= 0f)
        {
            pokeT += dt;
            float k = Mathf.Sin(Mathf.Clamp01(pokeT / 0.45f) * Mathf.PI);
            tR.p += new Vector3(-0.06f, 0.12f, 0.2f) * k; tR.e += new Vector3(-35f, 0f, 0f) * k; tR.index = -k; tR.curl = Mathf.Lerp(tR.curl, 0.8f, k);
            if (pokeT > 0.5f) pokeT = -1f;
        }
        Pose tL = sitting ? sitR : idleR; tL = Lerp(tL, introR, introPose);
        tL.p.x = -tL.p.x; tL.e.y = -tL.e.y; tL.e.z = -tL.e.z;
        tL.p.y += Mathf.Sin(walk + Mathf.PI) * 0.010f - bob;
        if (!play && introPose <= 0f && !sitting) { }
        if (!init) { curR = tR; curL = tL; init = true; }
        float s = 1f - Mathf.Exp(-dt * 14f);
        curR = Lerp(curR, tR, s); curL = Lerp(curL, tL, s);
        Apply(rootR, bR, restR, curR, t);
        Apply(rootL, bL, restL, curL, t + 0.7f);
        Skin(skinR); Skin(skinL);
        if (play && !loggedView)
        {
            loggedView = true;
            var cam = eye != null ? eye.GetComponentInChildren<Camera>() : null;
            if (cam != null) Debug.Log("RealRoom: hands at viewport R " + cam.WorldToViewportPoint(rootR.position).ToString("F2") + " L " + cam.WorldToViewportPoint(rootL.position).ToString("F2"));
        }
        WaveOrigin = (rootR.position + rootL.position) * 0.5f + eye.forward * 0.08f;
    }

    static void Apply(Transform root, Transform[] b, Quaternion[] rest, Pose p, float t)
    {
        root.localPosition = p.p;
        root.localRotation = Quaternion.Euler(p.e);
        for (int i = 0; i < b.Length; i++)
        {
            string n = names[i];
            if (n.Length < 4 || n[0] != 'f') continue;
            int finger = n[1] - '0', j = n[3] - '0';
            float c = p.curl + Mathf.Sin(t * 1.7f + finger) * 0.03f;
            if (finger == 1 && p.index < 0f) c = Mathf.Lerp(c, -0.15f, -p.index);
            float ang = c * (j == 0 ? 38f : j == 1 ? 55f : 40f) + finger * 1.5f;
            b[i].localRotation = rest[i] * Quaternion.Euler(ang, 0f, 0f);
        }
    }
}
