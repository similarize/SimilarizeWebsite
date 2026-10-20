using UnityEngine;

// ffu66 demo for the probe: ?ffdemo=1&ffshot=dad66 - Dad = a full froggy. He steps out of the house as an AI froggy agent (not a
// roster seat), hops about, swims in the pond, sits in the garden (swing / hammock), comes to play ("Dad, come play" phone order),
// drives HIS OWN Cybertruck round the ranch ("Dad, go for a drive") and parks it back in bay 1, then goes back in the house.
// Logs "DAD66T PASS|FAIL ...", "DAD66T RESULT n/m", "FFDEMO dad66 ph N <name>", "FFDEMO dad66 done".
[DefaultExecutionOrder(30003)]
public class Ffu66 : MonoBehaviour
{
    public static Ffu66 I;
    string shot = ""; bool done; float t0 = -1f, phT; int ph = -1, pass, total;
    Frog f; Vector3 p0; float moved; int c0; bool camOn = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        I = new GameObject("ffu66").AddComponent<Ffu66>();
        DontDestroyOnLoad(I.gameObject);
    }

    void Update()
    {
        if (shot.Length == 0) { string s = Ffu33.Url("ffdemo") != null ? Ffu33.Url("ffshot") : null; if (s != "dad66") { enabled = false; return; } shot = s; Frog.DtCap = 0.1f; }
        if (done || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        if (t0 < 0f) { t0 = Time.time; f = Game.I.frogs[Game.I.slots[0].frog]; Debug.Log("FFDEMO dad66 start p1 " + f.nick); }
        phT += Time.deltaTime;
        try { Run(); } catch (System.Exception e) { Debug.LogError("FFDEMO dad66 error " + e); Done(); }
    }
    void Next(int p, string name) { ph = p; phT = 0f; Debug.Log("FFDEMO dad66 ph " + p + " " + name + " t " + (Time.time - t0).ToString("0.0")); }
    void Check(string what, bool ok, string detail) { total++; if (ok) pass++; Debug.Log("DAD66T " + (ok ? "PASS" : "FAIL") + " " + what + " | " + detail); }
    void Done() { if (done) return; done = true; Debug.Log("DAD66T RESULT " + pass + "/" + total); Debug.Log("FFDEMO dad66 done"); }
    Frog D { get { return DadFroggy.F; } }
    Vector3 DP { get { return D == null ? Vector3.zero : D.vehicle != null ? D.vehicle.transform.position : D.transform.position; } }
    void Put(Vector3 p) { if (D != null) { if (D.vehicle != null) D.ExitVehicle(); D.Teleport(new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.4f, p.z)); } }
    void FrogTo(Vector3 p) { if (f.vehicle != null) f.ExitVehicle(); f.SendTo(WorldId.Ranch, new Vector3(p.x, Ranch.GY(p.x, p.z) + 0.3f, p.z), 0f); }

    void Run()
    {
        switch (ph)
        {
            case -1:
                if (phT > 4f && Dad.I != null)
                {
                    FrogTo(Dad.PorchPoint() + new Vector3(4f, 0f, 3f));
                    Dad.I.OutNow("hop", null);
                    Next(0, "out");
                }
                else if (phT > 20f) { Check("Dad exists", false, "no house Dad"); Done(); }
                break;
            case 0:
                if (phT > 1.5f)
                {
                    bool ok = D != null && D.isDad && D.gameObject.activeSelf && !Game.I.frogs.Contains(D) && D.model != null && D.model.transform.localScale.x > 1.3f && D.cc != null;
                    Check("Dad is a froggy agent on the ranch (1.5x froggy, CharacterController, NOT a roster seat)", ok, D == null ? "no agent" : "world " + D.world + " in Game.frogs " + Game.I.frogs.Contains(D) + " roster " + Game.I.frogs.Count + " scale " + D.model.transform.localScale.x.ToString("0.0") + " house Dad outside " + Dad.I.outside);
                    if (D == null) { Done(); return; }
                    p0 = D.transform.position; moved = 0f; c0 = DadFroggy.Hops;
                    Next(1, "dadhop");
                }
                break;
            case 1:
                moved = Mathf.Max(moved, Vector3.Distance(D.transform.position, p0));
                if (phT > 10f)
                {
                    Check("Dad hops about the lawn", DadFroggy.Hops - c0 >= 3 && moved > 3f, "hops " + (DadFroggy.Hops - c0) + " moved " + moved.ToString("0.0") + " m act " + D.DadAct);
                    Put(Frog.DadPond(D.transform.position, 1.06f, 0f));
                    FrogTo(Frog.DadPond(D.transform.position, 1.1f, 0.03f));
                    DadFroggy.Order(f, "swim"); c0 = DadFroggy.Swims;
                    Next(2, "dadswim");
                }
                break;
            case 2:
                if (DadFroggy.Swims > c0 && D.DadSwimming && phT > 6f || phT > 70f)
                {
                    Check("Dad swims in the pond (\"Dad, swim with me\")", DadFroggy.Swims > c0, "swims " + (DadFroggy.Swims - c0) + " swimming " + D.DadSwimming + " at " + D.transform.position.ToString("0.0") + " act " + D.DadAct);
                    var g = Garden.I;
                    if (g != null) { Put(g.C + new Vector3(3f, 0f, 3f)); FrogTo(g.C + new Vector3(-3f, 0f, 4f)); }
                    D.DadForce("swing"); c0 = DadFroggy.Seats;
                    Next(3, "dadswing");
                }
                break;
            case 3:
                if (DadFroggy.Seats > c0 && phT > 4f || phT > 70f)
                {
                    Check("Dad sits on a swing / hammock in the garden", DadFroggy.Seats > c0 && D.vehicle is GardenSeat, "seats " + (DadFroggy.Seats - c0) + " vehicle " + (D.vehicle != null ? D.vehicle.Title : "-"));
                    FrogTo(new Vector3(-10f, 0f, 40f));
                    Put(new Vector3(-2f, 0f, 46f));
                    DadFroggy.Order(f, "play"); c0 = DadFroggy.Plays;
                    Next(4, "dadplay");
                }
                break;
            case 4:
                if (DadFroggy.Plays > c0 && phT > 5f || phT > 60f)
                {
                    float d = Vector3.Distance(D.transform.position, f.transform.position);
                    Check("Dad comes to play (\"Dad, come play\")", DadFroggy.Plays > c0 && d < 5f, "plays " + (DadFroggy.Plays - c0) + " dist " + d.ToString("0.0") + " m act " + D.DadAct);
                    var t = DadFroggy.Truck();
                    if (t != null) Put(t.transform.position - t.transform.right * 3f);
                    DadFroggy.Order(f, "drive"); c0 = DadFroggy.Drives; moved = 0f;
                    Next(5, "daddrive");
                }
                break;
            case 5:
                {
                    var t = DadFroggy.Truck();
                    if (t != null && D.vehicle == t) moved = Mathf.Max(moved, Vector3.Distance(t.transform.position, t.HomePos));
                    if (moved > 12f && phT > 8f || phT > 120f)
                    {
                        Check("Dad drives HIS OWN Cybertruck round the ranch (\"Dad, go for a drive\")", t != null && DadFroggy.Drives > c0 && moved > 12f, "truck " + (t != null ? t.Title + " owner " + t.ownerName : "none") + " drives " + (DadFroggy.Drives - c0) + " max from bay " + moved.ToString("0.0") + " m in it " + (t != null && D.vehicle == t));
                        c0 = DadFroggy.Parks;
                        Next(6, "dadpark");
                    }
                    break;
                }
            case 6:
                {
                    var t = DadFroggy.Truck();
                    if (DadFroggy.Parks > c0 && phT > 3f || phT > 240f)
                    {
                        float d = t != null ? Vector3.Distance(t.transform.position, t.HomePos) : 99f;
                        Check("Dad parks it back in his bay", DadFroggy.Parks > c0 && d < 2f && D.vehicle == null, "parks " + (DadFroggy.Parks - c0) + " " + d.ToString("0.0") + " m from the bay spot, Dad in vehicle " + (D.vehicle != null));
                        Put(Dad.PorchPoint() + new Vector3(2.5f, 0f, 2f));
                        D.DadForce("home"); c0 = DadFroggy.Homes;
                        Next(7, "dadhome");
                    }
                    break;
                }
            case 7:
                if (DadFroggy.Homes > c0 || phT > 60f)
                {
                    Check("Dad goes back in the house (life loop resumes)", DadFroggy.Homes > c0 && !DadFroggy.Active && Dad.I != null && !Dad.I.outside, "homes " + (DadFroggy.Homes - c0) + " agent active " + DadFroggy.Active + " house Dad outside " + (Dad.I != null && Dad.I.outside));
                    camOn = false; Next(8, "end");
                }
                break;
            case 8:
                if (phT > 2f) Done();
                break;
        }
    }

    void LateUpdate()
    {
        if (!camOn || done || ph < 0 || D == null || !D.gameObject.activeSelf || Game.I == null || Game.I.slots.Count == 0) return;
        Camera c = Game.I.slots[0].cam; if (c == null) return;
        Vector3 p = DP; bool car = D.vehicle != null && !(D.vehicle is GardenSeat);
        Vector3 back = (D.vehicle != null ? D.vehicle.transform.forward : D.transform.forward); back.y = 0f; if (back.sqrMagnitude < 0.01f) back = Vector3.forward; back.Normalize();
        c.transform.position = p - back * (car ? 9f : 4.2f) + Vector3.up * (car ? 4.5f : 2.4f) + Vector3.Cross(Vector3.up, back) * 1.2f;
        c.transform.LookAt(p + Vector3.up * 1.0f);
    }
}
