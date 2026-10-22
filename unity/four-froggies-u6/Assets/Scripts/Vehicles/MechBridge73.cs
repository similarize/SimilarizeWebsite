using System.Collections.Generic;
using UnityEngine;

// ffu73 (Bill 2026-10-22): James's TRILLION-story mech gets a command BRIDGE for its pilot and riders instead of outside
// cockpit bubbles (a bubble makes no sense at that scale). An enclosed deck inside the head: a wide forward viewscreen
// window (flush with the face, a dark glossy visor from outside, clear from inside), the captain's chair, helm + ops consoles
// with crew seats, two side stations, a rear arc console, a lift door, a little spinning Earth hologram, ceiling light strips.
// Everything froggy-sized (a 0.9 m chair inside a 160 m hull). The froggy pilot sits in the captain's chair (visible), AI
// froggy riders take the crew seats, a robot pilot sits in the chair too (no glass canopy on the head), and in space the
// space-scale copy carries the same bridge (riders inside, no spacesuits on the shoulders). Bridge view: R3 / key 0 /
// double-tap the look side toggles chase <-> bridge for anyone in or aboard it (look around with the right stick). The
// player's own camera is reused (no new camera); while any camera is inside the room the head shell is hidden for that
// camera only and its near plane is pulled in, so the outside shows through the viewscreen.
// 10 / 100 / 1000-story mechs are untouched (bubbles as before). Knob &ff73old=1 = the old trillion-story (no bridge).
// Probe ?ffdemo=1&ffshot=ffu73 (GameFfu73.cs). Logs "FF73 ...".
public static class F73
{
    static int old = -1;
    public static bool Old { get { if (old < 0) old = Ffu33.Url("ff73old") == "1" ? 1 : 0; return old == 1; } }
}

public class Bridge73 : MonoBehaviour
{
    public static readonly List<Bridge73> All = new List<Bridge73>();
    public float k = 1f;                 // world metres per bridge metre (1 = the ranch trillion-story mech)
    public Transform room, captain, robotSeat, eye, holo;
    public readonly Transform[] crew = new Transform[4];
    public Renderer visorR;              // the outer visor (from outside only; hidden for a camera inside)
    public Transform hideRoot;           // the head whose shell is hidden for a camera inside the room
    public const float RobotH = 1.4f;    // a robot pilot drawn this tall (bridge metres) in the chair
    public float RobotWorldH { get { return RobotH * k; } }
    // room box (bridge metres): x +-6, y 0..4.2, z -6..3 (the viewscreen wall at +3)
    const float HW = 6f, HT = 4.2f, ZB = -6f, ZF = 3f;
    static readonly Vector3 EyeL = new Vector3(1.35f, 2.45f, -4.1f), ScreenL = new Vector3(-0.35f, 1.65f, 3f);   // fix 1: over the captain's right shoulder (the chair back hid the view)

    public bool Inside(Vector3 world)
    {
        if (room == null) return false;
        Vector3 l = room.InverseTransformPoint(world);
        return l.x > -HW && l.x < HW && l.y > 0f && l.y < HT && l.z > ZB && l.z < ZF + 0.02f;
    }
    public Vector3 Centre { get { return room != null ? room.TransformPoint(new Vector3(0f, 1.5f, -1.5f)) : transform.position; } }

    void Awake() { All.Add(this); Hook(); }
    void OnDestroy() { All.Remove(this); }
    void Update() { if (holo != null) holo.Rotate(0f, Time.deltaTime * 20f, 0f, Space.Self); }

