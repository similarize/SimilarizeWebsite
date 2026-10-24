using System.Collections.Generic;
using UnityEngine;

// ffarc66 SAVE + RESUME (Ben 2026-10-24): right before an arcade sign opens another game, every playing froggy's spot is saved
// (PlayerPrefs "ff.resume66", PlayerPrefs.Save(); IndexedDB on the web). Coming back within 2 h: once the game is in Play the
// players are put back where they were (by slot order, the same froggy first): on the ranch on foot = the exact spot + facing;
// anyone in a vehicle / mech or off Earth (Moon, Mars, space ...) comes back on foot next to the sign that was used (vehicles,
// mechs and the Starship are not rebuilt - they start at home). Then the snapshot is cleared. Mowed grass already persists.
public class Resume66 : MonoBehaviour
{
    const string Key = "ff.resume66";
    const double MaxAgeH = 2.0;
    public static int Saved, Restored;
    static Resume66 I;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null || ArcadeSigns66.Old) return;
        I = new GameObject("Resume66").AddComponent<Resume66>();
        DontDestroyOnLoad(I.gameObject);
    }

    public static void Save(Vector3 signPos, float signYaw)
    {
        try
        {
            var g = Game.I;
            if (g == null) return;
            var sb = new System.Text.StringBuilder();
            sb.Append(System.DateTime.UtcNow.Ticks).Append('|').Append(F(signPos.x)).Append(',').Append(F(signPos.y)).Append(',').Append(F(signPos.z)).Append(',').Append(F(signYaw));
            int n = 0;
            foreach (var s in g.slots)
            {
                if (s == null || s.frog < 0 || s.frog >= g.frogs.Count) continue;
                Frog f = g.frogs[s.frog];
                if (f == null) continue;
                bool foot = f.world == WorldId.Ranch && f.vehicle == null && f.passengerOf == null && f.rideAnimal == null;
                Vector3 p = f.transform.position;
                sb.Append('|').Append(f.charId).Append(',').Append(foot ? 1 : 0).Append(',').Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z)).Append(',').Append(F(f.transform.eulerAngles.y));
                n++;
            }
            PlayerPrefs.SetString(Key, sb.ToString());
            PlayerPrefs.Save();
            Saved++;
            Debug.Log("RESUME66 saved " + n + " player(s)");
        }
        catch (System.Exception e) { Debug.LogWarning("RESUME66 save failed: " + e.Message); }
    }
    static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    static float P(string v) { return float.Parse(v, System.Globalization.CultureInfo.InvariantCulture); }

    float playT = -1f; bool done;
    void Update()
    {
        if (done) return;
        var g = Game.I;
        if (g == null || g.state != Game.State.Play) { playT = -1f; return; }
        if (playT < 0f) playT = 0f;
        playT += Time.deltaTime;
        if (playT < 1.5f) return;   // after the game's own spawn
        done = true;
        Apply(g);
    }

    static void Apply(Game g)
    {
        string v = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(v)) return;
        PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();   // one resume only; a later visit starts fresh
        try
        {
            string[] parts = v.Split('|');
            double ageH = (System.DateTime.UtcNow - new System.DateTime(long.Parse(parts[0]), System.DateTimeKind.Utc)).TotalHours;
            if (ageH < 0 || ageH > MaxAgeH) { Debug.Log("RESUME66 snapshot " + ageH.ToString("0.0") + " h old - fresh start"); return; }
            string[] sg = parts[1].Split(',');
            Vector3 sp = new Vector3(P(sg[0]), P(sg[1]), P(sg[2])); float syaw = P(sg[3]);
            var rows = new List<string[]>();
            for (int i = 2; i < parts.Length; i++) rows.Add(parts[i].Split(','));
            var used = new HashSet<int>();
            var frogs = new List<Frog>();
            foreach (var s in g.slots) if (s != null && s.frog >= 0 && s.frog < g.frogs.Count && g.frogs[s.frog] != null) frogs.Add(g.frogs[s.frog]);
            int k = 0;
            foreach (Frog f in frogs)
            {
                int pick = -1;
                for (int i = 0; i < rows.Count; i++) if (!used.Contains(i) && int.Parse(rows[i][0]) == f.charId) { pick = i; break; }
                if (pick < 0) for (int i = 0; i < rows.Count; i++) if (!used.Contains(i)) { pick = i; break; }
                Vector3 p; float yaw;
                if (pick >= 0 && rows[pick][1] == "1") { p = new Vector3(P(rows[pick][2]), P(rows[pick][3]), P(rows[pick][4])); yaw = P(rows[pick][5]); used.Add(pick); }
                else
                {
                    if (pick >= 0) used.Add(pick);
                    Quaternion q = Quaternion.Euler(0f, syaw, 0f);
                    p = sp + q * new Vector3(-1.5f + 1.2f * k, 0f, 2.5f); p.y = Ranch.GY(p.x, p.z) + 0.1f; yaw = syaw + 180f;
                }
                if (f.vehicle != null || f.passengerOf != null) continue;
                f.SendTo(WorldId.Ranch, p, yaw);
                k++; Restored++;
            }
            if (k > 0 && frogs.Count > 0) frogs[0].Toast("Welcome back! Right where you left off", 2.5f);
            Debug.Log("RESUME66 restored " + k + " player(s) (" + ageH.ToString("0.00") + " h old)");
        }
        catch (System.Exception e) { Debug.LogWarning("RESUME66 restore failed: " + e.Message); }
    }
}
