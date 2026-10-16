using UnityEngine;

// ffu48 hook-up + demo, kept out of Bootstrap / Game (like ffu33): once the ranch + Game exist it builds Spotty's Spot,
// the Carrier Camper and Spotty's Tourer (CarrierRig.Spawn).
//  ?ffdemo=1&ffshot=carrier : P1 (asks Spotty to borrow) rides Spotty's Tourer with the camper hitched down to the pond,
//      the rig drives in -> CARRIER MODE (slow motion while k moves), cruises out, turns, heads back to the shore -> back to
//      bike + camper -> rides up the bank and stops. Logs "FFDEMO carrier ..." every second + "carrier48 done".
//      &cshot=side|low|deck picks the camera for the transform (default side).
[DefaultExecutionOrder(100)]
public class Ffu48 : MonoBehaviour
{
    public static Ffu48 I;
    bool built; string shot = "";
    float t0 = -1f, logT, phT; int ph;
    Vector3 camPos, camLook; bool camInit;
    Frog f; Vector3 entry;
    float turned; float yaw0;
    static float minK = 9f, maxK = -1f; static int deckFrames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu48").AddComponent<Ffu48>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (!built)
        {
            if (Bootstrap.I == null || Game.I == null || Time.timeSinceLevelLoad < 0.5f) return;
            built = true;
            try { CarrierRig.Spawn(); } catch (System.Exception e) { Debug.LogError("ffu48 carrier: " + e); }
            shot = Ffu33.Url("ffdemo") != null ? (Ffu33.Url("ffshot") ?? "") : "";
            if (shot != "carrier") shot = "";
            if (shot.Length > 0) Debug.Log("FFDEMO ffu48 demo " + shot);
        }
        if (shot.Length == 0 || Game.I.state != Game.State.Play || Game.I.slots.Count == 0 || CarrierRig.I == null) return;
        var rig = CarrierRig.I;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Frog.DtCap = 0.15f; var bai = FindObjectOfType<BikeAI>(); if (bai != null) bai.enabled = false; }
        float t = Time.time - t0;
        phT += Time.deltaTime;
        // slow motion while it transforms (either way)
        Time.timeScale = rig.k > 0.001f && rig.k < 0.999f ? 0.4f : 1f;
        Bike b = rig.bike;
        switch (ph)
        {
            case 0:
                if (t < 4f) break;
                if (f.vehicle != null) f.ExitVehicle();
                f.SendTo(WorldId.Ranch, b.transform.position - b.transform.right * 1.3f + Vector3.up * 0.2f, b.transform.eulerAngles.y);
                f.EnterVehicle(b);
                entry = new Vector3(Layout.PondC.x, 0f, Layout.PondC.y);
                b.demoHook = i => DriveTo(b, entry, 7f);
                Next(1); Debug.Log("FFDEMO carrier start: P1 on " + b.Title + " hitched=" + rig.trailer.Hitched + " at " + b.transform.position.ToString("0.0"));
                break;
            case 1:
                if (rig.k > 0f) { Next(2); Debug.Log("FFDEMO carrier transform begins at " + rig.transform.position.ToString("0.0")); }
                else if (phT > 40f) { Debug.Log("FFDEMO carrier never reached deep water (stuck?) - forcing"); rig.ForceCarrier(rig.transform.position + rig.transform.forward * 20f, rig.transform.eulerAngles.y); Next(2); }
                break;
            case 2:
                if (rig.Carrier) { Next(3); yaw0 = rig.transform.eulerAngles.y; turned = 0f; rig.helm.demoHook = i => { var o = new PIn(); o.gas = 0.8f; return o; }; Debug.Log("FFDEMO carrier CARRIER MODE reached; helm driver " + (rig.helm.driver != null ? rig.helm.driver.nick : "none")); }
                break;
            case 3:
                if (phT > 7f) { Next(4); rig.helm.demoHook = i => { var o = new PIn(); o.gas = 0.7f; o.move = new Vector2(1f, 0f); return o; }; }
                break;
            case 4:
                {
                    float dy = Mathf.DeltaAngle(yaw0, rig.transform.eulerAngles.y);
                    if (Mathf.Abs(dy) > 165f || phT > 40f)
                    {
                        Next(5);
                        Vector2 s = CarrierLayout.Site; Vector3 home = new Vector3(s.x, 0f, s.y);
                        rig.helm.demoHook = i => SteerTo(rig, home, 0.8f);
                    }
                }
                break;
            case 5:
                if (!rig.toCarrier) { Next(6); Debug.Log("FFDEMO carrier reversing at " + rig.transform.position.ToString("0.0")); }
                else if (phT > 60f) { Debug.Log("FFDEMO carrier never reached the shore - reversing by hand"); rig.Begin(false); Next(6); }
                break;
            case 6:
                if (rig.k <= 0f && !rig.toCarrier)
                {
                    Next(7);
                    Vector2 s = CarrierLayout.Site; Vector3 home = new Vector3(s.x, 0f, s.y);
                    b.demoHook = i => DriveTo(b, home, 6f);
                    Debug.Log("FFDEMO carrier back to bike + camper: rider on bike " + (f.vehicle == b) + " hitched " + (rig.trailer.Hitched && rig.trailer.tower == b));
                }
                break;
            case 7:
                {
                    Vector2 s = CarrierLayout.Site;
                    float d = new Vector2(b.transform.position.x - s.x, b.transform.position.z - s.y).magnitude;
                    float sd, dd; bool wet = Water.At(b.transform.position.x, b.transform.position.z, out sd, out dd);
                    if ((!wet && phT > 6f && d < 30f) || phT > 40f)
                    {
                        b.demoHook = i => { var o = new PIn(); o.brake = 1f; return o; };
                        Next(8);
                    }
                }
                break;
            case 8:
                if (phT > 4f) { Next(9); Debug.Log("FFDEMO carrier48 done: transforms " + CarrierRig.Transforms + " bike at " + b.transform.position.ToString("0.0") + " wet " + Water.At(b.transform.position.x, b.transform.position.z, out float _a, out float _b) + " hitched " + (rig.trailer.Hitched && rig.trailer.tower == b) + " deckFrames " + deckFrames + " t=" + t.ToString("0")); }
                break;
        }
        if (Time.unscaledTime > logT)
        {
            logT = Time.unscaledTime + 1f;
            Vector3 p = rig.transform.position;
            Debug.Log("FFDEMO carrier ph " + ph + " t " + t.ToString("0.0") + " k " + rig.k.ToString("0.00") + " to " + rig.toCarrier + " pos " + p.ToString("0.0") + " y " + p.y.ToString("0.00") + " up " + rig.transform.up.y.ToString("0.00") + " v " + rig.trailer.rb.velocity.magnitude.ToString("0.0") + " bike " + b.transform.position.ToString("0.0") + " carried " + b.carried + " hitched " + rig.trailer.Hitched + " f.veh " + (f.vehicle != null ? f.vehicle.Title : "-") + " wl " + (Ripples.SurfaceY - p.y).ToString("0.00"));
        }
        Cam(rig, b, t);
    }

    void Next(int p) { ph = p; phT = 0f; }

    static PIn DriveTo(Bike b, Vector3 tgt, float vt)
    {
        var o = new PIn();
        Vector3 d = tgt - b.transform.position; d.y = 0f;
        Vector3 fw = b.transform.forward; fw.y = 0f;
        float ang = Vector3.SignedAngle(fw, d, Vector3.up);
        o.move = new Vector2(Mathf.Clamp(ang / 30f, -1f, 1f), 0f);
        float fs = b.ForwardSpeed;
        if (fs < vt) o.gas = Mathf.Clamp01((vt - fs) * 0.3f + 0.25f); else o.brake = Mathf.Clamp01((fs - vt) * 0.3f);
        return o;
    }
    static PIn SteerTo(CarrierRig rig, Vector3 tgt, float gas)
    {
        var o = new PIn();
        Vector3 d = tgt - rig.transform.position; d.y = 0f;
        Vector3 fw = rig.transform.forward; fw.y = 0f;
        float ang = Vector3.SignedAngle(fw, d, Vector3.up);
        o.move = new Vector2(Mathf.Clamp(ang / 25f, -1f, 1f), 0f);
        o.gas = gas;
        return o;
    }

    void Cam(CarrierRig rig, Bike b, float t)
    {
        Vector3 c = rig.transform.position;
        Vector3 fw = rig.transform.forward; fw.y = 0f; fw.Normalize();
        Vector3 rt = new Vector3(fw.z, 0f, -fw.x);
        string cs = Ffu33.Url("cshot") ?? "side";
        Vector3 p, look; float ease = 2f;
        if (ph <= 1) { Vector3 bc = b.transform.position; p = bc - b.transform.forward * 9f + rt * 7f + Vector3.up * 4f; look = (bc + c) * 0.5f + Vector3.up; }
        else if (ph == 2 || ph == 6)
        {
            if (cs == "low") { p = c + rt * 22f + fw * 8f; p.y = Ripples.SurfaceY + 0.6f; look = c + Vector3.up * 2.5f; }
            else if (cs == "deck") { p = c + rt * 10f + fw * 18f + Vector3.up * 9f; look = c + Vector3.up * 2f; }
            else { p = c + rt * 26f + fw * 4f + Vector3.up * 7f; look = c + Vector3.up * 1.5f; }
            ease = 1f;
        }
        else if (ph >= 3 && ph <= 5) { p = c - fw * 30f + rt * 14f + Vector3.up * 14f; look = c + Vector3.up * 3f + fw * 4f; }
        else { Vector3 bc = b.transform.position; p = bc - b.transform.forward * 10f + rt * 6f + Vector3.up * 5f; look = bc + Vector3.up; }
        if (!camInit) { camPos = p; camLook = look; camInit = true; }
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * ease);
        camPos = Vector3.Lerp(camPos, p, k); camLook = Vector3.Lerp(camLook, look, k);
        if (CarrierRig.OnDeck(f.transform.position)) deckFrames++;
    }

    void LateUpdate()
    {
        if (shot.Length == 0 || !camInit || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam;
        if (c == null) return;
        c.transform.position = camPos;
        c.transform.LookAt(camLook);
    }
}
