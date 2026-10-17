using System.Collections.Generic;
using UnityEngine;

// ffu61 FROGGY BOWS (Bill, 2026-10-17: after crafting arrows the bench just said "quiver: 6 arrows" and he didn't know how to
// go hunting - only robots could hunt, from the phone). Now any player froggy can hunt too:
//  * A BOW RACK beside the garden workbench holds four kid-sized FROGGY BOWS (green / lime / teal / orange). They are shared
//    ranch kit (anyone may take one, like the ranch toys) - Figure 03's own bow stays his (robots ask to borrow it, ffu54).
//    A / E at the rack: take a bow / put it back. Pets get "paws can't hold a bow". CANON.md "Froggy bows".
//  * ARROWS come from the shared quiver at the workbench (Stash50.arrows); Figure 03's own arrows stay his.
//  * AIM + SHOOT with the controls the game already uses for aiming / firing: pad LT aim (over the shoulder, like the mech
//    arm cannon) + RT (or X) shoot; keyboard + mouse hold right mouse aim + left click (or F) shoot; touch AIM (toggle) +
//    SHOOT. Without aiming a shot goes to the nearest cow ahead (kid-friendly aim assist); while aiming the arc preview shows
//    where the arrow will fly (gravity) and turns yellow when it locks onto a cow. The ffu59 lobby LT latch applies (an LT
//    held from the lobby is not an aim until released) - LtLatch clears aimHeld before we see it.
//  * Arrow50 flight (gravity) -> a hit cow tips over with stars -> POOF -> drumstick (TipOver50 / Meat50, no blood) -> pick
//    it up, cook it on the garden campfire, eat it (ffu50). Missed arrows stay stuck in the ground: A / E picks them up (back
//    into the quiver); left alone they go back to the quiver after 2 minutes. Other animals: the arrow just bonks off.
//  * Per player (split-screen: each player's own camera / aim / arc), on foot on the ranch only. In a vehicle / riding /
//    swimming the bow rides on the froggy's back (no shooting); leaving the ranch puts the bow back on the rack.
//    Robots still hunt from the phone (PLAY / ORDERS > Hunt (bow)), James's order wins over chores (unchanged).
// Local per device (not synced). Logs "BOW61: ...".
public static class Bow61
{
    class St
    {
        public Transform vis, nock; public int slot; public float aimK, cool, lastShotT = -10f; public bool lockOn;
        public Transform[] dots; public Animal lockCow; public int shots, hits;
    }
    static readonly Dictionary<Frog, St> st = new Dictionary<Frog, St>();
    static readonly Frog[] rackUser = new Frog[4];
    static readonly Transform[] rackBows = new Transform[4];
    public static Vector3 RackPos, RackFront; public static float RackYaw; public static bool Built;
    public static int Shots, Hits, Picked;
    public static bool OldHints;   // &ff61=old: the ffu60 bench lines, no rack (before shots)
    public static Frog LastShooter;
    public const float Speed = 36f;
    static readonly Color[] Tints = { new Color(0.25f, 0.7f, 0.25f), new Color(0.6f, 0.8f, 0.2f), new Color(0.18f, 0.66f, 0.74f), new Color(0.92f, 0.38f, 0.17f) };

    public static bool Has(Frog f) { return f != null && st.ContainsKey(f); }
    public static float AimK(Frog f) { St s; return f != null && st.TryGetValue(f, out s) ? s.aimK : 0f; }
    public static bool LockedOn(Frog f) { St s; return f != null && st.TryGetValue(f, out s) && s.lockOn; }
    public static int Free { get { int n = 0; for (int i = 0; i < 4; i++) if (rackUser[i] == null) n++; return n; } }

