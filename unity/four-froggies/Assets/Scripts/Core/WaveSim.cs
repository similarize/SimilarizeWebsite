using System.Collections.Generic;
using UnityEngine;

// ffu38: the pond surface really responds to whatever wades through it.
//  * GPU height-field ripple sim (Resources/FFWaveSim.shader): a square window of the ranch pond that follows the view
//    (96 m at 256 px on desktop, 80 m at 160 px on phones / Tesla), stepped at 30 Hz with ping-pong ARGBHalf render
//    textures. Sources (Ripples.Press / Kick) are stamped in each step: legs, hulls and bodies crossing the surface press
//    the water down, so walking / driving pushes out a bow wave and a V wake; footfalls and splashes kick rings out.
//    Foam (contact lines round each leg / hull, wake foam, whitecaps) lives in the same texture.
//  * Analytic big slow swells (Ripples.Swell) from mechs: wide rings with a deep-water speed, over the whole pond.
//  * The water shaders (FF/RealWater, LB/Water via Shaders/FFRipple.cginc) read it for normals + foam; with the realistic
//    water a dense patch mesh (128 x 128 desktop / 72 x 72 phones) over the window displaces its vertices for real and
//    the big flat pond plane skips that square.
//  * ffu39 real-pond look (Bill: "looks like foam"): B is now SILT (muddy sediment stirred up where legs / wheels / feet
//    reach the bed: spreads, settles over ~10 s, tints the water brown-green), A is brief white SPRAY (only real splashes
//    and footfalls, gone in ~1 s); no whitecaps, no contact foam. Wave heights ~half of ffu38 (AmpK) - a wading person
//    makes 2-5 cm ripples, a 10-story mech tens of cm, not metres. Contact circles (_FFCont) give the water shader a thin
//    meniscus + wet dark line round each leg / hull.
//  * Fallback: no half-float render textures (or ?waves=0) -> no sim; the particle rings / splashes of ffw / ffu37 stay.
//    The window is idle (no steps, no patch) whenever it doesn't touch the pond.
public class WaveSim : MonoBehaviour
{
    public static WaveSim I;
    public static bool Active { get { return I != null && I.on; } }
    public float Size = 96f; public int Res = 256;
    public const float Rate = 30f, WaveC = 2.4f;   // m/s ripple speed (slow enough that a wading mech / truck leaves a V)
    RenderTexture a, b;
    Material sim;
    bool on, ok;
    Vector2 origin;            // world xz of the window's (0,0) corner (texel-snapped)
    float acc;
    GameObject patch; Material patchSrc, patchMat;
    Renderer plane;

    struct Src { public Vector2 p, v; public float r, amt, foam, ring, str, silt; public int mode; public float seen; }
    public const float AmpK = 0.5f;   // ffu39: real-water wave heights (see header)
    readonly Dictionary<int, Src> press = new Dictionary<int, Src>();
    readonly List<Src> kicks = new List<Src>();
    readonly List<int> drop = new List<int>();
    const int MaxSrc = 20;
    readonly Vector4[] sa = new Vector4[MaxSrc], sb = new Vector4[MaxSrc], sc = new Vector4[MaxSrc];
    readonly Vector4[] cont = new Vector4[8];   // ffu39: contact circles for the meniscus (x, z, r, k)
    // swells
    readonly Vector4[] sw = new Vector4[4], swk = new Vector4[4];
    readonly float[] swAge = new float[4], swC = new float[4], swAmp = new float[4];
    public static int Steps;   // probe stat

    public static void Init(GameObject water)
    {
        if (I != null) return;
        var go = new GameObject("PondWaveSim");
        I = go.AddComponent<WaveSim>();
        I.plane = water.GetComponent<Renderer>();
        I.Setup();
    }

