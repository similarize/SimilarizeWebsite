using UnityEngine;

// ffu23 REAL ROOM first-person frog hands (rewritten). The hand set of frog.bin (RealRoomFrog.cs): a muscular forelimb,
// broad palm and four webbed fingers with round adhesive toe pads, wet olive-green skin (FF/RRSkin, dorsal / belly
// mask baked per vertex). Each hand is driven by a two-bone IK chain from a virtual shoulder below the camera, so the
// forearm swings naturally as the wrist goes where it is sent; the wrist then turns the palm (clamped) and the fingers
// curl per finger. Motion: idle breathing, walk bob (figure eight), lag behind the look (mouse / stick), anticipation
// towards whatever is in reach, poke (index finger on the light switch), grab (reach, close round the duck / plush,
// carry it), wind-up + throw, palms on the armrests when sitting, and the intro "look at my hands".
public static class Hands
{
    static FrogAsset.Rig R, L;
    static readonly int[][] fb = new int[2][];
    static Material matR, matL;
    static Transform eye;
    public static float introPose, walk;
    public static Vector3 WaveOrigin = Vector3.zero;
    static bool loggedView, visible = true;

    // ---- inputs from RealRoom (world space) ----
    public static Vector3 nearTarget; public static float nearW;        // anticipation: something reachable in view
    public static bool sitting; public static Vector3 armrestR, armrestL; public static bool armrestOk;
    public static float charge;                                          // throw wind-up 0..1
    public static bool holding; public static float objR = 0.06f;
    public static float crouch;                                          // eye lowered (m) for reaching low things

    // ---- actions ----
    enum Act { None, Poke, Grab, Throw }
    static Act act = Act.None; static float actT, actDur;
    static Vector3 actP; static float actR;
    public static bool PokeContact, GrabAttach, ThrowRelease;
    public static bool demoHold;   // probe shot: freeze the poke with the finger on the switch
    public static bool Busy { get { return act != Act.None; } }
    public static float ActProgress { get { return act == Act.None ? 0f : Mathf.Clamp01(actT / actDur); } }

    // hand-space reference points (metres, frog.bin hand scale 1.22): index tip when pointing, palm centre (underside)
    static readonly Vector3 IndexTip = new Vector3(-0.026f, -0.010f, 0.168f);
    static readonly Vector3 PalmC = new Vector3(0.0f, -0.024f, 0.058f);
    const float UpperLen = 0.30f, ForeLen = 0.317f;
    static readonly Vector3 ShoulderE = new Vector3(0.19f, -0.29f, -0.06f);   // eye space (right; left mirrored)

    // current smoothed state per hand: wrist (world), hand rotation (world), finger curls
    static readonly Vector3[] wrist = new Vector3[2];
    static readonly Quaternion[] hrot = new Quaternion[2];
    static readonly float[][] curl = { new float[4], new float[4] };
    static bool init;
    static Vector2 lag, lagV;
    static float lastYaw, lastPitch;

    public static Vector3 HoldPoint { get { return R != null ? R.b[pal(0)].TransformPoint(PalmC + new Vector3(0f, -objR * 0.95f - 0.006f, 0.012f)) : Vector3.zero; } }
    // the toy stays upright and turned toward the froggy (a palm-relative rotation laid it on its side)
    public static Quaternion HoldRot { get { if (eye == null) return Quaternion.identity; Vector3 f = Vector3.ProjectOnPlane(-eye.forward, Vector3.up); if (f.sqrMagnitude < 1e-4f) f = -eye.up; return Quaternion.LookRotation(f.normalized, Vector3.up) * Quaternion.Euler(-8f, -28f, 0f); } }
    public static Vector3 Wrist(int k) { return wrist[k]; }
    public static Quaternion HandRot(int k) { return hrot[k]; }
    public static float[] Curls(int k) { return curl[k]; }
    static int palR, palL;
    static int pal(int k) { return k == 0 ? palR : palL; }

