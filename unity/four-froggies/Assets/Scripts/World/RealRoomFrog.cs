using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ffu23 REAL ROOM frog assets + the third-person body. frog.bin ("RRF1", work/rr/frog/export.py) holds four sets:
//  hand  - the first-person right forelimb (procedural SDF model: muscular forearm, broad palm, four webbed fingers with
//          round adhesive toe pads + subarticular tubercles; 14 bones). The left hand is the mirror (RealRoomHands.cs).
//  body  - a realistic frog standing on its hind legs (52 bones: hips, spine, chest, head; per side upper arm, forearm,
//          palm, 4x3 finger joints, thigh, shin, tarsus, foot, 5 toes), wet-skin RRSkin with a baked dorsal / belly mask.
//  eye   - unit sphere for the two eyeballs (FF/RREye: gold iris, horizontal pupil, wet cornea).
//  plush - the plush frog toy (vertex colours, FF/RRPlush fleece).
// Animation is all procedural: two-bone IK for arms and legs, walk cycle, idle breathing, sit, reach, grab, throw.
public static class FrogAsset
{
    public class Set
    {
        public string name;
        public string[] bone; public int[] parent; public Vector3[] head, tail, up;
        public Vector3[] pos, nrm; public BoneWeight[] bw; public Color32[] col; public int[] tri;
    }