    void Setup()
    {
        string url = "";
        try { url = Application.absoluteURL ?? ""; } catch { }
        bool off = url.Contains("waves=0") || url.Contains("waves=off");
        bool mobile = Look.Mobile || (Realism.On && Realism.Lite);
        if (mobile) { Size = 80f; Res = 160; }
        if (off) { Debug.Log("WaveSim: off (?waves=0) - particle ripples only"); return; }
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) { Debug.Log("WaveSim: no half-float render textures - particle ripples only"); return; }
        Shader s = Resources.Load<Shader>("FFWaveSim");
        if (s == null || !s.isSupported) { Debug.Log("WaveSim: sim shader " + (s == null ? "missing" : "unsupported") + " - particle ripples only"); return; }
        sim = new Material(s);
        a = NewRT(); b = NewRT();
        Clear(a); Clear(b);
        float dx = Size / Res, dt = 1f / Rate;
        float k = Mathf.Min(0.45f, WaveC * WaveC * dt * dt / (dx * dx));
        sim.SetVector("_Shift", new Vector4(0f, 0f, k, 0.9955f));
        sim.SetVector("_Foam", new Vector4(0.9972f, 0.3f, 0.9f, 0f));   // ffu39: silt settle / spread per step, spray decay per step
        ok = true;
        Shader.SetGlobalTexture("_FFRipTex", a);
        Shader.SetGlobalVector("_FFRipRect", Vector4.zero);
        Shader.SetGlobalVector("_FFRipP", new Vector4(1f / Res, dx, 0f, 1.25f));
        Shader.SetGlobalVectorArray("_FFCont", cont);
        Debug.Log("WaveSim: on " + Res + " px / " + Size + " m (" + dx.ToString("0.00") + " m/px), k " + k.ToString("0.000") + (mobile ? " (lite)" : ""));
    }

    RenderTexture NewRT()
    {
        var r = new RenderTexture(Res, Res, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        r.wrapMode = TextureWrapMode.Clamp; r.filterMode = FilterMode.Bilinear; r.useMipMap = false; r.name = "PondWaves";
        r.Create();
        return r;
    }
    static void Clear(RenderTexture r) { var o = RenderTexture.active; RenderTexture.active = r; GL.Clear(false, true, Color.clear); RenderTexture.active = o; }

    // ---------------- sources ----------------
    public void AddPress(int key, Vector3 p, Vector3 v, float r, float depth, float foam, float ring, float str, float silt = -1f)
    {
        var s = new Src { p = new Vector2(p.x, p.z), v = new Vector2(v.x, v.z), r = r, amt = -depth * AmpK, foam = foam, ring = ring, str = str, silt = silt, mode = 1, seen = Time.time };
        press[key] = s;
    }
    public void AddKick(Vector3 p, float r, float amt, float foam, float ring, float silt = -1f)
    {
        if (kicks.Count >= 12) return;
        kicks.Add(new Src { p = new Vector2(p.x, p.z), r = r, amt = amt * AmpK, foam = foam, ring = ring, silt = silt, mode = 0 });
    }
    public bool Inside(Vector3 p, float margin = 4f)
    {
        if (!on) return false;
        return p.x > origin.x + margin && p.z > origin.y + margin && p.x < origin.x + Size - margin && p.z < origin.y + Size - margin;
    }

    public static void AddSwell(Vector3 p, float lambda, float amp, float r0)
    {
        if (I == null) return;
        int i = 0;   // replace the weakest (or a free) slot so a live swell never pops off
        for (int j = 1; j < 4; j++) if (I.sw[j].w < I.sw[i].w) i = j;
        float k = 2f * Mathf.PI / lambda;
        I.swC[i] = Mathf.Sqrt(9.81f * lambda / (2f * Mathf.PI));   // deep-water wave speed: long swells travel slow-ish but steady
        I.swAge[i] = 0f; I.swAmp[i] = amp;
        I.sw[i] = new Vector4(p.x, p.z, 0f, amp);
        I.swk[i] = new Vector4(k, lambda * 0.9f, r0, Mathf.Clamp(lambda * 3.5f, 30f, 200f));   // w = how far it runs (C# only)
    }

    // ---------------- per frame ----------------
    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.25f);
        TickSwells(dt);
        if (!ok) return;
        // window centre: ahead of the main view camera, on the water
        Camera cam = ViewCam();
        bool want = false;
        Vector2 c = Vector2.zero;
        if (cam != null)
        {
            Vector3 cp = cam.transform.position, f = cam.transform.forward;
            Vector2 ff = new Vector2(f.x, f.z);
            float dist = Size * 0.22f;
            if (f.y < -0.05f) dist = Mathf.Clamp((cp.y - Layout.WaterY) / -f.y, 0f, Size * 0.3f);
            c = new Vector2(cp.x, cp.z) + (ff.sqrMagnitude > 1e-4f ? ff.normalized : Vector2.zero) * dist;
            Vector2 pc = Layout.PondC, pr = Layout.PondR * 1.15f;
            want = Mathf.Abs(c.x - pc.x) < pr.x + Size * 0.5f && Mathf.Abs(c.y - pc.y) < pr.y + Size * 0.5f
                   && Mathf.Abs(cp.x) < Layout.Half + 50f && Mathf.Abs(cp.z) < Layout.Half + 50f && cp.y > Layout.WaterY - 40f && cp.y < 400f;
        }
        // expire presses not refreshed lately (FixedUpdate callers can skip a frame)
        drop.Clear();
        foreach (var kv in press) if (Time.time - kv.Value.seen > 0.15f) drop.Add(kv.Key);
        foreach (int k in drop) press.Remove(k);
        if (!want)
        {
            if (on) { on = false; Clear(a); Clear(b); Shader.SetGlobalVector("_FFRipRect", Vector4.zero); ShowPatch(false); for (int i = 0; i < 8; i++) cont[i] = Vector4.zero; Shader.SetGlobalVectorArray("_FFCont", cont); }
            kicks.Clear();
            return;
        }
        float dx = Size / Res;
        Vector2 o = new Vector2(Mathf.Round((c.x - Size * 0.5f) / dx) * dx, Mathf.Round((c.y - Size * 0.5f) / dx) * dx);
        Vector2 shift = Vector2.zero;
        if (!on) { origin = o; on = true; acc = 0f; }
        else if ((o - origin).sqrMagnitude > (Size * 0.08f) * (Size * 0.08f)) { shift = (o - origin) / Size; origin = o; }   // move in ~8% hops
        acc += dt;
        int n = Mathf.Min(Mathf.FloorToInt(acc * Rate), Look.Mobile ? 2 : 4);
        acc = Mathf.Min(acc - n / Rate, 0.1f);
        if (n == 0 && shift != Vector2.zero) n = 1;
        for (int s = 0; s < n; s++)
        {
            float back = dt * (1f - (s + 1f) / n);   // presses: where the body was at this sub-step (smooth trails at low fps)
            int m = 0;
            foreach (var kv in press)
            {
                if (m >= MaxSrc) break;
                Src q = kv.Value;
                Vector2 p = q.p - q.v * back;
                Pack(m++, p, q);
            }
            if (s == 0) foreach (var q in kicks) { if (m >= MaxSrc) break; Pack(m++, q.p, q); }
            sim.SetVectorArray("_SrcA", sa); sim.SetVectorArray("_SrcB", sb); sim.SetVectorArray("_SrcC", sc); sim.SetFloat("_SrcN", m);
            Vector4 sh = sim.GetVector("_Shift"); sh.x = s == 0 ? shift.x : 0f; sh.y = s == 0 ? shift.y : 0f; sim.SetVector("_Shift", sh);
            Graphics.Blit(a, b, sim);
            var t = a; a = b; b = t;
            Steps++;
        }
        if (n > 0) kicks.Clear();
        Shader.SetGlobalTexture("_FFRipTex", a);
        Shader.SetGlobalVector("_FFRipRect", new Vector4(origin.x, origin.y, 1f / Size, 1f));
        // ffu39 meniscus: the 8 biggest live contacts (legs / hulls; frogs too)
        int nc = 0;
        for (int i = 0; i < 8; i++) cont[i] = Vector4.zero;
        foreach (var kv in press)
        {
            Src q = kv.Value; float rr = q.ring > 0f ? q.ring : q.r;
            int slot = nc < 8 ? nc++ : -1;
            if (slot < 0) { int w = 0; for (int i = 1; i < 8; i++) if (cont[i].z < cont[w].z) w = i; if (cont[w].z >= rr) continue; slot = w; }
            cont[slot] = new Vector4(q.p.x, q.p.y, rr, 1f);
        }
        Shader.SetGlobalVectorArray("_FFCont", cont);
        UpdatePatch();
    }

    void Pack(int i, Vector2 p, Src q)
    {
        sa[i] = new Vector4((p.x - origin.x) / Size, (p.y - origin.y) / Size, q.r / Size, q.amt);
        // ffu39: silt only where the thing can reach the bed (auto: bed within ~2.5-5.5 m of the surface), spray only from
        // kicks (real splashes / footfalls)
        float sk = q.silt;
        if (sk < 0f) { float bed = Layout.WaterY - Ranch.GY(p.x, p.y); sk = Mathf.Clamp01(1f - (bed - 2.5f) / 3f); }
        sb[i] = new Vector4(q.mode, q.foam * sk, q.ring / Size, q.str);
        sc[i] = new Vector4(q.mode == 0 ? q.foam : 0f, 0f, 0f, 0f);
    }

    void TickSwells(float dt)
    {
        for (int i = 0; i < 4; i++)
        {
            if (swAmp[i] <= 0f) { sw[i].w = 0f; continue; }
            swAge[i] += dt;
            float front = swC[i] * swAge[i];
            float run = swk[i].w, u = Mathf.Clamp01(1f - front / run);
            float amp = swAmp[i] * Mathf.Clamp01(swAge[i] * 1.5f) * u * u;
            if (front > run) { swAmp[i] = 0f; amp = 0f; }
            sw[i].z = front; sw[i].w = amp;
        }
        Shader.SetGlobalVectorArray("_FFSwell", sw);
        Shader.SetGlobalVectorArray("_FFSwellK", swk);
    }

    static Camera ViewCam()
    {
        Camera best = null; float area = 0f;
        foreach (var c in Camera.allCameras)
        {
            if (c == null || !c.enabled || c.targetTexture != null) continue;
            float ar = c.rect.width * c.rect.height + c.depth * 1e-4f;
            if (ar > area) { area = ar; best = c; }
        }
        return best;
    }

    // ---------------- near-view patch mesh (realistic water only) ----------------
    void UpdatePatch()
    {
        Material pm = plane != null ? plane.sharedMaterial : null;
        bool real = pm != null && pm.shader != null && pm.shader.name == "FF/RealWater" && SystemInfo.graphicsShaderLevel >= 30;
        if (!real) { ShowPatch(false); return; }
        if (patch == null) BuildPatch();
        if (patchSrc != pm || patchMat == null)
        {
            patchSrc = pm;
            patchMat = new Material(pm) { name = "PondRipplePatch" };
            patchMat.SetFloat("_Patch", 1f);
            patch.GetComponent<Renderer>().sharedMaterial = patchMat;
        }
        else if (Time.frameCount % 60 == 0) { patchMat.CopyPropertiesFromMaterial(pm); patchMat.SetFloat("_Patch", 1f); }   // follow Realism tweaks
        ShowPatch(true);
        patch.transform.position = new Vector3(origin.x, Ripples.SurfaceY, origin.y);
    }

    void ShowPatch(bool show)
    {
        if (patch != null && patch.activeSelf != show) patch.SetActive(show);
        Vector4 p = Shader.GetGlobalVector("_FFRipP"); p.z = show ? 1f : 0f; Shader.SetGlobalVector("_FFRipP", p);
    }

    void BuildPatch()
    {
        int n = (Look.Mobile || (Realism.On && Realism.Lite)) ? 72 : 128;
        var mesh = new Mesh { name = "PondRipplePatch" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var v = new Vector3[(n + 1) * (n + 1)];
        for (int z = 0; z <= n; z++) for (int x = 0; x <= n; x++) v[z * (n + 1) + x] = new Vector3(x * Size / n, 0f, z * Size / n);
        var tri = new int[n * n * 6];
        int k = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * (n + 1) + x;
                tri[k++] = i; tri[k++] = i + n + 1; tri[k++] = i + 1;
                tri[k++] = i + 1; tri[k++] = i + n + 1; tri[k++] = i + n + 2;
            }
        mesh.vertices = v; mesh.triangles = tri;
        var nn = new Vector3[v.Length]; for (int i = 0; i < nn.Length; i++) nn[i] = Vector3.up; mesh.normals = nn;
        mesh.bounds = new Bounds(new Vector3(Size * 0.5f, 0f, Size * 0.5f), new Vector3(Size, 8f, Size));
        patch = new GameObject("PondRipplePatch");
        patch.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = patch.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        if (plane != null) { patch.layer = plane.gameObject.layer; r.reflectionProbeUsage = plane.reflectionProbeUsage; r.probeAnchor = plane.probeAnchor != null ? plane.probeAnchor : plane.transform; r.lightProbeUsage = plane.lightProbeUsage; }
        Debug.Log("WaveSim: ripple patch " + n + "x" + n);
    }
}