    public static void Build(FrogAsset.Set hand, Transform eyeT, Texture2D skin, Color frogColor)
    {
        eye = eyeT;
        var sh = Shader.Find("FF/RRSkin");
        if (!sh.isSupported) Debug.LogWarning("RealRoom: FF/RRSkin not supported here");
        Color toon = frogColor.linear;
        for (int k = 0; k < 2; k++)
        {
            var m = new Material(sh) { name = k == 0 ? "FrogHandR" : "FrogHandL" };
            if (skin != null) m.SetTexture("_SkinTex", skin);
            m.SetColor("_Toon", new Color(toon.r, toon.g, toon.b, 1f));
            m.SetFloat("_TexScale", 15f);
            var rig = FrogAsset.Build(hand, eyeT, m.name, m, k == 1, RealRoom.Layer);
            fb[k] = new int[12];
            for (int i = 0; i < 4; i++) for (int j = 0; j < 3; j++) fb[k][i * 3 + j] = rig.Find("f" + i + "_" + j);
            if (k == 0) { R = rig; matR = m; palR = rig.Find("palm"); } else { L = rig; matL = m; palL = rig.Find("palm"); }
        }
        Debug.Log("RealRoom: frog hands " + hand.bone.Length + " bones, " + hand.pos.Length + " verts");
    }

    public static void SetVisible(bool on)
    {
        visible = on;
        if (R != null) { R.smr.enabled = on; L.smr.enabled = on; }
    }

    public static void SetMorph(float k)
    {
        if (matR != null) { matR.SetFloat("_Morph", k); matL.SetFloat("_Morph", k); }
        if (R != null) WaveOrigin = (R.root.position + L.root.position) * 0.5f;
    }

    public static void SetLights(Vector3 lamp, Vector3 tv)
    {
        if (matR == null) return;
        matR.SetVector("_LampPos", lamp); matL.SetVector("_LampPos", lamp);
        matR.SetVector("_TvPos", tv); matL.SetVector("_TvPos", tv);
    }

    public static void ResetPose() { init = false; act = Act.None; holding = false; charge = 0f; }

    public static bool Poke(Vector3 p) { if (act != Act.None) return false; act = Act.Poke; actT = 0f; actDur = 0.85f; actP = p; return true; }
    public static bool Grab(Vector3 c, float r) { if (act != Act.None) return false; act = Act.Grab; actT = 0f; actDur = 0.95f; actP = c; actR = r; return true; }
    public static void Throw() { act = Act.Throw; actT = 0f; actDur = 0.55f; }
    public static void Cancel() { act = Act.None; }

    static Vector3 E(Vector3 p) { return eye.TransformPoint(p); }
    static Vector3 ED(Vector3 d) { return eye.TransformDirection(d); }
    static Quaternion ER(Vector3 fwd, Vector3 up) { return Quaternion.LookRotation(ED(fwd), ED(up)); }
    static Vector3 Mir(Vector3 v) { return new Vector3(-v.x, v.y, v.z); }

    // eye-space pose -> world wrist + rotation (left hand mirrored)
    static void PoseE(int k, Vector3 p, Vector3 fwd, Vector3 up, out Vector3 w, out Quaternion q)
    {
        if (k == 1) { p = Mir(p); fwd = Mir(fwd); up = Mir(up); }
        w = E(p); q = ER(fwd, up);
    }

