using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ffu15: a story mech that rocketed past 2.4 km flies on in space AS A MECH. It reuses the Starship flight model
// (orbit / free flight / auto-transfer / warp / targets / landing) and swaps the Starship visual for a space-scale copy
// of that mech in a Superman pose (body level, head first, back rockets as the main engines). Shared-screen crew ride on
// its shoulders in spacesuits; the cargo bay on its back opens (pad B / key B / touch BAY) and they jetpack out (each
// froggy flies its own jetpack, flies back into the open bay to re-board; the doors close when everyone is back).
public partial class Starship
{
    public StoryMech mechForm;
    public const float MechVisH = 16f;          // the space copy is drawn 16 m tall whatever its band (planets are 10x scaled)
    public const float RiderScale = 2.2f;       // froggies are drawn bigger next to it so they read
    Transform shipVis, mechVis, mechTorso, mechArmL, mechArmR, mechLegL, mechLegR, mechPose;
    readonly Transform[] mechFlames = new Transform[2];
    readonly Transform[] bayDoors = new Transform[2];
    readonly List<Transform> riderSeats = new List<Transform>();
    GameObject bayGlow;
    Light mechJetLight;
    int mechKey = -1;
    float bayK, flameK2, bank, bayEmptyT;
    bool bayOpen, hadJetters;
    string shipTitle = "Starship";
    public bool BayOpen { get { return bayOpen && mechForm != null; } }
    public Vector3 BayWorld { get { return mechTorso != null ? mechTorso.TransformPoint(new Vector3(0f, 0.2f * MechVisH, -0.16f * MechVisH)) + Vector3.up * 1.2f : transform.position + Vector3.up * 3f; } }

