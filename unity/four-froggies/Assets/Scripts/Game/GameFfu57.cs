using System.Collections.Generic;
using UnityEngine;

// ffu57 demo / probe hook (Core/Animals57.cs). Only with ?ffdemo=1&ffshot=animals57 [&ffr57=<phases>] (+ &animals=old for the
// same tour with the ffu56 look = before shots). Wall-clock phases:
//   house    the house pets (Spotty, Tigy, Kitty, Little White Socks, Germy, Daisy) in the parlor, fur close-up
//   pets     camera visits each named pet (Roster coats)           petfeed  James pets Spotty, feeds Germy a bone
//   fetch    James throws the ball for Germy                       ride     James rides Daisy (stick steers) then hops off
//   follow   Spotty + Germy come along: house -> ranch -> Mars     herd     pasture: cows + horses walk, trot, gallop (scared)
//   robot    robot PLAY > Pet + feed animals, then Ride a cow      frog     James's wet skin close-up + the other froggies
//   mech     mech foot plants on the ranch dirt (Tracks56.MechFoot via MechStomp.Foot)
// Logs "FFDEMO animals57 ..." and "FFDEMO animals57 done".
[DefaultExecutionOrder(100)]
public class Ffu57 : MonoBehaviour
{
    public static Ffu57 I;
    bool started, allDone; int ph = -1, step; float phW, logT, stepT;
    Vector3 camPos, camLook; bool camOn; Transform camFollow; Vector3 camOff;
    Frog f;
    string[] order = { "house", "pets", "petfeed", "fetch", "ride", "follow", "herd", "robot", "frog", "mech" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        if (Ffu33.Url("ffdemo") == null || Ffu33.Url("ffshot") != "animals57") return;
        I = new GameObject("ffu57").AddComponent<Ffu57>();
        string o = Ffu33.Url("ffr57");
        if (!string.IsNullOrEmpty(o)) I.order = o.Split(',');
        DontDestroyOnLoad(I.gameObject);
    }

    static string Stats()
    {
        return "dressed " + Animals57.Dressed + " frogs " + Animals57.FrogsDressed + " pets " + Animals57.Pets + " feeds " + Animals57.Feeds + " rides " + Animals57.Rides + " fetches " + Animals57.Fetches + " follows " + Animals57.Follows + " arrivals " + Animals57.Arrivals + " robot pets " + Animals57.RobotPets + " robot rides " + Animals57.RobotRides + " mech prints " + Tracks56.MechPrints + " prints " + Tracks56.Prints;
    }
    void Next(string what) { ph++; step = 0; phW = Time.realtimeSinceStartup; camOn = false; camFollow = null; Animals57.DemoSteer = null; Debug.Log("FFDEMO animals57 ph " + ph + " " + (ph < order.Length ? order[ph] : "end") + " " + what + (Animals57.On ? " (ffu57 animals)" : " (OLD animals)") + " " + Stats()); }
    void Cam(Vector3 p, Vector3 look) { camPos = p; camLook = look; camOn = true; camFollow = null; }
    void CamOn(Transform t, Vector3 off, float lookUp) { camFollow = t; camOff = off; camLook = Vector3.up * lookUp; camOn = true; }
    // Animal.All drops animals whose world root is inactive (the house pets while nobody is in the house), so the demo also
    // looks them up among inactive scene objects (cached, at most once a second).
    static readonly Dictionary<string, Animal> petCache = new Dictionary<string, Animal>(); static float petFindT;
    static Animal Pet(string n)
    {
        foreach (var a in Animal.All) if (a != null && a.petName == n) return a;
        Animal c;
        if (petCache.TryGetValue(n, out c) && c != null) return c;
        if (Time.unscaledTime < petFindT) return null;
        petFindT = Time.unscaledTime + 1f;
        foreach (var a in Resources.FindObjectsOfTypeAll<Animal>())
            if (a != null && a.gameObject.scene.IsValid() && a.petName.Length > 0) petCache[a.petName] = a;
        return petCache.TryGetValue(n, out c) ? c : null;
    }
    static Vector3 Beside(Animal a, float d) { return a.transform.position - a.transform.forward * 0.2f + a.transform.right * d; }

