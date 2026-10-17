using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ffu23 REAL ROOM frog assets + the third-person body. frog.bin ("RRF1", work/rr/frog/export.py) holds four sets:
//  hand  - the first-person right forelimb (procedural SDF model: muscular forearm, broad palm, four webbed fingers with
//          round adhesive toe pads + subarticular tubercles; 14 bones). The left hand is the mirror (RealRoomHands.cs).
//  body  - ffu51: a REAL tree frog (work/ffu51/frog/body51.py), bound in its default crouch: squat flattened body, no neck,
//          big bulging eyes, wide mouth line, nostrils, tympanum, short front arms with thin fingers + sticky discs, long
//          folded hind legs (53 bones: hips, spine, chest, head, throat; per side upper arm, forearm, palm, 4x3 finger
//          joints, thigh, shin, tarsus, foot, 5 toes), wet-skin RRSkin with a baked dorsal / belly / lip-line mask.
//  eye   - unit sphere for the two eyeballs (FF/RREye: gold iris, horizontal pupil, wet cornea).
//  plush - the plush frog toy (vertex colours, FF/RRPlush fleece).
// Animation is all procedural: two-bone IK for arms and legs, hop cycle (ffu51), throat-pouch breathing, blink, reach, grab.
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

// ---------------------------------------------------------------------------------------------------------------- skin
// ffu51: tree-frog colours for the body + the first-person front feet (same wet RRSkin): leaf-green back, cream belly,
// subtle (not blotchy) spots, a little glossier
public static class FrogSkin
{
    public static void Apply(Material m, bool body)
    {
        if (m == null) return;
        m.SetColor("_Dorsal", new Color(0.034f, 0.150f, 0.017f, 1f));
        m.SetColor("_Ventral", new Color(0.50f, 0.46f, 0.30f, 1f));
        m.SetFloat("_Spots", body ? 0.40f : 0.30f);
        m.SetFloat("_RoughK", 0.80f);
    }
}

// ---------------------------------------------------------------------------------------------------------------- body
// ffu51 REAL frog body. Bind pose = the default crouch (sitting frog posture). Root = the floor point under the frog,
// facing +z; RealRoom puts it EyeZ behind the controller so the eyes sit over the first-person camera.
//  hop: a full cycle (push off with the hind legs, fly, front feet reach, land, fold) driven by Locomotion() from RealRoom
//       every frame in both views (HopY / Surge also drive the first-person bob + the hop speed surge)
//  throat: the pouch pulses (buccal breathing), bigger slow gulp every few seconds; eyes blink by retracting
public static class FrogBody
{
    static FrogAsset.Rig rig;
    static Transform eyeL, eyeR;
    static Material skin, eyeMat;
    static int hips, spine, chest, head, throat;
    static readonly int[] arm = new int[6], leg = new int[8];   // per side: upper, fore, palm | thigh, shin, tarsus, foot
    static int[][] fingers = new int[2][];
    static int[][] toes = new int[2][];
    static Vector3[] mPos; static Quaternion[] mRot;            // rest pose in model (root) space
    static float[] armLen = new float[4], legLen = new float[4];
    public static bool Visible { get { return rig != null && rig.smr.enabled; } }
    public static Transform Root { get { return rig != null ? rig.root : null; } }
    // the model: eye centre (right) + radius, eye height / forward offset of the first-person camera
    public static readonly Vector3 EyeC = new Vector3(0.158f, 0.812f, 0.215f);
    public const float EyeRad = 0.097f, EyeZ = 0.215f;
    public const float HandS = 0.85f;            // body hand vs the first-person hand units (PalmPoint)
    // right / left wrist targets (world) + palm rotation; w = blend (0 = planted on the floor)
    public static Vector3[] handT = new Vector3[2]; public static Quaternion[] handR = new Quaternion[2]; public static float[] handW = new float[2];
    public static float[][] curl = { new float[4], new float[4] };
    public static bool sitting; public static Vector3 seatPos; public static float seatYaw;
    public static float speed;                   // kept for RealRoom (0..1); the hop uses Locomotion()
    public static float lookPitch;
    public static float rise;                    // 0..1 stretch up tall (reaching high, e.g. the light switch)
    // hop state (shared with the first-person view)
    public static float HopP, HopY, Surge = 1f;
    static float breathe, gulpT = 3f, gulp;