    public void SetMechForm(StoryMech m)
    {
        if (shipVis == null) shipVis = transform.Find("Vis");
        if (m == null)
        {
            if (mechForm == null) return;
            CloseBayNow();
            mechForm = null;
            Title = shipTitle;
            if (mechVis != null) mechVis.gameObject.SetActive(false);
            if (shipVis != null) shipVis.gameObject.SetActive(true);
            // riders go back inside the Starship (hidden)
            if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.passengerOf == this) { f.SetSuit(false); f.BoardAsPassenger(this); }
            return;
        }
        mechForm = m;
        Title = m.Title + " (space)";
        int key = m.owner * 4 + m.band;
        if (mechVis == null || key != mechKey)
        {
            if (mechVis != null) Destroy(mechVis.gameObject);
            BuildMechVis(m);
            mechKey = key;
        }
        mechVis.gameObject.SetActive(true);
        if (shipVis != null) shipVis.gameObject.SetActive(false);
        bayOpen = false; bayK = 0f;
    }

    void BuildMechVis(StoryMech m)
    {
        float H = MechVisH;
        mechVis = Mats.Node(transform, "MechVis", Vector3.zero);
        // Superman pose: standing +y -> forward (+z), chest faces down, back (with the rockets + bay) faces up
        mechPose = Mats.Node(mechVis, "Pose", new Vector3(0f, 0f, -H * 0.5f));
        mechPose.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var parts = StoryMech.BuildBodyCopy(mechPose, m.owner, m.band, H);
        mechLegL = parts[1]; mechLegR = parts[2]; mechTorso = parts[3]; mechArmL = parts[4]; mechArmR = parts[5];
        // arms reach forward (above the head in the standing frame), legs straight back
        mechArmR.localRotation = Quaternion.Euler(172f, 0f, -6f);
        mechArmL.localRotation = Quaternion.Euler(165f, 0f, 9f);
        Material gun = Mats.Steel(new Color(0.22f, 0.23f, 0.26f));
        var bar = Mats.Prim(PrimitiveType.Cylinder, mechArmR, new Vector3(0f, -0.33f * H, 0f), new Vector3(0.05f * H, 0.07f * H, 0.05f * H), gun);
        bar.name = "CannonBarrel";
        // back rockets (main engines): steel nozzles + layered plume + a light that washes the legs
        Material nozzle = Mats.Steel(new Color(0.35f, 0.36f, 0.4f));
        Material outer = new Material(Mats.Fx); outer.color = new Color(1f, 0.55f, 0.18f, 0.55f);
        Material core = Mats.Unlit(new Color(1f, 0.95f, 0.75f));
        for (int s = 0; s < 2; s++)
        {
            float x = (s == 0 ? -1f : 1f) * 0.055f * H;
            Vector3 np = new Vector3(x, 0.105f * H, -0.12f * H);
            Mats.Prim(PrimitiveType.Cylinder, mechTorso, np + new Vector3(0f, -0.01f * H, 0f), new Vector3(0.05f * H, 0.014f * H, 0.05f * H), nozzle);
            Mats.Prim(PrimitiveType.Cylinder, mechTorso, np + new Vector3(0f, -0.022f * H, 0f), new Vector3(0.036f * H, 0.002f * H, 0.036f * H), core);   // hot throat
            Transform f = Mats.Node(mechTorso, "Flame", np + new Vector3(0f, -0.025f * H, 0f));
            f.localRotation = Quaternion.Euler(35f, 0f, 0f);   // tilted off the back so the plume clears the legs
            Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.5f, 0f), new Vector3(0.62f, 0.55f, 0.62f), outer);
            Mats.Prim(PrimitiveType.Capsule, f, new Vector3(0f, -0.4f, 0f), new Vector3(0.3f, 0.45f, 0.3f), core);
            mechFlames[s] = f;
        }
        var lg = new GameObject("MechJetLight"); lg.transform.SetParent(mechVis, false); lg.transform.localPosition = new Vector3(0f, 1f, -H * 0.55f);
        mechJetLight = lg.AddComponent<Light>(); mechJetLight.type = LightType.Point; mechJetLight.color = new Color(1f, 0.6f, 0.3f); mechJetLight.range = H * 1.2f; mechJetLight.shadows = LightShadows.None;
        // cargo bay on the back: two doors hinged at their outer edges + a lit interior panel
        Material door = Mats.Shiny(Color.Lerp(Froggies.Color(m.owner), Color.white, 0.25f)), dark = Mats.Lit(new Color(0.08f, 0.09f, 0.1f));
        Vector3 bc = new Vector3(0f, 0.2f * H, -0.15f * H);
        bayGlow = new GameObject("BayInside"); bayGlow.transform.SetParent(mechTorso, false);
        Mats.Prim(PrimitiveType.Cube, bayGlow.transform, bc + new Vector3(0f, 0f, 0.006f * H), new Vector3(0.17f * H, 0.15f * H, 0.004f * H), dark);
        Mats.Prim(PrimitiveType.Cube, bayGlow.transform, bc + new Vector3(0f, 0.065f * H, 0.002f * H), new Vector3(0.15f * H, 0.008f * H, 0.004f * H), Mats.Unlit(new Color(0.55f, 0.95f, 1f)));
        bayGlow.SetActive(false);
        for (int s = 0; s < 2; s++)
        {
            float sx = s == 0 ? -1f : 1f;
            Transform hinge = Mats.Node(mechTorso, s == 0 ? "BayDoorL" : "BayDoorR", bc + new Vector3(sx * 0.09f * H, 0f, -0.004f * H));
            Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(-sx * 0.045f * H, 0f, 0f), new Vector3(0.09f * H, 0.16f * H, 0.012f * H), door);
            Mats.Prim(PrimitiveType.Cube, hinge, new Vector3(-sx * 0.045f * H, 0.07f * H, -0.007f * H), new Vector3(0.085f * H, 0.006f * H, 0.003f * H), Mats.Unlit(new Color(1f, 0.75f, 0.2f)));   // warning stripe
            bayDoors[s] = hinge;
        }
        // shoulder seats (upright in ship space, on top of the back near the shoulders), then two more on the back
        riderSeats.Clear();
        Vector3[] sp = { new Vector3(-0.15f, 0.36f, -0.1f), new Vector3(0.15f, 0.36f, -0.1f), new Vector3(-0.12f, 0.05f, -0.12f), new Vector3(0.12f, 0.05f, -0.12f) };
        foreach (Vector3 v in sp)
        {
            Vector3 w = mechTorso.TransformPoint(v * H);
            Transform seatT = Mats.Node(transform, "RiderSeat", transform.InverseTransformPoint(w) + Vector3.up * 0.2f);
            seatT.SetParent(mechVis, true);
            riderSeats.Add(seatT);
        }
        Mats.SetLayer(mechVis.gameObject, VehicleLayer);
        Mats.NoShadows(mechVis.gameObject);
    }

    void CloseBayNow() { bayOpen = false; }

    public void ToggleBay()
    {
        if (mechForm == null) return;
        if (!bayOpen)
        {
            bayOpen = true; bayEmptyT = 0f; hadJetters = false;
            Sfx.Play(Sfx.Servo != null && Sfx.Servo.Length > 0 ? Sfx.Pick(Sfx.Servo) : Sfx.Door, 0.8f, 0.8f);
            int n = 0;
            if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.passengerOf == this) { ExitToJet(f); n++; }
            if (driver != null) driver.Toast(n > 0 ? "Cargo bay open - your crew is jetpacking out!" : "Cargo bay open (nobody aboard to jump out)", 2.5f);
            return;
        }
        int outside = Jetters();
        if (outside > 0) { if (driver != null) driver.Toast(outside + " froggy" + (outside > 1 ? "s are" : " is") + " still outside - wait for them to fly back in", 2.5f); return; }
        bayOpen = false;
        Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Door, 0.8f, 0.9f);
    }

    int Jetters() { int n = 0; if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.jetOf == this) n++; return n; }

    public void ExitToJet(Frog f)
    {
        if (f == null) return;
        f.LeavePassenger();
        if (f.cc != null) f.cc.enabled = false;
        f.jetOf = this;
        f.jetOff = BayWorld - transform.position + new Vector3(Random.Range(-1f, 1f) * 6f, 6f + f.id * 1.2f, Random.Range(-1f, 1f) * 6f);
        f.jetT = 0f;
        f.transform.position = transform.position + f.jetOff;
        f.transform.localScale = Vector3.one * RiderScale;
        f.SetSuit(true);
        hadJetters = true;
        for (int k = 0; k < 6; k++) FX.Spray(f.transform.position, Random.insideUnitSphere * 3f, 1.2f, 0.8f, new Color(0.95f, 0.97f, 1f, 0.6f));
        if (f.human) { Sfx.Play(Sfx.Pickup, 0.5f, 0.7f); f.Toast("Jetpack! Stick to fly, " + (f.inputKind == InputKind.Gamepad ? "A up / B down" : f.inputKind == InputKind.Touch ? "UP / DOWN" : "SPACE up / SHIFT down") + ". Fly back into the bay to re-board.", 4f); }
        Debug.Log("SpaceMech: " + f.nick + " jetpacked out of the bay");
    }

    public void Reboard(Frog f)
    {
        if (f == null) return;
        f.jetOf = null;
        f.transform.localScale = Vector3.one;
        f.BoardAsPassenger(this);
        Sfx.Play(Sfx.Door, 0.6f, 1.1f);
        if (f.human) f.Toast("Back aboard!", 1.5f);
        Debug.Log("SpaceMech: " + f.nick + " re-boarded");
    }

    // a froggy in space that is neither piloting, riding nor jetpacking (should not happen) joins the crew
    public void AdoptStray(Frog f)
    {
        if (f.jetOf != null || f.passengerOf != null || f.vehicle != null) return;
        if (driver == null) f.EnterVehicle(this); else f.BoardAsPassenger(this);
    }

    public void ReboardAll() { if (Game.I != null) foreach (Frog f in Game.I.frogs) if (f != null && f.jetOf == this) Reboard(f); bayOpen = false; }

    // demo: put AI froggies on the shoulders (shared-screen look without extra players)
    public void DemoRiders(params Frog[] fs) { foreach (Frog f in fs) if (f != null && f.vehicle == null && f.passengerOf == null) { f.SendTo(WorldId.Space, transform.position, 0f); f.BoardAsPassenger(this); } }

    // one-button "warp home": target Earth + auto-transfer
    public void WarpHome()
    {
        int earth = W.Find("earth");
        if (mode == Mode.Orbit && orbitBody == earth) { if (driver != null) driver.Toast("You're in Earth orbit - " + (driver.inputKind == InputKind.Gamepad ? "Y" : driver.inputKind == InputKind.Touch ? "LAND" : "F") + " lands at the ranch", 2.5f); return; }
        if (mode == Mode.Transfer && tBody == earth) return;
        target = earth;
        if (mode == Mode.Transfer) mode = Mode.Free;
        StartTransfer();
        if (driver != null) driver.Toast("Warping home to Earth!", 2f);
    }

    void LateUpdate()
    {
        if (mechForm == null || mechVis == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.1f);
        // pose life: bank into turns, gentle bob, legs flutter
        float turn = driver != null ? inp.move.x : 0f;
        bank = Mathf.Lerp(bank, -turn * 28f, dt * 3f);
        float t = Time.time;
        mechVis.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2f, 0f, bank);
        mechVis.localPosition = new Vector3(0f, Mathf.Sin(t * 1.3f) * 0.25f, 0f);
        if (mechLegL != null) { mechLegL.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.2f) * 5f, 0f, 3f); mechLegR.localRotation = Quaternion.Euler(-Mathf.Sin(t * 2.2f) * 5f, 0f, -3f); }
        // engines
        bool thrust = driver != null && (inp.move.y > 0.05f || inp.gas > 0.05f);
        bool boost = driver != null && inp.boostHeld;
        float want = (thrust ? 1f : 0.3f) + (boost ? 0.6f : 0f) + (mode == Mode.Transfer ? 0.7f : 0f);
        flameK2 = Mathf.MoveTowards(flameK2, want, dt * 5f);
        float H = MechVisH;
        for (int i = 0; i < 2; i++)
        {
            if (mechFlames[i] == null) continue;
            float fl = 1f + Mathf.Sin(t * (41f + i * 7f)) * 0.1f + Random.Range(-0.06f, 0.06f);
            mechFlames[i].localScale = new Vector3(H * (0.05f + 0.02f * flameK2), H * (0.1f + 0.22f * flameK2) * fl, H * (0.05f + 0.02f * flameK2));
        }
        if (mechJetLight != null) { mechJetLight.enabled = !Look.Mobile; mechJetLight.intensity = flameK2 * 2.4f * (0.9f + Random.value * 0.2f); }
        // cargo bay doors swing open on their outer hinges
        bayK = Mathf.MoveTowards(bayK, bayOpen ? 1f : 0f, dt * 1.6f);
        float ang = Mathf.SmoothStep(0f, 1f, bayK) * 105f;
        if (bayDoors[0] != null) { bayDoors[0].localRotation = Quaternion.Euler(0f, ang, 0f); bayDoors[1].localRotation = Quaternion.Euler(0f, -ang, 0f); }
        if (bayGlow != null && bayGlow.activeSelf != bayK > 0.05f) bayGlow.SetActive(bayK > 0.05f);
        // riders on the shoulders
        int ri = 0;
        if (Game.I != null)
            foreach (Frog f in Game.I.frogs)
                if (f != null && f.passengerOf == this && riderSeats.Count > 0) f.RideShoulder(riderSeats[Mathf.Min(ri++, riderSeats.Count - 1)], RiderScale);
        // doors close by themselves once everyone who jumped out is back aboard
        if (bayOpen && hadJetters)
        {
            if (Jetters() == 0) { bayEmptyT += dt; if (bayEmptyT > 1.5f) { bayOpen = false; hadJetters = false; Sfx.Play(Sfx.Clank != null ? Sfx.Clank : Sfx.Door, 0.7f, 0.9f); if (driver != null) driver.Toast("Everyone's back - bay doors closed", 2f); } }
            else bayEmptyT = 0f;
        }
    }
}