    void Update()
    {
        if (Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (!started)
        {
            if (Realism.On && !Realism.Ready) return;
            if (Terrain56.On && !Terrain56.Ready) return;
            if (Pet("Spotty") == null) return;
            started = true;
            f = Game.I.frogs[Game.I.slots[0].frog];
            if (RanchLife.I != null) { Robot.DtCap = 0.25f; Robot.IdleChoreAfter = 9999f; }
            var names = new List<string>(); foreach (var a in Animal.All) if (a != null && a.petName.Length > 0) names.Add(a.petName + "@" + a.transform.position.ToString("0"));
            Debug.Log("FFDEMO animals57 start order " + string.Join(",", order) + " animals " + Animal.All.Count + " named " + string.Join(" ", names.ToArray()) + " skin57 " + (Animals57.Base != null));
            Next("start");
            return;
        }
        if (ph >= order.Length) { if (!allDone) { allDone = true; Debug.Log("FFDEMO animals57 done " + Stats()); } return; }
        float w = Time.realtimeSinceStartup - phW;
        Animal spotty = Pet("Spotty"), germy = Pet("Germy"), daisy = Pet("Daisy");
        switch (order[ph])
        {
            case "house":
                if (step == 0 && spotty != null) { f.SendTo(WorldId.House, Beside(spotty, 2.2f) + Vector3.up * 0.2f, 0f); step = 1; }
                if (spotty != null) Cam(spotty.transform.position + new Vector3(2.4f, 1.3f, -2.2f), spotty.transform.position + new Vector3(-1.5f, 0.2f, 0f));
                if (w > 16f) Next("house done");
                break;
            case "pets":
                {
                    string[] ns = { "Spotty", "Tigy", "Kitty", "Little White Socks", "Germy", "Daisy" };
                    int k = Mathf.Min(ns.Length - 1, (int)(w / 3.5f));
                    var a = Pet(ns[k]);
                    if (a != null) { float s = a.kind == Animal.Kind.Dog ? 1.5f : 1f; CamOn(a.transform, new Vector3(0.9f, 0.55f, 1.1f) * s, 0.25f * s); }
                    if (k != step) { step = k; Debug.Log("FFDEMO animals57 pet close-up " + ns[k] + (a != null ? " at " + a.transform.position.ToString("0.0") : " MISSING")); }
                    if (w > ns.Length * 3.5f) Next("pets done");
                    break;
                }
            case "petfeed":
                if (spotty == null || germy == null || Animals57.I == null) { Next("petfeed skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(spotty, 1.0f) + Vector3.up * 0.2f, 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { Animals57.I.StartPet(Animals57.I.Of(spotty), f, null); step = 2; stepT = w; }
                if (step == 2 && w - stepT > 5f) { f.SendTo(WorldId.House, Beside(germy, 1.3f) + Vector3.up * 0.2f, 0f); step = 3; stepT = w; }
                if (step == 3 && w - stepT > 1.5f) { Animals57.I.StartFeed(Animals57.I.Of(germy), f, null); step = 4; stepT = w; }
                CamOn(step < 3 ? spotty.transform : germy.transform, new Vector3(1.8f, 1.2f, 1.6f), 0.3f);
                if (step == 4 && w - stepT > 5.5f) Next("petfeed done");
                break;
            case "fetch":
                if (germy == null || Animals57.I == null) { Next("fetch skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(germy, 1.5f) + Vector3.up * 0.2f, 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { Animals57.I.StartFetch(Animals57.I.Of(germy), f, null); step = 2; stepT = w; }
                Cam(f.transform.position + new Vector3(-3f, 2.6f, -4f), (f.transform.position + germy.transform.position) * 0.5f);
                if (step == 2 && w - stepT > 16f) Next("fetch done");
                break;
            case "ride":
                if (daisy == null || Animals57.I == null) { Next("ride skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(daisy, 1.0f) + Vector3.up * 0.2f, 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { Animals57.I.Act(f, daisy, "ride"); step = 2; stepT = w; }
                if (step == 2) { Animals57.DemoSteer = new Vector2(Mathf.Sin((w - stepT) * 0.4f), 0.8f); CamOn(daisy.transform, new Vector3(1.6f, 1.1f, -1.8f), 0.4f); }
                if (step == 2 && w - stepT > 12f) { Animals57.DemoSteer = null; if (f.rideAnimal != null) { var s = f.rideSt; f.EndAnimalRide(true); if (s != null) Animals57.I.EndRide(s); } step = 3; stepT = w; }
                if (step == 3 && w - stepT > 2f) Next("ride done " + (f.rideAnimal == null ? "(hopped off)" : "(STILL ON)"));
                break;
            case "follow":
                if (spotty == null || germy == null || Animals57.I == null) { Next("follow skipped"); break; }
                if (step == 0) { f.SendTo(WorldId.House, Beside(spotty, 1.0f) + Vector3.up * 0.2f, 0f); stepT = w; step = 1; }
                if (step == 1 && w - stepT > 1.5f) { Animals57.I.Act(f, spotty, "come"); Animals57.I.Act(f, germy, "come"); step = 2; stepT = w; }
                if (step == 2 && w - stepT > 3f) { Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f; f.SendTo(WorldId.Ranch, new Vector3(hc.x + 6f, Ranch.GY(hc.x + 6f, z1 + 12f) + 0.3f, z1 + 12f), 0f); step = 3; stepT = w; }
                if (step == 3) Cam(f.transform.position + new Vector3(2.5f, 1.8f, -4.5f), f.transform.position);
                if (step == 3 && w - stepT > 10f) { f.SendTo(WorldId.Mars, SurfaceWorlds.M(4f, SurfaceWorlds.MarsY(4f, 60f) + 0.3f, 60f), 0f); step = 4; stepT = w; }
                if (step == 4) Cam(f.transform.position + new Vector3(2.5f, 1.8f, -4.5f), f.transform.position);
                if (step == 4 && w - stepT > 12f)
                {
                    Debug.Log("FFDEMO animals57 follow Mars: Spotty d " + (spotty.transform.position - f.transform.position).magnitude.ToString("0.0") + " Germy d " + (germy.transform.position - f.transform.position).magnitude.ToString("0.0"));
                    Animals57.I.Act(f, spotty, "come"); Animals57.I.Act(f, germy, "come");   // stay on Mars (demo: back to normal)
                    Next("follow done");
                }
                break;
            case "herd":
                {
                    Animal cow = null, horse = null; float bc = 1e9f;
                    foreach (var a in Animal.All) { if (a == null) continue; if (a.kind == Animal.Kind.Cow && (cow == null)) cow = a; if (a.kind == Animal.Kind.Horse && horse == null) horse = a; }
                    if (cow == null) { Next("herd skipped (no cows)"); break; }
                    if (step == 0) { Vector3 c = cow.transform.position; f.SendTo(WorldId.Ranch, c + new Vector3(10f, Ranch.GY(c.x + 10f, c.z) - c.y + 0.3f, 0f), 270f); step = 1; stepT = w; bc = 0f; }
                    Animal tgt = w < 12f || horse == null ? cow : horse;
                    CamOn(tgt.transform, new Vector3(5.5f, 2.6f, 4.5f), 0.9f);
                    if (step == 1 && w - stepT > 6f) { foreach (var a in Animal.All) if (a != null && (a.kind == Animal.Kind.Cow || a.kind == Animal.Kind.Horse)) a.Scare(a.transform.position - a.transform.forward * 3f); step = 2; stepT = w; Debug.Log("FFDEMO animals57 herd scared (gallop)"); }
                    if (step == 2 && w - stepT > 6f) { step = 3; stepT = w; }
                    if (w > 24f) Next("herd done");
                    break;
                }
            case "robot":
                if (RanchLife.I == null || RanchLife.I.robots.Count < 2 || RobotPhone.I == null) { Next("robot skipped (no robots)"); break; }
                {
                    var r = RanchLife.I.robots[1];
                    if (step == 0) { r.battery = 1f; RobotPhone.I.DemoOpen(f, 1, "play:pets"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO animals57 phone play:pets -> '" + (f.toast ?? "") + "'"); step = 1; stepT = w; }
                    if (step == 1 && w - stepT > 50f || step == 1 && r.visit != null && r.visit.verb != "pets" && w - stepT > 15f) { RobotPhone.I.DemoOpen(f, 1, "play:ridepet"); RobotPhone.I.DemoSend(); RobotPhone.I.open = false; Debug.Log("FFDEMO animals57 phone play:ridepet -> '" + (f.toast ?? "") + "'"); step = 2; stepT = w; }
                    CamOn(r.transform, new Vector3(3.5f, 2.4f, -4f), 1f);
                    if (Time.unscaledTime > logT) { logT = Time.unscaledTime + 3f; Debug.Log("FFDEMO animals57 robot " + r.robotName + " world " + r.world + " visit " + (r.visit == null ? "-" : r.visit.verb + "/" + r.visit.vs + "/" + r.visit.line) + " pos " + r.transform.position.ToString("0.0") + " " + Stats()); }
                    if (step == 2 && w - stepT > 45f) Next("robot done");
                }
                break;
            case "frog":
                if (step == 0) { Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f; f.SendTo(WorldId.Ranch, new Vector3(hc.x + 4f, Ranch.GY(hc.x + 4f, z1 + 16f) + 0.3f, z1 + 16f), 200f); step = 1; }
                CamOn(f.transform, new Vector3(1.3f, 1.0f, -1.6f), 0.55f);
                if (w > 12f) Next("frog done");
                break;
            case "mech":
                {
                    Vector2 hc = Layout.HouseC; float z1 = hc.y + Layout.HouseSize.y * 0.5f;
                    Vector3 o = new Vector3(hc.x + 18f, 0f, z1 + 24f);
                    if (step == 0) { f.SendTo(WorldId.Ranch, new Vector3(o.x - 6f, Ranch.GY(o.x - 6f, o.z) + 0.3f, o.z - 6f), 45f); step = 1; stepT = w; }
                    if (step >= 1 && step < 13 && w - stepT > 0.7f) { float x = o.x + step * 2.2f, z = o.z + (step % 2 == 0 ? 0.9f : -0.9f); MechStomp.Foot(new Vector3(x, Ranch.GY(x, z), z), 3.3f, 1f, false); step++; stepT = w; }
                    Cam(o + new Vector3(4f, Ranch.GY(o.x, o.z) + 7f, -9f), o + new Vector3(13f, Ranch.GY(o.x, o.z), 0f));
                    if (w > 16f) Next("mech done (" + Tracks56.MechPrints + " mech prints)");
                    break;
                }
            default: Next("unknown phase"); break;
        }
    }

    void LateUpdate()
    {
        if (!camOn || ph < 0 || ph >= order.Length || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        if (camFollow != null)
        {
            Vector3 p = camFollow.position;
            c.transform.position = p + camFollow.rotation * camOff;
            c.transform.LookAt(p + camLook);
            return;
        }
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
