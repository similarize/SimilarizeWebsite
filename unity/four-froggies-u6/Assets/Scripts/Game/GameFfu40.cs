using UnityEngine;

// ffu40 demo shot for the probe (?ffdemo=1&ffshot=fig03charge):
//   Figure 03 starts a chore ~10 m from his own wireless pad (outside the garage's west wall) at 12% battery; at t~3 s the
//   phone's "Go charge" order is sent; he walks onto the pad, stands still upright and charges (green pulsing coil ring,
//   soft sparkle, battery bar + tag). The phone opens on him at t~45 s to show the battery %.
//   Logs "FFDEMO fig03charge t pos dPad status battery charging padUser" every second.
public partial class Game
{
    static bool Is40(string sc) { return sc == "fig03charge"; }
    Robot f40R; Frog f40F; float f40T, f40Log; int f40Phase;

    void Demo40Start(Frog f, string sc)
    {
        if (f.vehicle != null) f.ExitVehicle();
        if (f.world != WorldId.Ranch) f.SendTo(WorldId.Ranch, Ranch.FrogSpawn(f.id), 0f);
        f40F = f; f40T = 0f; f40Log = 0f; f40Phase = 0; f40R = null;
        var rl = RanchLife.I;
        if (rl != null) foreach (var r in rl.robots) if (r.id == "figure03") f40R = r;
        if (f40R == null || RanchJobs.I == null || RanchJobs.I.figPad == null) { Debug.Log("FFDEMO fig03charge: no Figure 03 / pad"); return; }
        Robot.DtCap = 0.4f;   // SwiftShader ~3 fps: keep robot game time near wall time
        f40R.DemoStart(new Vector3(-39f, 0f, 34f), 120f, Chores.Litter, 0.12f);
        f.DemoPose(new Vector3(-37.5f, 0f, 24f), 50f);
        var r3 = f40R;
        Debug.Log("FFDEMO fig03charge start: pad (" + RanchJobs.FigPadP.x + ", " + RanchJobs.FigPadP.y + ") drainMul " + r3.drainMul.ToString("0.00000") + " chargeSecs " + r3.chargeSecs +
            " lowAt " + r3.lowAt + " | full battery: idle " + (420f / r3.drainMul / 60f).ToString("0") + " min, chores (60% walk / 35% work) " + (182.6f / r3.drainMul / 60f).ToString("0") +
            " min, walking nonstop " + (1f / (r3.drainMul * (1f / 420f + 1f / 300f)) / 60f).ToString("0") + " min; empty -> full " + (r3.chargeSecs / 60f).ToString("0.0") + " min");
    }

    bool Demo40Cam(Frog f, string sc, out Vector3 pos, out Vector3 look)
    {
        pos = look = Vector3.zero;
        if (f40R == null) return false;
        float dt = Mathf.Min(Time.deltaTime, 0.4f);
        f40T += dt; f40Log -= Time.unscaledDeltaTime;
        var pad = RanchJobs.I.figPad;
        if (f40Phase == 0 && f40T > 3f)
        {
            f40Phase = 1;
            Debug.Log("FFDEMO fig03charge -> phone Go charge: " + f40R.Order("charge", f));
        }
        else if (f40Phase == 1 && f40R.Charging) { f40Phase = 2; Debug.Log("FFDEMO fig03charge -> charging on pad t=" + f40T.ToString("0.0") + " battery " + f40R.Pct); }
        else if (f40Phase == 2 && f40T > 45f && RobotPhone.I != null)
        {
            f40Phase = 3;
            RobotPhone.I.Toggle(f);
            var rl = RanchLife.I;
            int idx = rl.robots.IndexOf(f40R);
            Debug.Log("FFDEMO fig03charge -> phone open (Figure 03 = row " + idx + ")");
        }
        Vector3 c = pad.stand;
        if (f40Phase < 2 && RobotNav.Flat(f40R.transform.position - c) > 6f)
        {
            // follow him in, from behind-left, until he is near the pad
            Vector3 rp = f40R.transform.position;
            pos = Vector3.Lerp(rp, c, 0.5f) + new Vector3(-7f, 4.2f, -7.5f);
            look = Vector3.Lerp(rp, c, 0.5f) + Vector3.up * 1f;
        }
        else
        {
            pos = c + new Vector3(-5.2f, 2.5f, -4.4f);
            look = c + new Vector3(0.2f, 1.0f, 0.2f);
        }
        if (f40Log <= 0f)
        {
            f40Log = 1f;
            Vector3 rp = f40R.transform.position;
            Debug.Log("FFDEMO fig03charge t=" + f40T.ToString("0.0") + " pos (" + rp.x.ToString("0.0") + ", " + rp.y.ToString("0.00") + ", " + rp.z.ToString("0.0") + ") dPad " + RobotNav.Flat(rp - c).ToString("0.00") +
                " status '" + f40R.StatusLine + "' battery " + (f40R.battery * 100f).ToString("0.0") + "% charging " + f40R.Charging + " padUser " + (pad.user != null ? pad.user.robotName : "-"));
        }
        return true;
    }
}