// ffu15: space radar in the top-right corner of each view in space: you at the centre (camera-up), Earth / Moon / Mars /
// Callisto / Jupiter / Sun / Station as dots (log distance), a HOME arrow towards Earth with its distance, the nearest
// distances, the warp controls for the player's device and a legend for the lines drawn in space. Tap / click = warp home.
public class SpaceRadar
{
    readonly RectTransform root;
    readonly Image bg, homeArrow, me;
    readonly Text title, dists, hint, legend, homeText;
    readonly Dictionary<string, Image> dots = new Dictionary<string, Image>();
    readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
    static readonly string[] Ids = { "sun", "earth", "moon", "station", "mars", "jupiter", "callisto", "venus", "mercury", "saturn" };
    static Sprite tri;
    const float R = 92f;

    static Sprite Tri
    {
        get
        {
            if (tri != null) return tri;
            const int n = 48;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;
                    float half = (1f - v) * 0.5f;            // apex at the top
                    float a = Mathf.Clamp01((half - Mathf.Abs(u)) * n * 0.7f) * Mathf.Clamp01(v * n * 0.5f) * Mathf.Clamp01((1f - v) * n);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            tri = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return tri;
        }
    }

    public SpaceRadar(Transform panel)
    {
        root = UIK.Rect(panel, "SpaceRadar", new Vector2(1f, 1f), new Vector2(-122f, -190f), new Vector2(2 * R + 16f, 2 * R + 16f));
        var shadow = UIK.Img(root, UIK.Circle, new Color(0f, 0f, 0f, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(3f, -4f), new Vector2(2 * R + 14f, 2 * R + 14f));
        bg = UIK.Img(root, UIK.Circle, new Color(0.03f, 0.06f, 0.12f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2 * R + 8f, 2 * R + 8f));
        UIK.Img(root, UIK.Ring, new Color(0.45f, 0.75f, 1f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2 * R + 8f, 2 * R + 8f));
        UIK.Img(root, UIK.Ring, new Color(0.45f, 0.75f, 1f, 0.18f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(R, R));
        UIK.Img(root, null, new Color(0.45f, 0.75f, 1f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2 * R, 1.5f));
        UIK.Img(root, null, new Color(0.45f, 0.75f, 1f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1.5f, 2 * R));
        foreach (string id in Ids)
        {
            dots[id] = UIK.Img(root, UIK.Circle, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            if (id == "earth" || id == "moon" || id == "mars" || id == "callisto" || id == "sun" || id == "jupiter")
            {
                var l = UIK.Label(root, Name(id), 13, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 18f), new Color(0.9f, 0.95f, 1f));
                labels[id] = l;
            }
        }
        me = UIK.Img(root, Tri, new Color(0.4f, 1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 18f));
        homeArrow = UIK.Img(root, Tri, new Color(0.35f, 0.8f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        homeText = UIK.Label(root, "", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 18f), new Color(0.6f, 0.9f, 1f));
        title = UIK.Label(root, "RADAR", 13, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(160f, 18f), new Color(0.7f, 0.85f, 1f));
        dists = UIK.Label(root, "", 14, TextAnchor.UpperRight, new Vector2(1f, 0f), new Vector2(-4f, -40f), new Vector2(300f, 60f), Color.white);
        dists.rectTransform.pivot = new Vector2(1f, 0.5f);
        hint = UIK.Label(root, "", 13, TextAnchor.UpperRight, new Vector2(1f, 0f), new Vector2(-4f, -92f), new Vector2(330f, 40f), new Color(1f, 0.92f, 0.6f));
        hint.rectTransform.pivot = new Vector2(1f, 0.5f);
        legend = UIK.Label(root, "", 12, TextAnchor.UpperRight, new Vector2(1f, 0f), new Vector2(-4f, -128f), new Vector2(330f, 34f), new Color(0.85f, 0.9f, 1f, 0.9f));
        legend.rectTransform.pivot = new Vector2(1f, 0.5f);
        foreach (var t in new[] { dists, hint, legend }) t.supportRichText = true;
        root.gameObject.SetActive(false);
    }

    static string Name(string id) { return id.Length > 0 ? char.ToUpper(id[0]) + id.Substring(1) : id; }
    static string Dist(float d) { return d >= 1000f ? (d / 1000f).ToString("0.0") + " km" : Mathf.RoundToInt(d) + " m"; }
    static Color Col(string id)
    {
        switch (id)
        {
            case "sun": return new Color(1f, 0.85f, 0.3f);
            case "earth": return new Color(0.3f, 0.65f, 1f);
            case "moon": return new Color(0.8f, 0.8f, 0.82f);
            case "mars": return new Color(1f, 0.4f, 0.25f);
            case "jupiter": return new Color(0.9f, 0.75f, 0.55f);
            case "callisto": return new Color(0.75f, 0.85f, 0.95f);
            case "station": return new Color(1f, 1f, 1f);
            default: return new Color(0.6f, 0.6f, 0.65f);
        }
    }

    public RectTransform Rect { get { return root; } }

    public void Tick(Frog f, Camera cam, bool narrow)
    {
        var sw = SpaceWorld.I;
        bool show = f != null && f.world == WorldId.Space && sw != null && sw.ship != null;
        if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
        if (!show) return;
        root.localScale = Vector3.one * (narrow ? 0.85f : 1f);
        Starship ship = sw.ship;
        Vector3 c = ship.transform.position;
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        Quaternion toRadar = Quaternion.Euler(0f, -yaw, 0f);
        const float maxD = 3600f;
        System.Func<float, float> map = d => R * Mathf.Log(1f + d / 25f) / Mathf.Log(1f + maxD / 25f);
        var near = new List<KeyValuePair<float, string>>();
        foreach (string id in Ids)
        {
            int bi = sw.Find(id);
            if (bi < 0) continue;
            Vector3 d = sw.bodies[bi].pos - c; d.y = 0f;
            float dist = d.magnitude;
            Vector3 r = toRadar * d;
            Vector2 p = dist > 0.01f ? new Vector2(r.x, r.z).normalized * Mathf.Min(map(dist), R) : Vector2.zero;
            Image dot = dots[id];
            dot.rectTransform.anchoredPosition = p;
            bool tgt = ship.target == bi;
            float sz = id == "sun" ? 14f : id == "earth" ? 12f : id == "station" ? 5f : 9f;
            dot.rectTransform.sizeDelta = Vector2.one * (tgt ? sz + 5f : sz);
            Color cc = Col(id); if (tgt) cc = Color.Lerp(cc, new Color(1f, 0.85f, 0.2f), 0.5f);
            dot.color = cc;
            Text l;
            if (labels.TryGetValue(id, out l)) { l.rectTransform.anchoredPosition = p + new Vector2(52f, 0f); l.color = tgt ? new Color(1f, 0.88f, 0.4f) : new Color(0.9f, 0.95f, 1f, 0.9f); }
            if (id != "station" && id != "sun") near.Add(new KeyValuePair<float, string>(dist, id));
        }
        // HOME arrow at the rim towards Earth
        int ei = sw.Find("earth");
        Vector3 de = sw.bodies[ei].pos - c; de.y = 0f;
        Vector3 rr = toRadar * de;
        Vector2 dir = new Vector2(rr.x, rr.z).sqrMagnitude > 0.01f ? new Vector2(rr.x, rr.z).normalized : Vector2.up;
        homeArrow.rectTransform.anchoredPosition = dir * (R + 2f);
        homeArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
        homeText.rectTransform.anchoredPosition = dir * (R - 20f) + new Vector2(0f, dir.y > 0.5f ? -14f : 0f);
        homeText.text = "HOME " + Dist(de.magnitude);
        // me: heading relative to the camera
        float hd = ship.transform.eulerAngles.y - yaw;
        me.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -hd);
        near.Sort((a, b) => a.Key.CompareTo(b.Key));
        string ds = "";
        for (int i = 0; i < near.Count && i < 3; i++) ds += (i > 0 ? "   " : "") + "<color=#" + ColorUtility.ToHtmlStringRGB(Col(near[i].Value)) + ">" + Name(near[i].Value) + "</color> " + Dist(near[i].Key);
        if (ship.target >= 0) ds += "\n<color=#ffd84a>target " + sw.bodies[ship.target].name + " " + Dist((sw.bodies[ship.target].pos - c).magnitude) + "</color>";
        dists.text = ds;
        InputKind ik = ship.driver != null ? ship.driver.inputKind : f.inputKind;
        hint.text = ik == InputKind.Gamepad ? "<b>L3 WARP HOME</b>   LB/RB warp   D-pad target   X auto"
                  : ik == InputKind.Touch ? "<b>TAP RADAR = WARP HOME</b>   TGT target   AUTO fly there"
                  : "<b>R WARP HOME</b>   Z/C warp   T target   G auto";
        legend.text = "<color=#7f94c8>=====</color> orbits   <color=#8fe6ff>=====</color> your course   <color=#ffd84a>( O )</color> target";
        // tap / click the radar = warp home (the pilot's ship)
        bool hit = false;
        foreach (Vector2 tp in Kb.TouchesBegan()) if (RectTransformUtility.RectangleContainsScreenPoint(bg.rectTransform, tp, null)) hit = true;
        if (!hit && Kb.MouseLeftDown() && Cursor.lockState != CursorLockMode.Locked && UnityEngine.InputSystem.Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(bg.rectTransform, UnityEngine.InputSystem.Mouse.current.position.ReadValue(), null)) hit = true;
        if (hit && ship.driver != null) ship.WarpHome();
    }
}