    public static void Tick(float dt, bool play)
    {
        if (R == null) return;
        float t = Time.time;
        PokeContact = GrabAttach = ThrowRelease = false;
        // look lag: the hands trail behind fast turns and catch up (spring)
        float yaw = eye.eulerAngles.y, pitch = eye.eulerAngles.x;
        if (!init) { lastYaw = yaw; lastPitch = pitch; }
        float dy = Mathf.DeltaAngle(lastYaw, yaw), dp = Mathf.DeltaAngle(lastPitch, pitch);
        lastYaw = yaw; lastPitch = pitch;
        Vector2 kick = new Vector2(-dy, dp) * 0.0011f;
        lagV += kick / Mathf.Max(dt, 1e-3f) * 0.016f;
        lagV += (-lag * 90f - lagV * 13f) * dt;
        lag += lagV * dt;
        lag = Vector2.ClampMagnitude(lag, 0.06f);
        float bobX = Mathf.Sin(walk) * 0.011f, bobY = Mathf.Abs(Mathf.Cos(walk)) * 0.012f - 0.006f;
        float breath = Mathf.Sin(t * 1.9f) * 0.0035f;

        for (int k = 0; k < 2; k++)
        {
            float sx = k == 0 ? 1f : -1f;
            Vector3 tw; Quaternion tq; float[] tc = new float[4];
            // idle: low in the lower corners, backs of the hands up, fingers relaxed + slightly spread
            // (ffu23 framing, checked with work/rr/frog/simfp.py: backs of the hands + spread webbed fingers in the corners)
            Vector3 ip = new Vector3(0.16f, -0.155f, 0.31f) + new Vector3(bobX * sx + lag.x, bobY + breath + lag.y, 0f);
            PoseE(k, ip, new Vector3(-0.6f, 0.55f, 0.6f), new Vector3(0.35f, 0.55f, -0.75f), out tw, out tq);
            for (int i = 0; i < 4; i++) tc[i] = 0.08f + i * 0.025f;   // relaxed (the rest pose is already slightly curled)
            // anticipation: the right hand drifts toward something reachable in view
            if (k == 0 && nearW > 0.001f && act == Act.None && !holding && !sitting)
            {
                Vector3 toward = Vector3.Lerp(tw, nearTarget - ED(new Vector3(0.02f, 0.02f, 0.2f)), 0.22f * nearW);
                tw = Vector3.Lerp(tw, toward, nearW);
                for (int i = 0; i < 4; i++) tc[i] = Mathf.Lerp(tc[i], -0.05f, nearW * 0.6f);   // fingers open up
            }
            if (sitting)
            {
                Vector3 ar = k == 0 ? armrestR : armrestL;
                if (armrestOk)
                {
                    Vector3 f = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
                    Quaternion q = Quaternion.LookRotation(Vector3.Lerp(f, eye.right * -sx, 0.15f).normalized, Vector3.up);
                    tw = ar + Vector3.up * 0.032f - q * new Vector3(0f, 0f, 0.07f);
                    tq = q;
                    for (int i = 0; i < 4; i++) tc[i] = 0.38f + 0.05f * i;   // fingers draped over the front of the armrest
                }
                else PoseE(k, new Vector3(0.24f, -0.33f, 0.24f), new Vector3(-0.1f, -0.4f, 1f), new Vector3(0.2f, 1f, 0.2f), out tw, out tq);
            }
            if (holding && k == 0)
            {
                // carry: lower right, palm down round the object; wind-up pulls it back over the shoulder
                // palm turned in toward the middle, the toy held against it (lower right of the view)
                PoseE(0, new Vector3(0.21f + lag.x, -0.20f + bobY + lag.y, 0.40f), new Vector3(-0.25f, 0.45f, 0.85f), new Vector3(0.95f, 0.15f, 0.1f), out tw, out tq);
                for (int i = 0; i < 4; i++) tc[i] = objR > 0.09f ? 0.42f : 0.58f;   // round the plush / the duck
                if (charge > 0f)
                {
                    Vector3 wp; Quaternion wq;
                    PoseE(0, new Vector3(0.26f, -0.02f, 0.14f), new Vector3(-0.2f, 0.85f, 0.4f), new Vector3(0.4f, 0.1f, -1f), out wp, out wq);
                    float c = Mathf.SmoothStep(0f, 1f, charge);
                    tw = Vector3.Lerp(tw, wp, c); tq = Quaternion.Slerp(tq, wq, c);
                }
            }
            if (introPose > 0f)
            {
                // "look at my hands": raised in front of the chest, backs up, fingers slowly flexing
                Vector3 p; Quaternion q;
                PoseE(k, new Vector3(0.085f, -0.11f, 0.34f), new Vector3(-0.25f, 0.65f, 0.7f), new Vector3(0.3f, 0.5f, -0.8f), out p, out q);
                tw = Vector3.Lerp(tw, p, introPose); tq = Quaternion.Slerp(tq, q, introPose);
                float fl = 0.02f + 0.30f * (0.5f + 0.5f * Mathf.Sin(t * 2.2f + k));
                for (int i = 0; i < 4; i++) tc[i] = Mathf.Lerp(tc[i], fl + 0.05f * i, introPose);
            }
            // actions (right hand)
            if (k == 0 && act != Act.None)
            {
                actT += dt;
                if (demoHold && act == Act.Poke && actT > actDur * 0.47f) actT = actDur * 0.47f;
                float u = Mathf.Clamp01(actT / actDur);
                Vector3 sh = E(ShoulderE);
                if (act == Act.Poke)
                {
                    // approach 0-.42, press .42-.58 (contact at .45), return .58-1
                    Vector3 dir = (actP - sh).normalized;
                    Quaternion q = Quaternion.LookRotation(Vector3.Lerp(dir, eye.forward, 0.3f).normalized, Vector3.Lerp(Vector3.up, -eye.right, 0.25f));
                    Vector3 touch = actP - q * (IndexTip + new Vector3(0f, 0f, 0.004f));
                    Vector3 press = touch + q * new Vector3(0f, 0f, 0.012f);
                    float a = Smooth((u - 0.0f) / 0.42f), b = Smooth((u - 0.42f) / 0.08f) - Smooth((u - 0.5f) / 0.08f), back = Smooth((u - 0.58f) / 0.42f);
                    Vector3 reach = Vector3.Lerp(touch, press, b);
                    float w = a * (1f - back);
                    tw = Vector3.Lerp(tw, reach, w); tq = Quaternion.Slerp(tq, q, w);
                    // index straight, the rest curled in
                    tc[0] = Mathf.Lerp(tc[0], 0.95f, w); tc[1] = Mathf.Lerp(tc[1], -0.12f, w); tc[2] = Mathf.Lerp(tc[2], 0.95f, w); tc[3] = Mathf.Lerp(tc[3], 0.95f, w);
                    if (actT - dt < actDur * 0.45f && actT >= actDur * 0.45f) PokeContact = true;
                }
                else if (act == Act.Grab)
                {
                    // reach over the object (fingers open), lower round it and close, lift it back into the carry pose
                    Vector3 f = Vector3.ProjectOnPlane(actP - sh, Vector3.up); if (f.sqrMagnitude < 1e-4f) f = eye.forward; f.Normalize();
                    Quaternion q = Quaternion.LookRotation((f * 0.75f - Vector3.up * 0.65f).normalized, (f * 0.6f + Vector3.up * 0.8f).normalized);
                    Vector3 over = actP + Vector3.up * (actR + 0.09f) - q * PalmC;
                    Vector3 on = actP + Vector3.up * (actR * 0.85f + 0.012f) - q * PalmC;
                    float a = Smooth(u / 0.40f), down = Smooth((u - 0.38f) / 0.16f), close = Smooth((u - 0.48f) / 0.14f), lift = Smooth((u - 0.62f) / 0.38f);
                    Vector3 grabP = Vector3.Lerp(over, on, down);
                    float w = a * (1f - lift);
                    tw = Vector3.Lerp(tw, grabP, w); tq = Quaternion.Slerp(tq, q, w);
                    for (int i = 0; i < 4; i++) tc[i] = Mathf.Lerp(Mathf.Lerp(tc[i], -0.08f, a), actR > 0.09f ? 0.42f : 0.58f, close);
                    if (actT - dt < actDur * 0.6f && actT >= actDur * 0.6f) GrabAttach = true;
                }
                else if (act == Act.Throw)
                {
                    // fast over-arm swing from the wind-up to arm's length, release at .35, follow-through, back
                    Vector3 p0, p1; Quaternion q0, q1;
                    PoseE(0, new Vector3(0.26f, -0.02f, 0.14f), new Vector3(-0.2f, 0.85f, 0.4f), new Vector3(0.4f, 0.1f, -1f), out p0, out q0);
                    PoseE(0, new Vector3(0.05f, -0.10f, 0.60f), new Vector3(-0.1f, -0.2f, 1f), new Vector3(0.1f, 1f, 0.2f), out p1, out q1);
                    float s = Smooth(u / 0.38f), back = Smooth((u - 0.5f) / 0.5f);
                    Vector3 arc = Vector3.Lerp(p0, p1, s) + ED(Vector3.up) * Mathf.Sin(s * Mathf.PI) * 0.06f;
                    float w = 1f - back;
                    tw = Vector3.Lerp(tw, arc, w); tq = Quaternion.Slerp(tq, Quaternion.Slerp(q0, q1, s), w);
                    for (int i = 0; i < 4; i++) tc[i] = Mathf.Lerp(tc[i], u < 0.35f ? 0.5f : -0.06f, w);
                    if (actT - dt < actDur * 0.35f && actT >= actDur * 0.35f) ThrowRelease = true;
                }
                if (actT >= actDur) act = Act.None;
            }
            // smoothing (fast enough to keep IK reaches crisp, slow enough to hide pose switches)
            if (!init) { wrist[k] = tw; hrot[k] = tq; for (int i = 0; i < 4; i++) curl[k][i] = tc[i]; }
            float s1 = 1f - Mathf.Exp(-dt * (act != Act.None && k == 0 ? 22f : 12f));
            wrist[k] = Vector3.Lerp(wrist[k], tw, s1);
            hrot[k] = Quaternion.Slerp(hrot[k], tq, s1);
            for (int i = 0; i < 4; i++) curl[k][i] = Mathf.Lerp(curl[k][i], tc[i], 1f - Mathf.Exp(-dt * 16f));
            ApplyIK(k == 0 ? R : L, k, sx);
        }
        init = true;
        if (play && !loggedView)
        {
            loggedView = true;
            var cam = eye != null ? eye.GetComponentInChildren<Camera>() : null;
            if (cam != null) Debug.Log("RealRoom: hands at viewport R " + cam.WorldToViewportPoint(R.root.position).ToString("F2") + " L " + cam.WorldToViewportPoint(L.root.position).ToString("F2"));
        }
        WaveOrigin = (R.root.position + L.root.position) * 0.5f + eye.forward * 0.12f;
    }