    public static void Build(FrogAsset.Set s, FrogAsset.Set eye, Transform parent, Texture2D skinTex, int layer)
    {
        skin = new Material(Shader.Find("FF/RRSkin")) { name = "RR frog body" };
        if (skinTex != null) skin.SetTexture("_SkinTex", skinTex);
        skin.SetFloat("_Morph", 1f); skin.SetFloat("_TexScale", 9f); skin.SetFloat("_Body", 1f);
        FrogSkin.Apply(skin, true);
        rig = FrogAsset.Build(s, parent, "FrogBody", skin, false, layer);
        hips = rig.Find("hips"); spine = rig.Find("spine"); chest = rig.Find("chest"); head = rig.Find("head"); throat = rig.Find("throat");
        string[] sides = { "R_", "L_" };
        for (int k = 0; k < 2; k++)
        {
            string p = sides[k];
            arm[k * 3] = rig.Find(p + "upperarm"); arm[k * 3 + 1] = rig.Find(p + "forearm"); arm[k * 3 + 2] = rig.Find(p + "palm");
            leg[k * 4] = rig.Find(p + "thigh"); leg[k * 4 + 1] = rig.Find(p + "shin"); leg[k * 4 + 2] = rig.Find(p + "tarsus"); leg[k * 4 + 3] = rig.Find(p + "foot");
            fingers[k] = new int[12]; for (int i = 0; i < 4; i++) for (int j = 0; j < 3; j++) fingers[k][i * 3 + j] = rig.Find(p + "f" + i + "_" + j);
            toes[k] = new int[5]; for (int i = 0; i < 5; i++) toes[k][i] = rig.Find(p + "t" + i);
        }
        int nb = rig.b.Length; mPos = new Vector3[nb]; mRot = new Quaternion[nb];
        Quaternion iq = Quaternion.Inverse(rig.root.rotation);
        for (int i = 0; i < nb; i++) { mPos[i] = rig.root.InverseTransformPoint(rig.b[i].position); mRot[i] = iq * rig.b[i].rotation; }
        armLen[0] = rig.len[arm[0]]; armLen[1] = rig.len[arm[1]];
        legLen[0] = rig.len[leg[0]]; legLen[1] = rig.len[leg[1]];
        // eyes: rigid spheres on the head bone (FF/RREye: gold iris, horizontal pupil, wet cornea)
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
            go.transform.position = rig.root.TransformPoint(new Vector3(EyeC.x * sx, EyeC.y, EyeC.z));
            go.transform.rotation = rig.root.rotation * Quaternion.LookRotation(new Vector3(0.62f * sx, 0.25f, 0.74f), Vector3.up);
            go.transform.localScale = Vector3.one * EyeRad;
            if (k == 0) eyeR = go.transform; else eyeL = go.transform;
        }
        eyeRestR = eyeR.localPosition; eyeRestL = eyeL.localPosition;
        Debug.Log("RealRoom: real frog body " + nb + " bones, " + s.pos.Length + " verts, throat " + (throat >= 0));
        SetVisible(false);
    }
    static Vector3 eyeRestR, eyeRestL;

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
        return rig.b[arm[k * 3 + 2]].TransformPoint(local * HandS);
    }
    public static Quaternion PalmRot(int k) { return rig != null ? rig.b[arm[k * 3 + 2]].rotation : Quaternion.identity; }
    public static Vector3 Shoulder(int k) { return rig != null ? rig.b[arm[k * 3]].position : Vector3.zero; }

    // ---- hop clock (call every frame, both views). moving = the player is moving (0..1)
    public const float HopT = 0.62f, HopH = 0.17f;
    public static void Locomotion(float dt, float moving, bool seated)
    {
        if (seated) { HopP = 0f; HopY = 0f; Surge = 1f; return; }
        if (moving > 0.15f || HopP > 0f)
        {
            HopP += dt / HopT;
            if (HopP >= 1f) HopP = moving > 0.15f ? HopP - 1f : 0f;
        }
        HopY = Air(HopP) * HopH * Mathf.Clamp01(moving * 1.5f + (HopP > 0f ? 0.4f : 0f));
        // speed surge: slow while pushing off / landing, fast in the air (averages ~1)
        float a = Air(HopP);
        Surge = HopP > 0f ? Mathf.Lerp(0.35f, 1.55f, a) : 1f;
    }
    static float Air(float p) { return p > 0.16f && p < 0.80f ? Mathf.Sin(Mathf.PI * (p - 0.16f) / 0.64f) : 0f; }
    static float Ext(float p)   // hind legs: 0 folded .. 1 fully extended
    {
        if (p <= 0f) return 0f;
        if (p < 0.20f) return S(p / 0.20f);
        if (p < 0.45f) return 1f;
        if (p < 0.88f) return 1f - S((p - 0.45f) / 0.43f);
        return 0f;
    }
    static float Reach(float p) { return p > 0.38f && p < 0.92f ? Mathf.Sin(Mathf.PI * (p - 0.38f) / 0.54f) : 0f; }
    static float S(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    static Vector3 W(Vector3 m) { return rig.root.TransformPoint(m); }
    static Vector3 Up(int i) { return rig.root.rotation * (mRot[i] * Vector3.up); }
    static Vector3 Mir(Vector3 v, float sx) { return new Vector3(v.x * sx, v.y, v.z); }

    public static void Tick(float dt, float walkPhase)
    {
        if (rig == null || !rig.smr.enabled) return;
        float t = Time.time;
        breathe += dt;
        for (int i = 0; i < rig.b.Length; i++) { rig.b[i].localRotation = rig.rest[i]; rig.b[i].localPosition = rig.restPos[i]; rig.b[i].localScale = Vector3.one; }
        Transform R = rig.root;
        Vector3 up = R.up, fwd = R.forward;
        float p = sitting ? 0f : HopP;
        float ext = Ext(p), air = Air(p), reach = Reach(p);
        float lift = sitting ? 0f : HopY;
        float rs = sitting ? 0f : Mathf.Clamp01(rise);
        // ---- body: lifted by the hop, pitched flatter (nose down) while the legs push, up while stretching tall; idle sway
        float idle = Mathf.Sin(breathe * 0.9f) * 0.004f;
        rig.b[hips].localPosition = rig.restPos[hips] + new Vector3(0f, lift + rs * 0.10f + idle + ext * 0.03f, ext * 0.05f);
        rig.b[hips].localRotation = Quaternion.AngleAxis(ext * 16f - air * 6f - rs * 22f, Vector3.right) * rig.rest[hips];
        rig.b[spine].localRotation = rig.rest[spine] * Quaternion.Euler(ext * 4f - rs * 6f, 0f, 0f);
        // head: follows the look pitch a little; counters the body pitch so the eyes stay level
        rig.b[head].localRotation = rig.rest[head] * Quaternion.Euler(Mathf.Clamp(lookPitch * 0.30f, -12f, 14f) - ext * 10f + rs * 14f, 0f, 0f);
        // ---- throat pouch: fast shallow flutter + a slow full gulp every few seconds
        gulpT -= dt; if (gulpT < 0f) { gulpT = Random.Range(3.5f, 6.5f); gulp = 1f; }
        gulp = Mathf.MoveTowards(gulp, 0f, dt * 0.9f);
        float fl = Mathf.Sin(breathe * Mathf.PI * 2f * 2.2f) * 0.5f + 0.5f;
        float g = Mathf.Sin((1f - gulp) * Mathf.PI) * (gulp > 0f ? 1f : 0f);
        if (throat >= 0) rig.b[throat].localScale = new Vector3(1f + 0.03f * fl + 0.07f * g, 1f + 0.06f * fl + 0.20f * g, 1f + 0.02f * fl + 0.05f * g);
        // ---- hind legs: two-bone IK hip -> knee -> heel; the folded crouch is the rest pose
        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? 1f : -1f;
            int th = leg[k * 4], sh = leg[k * 4 + 1], ta = leg[k * 4 + 2], ft = leg[k * 4 + 3];
            Vector3 hip = rig.b[th].position;
            Vector3 heel0 = mPos[ta], tb0 = mPos[ft];
            // extended: heel far back + up behind the body, the foot trailing back, toes pointed
            Vector3 heelX = Mir(new Vector3(0.20f, 0.20f, -1.05f), sx), tbX = Mir(new Vector3(0.22f, 0.12f, -1.30f), sx);
            Vector3 heelM = Vector3.Lerp(heel0, heelX, ext), tbM = Vector3.Lerp(tb0, tbX, ext);
            if (rs > 0f) { heelM += new Vector3(0f, rs * 0.04f, rs * 0.10f); tbM += new Vector3(0f, 0f, rs * 0.04f); }
            Vector3 heel = W(heelM), tb = W(tbM);
            float onGround = 1f - S(air * 2f);           // feet stay on the floor while pushing off, lift with the body in flight
            heel += up * lift * (1f - onGround); tb += up * lift * (1f - onGround);
            if (onGround > 0.5f) { tb.y = Mathf.Max(tb.y, R.position.y + 0.02f); }
            // knee pole = the rest knee's side (out + forward)
            Vector3 poleM = mPos[sh] - (mPos[th] + heel0) * 0.5f;
            Vector3 pole = R.rotation * Vector3.Lerp(poleM, new Vector3(poleM.x, poleM.y + 0.2f, poleM.z - 0.1f), ext);
            Vector3 knee = FrogAsset.SolveJoint(hip, heel, legLen[0], legLen[1], pole);
            FrogAsset.Aim(rig.b[th], knee, Up(th));
            FrogAsset.Aim(rig.b[sh], heel, Up(sh));
            FrogAsset.Aim(rig.b[ta], tb, Up(ta));
            // foot: flat forward on the floor (rest) -> pointing back when extended
            Vector3 fd0 = R.rotation * (mRot[ft] * Vector3.forward), fdX = -fwd * 0.92f - up * 0.35f;
            Vector3 fd = Vector3.Slerp(fd0, fdX.normalized, ext);
            rig.b[ft].rotation = Quaternion.LookRotation(fd, Vector3.Slerp(Up(ft), fwd, ext * 0.8f));
            foreach (int ti in toes[k]) if (ti >= 0) rig.b[ti].localRotation = rig.rest[ti] * Quaternion.Euler(ext * 10f - air * 6f, 0f, 0f);
        }
        // ---- front arms: planted on the floor (rest), reach forward to land, or follow the first-person hand targets
        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? 1f : -1f;
            int ua = arm[k * 3], fa = arm[k * 3 + 1], pa = arm[k * 3 + 2];
            Vector3 shp = rig.b[ua].position;
            Vector3 wr0 = mPos[pa];
            Vector3 wrM = wr0 + new Vector3(0f, air * 0.10f + reach * 0.02f, reach * 0.14f - ext * 0.06f) + new Vector3(0f, rs * 0.08f, 0f);
            Vector3 wrist = W(wrM) + up * lift * S(air * 1.6f);
            if (!sitting && air < 0.05f) wrist.y = Mathf.Max(R.position.y + (wr0.y), wrist.y);
            float w = Mathf.Clamp01(handW[k]);
            wrist = Vector3.Lerp(wrist, handT[k], w);
            Vector3 poleM = mPos[fa] - (mPos[ua] + wr0) * 0.5f;
            Vector3 elbow = FrogAsset.SolveJoint(shp, wrist, armLen[0], armLen[1], R.rotation * poleM);
            FrogAsset.Aim(rig.b[ua], elbow, Up(ua));
            FrogAsset.Aim(rig.b[fa], wrist, Up(fa));
            // palm flat on the floor (rest orientation) unless a hand target drives it
            Quaternion flat = R.rotation * mRot[pa];
            if (air > 0.05f) flat = flat * Quaternion.Euler(-air * 25f, 0f, 0f);
            rig.b[pa].rotation = w > 0.01f ? Quaternion.Slerp(flat, handR[k], w) : flat;
            if (w > 0.01f) FrogAsset.Curl(rig, fingers[k], curl[k], t + k, 0.02f);
            else for (int i = 0; i < 12; i++) { int fi = fingers[k][i]; if (fi >= 0) rig.b[fi].localRotation = rig.rest[fi] * Quaternion.Euler(-air * 8f * (i % 3 == 0 ? 1f : 0.5f), 0f, 0f); }
        }
        // ---- eyes: blink = retract into the sockets for a moment every few seconds
        float bl = Mathf.Repeat(t, 4.3f) < 0.16f ? 1f : 0f;
        if (eyeR != null)
        {
            eyeR.localScale = Vector3.one * EyeRad * (1f - 0.12f * bl); eyeL.localScale = eyeR.localScale;
            eyeR.localPosition = eyeRestR + rig.b[head].InverseTransformVector(-up * 0.035f * bl);
            eyeL.localPosition = eyeRestL + rig.b[head].InverseTransformVector(-up * 0.035f * bl);
        }
    }
}