    // ---------------- the rack (garden, at the workbench's left end) ----------------
    public static void Build()
    {
        if (Built || Fig50.I == null || Fig50.BenchPos == Vector3.zero) return;
        Built = true;
        Vector3 bp = Fig50.BenchPos;
        Vector3 to = (Campfire.I != null ? Campfire.I.Pos : (Garden.I != null ? Garden.I.FirePos : bp + Vector3.forward)) - bp; to.y = 0f;
        float yaw = to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : 0f;   // same as the bench (its front faces the fire)
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 p = bp + q * new Vector3(-1.75f, 0f, 0.1f); p.y = Ranch.GY(p.x, p.z);
        RackPos = p; RackYaw = yaw;
        Transform r = new GameObject("Froggy bow rack").transform; r.position = p; r.rotation = q;
        Material wood = Mats.TexTint("LB/wood", new Color(0.55f, 0.38f, 0.22f), 0.08f), woodD = Mats.Lit(new Color(0.36f, 0.24f, 0.14f));
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * 0.42f, 0.6f, 0f), new Vector3(0.07f, 1.2f, 0.07f), woodD, true);
            Mats.Prim(PrimitiveType.Cube, r, new Vector3(s * 0.42f, 0.03f, 0f), new Vector3(0.12f, 0.06f, 0.45f), woodD);
        }
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 1.12f, 0f), new Vector3(0.95f, 0.06f, 0.1f), wood);
        Mats.Prim(PrimitiveType.Cube, r, new Vector3(0f, 0.25f, 0.02f), new Vector3(0.9f, 0.05f, 0.08f), wood);
        for (int i = 0; i < 4; i++)
        {
            Transform b = MakeBow(r, Tints[i]);
            b.localPosition = new Vector3(-0.3f + i * 0.2f, 0.7f, 0.08f);
            b.localRotation = Quaternion.Euler(0f, 90f, 0f);
            rackBows[i] = b;
            Mats.Prim(PrimitiveType.Cylinder, r, new Vector3(-0.3f + i * 0.2f, 1.12f, 0.07f), new Vector3(0.02f, 0.05f, 0.02f), Vector3.right * 90f, woodD);   // peg
        }
        Ranch.Sign(p + q * new Vector3(0f, 1.62f, 0f), yaw, "FROGGY BOWS\n<size=20>take one to hunt</size>", new Color(0.32f, 0.2f, 0.1f), 1.5f, 0.6f);
        Mats.NoShadows(r.gameObject);
        RackFront = RobotNav.G((p + q * new Vector3(0f, 0f, 1.0f)).x, (p + q * new Vector3(0f, 0f, 1.0f)).z);
        var h = Interact.Add(RackFront, 1.6f, "froggy bows", f => RackAct(f));
        h.enabled = f => f.vehicle == null && f.world == WorldId.Ranch;
        h.dynLabel = f =>
        {
            if (Has(f)) return "put the froggy bow back on the rack";
            if (f.IsPet) return "froggy bows (paws can't hold a bow - a froggy can)";
            if (Free == 0) return "froggy bows - all 4 are out hunting";
            return "take a froggy bow (quiver: " + Stash50.arrows + " arrow" + (Stash50.arrows == 1 ? "" : "s") + ")";
        };
        Debug.Log("BOW61: rack built at " + p.ToString("0.0") + " yaw " + yaw.ToString("0") + " (bench " + bp.ToString("0.0") + "), front " + RackFront.ToString("0.0") + ", quiver " + Stash50.arrows);
    }

    public static Transform MakeBow(Transform parent, Color tint)
    {
        var b = new GameObject("Froggy bow").transform; b.SetParent(parent, false);
        Material limb = Mats.Paint(tint, 0.45f), grip = Mats.Lit(new Color(0.45f, 0.3f, 0.17f)), str = Mats.Unlit(new Color(0.92f, 0.92f, 0.86f));
        // kid-sized (0.75 m tall), curved limbs from 6 segments
        for (int k = -3; k <= 3; k++)
        {
            if (k == 0) continue;
            float y = k * 0.12f, z = 0.09f * (1f - (k / 3.2f) * (k / 3.2f));
            float slope = -2f * 0.09f * k / (3.2f * 3.2f) / 0.12f;
            var seg = Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, y, z), new Vector3(0.035f, 0.135f, 0.04f), limb);
            seg.transform.localRotation = Quaternion.Euler(-Mathf.Atan(slope) * Mathf.Rad2Deg, 0f, 0f);
        }
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0f, 0.09f), new Vector3(0.05f, 0.14f, 0.05f), grip);
        Mats.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0f, -0.01f), new Vector3(0.008f, 0.74f, 0.008f), str).name = "String";
        Mats.NoShadows(b.gameObject);
        return b;
    }

    static void RackAct(Frog f)
    {
        if (f == null) return;
        if (Has(f)) { Return(f, "rack"); return; }
        if (f.IsPet) { f.Toast("Paws can't hold a bow - ask a froggy (James, Jimmy, Bubbles or Rexy)", 2.8f); return; }
        int slot = -1; for (int i = 0; i < 4; i++) if (rackUser[i] == null) { slot = i; break; }
        if (slot < 0) { f.Toast("All 4 froggy bows are out hunting", 2f); return; }
        // the froggy's own colour if it's free
        if (f.id >= 0 && f.id < 4 && rackUser[f.id] == null) slot = f.id;
        rackUser[slot] = f;
        if (rackBows[slot] != null) rackBows[slot].gameObject.SetActive(false);
        var s = new St { slot = slot };
        st[f] = s;
        s.vis = MakeBow(null, Tints[slot]);
        s.nock = Mats.Prim(PrimitiveType.Cylinder, s.vis, new Vector3(0.02f, 0f, 0.12f), new Vector3(0.016f, 0.27f, 0.016f), new Vector3(90f, 0f, 0f), Mats.Lit(new Color(0.62f, 0.45f, 0.26f))).transform;
        s.dots = new Transform[18];
        Material dm = Mats.Unlit(new Color(1f, 1f, 1f));
        for (int i = 0; i < s.dots.Length; i++)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(d.GetComponent<Collider>());
            d.GetComponent<Renderer>().sharedMaterial = dm; d.name = "Arc61"; d.transform.localScale = Vector3.one * 0.07f;
            Mats.NoShadows(d); d.SetActive(false);
            s.dots[i] = d.transform;
        }
        Place(f, s);
        Sfx.PlayAt(Sfx.Pickup, f.transform.position, 0.5f, 30f, 1.2f);
        string k = Ctl(f);
        f.Toast(Stash50.arrows > 0 ? "Froggy bow! " + k + " (" + Stash50.arrows + " arrows in the quiver) - find a cow in the ranch pasture"
                                   : "Froggy bow! The quiver is empty - craft arrows at the workbench first (" + k + ")", 4f);
        Debug.Log("BOW61: " + f.nick + " (" + f.inputKind + ") took froggy bow " + slot + " | quiver " + Stash50.arrows);
    }

    public static void Return(Frog f, string why)
    {
        St s; if (f == null || !st.TryGetValue(f, out s)) return;
        if (s.vis != null) Object.Destroy(s.vis.gameObject);
        foreach (var d in s.dots) if (d != null) Object.Destroy(d.gameObject);
        st.Remove(f);
        if (s.slot >= 0 && s.slot < 4) { rackUser[s.slot] = null; if (rackBows[s.slot] != null) rackBows[s.slot].gameObject.SetActive(true); }
        if (f.human) f.Toast(why == "rack" ? "Froggy bow back on the rack" : "Your froggy bow went back on the rack by the garden workbench", 2.4f);
        Debug.Log("BOW61: " + f.nick + " bow back on the rack (" + why + "), shots " + s.shots + " hits " + s.hits);
    }

    // the controls for this froggy's device, in its own words
    public static string Ctl(Frog f)
    {
        InputKind k = Game.HintKind(f);
        return k == InputKind.Gamepad ? "LT aim, RT shoot" : k == InputKind.Touch ? "AIM, then SHOOT" : "hold right mouse to aim, click (or F) to shoot";
    }

    // the HUD line while holding the bow (no other prompt)
    public static string Hint(Frog f)
    {
        St s; if (!st.TryGetValue(f, out s) || f.vehicle != null || f.world != WorldId.Ranch) return null;
        InputKind k = Game.HintKind(f);
        bool pad = k == InputKind.Gamepad, touch = k == InputKind.Touch;
        if (Fig50.HeldKind(f) != 0) return null;
        if (Stash50.arrows <= 0) return "Froggy bow: the quiver is empty - craft arrows at the garden workbench (or pick up missed ones)";
        string aim = pad ? "LT" : touch ? "AIM" : "Hold right mouse", sh = pad ? "RT" : touch ? "SHOOT" : "Click / F";
        if (s.aimK > 0.5f) return sh + ": shoot" + (s.lockOn ? " - <color=#ffd34d>on the cow!</color>" : " - follow the arc") + "   (" + Stash50.arrows + " arrows)";
        return aim + ": aim   " + sh + ": shoot at a cow   (" + Stash50.arrows + " arrows)  |  rack: put the bow back";
    }

    static void Place(Frog f, St s)
    {
        if (s.vis == null || f.model == null) return;
        Transform at = f.model.bob != null ? f.model.bob : f.model.transform;
        if (s.vis.parent != at) s.vis.SetParent(at, false);
        bool hand = InHand(f);
        if (hand) { s.vis.localPosition = new Vector3(-0.3f, 0.55f + 0.05f * s.aimK, 0.28f + 0.1f * s.aimK); s.vis.localRotation = Quaternion.Euler(0f, 0f, -12f * (1f - s.aimK)); }
        else { s.vis.localPosition = new Vector3(0.05f, 0.62f, -0.3f); s.vis.localRotation = Quaternion.Euler(0f, 180f, 38f); }
        if (s.nock != null) s.nock.gameObject.SetActive(hand && Stash50.arrows > 0 && Time.time - s.lastShotT > 0.45f);
        bool show = f.vehicle == null && f.passengerOf == null && f.model.gameObject.activeInHierarchy;
        if (s.vis.gameObject.activeSelf != show) s.vis.gameObject.SetActive(show);
    }
    static bool InHand(Frog f) { return Fig50.HeldKind(f) == 0 && f.vehicle == null && !f.Swimming && !f.Climbing && f.rideAnimal == null && f.piggyOf == null && f.remote == null; }

    // called from Frog.Walk (on foot, every frame) - returns the aim weight (the froggy slows + squares up to the camera)
    public static float Tick(Frog f, PIn i, float dt)
    {
        St s; if (f == null || !st.TryGetValue(f, out s)) return 0f;
        if (f.world != WorldId.Ranch) { Return(f, "left the ranch"); return 0f; }
        bool ready = InHand(f) && f.human && f.remote == null;
        bool aim = ready && i.aimHeld;
        s.aimK = Mathf.MoveTowards(s.aimK, aim ? 1f : 0f, dt * 6f);
        s.cool -= dt;
        Camera cam = Game.I != null ? Game.I.CamFor(f) : null;
        Vector3 from = BowTip(f, s);
        Vector3 v; Animal lockCow;
        Solve(f, cam, from, s.aimK > 0.5f, out v, out lockCow);
        s.lockOn = lockCow != null && s.aimK > 0.5f; s.lockCow = lockCow;
        Arc(s, from, v, s.aimK > 0.5f);
        if (ready && i.bowFire)
        {
            if (Stash50.arrows <= 0) { if (s.cool <= 0f) { s.cool = 1.2f; f.Toast("The quiver is empty - craft arrows at the garden workbench (or pick up missed ones)", 2.6f); Sfx.PlayAt(Sfx.Click, f.transform.position, 0.4f, 20f, 0.7f); } }
            else if (s.cool <= 0f)
            {
                s.cool = 0.6f; s.lastShotT = Time.time; s.shots++; Shots++; LastShooter = f;
                Stash50.arrows--; Stash50.Save();
                var a = Arrow50.FireV(from, v);
                a.shooter = f;
                if (Game.I != null) { CamRig rg = Game.I.RigFor(f); if (rg != null) rg.Kick(0.8f); }
                Debug.Log("BOW61 SHOT: " + f.nick + " (" + f.inputKind + ", " + (s.aimK > 0.5f ? "aimed" : "hip") + ") from " + from.ToString("0.0") + " v " + v.ToString("0.0") + " lock " + (lockCow != null ? lockCow.Pos.ToString("0.0") : "none") + " | quiver " + Stash50.arrows);
                if (f.toastT <= 0.2f) f.Toast("Arrow away! (" + Stash50.arrows + " left)", 1.4f);
            }
        }
        Place(f, s);
        return s.aimK;
    }

    static Vector3 BowTip(Frog f, St s)
    {
        if (s.vis != null && s.vis.gameObject.activeInHierarchy) return s.vis.position + f.transform.forward * 0.25f;
        return f.transform.position + Vector3.up * 0.7f + f.transform.forward * 0.5f;
    }

    // where the arrow goes: aimed = along the camera's centre ray (snaps onto a cow within ~5 deg); hip = the nearest cow ahead
    static void Solve(Frog f, Camera cam, Vector3 from, bool aimed, out Vector3 v, out Animal cow)
    {
        cow = null;
        Vector3 fw = f.transform.forward; fw.y = 0f; if (fw.sqrMagnitude < 0.01f) fw = Vector3.forward; fw.Normalize();
        if (aimed && cam != null)
        {
            Vector3 o = cam.transform.position, d = cam.transform.forward;
            float bestA = 5.5f;
            foreach (var a in Animal.All)
            {
                if (!Fig50.Huntable(a)) continue;
                Vector3 c = a.Pos + Vector3.up * 0.85f, to = c - o;
                float m = to.magnitude; if (m > 70f || m < 2f) continue;
                float ang = Vector3.Angle(d, to);
                if (ang < bestA) { bestA = ang; cow = a; }
            }
            if (cow != null) { v = Ballistic(from, cow.Pos + Vector3.up * 0.85f); return; }
            // aim point = what the centre ray hits (else 60 m out), then the arrow flies there along a straight launch (it drops)
            Vector3 tgt = o + d * 60f;
            RaycastHit h;
            if (Physics.Raycast(o + d * 2.5f, d, out h, 120f, ~((1 << 2) | (1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) tgt = h.point;
            Vector3 dir = (tgt - from).normalized;
            v = dir * Speed;
            return;
        }
        Vector3 p = f.transform.position;
        float bd = 34f;
        foreach (var a in Animal.All)
        {
            if (!Fig50.Huntable(a)) continue;
            Vector3 d = a.Pos - p; d.y = 0f; float m = d.magnitude;
            if (m < bd && m > 1.5f && Vector3.Angle(fw, d) < 35f) { bd = m; cow = a; }
        }
        if (cow != null) { v = Ballistic(from, cow.Pos + Vector3.up * 0.85f); return; }
        v = (fw + Vector3.up * 0.2f).normalized * Speed;
    }

    // flat-ish lob that lands on the target (time = distance / speed, gravity compensated) - same as Arrow50.Fire
    public static Vector3 Ballistic(Vector3 from, Vector3 target)
    {
        Vector3 d = target - from; float hd = new Vector2(d.x, d.z).magnitude;
        float T = Mathf.Max(0.12f, hd / Speed);
        return d / T + Vector3.up * (0.5f * 9.81f * T);
    }

    static void Arc(St s, Vector3 from, Vector3 v, bool on)
    {
        if (s.dots == null) return;
        Color c = s.lockOn ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f);
        float tStep = 0.075f;
        Vector3 p = from, vv = v; bool stop = !on;
        for (int i = 0; i < s.dots.Length; i++)
        {
            Transform d = s.dots[i]; if (d == null) continue;
            if (!stop)
            {
                for (int k = 0; k < 3; k++) { vv.y -= 9.81f * tStep / 3f; p += vv * tStep / 3f; }
                if (p.y < Ranch.GY(p.x, p.z)) stop = true;
            }
            bool show = !stop && i > 0;
            if (d.gameObject.activeSelf != show) d.gameObject.SetActive(show);
            if (show) { d.position = p; d.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.05f, i / (float)s.dots.Length); var r = d.GetComponent<Renderer>(); if (r.sharedMaterial.color != c) r.sharedMaterial = Mats.Unlit(c); }
        }
    }

    public static void Hit(Frog f)
    {
        St s; if (f != null && st.TryGetValue(f, out s)) s.hits++;
        Hits++;
        if (f != null && f.human) f.Toast("Got one! POOF - pick up the drumstick, then cook it on the garden campfire", 3.4f);
    }

    // per frame (Ffu61): bows of froggies that left / were removed, vehicles (bow on the back, hidden while seated)
    public static void Upkeep()
    {
        if (st.Count == 0) return;
        List<Frog> gone = null;
        foreach (var kv in st)
        {
            Frog f = kv.Key;
            if (f == null || !f.human || f.world != WorldId.Ranch) { if (gone == null) gone = new List<Frog>(); gone.Add(f); continue; }
            if (f.vehicle != null || f.passengerOf != null || f.Swimming || f.Climbing)
            {
                kv.Value.aimK = 0f; kv.Value.lockOn = false;
                foreach (var d in kv.Value.dots) if (d != null && d.gameObject.activeSelf) d.gameObject.SetActive(false);
                Place(f, kv.Value);
            }
        }
        if (gone != null) foreach (var f in gone)
            {
                if (f != null) Return(f, f.world != WorldId.Ranch ? "left the ranch" : "seat left");
                else { St s = st[f]; if (s.slot >= 0 && s.slot < 4) { rackUser[s.slot] = null; if (rackBows[s.slot] != null) rackBows[s.slot].gameObject.SetActive(true); } st.Remove(f); }
            }
    }

    public static string Line { get { return "bows out " + st.Count + " / 4, quiver " + Stash50.arrows + ", shots " + Shots + " hits " + Hits + " picked up " + Picked + " stuck " + Arrow50.Stuck.Count; } }
    public static void DemoTake(Frog f) { if (!Has(f)) RackAct(f); }
}