    public static Dictionary<string, Set> Parse(byte[] data)
    {
        var o = new Dictionary<string, Set>();
        var r = new BinaryReader(new MemoryStream(data));
        r.ReadBytes(4);
        int ns = r.ReadInt32();
        for (int s = 0; s < ns; s++)
        {
            var S = new Set();
            S.name = Str(r);
            int nb = r.ReadInt32();
            S.bone = new string[nb]; S.parent = new int[nb]; S.head = new Vector3[nb]; S.tail = new Vector3[nb]; S.up = new Vector3[nb];
            for (int i = 0; i < nb; i++)
            {
                S.bone[i] = Str(r); S.parent[i] = r.ReadInt32();
                S.head[i] = V3(r); S.tail[i] = V3(r); S.up[i] = V3(r);
            }
            int nv = r.ReadInt32(), nt = r.ReadInt32();
            S.pos = new Vector3[nv]; S.nrm = new Vector3[nv];
            for (int i = 0; i < nv; i++) S.pos[i] = V3(r);
            for (int i = 0; i < nv; i++) S.nrm[i] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()) / 127f;
            if (nb > 0)
            {
                var idx = r.ReadBytes(nv * 4); var w = r.ReadBytes(nv * 4);
                S.bw = new BoneWeight[nv];
                for (int i = 0; i < nv; i++)
                {
                    float a = w[i * 4], b = w[i * 4 + 1], c = w[i * 4 + 2], d = w[i * 4 + 3], sum = Mathf.Max(1f, a + b + c + d);
                    S.bw[i] = new BoneWeight { boneIndex0 = idx[i * 4], weight0 = a / sum, boneIndex1 = idx[i * 4 + 1], weight1 = b / sum, boneIndex2 = idx[i * 4 + 2], weight2 = c / sum, boneIndex3 = idx[i * 4 + 3], weight3 = d / sum };
                }
            }
            var cb = r.ReadBytes(nv * 4);
            S.col = new Color32[nv];
            for (int i = 0; i < nv; i++) S.col[i] = new Color32(cb[i * 4], cb[i * 4 + 1], cb[i * 4 + 2], cb[i * 4 + 3]);
            S.tri = new int[nt * 3];
            if (nv < 65536) for (int i = 0; i < S.tri.Length; i++) S.tri[i] = r.ReadUInt16();
            else for (int i = 0; i < S.tri.Length; i++) S.tri[i] = (int)r.ReadUInt32();
            o[S.name] = S;
        }
        return o;
    }
    static string Str(BinaryReader r) { int n = r.ReadByte(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }
    static Vector3 V3(BinaryReader r) { return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); }

    // a skinned rig: bone transforms (local +z along the bone, +y = the exported up hint), rest rotations, the renderer
    public class Rig
    {
        public Transform root; public Transform[] b; public Quaternion[] rest; public Vector3[] restPos; public string[] names;
        public SkinnedMeshRenderer smr; public Material mat; public float[] len;
        public int Find(string n) { for (int i = 0; i < names.Length; i++) if (names[i] == n) return i; return -1; }
    }

    public static Rig Build(Set s, Transform parent, string name, Material mat, bool mirror, int layer)
    {
        var go = new GameObject(name); go.layer = layer;
        var root = go.transform; root.SetParent(parent, false);
        int nb = s.bone.Length; float mx = mirror ? -1f : 1f;
        var rig = new Rig { root = root, b = new Transform[nb], rest = new Quaternion[nb], restPos = new Vector3[nb], names = s.bone, len = new float[nb] };
        for (int i = 0; i < nb; i++)
        {
            var t = new GameObject(s.bone[i]).transform;
            Vector3 h = M(s.head[i], mx), tl = M(s.tail[i], mx), up = M(s.up[i], mx);
            Vector3 d = tl - h; if (d.sqrMagnitude < 1e-10) d = Vector3.forward;
            t.SetParent(s.parent[i] >= 0 ? rig.b[s.parent[i]] : root, false);
            t.position = root.TransformPoint(h);
            Vector3 u = up - d.normalized * Vector3.Dot(up, d.normalized); if (u.sqrMagnitude < 1e-6) u = Vector3.forward;
            t.rotation = root.rotation * Quaternion.LookRotation(d.normalized, u.normalized);
            rig.b[i] = t; rig.len[i] = d.magnitude;
        }
        var bind = new Matrix4x4[nb];
        for (int i = 0; i < nb; i++) { bind[i] = rig.b[i].worldToLocalMatrix * root.localToWorldMatrix; rig.rest[i] = rig.b[i].localRotation; rig.restPos[i] = rig.b[i].localPosition; }
        int nv = s.pos.Length;
        var pos = new Vector3[nv]; var nrm = new Vector3[nv]; var rest = new List<Vector3>(nv); var restN = new List<Vector3>(nv);
        var col = new Color32[nv];
        for (int i = 0; i < nv; i++)
        {
            pos[i] = M(s.pos[i], mx); nrm[i] = M(s.nrm[i], mx);
            rest.Add(s.pos[i]); restN.Add(s.nrm[i]);     // same skin pattern on both sides (unmirrored rest space)
            col[i] = s.col[i];
        }
        int[] tri = s.tri;
        if (mirror) { tri = (int[])s.tri.Clone(); for (int i = 0; i < tri.Length; i += 3) { int k = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = k; } }
        var mesh = new Mesh { name = name };
        if (nv >= 65536) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = pos; mesh.normals = nrm; mesh.colors32 = col; mesh.triangles = tri;
        if (s.bw != null) { mesh.boneWeights = s.bw; mesh.bindposes = bind; }
        mesh.SetUVs(2, rest); mesh.SetUVs(3, restN);
        mesh.RecalculateBounds();
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh; smr.bones = rig.b; smr.rootBone = rig.b[0];
        smr.quality = SkinQuality.Bone4;
        smr.updateWhenOffscreen = true;
        smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false;
        smr.sharedMaterial = mat;
        rig.smr = smr; rig.mat = mat;
        return rig;
    }
    static Vector3 M(Vector3 v, float mx) { return new Vector3(v.x * mx, v.y, v.z); }

    public static Mesh StaticMesh(Set s)
    {
        var m = new Mesh { name = s.name };
        m.vertices = s.pos; m.normals = s.nrm; m.colors32 = s.col; m.triangles = s.tri;
        m.RecalculateBounds();
        return m;
    }

    // two-bone IK: elbow / knee position for a chain root -> joint -> end of lengths a, b reaching for t, bending towards pole
    public static Vector3 SolveJoint(Vector3 root, Vector3 t, float a, float b, Vector3 pole)
    {
        Vector3 d = t - root; float L = d.magnitude;
        L = Mathf.Clamp(L, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-4f);
        Vector3 dir = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        float x = (a * a - b * b + L * L) / (2f * L);
        float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
        Vector3 p = pole - dir * Vector3.Dot(pole, dir); if (p.sqrMagnitude < 1e-8f) p = Vector3.Cross(dir, Vector3.right);
        return root + dir * x + p.normalized * h;
    }

    // point a bone (local +z) at a world target keeping its up hint
    public static void Aim(Transform bone, Vector3 target, Vector3 up)
    {
        Vector3 d = target - bone.position; if (d.sqrMagnitude < 1e-10f) return;
        Vector3 u = up - d.normalized * Vector3.Dot(up, d.normalized); if (u.sqrMagnitude < 1e-8f) u = Vector3.Cross(d, Vector3.right);
        bone.rotation = Quaternion.LookRotation(d.normalized, u.normalized);
    }

    // finger curl: rotate each f<i>_<j> joint about its local x (towards the palm), index override for pointing
    public static void Curl(Rig r, int[] fb, float[] curl, float t, float wobble)
    {
        for (int k = 0; k < fb.Length; k++)
        {
            int i = fb[k]; if (i < 0) continue;
            int finger = k / 3, j = k % 3;
            float c = curl[finger] + Mathf.Sin(t * 1.7f + finger * 1.3f) * wobble;
            float ang = c * (j == 0 ? 42f : j == 1 ? 58f : 42f) + (c < 0f ? 0f : finger * 1.5f);
            r.b[i].localRotation = r.rest[i] * Quaternion.Euler(ang, 0f, 0f);
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------- body
public static class FrogBody
{
    static FrogAsset.Rig rig;
    static Transform eyeL, eyeR;
    static Material skin, eyeMat;
    static int hips, spine, chest, head;
    static readonly int[] arm = new int[6], leg = new int[8];   // per side: upper, fore, palm | thigh, shin, tarsus, foot
    static int[][] fingers = new int[2][];
    static int[][] toes = new int[2][];
    static Vector3 hipsRest; static float[] armLen = new float[4], legLen = new float[4];
    public static bool Visible { get { return rig != null && rig.smr.enabled; } }
    public static Transform Root { get { return rig != null ? rig.root : null; } }
    static float breathe, swayV;
    // right / left wrist targets (world) + palm rotation; w = blend (0 = hang)
    public static Vector3[] handT = new Vector3[2]; public static Quaternion[] handR = new Quaternion[2]; public static float[] handW = new float[2];
    public static float[][] curl = { new float[4], new float[4] };
    public static bool sitting; public static Vector3 seatPos; public static float seatYaw;
    public static float speed;   // 0..1 walking
    public static float lookPitch;

    public static void Build(FrogAsset.Set s, FrogAsset.Set eye, Transform parent, Texture2D skinTex, int layer)
    {
        skin = new Material(Shader.Find("FF/RRSkin")) { name = "RR frog body" };
        if (skinTex != null) skin.SetTexture("_SkinTex", skinTex);
        skin.SetFloat("_Morph", 1f); skin.SetFloat("_TexScale", 9f); skin.SetFloat("_Body", 1f);
        rig = FrogAsset.Build(s, parent, "FrogBody", skin, false, layer);
        hips = rig.Find("hips"); spine = rig.Find("spine"); chest = rig.Find("chest"); head = rig.Find("head");
        string[] sides = { "R_", "L_" };
        for (int k = 0; k < 2; k++)
        {
            string p = sides[k];
            arm[k * 3] = rig.Find(p + "upperarm"); arm[k * 3 + 1] = rig.Find(p + "forearm"); arm[k * 3 + 2] = rig.Find(p + "palm");
            leg[k * 4] = rig.Find(p + "thigh"); leg[k * 4 + 1] = rig.Find(p + "shin"); leg[k * 4 + 2] = rig.Find(p + "tarsus"); leg[k * 4 + 3] = rig.Find(p + "foot");
            fingers[k] = new int[12]; for (int i = 0; i < 4; i++) for (int j = 0; j < 3; j++) fingers[k][i * 3 + j] = rig.Find(p + "f" + i + "_" + j);
            toes[k] = new int[5]; for (int i = 0; i < 5; i++) toes[k][i] = rig.Find(p + "t" + i);
        }
        armLen[0] = rig.len[arm[0]]; armLen[1] = rig.len[arm[1]];
        legLen[0] = rig.len[leg[0]]; legLen[1] = rig.len[leg[1]];
        hipsRest = rig.restPos[hips];
        // eyes: rigid spheres on the head bone
        eyeMat = new Material(Shader.Find("FF/RREye")) { name = "RR frog eye" };
        var em = FrogAsset.StaticMesh(eye);
        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? 1f : -1f;
            var go = new GameObject(k == 0 ? "EyeR" : "EyeL"); go.layer = layer;
            go.AddComponent<MeshFilter>().sharedMesh = em;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = eyeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.transform.SetParent(rig.b[head], false);
            go.transform.position = rig.root.TransformPoint(new Vector3(0.128f * sx, 1.452f, 0.170f));
            go.transform.rotation = rig.root.rotation * Quaternion.LookRotation(new Vector3(0.62f * sx, 0.28f, 0.73f), Vector3.up);
            go.transform.localScale = Vector3.one * 0.064f;
            if (k == 0) eyeR = go.transform; else eyeL = go.transform;
        }
        SetVisible(false);
    }

    public static void SetVisible(bool on)
    {
        if (rig == null) return;
        rig.smr.enabled = on;
        if (eyeR != null) { eyeR.GetComponent<MeshRenderer>().enabled = on; eyeL.GetComponent<MeshRenderer>().enabled = on; }
    }

    public static void SetLights(Vector3 lamp, Vector3 tv)
    {
        if (skin == null) return;
        skin.SetVector("_LampPos", lamp); skin.SetVector("_TvPos", tv);
        eyeMat.SetVector("_LampPos", lamp);
    }

    public static Vector3 PalmPoint(int k, Vector3 local)
    {
        if (rig == null) return Vector3.zero;
        return rig.b[arm[k * 3 + 2]].TransformPoint(local * 1.45f);
    }
    public static Quaternion PalmRot(int k) { return rig != null ? rig.b[arm[k * 3 + 2]].rotation : Quaternion.identity; }
    public static Vector3 Shoulder(int k) { return rig != null ? rig.b[arm[k * 3]].position : Vector3.zero; }

    // walkPhase: same clock as the first-person bob
    public static void Tick(float dt, float walkPhase)
    {
        if (rig == null || !rig.smr.enabled) return;
        float t = Time.time;
        breathe += dt;
        // reset to rest
        for (int i = 0; i < rig.b.Length; i++) { rig.b[i].localRotation = rig.rest[i]; rig.b[i].localPosition = rig.restPos[i]; }
        Transform R = rig.root;
        Vector3 fwd = R.forward, right = R.right, up = Vector3.up;
        float sp = Mathf.Clamp01(speed);
        if (sitting)
        {
            // hips back on the cushion, thighs forward, shins down
            rig.b[hips].localPosition = hipsRest + new Vector3(0f, -0.13f, -0.06f);
            rig.b[spine].localRotation = rig.rest[spine] * Quaternion.Euler(-8f, 0f, 0f);
        }
        else
        {
            float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * 0.025f * sp;
            rig.b[hips].localPosition = hipsRest + new Vector3(Mathf.Sin(walkPhase) * 0.018f * sp, -bob - 0.01f * sp, 0f);
            rig.b[hips].localRotation = rig.rest[hips] * Quaternion.Euler(0f, Mathf.Sin(walkPhase) * 7f * sp, Mathf.Sin(walkPhase) * 3f * sp);
            rig.b[spine].localRotation = rig.rest[spine] * Quaternion.Euler(4f * sp, -Mathf.Sin(walkPhase) * 5f * sp, 0f);
        }
        // breathing: the throat + chest swell
        float br = Mathf.Sin(breathe * 2.1f) * 0.5f + 0.5f;
        rig.b[chest].localScale = new Vector3(1f + 0.012f * br, 1f, 1f + 0.02f * br);
        // head follows the look pitch a little (the eyes stay level with the camera)
        rig.b[head].localRotation = rig.rest[head] * Quaternion.Euler(Mathf.Clamp(lookPitch * 0.45f, -18f, 22f), 0f, 0f);
        // legs
        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? 1f : -1f;
            Transform th = rig.b[leg[k * 4]], sh = rig.b[leg[k * 4 + 1]], ta = rig.b[leg[k * 4 + 2]], ft = rig.b[leg[k * 4 + 3]];
            Vector3 hip = th.position;
            Vector3 ankle;
            Vector3 footFwd = Quaternion.AngleAxis(14f * sx, up) * fwd;
            if (sitting)
            {
                Vector3 knee = hip + fwd * 0.34f + right * sx * 0.06f + up * 0.03f;
                ankle = knee - up * 0.30f + fwd * 0.06f;
            }
            else
            {
                float ph = walkPhase + (k == 0 ? 0f : Mathf.PI);
                float stride = Mathf.Sin(ph) * 0.15f * sp, lift = Mathf.Max(0f, Mathf.Cos(ph)) * 0.07f * sp;
                ankle = R.TransformPoint(new Vector3(0.200f * sx, 0.125f, -0.050f)) + fwd * stride + up * lift;
            }
            Vector3 kneeP = FrogAsset.SolveJoint(hip, ankle, legLen[0], legLen[1], fwd + right * sx * 0.25f);
            FrogAsset.Aim(th, kneeP, fwd);
            FrogAsset.Aim(sh, ankle, fwd);
            // tarsus down to the foot, foot flat (toes droop when lifted)
            Vector3 fp = sitting ? ankle + fwd * 0.12f - up * 0.06f : ankle + footFwd * 0.137f - up * 0.089f;
            if (!sitting) fp.y = Mathf.Max(fp.y, R.position.y + 0.036f);
            FrogAsset.Aim(ta, fp, fwd);
            float lifted = Mathf.Clamp01((ankle.y - R.position.y - 0.125f) / 0.07f);
            ft.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(lifted * 25f, Vector3.Cross(up, footFwd)) * footFwd, up);
            foreach (int ti in toes[k]) if (ti >= 0) rig.b[ti].localRotation = rig.rest[ti] * Quaternion.Euler(lifted * 18f, 0f, 0f);
        }
        // arms: IK to the hand targets, else hanging with a counter-swing
        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? 1f : -1f;
            Transform ua = rig.b[arm[k * 3]], fa = rig.b[arm[k * 3 + 1]], pa = rig.b[arm[k * 3 + 2]];
            Vector3 sh = ua.position;
            float ph = walkPhase + (k == 0 ? Mathf.PI : 0f);
            Vector3 hang = R.TransformPoint(new Vector3(0.325f * sx, 0.735f + (sitting ? -0.05f : 0f), 0.17f)) + fwd * Mathf.Sin(ph) * 0.08f * sp + up * 0.006f * br;
            if (sitting) hang = sh + fwd * 0.12f + right * sx * 0.14f - up * 0.30f;
            float w = Mathf.Clamp01(handW[k]);
            Vector3 wrist = Vector3.Lerp(hang, handT[k], w);
            Vector3 elbow = FrogAsset.SolveJoint(sh, wrist, armLen[0], armLen[1], right * sx * 0.8f - fwd * 0.6f - up * 0.3f);
            FrogAsset.Aim(ua, elbow, fwd);
            FrogAsset.Aim(fa, wrist, fwd);
            if (w > 0.01f) pa.rotation = Quaternion.Slerp(pa.rotation, handR[k], w);
            FrogAsset.Curl(rig, fingers[k], curl[k], t + k, 0.02f);
        }
        // eyes: a slow blink (scale the eyeball into its socket) every few seconds
        float bl = Mathf.Repeat(t, 4.3f) < 0.12f ? 0.75f : 1f;
        if (eyeR != null) { eyeR.localScale = new Vector3(0.064f, 0.064f, 0.064f) * bl; eyeL.localScale = eyeR.localScale; }
    }
}