    // ---------- build ----------
    // parent + local pose of the room origin (floor centre); k = scale; hide = head shell hidden for cameras inside
    public static Bridge73 Build(Transform parent, Vector3 localPos, Quaternion localRot, float k, int owner, Transform hide, string plate)
    {
        var go = new GameObject("Bridge73");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos; go.transform.localRotation = localRot;
        float ps = parent.lossyScale.x > 1e-4f ? parent.lossyScale.x : 1f;
        go.transform.localScale = Vector3.one * (k / ps);
        var b = go.AddComponent<Bridge73>();
        b.k = k; b.room = go.transform; b.hideRoot = hide;
        Transform r = go.transform;
        Color oc = Froggies.Color(owner);
        Material deck = Mats.PBR(new Color(0.2f, 0.21f, 0.23f), 0.45f, 0.75f);
        Material carpet = Mats.Lit(Color.Lerp(new Color(0.12f, 0.16f, 0.2f), oc, 0.25f));
        Material wall = Mats.PBR(new Color(0.78f, 0.79f, 0.8f), 0.35f, 0.1f);
        Material wallDark = Mats.PBR(new Color(0.36f, 0.38f, 0.41f), 0.4f, 0.4f);
        Material ceil = Mats.PBR(new Color(0.85f, 0.86f, 0.87f), 0.2f, 0.05f);
        Material steel = Mats.Steel(new Color(0.5f, 0.52f, 0.55f));
        Material darkSteel = Mats.Steel(new Color(0.16f, 0.17f, 0.19f));
        Material leather = Mats.PBR(Color.Lerp(oc, new Color(0.15f, 0.1f, 0.08f), 0.55f), 0.55f, 0f);
        Material seatM = Mats.PBR(new Color(0.22f, 0.26f, 0.32f), 0.45f, 0f);
        Material screen = Mats.PBR(new Color(0.03f, 0.04f, 0.06f), 0.9f, 0.2f);
        Material lightW = Mats.Unlit(new Color(1f, 0.96f, 0.86f));
        Material accent = Mats.Unlit(Color.Lerp(oc, Color.white, 0.35f));
        Material cyan = Mats.Unlit(new Color(0.45f, 0.9f, 1f)), amber = Mats.Unlit(new Color(1f, 0.72f, 0.25f)), red = Mats.Unlit(new Color(1f, 0.35f, 0.3f)), green = Mats.Unlit(new Color(0.45f, 1f, 0.55f));
        Material visor = Mats.PBR(new Color(0.03f, 0.05f, 0.08f), 0.97f, 0.6f);
        // shell: deck, ceiling, side + back walls, the front frame round the viewscreen
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, -0.1f, (ZB + ZF) * 0.5f), new Vector3(HW * 2f, 0.2f, ZF - ZB), deck);
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, 0.01f, -1.9f), new Vector3(5.2f, 0.01f, 4.2f), carpet);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, HT + 0.1f, (ZB + ZF) * 0.5f), new Vector3(HW * 2f, 0.2f, ZF - ZB), ceil);
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW + 0.1f), HT * 0.5f, (ZB + ZF) * 0.5f), new Vector3(0.2f, HT + 0.4f, ZF - ZB), wall);
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.02f), 1.1f, (ZB + ZF) * 0.5f), new Vector3(0.04f, 0.05f, ZF - ZB - 0.4f), accent);      // glow stripe
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.02f), 0.06f, (ZB + ZF) * 0.5f), new Vector3(0.05f, 0.12f, ZF - ZB), wallDark);          // kick plate
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * 2.2f, HT - 0.01f, -1.5f), new Vector3(0.5f, 0.02f, 7.6f), lightW);                             // ceiling light strips
            // wall station (a screen with readouts) + its seat, facing the wall
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.06f), 2.0f, -1.6f), new Vector3(0.06f, 1.2f, 2.4f), screen);
            for (int j = 0; j < 4; j++) Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.1f), 1.65f + j * 0.22f, -1.6f + (j % 2 == 0 ? -0.3f : 0.25f)), new Vector3(0.02f, 0.06f, 0.9f + j * 0.2f), j == 1 ? amber : cyan);
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.45f), 0.45f, -1.6f), new Vector3(0.7f, 0.9f, 1.8f), darkSteel);                       // station desk
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.45f), 0.91f, -1.6f), new Vector3(0.72f, 0.03f, 1.82f), steel);
            b.crew[s < 0 ? 2 : 3] = Chair(r, new Vector3(s * (HW - 1.5f), 0f, -1.6f), s < 0 ? -90f : 90f, 0.62f, seatM, steel, false, accent);
        }
        // back wall + lift door + name plate + lily-pad emblem
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, HT * 0.5f, ZB - 0.1f), new Vector3(HW * 2f + 0.4f, HT + 0.4f, 0.2f), wall);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 1.15f, ZB + 0.02f), new Vector3(1.9f, 2.3f, 0.04f), wallDark);
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * 0.47f, 1.1f, ZB + 0.05f), new Vector3(0.9f, 2.15f, 0.03f), steel);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 2.36f, ZB + 0.05f), new Vector3(1.9f, 0.06f, 0.04f), accent);
        var pad = Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, 3.25f, ZB + 0.05f), new Vector3(0.8f, 0.02f, 0.8f), new Vector3(90f, 0f, 0f), Mats.PBR(new Color(0.25f, 0.6f, 0.3f), 0.5f, 0f));
        pad.name = "LilyPad";
        if (!string.IsNullOrEmpty(plate) && UIK.WorldFont != null)
        {
            var tg = new GameObject("Plate73"); tg.transform.SetParent(r, false);
            tg.transform.localPosition = new Vector3(0f, 2.7f, ZB + 0.03f);
            var tm = tg.AddComponent<TextMesh>();
            tm.text = plate; tm.font = UIK.WorldFont; tm.fontSize = 96; tm.characterSize = 0.012f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(0.15f, 0.17f, 0.2f);
            tg.GetComponent<MeshRenderer>().sharedMaterial = StoryMech.TextMat;
        }
        // rear arc console (also the rail behind the captain)
        for (int j = -2; j <= 2; j++)
        {
            float a = j * 22f * Mathf.Deg2Rad, R = 3.6f;
            Vector3 p = new Vector3(Mathf.Sin(a) * R, 0f, -1.9f - Mathf.Cos(a) * R);
            var seg = Mats.Node(r, "Arc", p); seg.localRotation = Quaternion.Euler(0f, j * 22f, 0f);
            Mats.Prim(PrimitiveType.Cube, seg, new Vector3(0f, 0.5f, 0f), new Vector3(1.35f, 1.0f, 0.5f), darkSteel);
            Mats.Prim(PrimitiveType.Cube, seg, new Vector3(0f, 1.02f, 0.02f), new Vector3(1.38f, 0.05f, 0.56f), steel);
            Mats.Prim(PrimitiveType.Cube, seg, new Vector3(0f, 0.75f, 0.255f), new Vector3(1.1f, 0.32f, 0.01f), screen);
            Mats.Prim(PrimitiveType.Cube, seg, new Vector3(-0.25f + (j & 1) * 0.3f, 0.78f, 0.262f), new Vector3(0.4f, 0.05f, 0.005f), j == 0 ? red : green);
        }
        // helm + ops consoles with their crew seats, the hologram pedestal between them
        for (int s = -1; s <= 1; s += 2)
        {
            var con = Mats.Node(r, s < 0 ? "Helm" : "Ops", new Vector3(s * 1.7f, 0f, 0.9f));
            Mats.Prim(PrimitiveType.Cube, con, new Vector3(0f, 0.42f, 0f), new Vector3(1.7f, 0.84f, 0.55f), darkSteel);
            Mats.Prim(PrimitiveType.Cube, con, new Vector3(0f, 0.93f, -0.05f), new Vector3(1.74f, 0.05f, 0.75f), new Vector3(-22f, 0f, 0f), steel);
            Mats.Prim(PrimitiveType.Cube, con, new Vector3(0f, 0.96f, -0.05f), new Vector3(1.4f, 0.02f, 0.55f), new Vector3(-22f, 0f, 0f), screen);
            for (int j = 0; j < 6; j++) Mats.Prim(PrimitiveType.Cube, con, new Vector3(-0.55f + j * 0.22f, 0.99f + (j % 2) * 0.03f, -0.1f + (j % 2) * 0.08f), new Vector3(0.14f, 0.015f, 0.08f), new Vector3(-22f, 0f, 0f), j % 3 == 0 ? amber : j % 3 == 1 ? cyan : green);
            b.crew[s < 0 ? 0 : 1] = Chair(r, new Vector3(s * 1.7f, 0f, 0.05f), 0f, 0.62f, seatM, steel, false, accent);
        }
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, 0.35f, 1.0f), new Vector3(0.5f, 0.35f, 0.5f), darkSteel);
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, 0.71f, 1.0f), new Vector3(0.56f, 0.02f, 0.56f), cyan);
        b.holo = Mats.Node(r, "Holo", new Vector3(0f, 1.15f, 1.0f));
        var hm = new Material(Mats.Fx); hm.color = new Color(0.4f, 0.8f, 1f, 0.45f);
        Mats.Prim(PrimitiveType.Sphere, b.holo, Vector3.zero, Vector3.one * 0.62f, hm);
        Mats.Prim(PrimitiveType.Sphere, b.holo, new Vector3(0.12f, 0.1f, 0.25f), new Vector3(0.22f, 0.16f, 0.14f), Mats.Unlit(new Color(0.45f, 1f, 0.55f)));   // a green "land"
        Mats.Prim(PrimitiveType.Cylinder, b.holo, Vector3.zero, new Vector3(0.95f, 0.005f, 0.95f), new Vector3(12f, 0f, 0f), hm);                               // orbit ring
        // the captain's chair (raised a step, armrest pads) and the robot seat on it
        Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(0f, 0.08f, -1.9f), new Vector3(1.9f, 0.08f, 1.9f), wallDark);
        b.captain = Chair(r, new Vector3(0f, 0.16f, -1.9f), 0f, 0.62f, leather, steel, true, accent);
        b.robotSeat = Mats.Node(b.captain, "RobotSeat", new Vector3(0f, 0.4f * RobotH, 0.05f));
        // front wall: frame round the viewscreen opening, mullions, HUD corner brackets, the outer visor (seen from outside only)
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 0.5f, ZF + 0.08f), new Vector3(HW * 2f + 0.4f, 1.0f, 0.16f), wall);                      // sill
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 3.9f, ZF + 0.08f), new Vector3(HW * 2f + 0.4f, 0.6f, 0.16f), wall);                      // header
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 1.02f, ZF - 0.1f), new Vector3(HW * 2f - 0.4f, 0.06f, 0.35f), steel);                   // sill ledge
        for (int s = -1; s <= 1; s += 2) Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * (HW - 0.3f), 2.3f, ZF + 0.08f), new Vector3(0.6f, 2.6f, 0.16f), wall);
        for (int j = -2; j <= 2; j++) if (j != 0) Mats.Prim(PrimitiveType.Cube, r, new Vector3(j * 2.2f, 2.3f, ZF + 0.06f), new Vector3(0.07f, 2.6f, 0.07f), darkSteel);
        for (int s = -1; s <= 1; s += 2)
            for (int v = -1; v <= 1; v += 2)
            {
                Vector3 c = new Vector3(s * (HW - 0.75f), 2.3f + v * 1.15f, ZF - 0.01f);
                Mats.Prim(PrimitiveType.Cube, r, c + new Vector3(-s * 0.2f, 0f, 0f), new Vector3(0.4f, 0.03f, 0.01f), cyan);
                Mats.Prim(PrimitiveType.Cube, r, c + new Vector3(0f, -v * 0.2f, 0f), new Vector3(0.03f, 0.4f, 0.01f), cyan);
            }
        var vq = Mats.Prim(PrimitiveType.Quad, r, new Vector3(0f, 2.3f, ZF + 0.17f), new Vector3(HW * 2f - 1.2f, 2.6f, 1f), new Vector3(0f, 180f, 0f), visor);
        vq.name = "Visor73"; b.visorR = vq.GetComponent<Renderer>();
        b.eye = Mats.Node(r, "Eye73", EyeL);
        b.eye.localRotation = Quaternion.LookRotation(ScreenL - EyeL);
        foreach (var rr in go.GetComponentsInChildren<Renderer>(true)) { rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rr.receiveShadows = false; }
        if (!Look.Mobile)
        {
            var lg = new GameObject("BridgeLight"); lg.transform.SetParent(r, false); lg.transform.localPosition = new Vector3(0f, 3.6f, -1f);
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.95f, 0.88f); l.range = 11f * k; l.intensity = 1.1f; l.shadows = LightShadows.None;
        }
        Debug.Log("FF73 bridge built in " + (parent != null ? parent.name : "-") + " scale " + k.ToString("0.###") + " (" + go.GetComponentsInChildren<Renderer>(true).Length + " parts)");
        return b;
    }

    // froggy-sized chair facing local yaw; returns the seat node (cushion top, where a seated froggy's origin goes)
    static Transform Chair(Transform r, Vector3 at, float yawDeg, float seatH, Material cushion, Material frame, bool captain, Material accent)
    {
        var c = Mats.Node(r, captain ? "CaptainChair" : "CrewSeat", at);
        c.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        float w = captain ? 0.95f : 0.72f;
        Mats.Prim(PrimitiveType.Cylinder, c, new Vector3(0f, seatH * 0.35f, 0f), new Vector3(0.16f, seatH * 0.35f, 0.16f), frame);        // pedestal
        Mats.Prim(PrimitiveType.Cylinder, c, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.03f, 0.6f), frame);                         // foot
        Mats.Prim(PrimitiveType.Cube, c, new Vector3(0f, seatH - 0.08f, 0f), new Vector3(w, 0.16f, 0.72f), cushion);                    // cushion
        Mats.Prim(PrimitiveType.Cube, c, new Vector3(0f, seatH + (captain ? 0.48f : 0.4f), -0.36f), new Vector3(w, captain ? 0.85f : 0.72f, 0.14f), new Vector3(-8f, 0f, 0f), cushion);   // back
        if (captain)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Mats.Prim(PrimitiveType.Cube, c, new Vector3(s * (w * 0.5f + 0.08f), seatH + 0.18f, 0.0f), new Vector3(0.14f, 0.08f, 0.7f), frame);
                Mats.Prim(PrimitiveType.Cube, c, new Vector3(s * (w * 0.5f + 0.08f), seatH + 0.225f, 0.18f), new Vector3(0.1f, 0.01f, 0.22f), accent);   // armrest pads
            }
        }
        return Mats.Node(c, "Seat", new Vector3(0f, seatH, 0.02f));
    }

    // ---------- the ranch mech (head-local) and the space copy (ship-local) ----------
    static bool LocalBounds(Transform space, Transform root, out Vector3 mn, out Vector3 mx)
    {
        mn = new Vector3(1e9f, 1e9f, 1e9f); mx = -mn; bool any = false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponentInParent<Bridge73>() != null) continue;
            Bounds bb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = space.InverseTransformPoint(mf.transform.TransformPoint(c));
                mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l); any = true;
            }
        }
        return any;
    }

    // fix 1: the head mesh's own "froggy cockpit dome" (pack material "glass") IS the outside bubble - the trillion-story head
    // has none (the other bands keep theirs). DestroyImmediate, before the mech collects its renderers.
    public static int NoDome(Transform head)
    {
        var dead = new List<GameObject>();
        foreach (var r in head.GetComponentsInChildren<Renderer>(true))
            if (r.GetComponentInParent<Bridge73>() == null && (r.gameObject.name == "glass" || (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Glass")))) dead.Add(r.gameObject);
        foreach (var g in dead) Object.DestroyImmediate(g);
        if (dead.Count > 0) Debug.Log("FF73 cockpit dome removed from " + head.name + " (" + dead.Count + ")");
        return dead.Count;
    }

    // the head "body" shell only (not the eye bulbs / dome) - where the viewscreen goes
    static bool BodyBounds(Transform space, Transform head, out Vector3 mn, out Vector3 mx)
    {
        mn = new Vector3(1e9f, 1e9f, 1e9f); mx = -mn; bool any = false;
        foreach (var mf in head.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.gameObject.name != "body") continue;
            Bounds bb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 l = space.InverseTransformPoint(mf.transform.TransformPoint(c));
                mn = Vector3.Min(mn, l); mx = Vector3.Max(mx, l); any = true;
            }
        }
        return any || LocalBounds(space, head, out mn, out mx);
    }

    // inside the head: the viewscreen wall flush with the face (head +z) just above its glow band, centred on the head
    public static Bridge73 ForHead(Transform head, float H, int owner, string plate)
    {
        float k = H / StoryMech.BandH[3];
        NoDome(head);
        Vector3 mn, mx;
        float hs = head.lossyScale.x > 1e-4f ? head.lossyScale.x : 1f;
        if (!BodyBounds(head, head, out mn, out mx)) { mn = new Vector3(-0.08f, 0f, -0.07f) * H / hs; mx = new Vector3(0.08f, 0.12f, 0.07f) * H / hs; }
        float cy = (mn.y + mx.y) * 0.5f;
        Vector3 p = new Vector3((mn.x + mx.x) * 0.5f, cy - 1.3f * k / hs, mx.z - (ZF + 0.12f) * k / hs);   // window centre 1 m (k) above the body centre
        return Build(head, p, Quaternion.identity, k, owner, head, plate);
    }

    // the space copy flies head first (Superman pose): the bridge sits level in ship space with the viewscreen on the crown
    public static Bridge73 ForSpaceCopy(Transform vis, Transform head, float H, int owner, string plate)
    {
        float k = H / StoryMech.BandH[3];
        NoDome(head);
        Vector3 mn, mx;
        if (!BodyBounds(vis, head, out mn, out mx)) return null;
        Vector3 p = new Vector3((mn.x + mx.x) * 0.5f, (mn.y + mx.y) * 0.5f - 2.3f * k, mx.z - (ZF + 0.12f) * k);
        return Build(vis, p, Quaternion.identity, k, owner, head, plate);
    }

    // ---------- cameras: hide the head shell + near plane for any camera inside the room (this camera only) ----------
    static bool hooked;
    static readonly List<Renderer> hidden = new List<Renderer>();
    static Camera nearCam; static float nearWas;
    public static int CamsInside;   // probe: frames rendered by a camera inside a bridge
    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        U6Cam.PreCull += Pre;
        U6Cam.PostRender += Post;
    }
    static void Pre(Camera c)
    {
        Restore();
        if (c == null) return;
        Pose p;
        if (poses.TryGetValue(c, out p))
        {
            if (p.b != null && p.b.eye != null && p.f == Time.frameCount) c.transform.SetPositionAndRotation(p.b.eye.position, p.b.eye.rotation * Quaternion.Euler(0f, p.yaw, 0f) * Quaternion.Euler(p.pitch, 0f, 0f));
            else poses.Remove(c);
        }
        Vector3 cp = c.transform.position;
        foreach (var b in All)
        {
            if (b == null || !b.isActiveAndEnabled || !b.Inside(cp)) continue;
            if (b.hideRoot != null)
                foreach (var r in b.hideRoot.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.GetComponentInParent<Bridge73>() == null && r.GetComponentInParent<Frog>() == null) { r.enabled = false; hidden.Add(r); }
            if (b.visorR != null && b.visorR.enabled) { b.visorR.enabled = false; hidden.Add(b.visorR); }
            nearCam = c; nearWas = c.nearClipPlane;
            c.nearClipPlane = Mathf.Min(c.nearClipPlane, 0.12f * b.k);
            CamsInside++;
            break;
        }
    }
    static void Post(Camera c) { Restore(); }
    static void Restore()
    {
        for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].enabled = true;
        hidden.Clear();
        if (nearCam != null) { nearCam.nearClipPlane = nearWas; nearCam = null; }
    }

    // ---------- who is on which bridge ----------
    public static Bridge73 Of(Frog f)
    {
        if (f == null) return null;
        Vehicle v = f.vehicle != null ? f.vehicle : f.passengerOf;
        var sm = v as StoryMech;
        if (sm != null) return sm.bridge73;
        var s = v as Starship;
        if (s != null && s.mechForm != null && s.bridge73 != null && s.bridge73.isActiveAndEnabled) return s.bridge73;
        return null;
    }

    struct Pose { public Bridge73 b; public float yaw, pitch; public int f; }
    static readonly Dictionary<Camera, Pose> poses = new Dictionary<Camera, Pose>();
    // bridge view per camera rig (chase <-> bridge), look offsets
    class View { public bool on; public float yaw, pitch; }
    static readonly Dictionary<CamRig, View> views = new Dictionary<CamRig, View>();
    public static int Toggles;
    public static bool ViewOn(CamRig rig) { View v; return rig != null && views.TryGetValue(rig, out v) && v.on; }
    public static bool Toggle(CamRig rig, Frog f)
    {
        if (Of(f) == null) return false;
        View v; if (!views.TryGetValue(rig, out v)) { v = new View(); views[rig] = v; }
        v.on = !v.on; v.yaw = 0f; v.pitch = 0f; Toggles++;
        if (f.human) f.Toast(v.on ? "BRIDGE VIEW - look around with the stick; " + Key(f) + " again for the outside view" : "Outside view - " + Key(f) + " for the bridge", 2.5f);
        Debug.Log("FF73 bridge view " + (v.on ? "on" : "off") + " for " + f.nick);
        return true;
    }
    public static void SetView(CamRig rig, bool on) { View v; if (!views.TryGetValue(rig, out v)) { v = new View(); views[rig] = v; } v.on = on; v.yaw = 0f; v.pitch = 0f; }
    public static string Key(Frog f) { return f.inputKind == InputKind.Gamepad ? "R3" : f.inputKind == InputKind.Touch ? "double-tap the look side" : "0"; }

    // after the chase camera: put the camera behind the captain's chair, looking at the viewscreen
    public static bool Apply(CamRig rig, Frog f, PIn i)
    {
        View v;
        if (rig == null || rig.cam == null || !views.TryGetValue(rig, out v) || !v.on) return false;
        var b = Of(f);
        if (b == null || b.eye == null || b.room.lossyScale.x < 0.2f * b.k) return false;   // head blown off: chase view until it grows back
        var sm = f.vehicle as StoryMech;
        if (sm != null && sm.aimK > 0.05f) return false;                                     // the arm-cannon aim camera
        v.yaw = Mathf.Clamp(v.yaw + i.look.x, -80f, 80f);
        v.pitch = Mathf.Clamp(v.pitch - i.look.y, -30f, 35f);
        rig.cam.transform.SetPositionAndRotation(b.eye.position, b.eye.rotation * Quaternion.Euler(0f, v.yaw, 0f) * Quaternion.Euler(v.pitch, 0f, 0f));
        poses[rig.cam] = new Pose { b = b, yaw = v.yaw, pitch = v.pitch, f = Time.frameCount };   // re-posed right before it renders
        return true;
    }

    // ---------- riders ----------
    public void SeatRiders(Vehicle v, Frog skip)
    {
        if (Game.I == null) return;
        int n = 0;
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || f.passengerOf != v || f == skip) continue;
            Transform s = crew[Mathf.Min(n++, crew.Length - 1)];
            Place(f, s);
        }
    }
    // pose a froggy on a seat without re-parenting it (it stays under its vehicle's seat node, so hiding / rebuilding the
    // space copy can never take the froggy with it)
    public static void Place(Frog f, Transform s)
    {
        if (f == null || s == null) return;
        f.SetSuit(false);
        Transform p = f.transform.parent;
        float ps = p != null && p.lossyScale.x > 1e-5f ? p.lossyScale.x : 1f;
        f.transform.SetPositionAndRotation(s.position, s.rotation);
        f.transform.localScale = Vector3.one * (s.lossyScale.x / ps);
        if (!f.model.gameObject.activeSelf) f.model.gameObject.SetActive(true);
        f.model.Animate(0f, false, true, false, Time.deltaTime);
    }

    // AI follower of the trillion-story pilot on the ranch: beam aboard and take a crew seat
    public static bool FollowBoard(Frog f, Frog lead)
    {
        if (F73.Old || f == null || lead == null) return false;
        var sm = lead.vehicle as StoryMech;
        if (sm == null || sm.bridge73 == null || sm.wrecked || f.world != WorldId.Ranch || lead.world != WorldId.Ranch) return false;
        if (f.passengerOf == sm) return true;
        if (Riders(sm) >= 4) return false;
        Vector3 d = f.transform.position - sm.transform.position; d.y = 0f;
        if (d.magnitude > 160f) return false;
        Board(f, sm);
        return true;
    }
    public static void Board(Frog f, StoryMech sm)
    {
        if (f.vehicle != null) f.ExitVehicle();
        f.LeavePassenger();
        FX.Sparkle(f.transform.position + Vector3.up * 0.6f, new Color(0.5f, 0.9f, 1f), 12);
        f.BoardAsPassenger(sm);
        sm.bridge73.SeatRiders(sm, null);
        if (f.human) f.Toast("Welcome aboard " + sm.Title + "'s bridge!", 2.5f);
        Debug.Log("FF73 " + f.nick + " beamed aboard the bridge of " + sm.Title);
    }
    public static int Riders(Vehicle v) { int n = 0; if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.passengerOf == v) n++; return n; }

    // the pilot climbed out (or got knocked out): riders beam down beside the mech's feet
    public static void Unload(StoryMech sm)
    {
        if (Game.I == null || sm == null) return;
        int n = 0;
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || f.passengerOf != sm) continue;
            f.LeavePassenger();
            Vector3 p = sm.transform.position + sm.transform.right * (sm.height * 0.28f + 3f + n * 1.6f) + sm.transform.forward * 2f;
            p.y = Ranch.GY(p.x, p.z) + 0.5f;
            f.Teleport(p);
            FX.Sparkle(p + Vector3.up * 0.6f, new Color(0.5f, 0.9f, 1f), 10);
            n++;
            Debug.Log("FF73 " + f.nick + " beamed down from the bridge of " + sm.Title);
        }
    }

    // the mech flew to space: its bridge riders go along aboard the space copy (never left inside the stowed ranch mech)
    public static void ToSpace(StoryMech sm, Frog pilot)
    {
        if (Game.I == null || sm == null || SpaceWorld.I == null) return;
        Starship s = SpaceWorld.I.ShipOf(pilot);
        foreach (Frog f in Game.I.frogs)
        {
            if (f == null || f.passengerOf != sm) continue;
            f.LeavePassenger();
            if (s == null) { Vector3 p = sm.transform.position + sm.transform.right * (sm.height * 0.3f + 3f); p.y = Ranch.GY(p.x, p.z) + 0.5f; f.Teleport(p); continue; }
            f.SendTo(WorldId.Space, s.transform.position, 0f);
            f.BoardAsPassenger(s);
            Debug.Log("FF73 " + f.nick + " rides the bridge to space");
        }
    }
}

public partial class StoryMech
{
    public Bridge73 bridge73;

    // Build(): the trillion-story mech gets the bridge; the pilot's seat is the captain's chair (froggy shown, real size)
    void Bridge73Build()
    {
        if (F73.Old || band != 3 || head == null || bridge73 != null) return;
        bridge73 = Bridge73.ForHead(head, height, owner, Froggies.Names[owner].ToUpper() + "'S BRIDGE");
        Mats.SetLayer(bridge73.gameObject, VehicleLayer);
        seat = bridge73.captain; seatScale = 1f; showDriver = true;
    }

    void Bridge73Tick()
    {
        if (bridge73 == null) return;
        bridge73.SeatRiders(this, null);
    }
}