    // two-bone IK from the virtual shoulder: the forearm (static in the hand root) points elbow -> wrist, the palm bone
    // turns toward the wanted hand rotation within wrist limits, then the fingers curl
    static void ApplyIK(FrogAsset.Rig r, int k, float sx)
    {
        Vector3 sh = E(new Vector3(ShoulderE.x * sx, ShoulderE.y, ShoulderE.z));
        Vector3 pole = ED(new Vector3(0.75f * sx, -1f, -0.35f));
        Vector3 elbow = FrogAsset.SolveJoint(sh, wrist[k], UpperLen, ForeLen, pole);
        Vector3 fdir = (wrist[k] - elbow).normalized;
        Vector3 up = hrot[k] * Vector3.up;
        up -= fdir * Vector3.Dot(up, fdir);
        if (up.sqrMagnitude < 1e-6f) up = ED(Vector3.up);
        // the root is the wrist frame: origin at the wrist, +z along the forearm
        r.root.rotation = Quaternion.LookRotation(fdir, up.normalized);
        r.root.position = wrist[k];
        int p = k == 0 ? palR : palL;
        if (p >= 0)
        {
            Quaternion want = hrot[k];
            Quaternion restW = r.b[p].parent.rotation * r.rest[p];
            Quaternion rel = Quaternion.Inverse(restW) * want;
            float ang; Vector3 ax; rel.ToAngleAxis(out ang, out ax);
            if (ang > 180f) ang -= 360f;
            ang = Mathf.Clamp(ang, -82f, 82f);
            r.b[p].localRotation = r.rest[p] * Quaternion.AngleAxis(ang, ax);
        }
        FrogAsset.Curl(r, fb[k], curl[k], Time.time + k * 0.7f, 0.025f);
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
