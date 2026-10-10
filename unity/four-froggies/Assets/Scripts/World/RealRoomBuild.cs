using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

// REAL ROOM part 2: streaming the assets (web/realroom/), building the room + colliders + player + camera, the demo
// shots and the ?realroom=1 test spawn. Formats are written by work/rr/pack.py (rr.txt) and the Blender exporters
// (/tmp/rr/bl/export.py -> room.bin "RRM1", hands.py -> hands.bin "RRS1").
public partial class RealRoom
{
    static string BaseUrl()
    {
        string u = Application.absoluteURL ?? "";
        int q = u.IndexOfAny(new[] { '?', '#' }); if (q >= 0) u = u.Substring(0, q);
        int s = u.LastIndexOf('/'); if (s >= 0) u = u.Substring(0, s + 1);
        return u + "realroom/";
    }

    static string Param(string key)
    {
        string u = Application.absoluteURL ?? "";
        int q = u.IndexOf('?'); if (q < 0) return null;
        foreach (var kv in u.Substring(q + 1).Split('&', '#'))
        {
            int e = kv.IndexOf('=');
            string k = e >= 0 ? kv.Substring(0, e) : kv;
            if (k == key) return e >= 0 ? kv.Substring(e + 1).ToLowerInvariant() : "1";
        }
        return null;
    }

    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    public void BeginLoad()
    {
        if (loadStarted) return;
        loadStarted = true;
        string p = Param("realroom");
        Lite = p == "lite" || (p != "full" && Look.Mobile);
        tierDir = Lite ? "lo/" : "hi/";
        StartCoroutine(LoadAll());
    }

    IEnumerator Fetch(string file, System.Action<byte[]> done)
    {
        using (var rq = UnityWebRequest.Get(BaseUrl() + file + V))
        {
            var op = rq.SendWebRequest();
            long last = 0;
            while (!op.isDone) { long b = (long)rq.downloadedBytes; bytesDone += b - last; last = b; progress = Mathf.Clamp01((float)bytesDone / bytesTotal); yield return null; }
            bytesDone += (long)rq.downloadedBytes - last;
            if (rq.result != UnityWebRequest.Result.Success) { Debug.LogWarning("RealRoom: " + file + " " + rq.error); loadFailed = true; yield break; }
            done(rq.downloadHandler.data);
        }
    }

    IEnumerator LoadTex(string file, string key, bool mips, bool compress, bool clamp = false)
    {
        if (tex.ContainsKey(key)) yield break;
        tex[key] = null;
        byte[] data = null;
        yield return Fetch(file, d => data = d);
        if (data == null) yield break;
        var t = new Texture2D(2, 2, TextureFormat.RGB24, mips, true);
        if (!ImageConversion.LoadImage(t, data, false)) { Debug.LogWarning("RealRoom: decode failed " + file); yield break; }
        t.name = key;
        t.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        t.filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear;
        t.anisoLevel = mips ? (Lite ? 2 : 8) : 1;
        if (mips) t.Apply(true, false);
        if (compress && !Lite && SystemInfo.SupportsTextureFormat(TextureFormat.DXT1))
        {
            try { t.Compress(false); } catch (System.Exception e) { Debug.LogWarning("RealRoom: compress " + file + " " + e.Message); }
        }
        t.Apply(false, true);   // free the CPU copy
        tex[key] = t;
    }

    IEnumerator LoadAll()
    {
        float t0 = Time.realtimeSinceStartup;
        Debug.Log("RealRoom: loading (" + (Lite ? "lite" : "full") + ")");
        byte[] txt = null;
        yield return Fetch("rr.txt", d => txt = d);
        if (txt == null) { loadFailed = true; yield break; }
        foreach (var raw in System.Text.Encoding.UTF8.GetString(txt).Split('\n'))
        {
            var p = raw.Trim().Split(' ');
            if (p.Length < 2) continue;
            switch (p[0])
            {
                case "mat": matLines.Add(p); break;
                case "obj": objLines.Add(p); break;
                case "pt": cfg["pt " + p[1]] = p; break;
                case "sh": cfg["sh " + p[1]] = p; break;
                case "size": cfg["size " + p[1]] = p; break;
                case "tvlight":
                    {
                        int n = int.Parse(p[1]);
                        tvLight = new float[n * 3];
                        for (int i = 0; i < n && i + 2 < p.Length; i++)
                        {
                            var c = p[i + 2].Split(',');
                            tvLight[i * 3] = F(c[0]); tvLight[i * 3 + 1] = F(c[1]); tvLight[i * 3 + 2] = F(c[2]);
                        }
                    }
                    break;
                default: cfg[p[0]] = p; break;
            }
        }
        string[] sz;
        if (cfg.TryGetValue("size " + tierDir.TrimEnd('/'), out sz)) bytesTotal = long.Parse(sz[2]);
        // geometry + lighting first, then the materials' textures (4 at a time)
        var jobs = new List<IEnumerator>();
        jobs.Add(Fetch("room.bin", d => roomBin = d));
        jobs.Add(Fetch("hands.bin", d => handsBin = d));
        foreach (var g in new[] { "env", "lamp", "tv" })
        {
            jobs.Add(LoadTex("lm_" + g + ".jpg", "lm_" + g, false, false, true));
            jobs.Add(LoadTex("pano_" + g + ".jpg", "pano_" + g, true, false));
        }
        jobs.Add(LoadTex(tierDir + "window.jpg", "window", false, false, true));
        jobs.Add(LoadTex("skin.jpg", "skin", true, false));
        foreach (var m in matLines)
            for (int k = 3; k <= 5; k++)
                if (m[k] != "-" && !Queued(jobs, m[k])) { queued.Add(m[k]); jobs.Add(LoadTex(tierDir + m[k], m[k], true, k == 3)); }
        int running = 0;
        foreach (var j in jobs)
        {
            while (running >= 4) yield return null;
            running++;
            StartCoroutine(Run(j, () => running--));
        }
        while (running > 0) yield return null;
        if (loadFailed || roomBin == null || handsBin == null) { loadFailed = true; yield break; }
        progress = 1f;
        Build();
        loaded = true;
        Debug.Log("RealRoom: loaded " + (bytesDone / 1048576f).ToString("0.0") + " MB in " + (Time.realtimeSinceStartup - t0).ToString("0.0") + " s, " + tex.Count + " textures");
    }
    readonly HashSet<string> queued = new HashSet<string>();
    bool Queued(List<IEnumerator> j, string f) { return queued.Contains(f); }
    IEnumerator Run(IEnumerator e, System.Action done) { yield return StartCoroutine(e); done(); }

    Texture2D T(string key) { Texture2D t; return key != null && key != "-" && tex.TryGetValue(key, out t) && t != null ? t : null; }

    Vector3 Pt(string n, int i = 0) { var p = cfg["pt " + n]; return new Vector3(F(p[2 + i]), F(p[3 + i]), F(p[4 + i])); }
    float PtF(string n, int i) { var p = cfg["pt " + n]; return F(p[2 + i]); }
    Vector4[] SH(string g)
    {
        var p = cfg["sh " + g]; var o = new Vector4[9];
        for (int i = 0; i < 9; i++) o[i] = new Vector4(F(p[2 + i * 3]), F(p[3 + i * 3]), F(p[4 + i * 3]), 0f);
        return o;
    }

    // ---------------------------------------------------------------- build
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    Material MatFor(string name, bool dynamic)
    {
        string key = name + (dynamic ? "#d" : "");
        Material m;
        if (mats.TryGetValue(key, out m)) return m;
        string[] p = null;
        foreach (var l in matLines) if (l[1] == name) { p = l; break; }
        string sh = p != null ? p[2] : "lit";
        switch (sh)
        {
            case "glass": m = new Material(Shader.Find("FF/RRGlass")); break;
            case "screen": m = new Material(Shader.Find("FF/RRScreen")); screenMat = m; break;
            case "window":
                m = new Material(Shader.Find("FF/RRWindow")); m.mainTexture = T("window");
                var c = cfg["crop"]; m.SetVector("_Crop", new Vector4(F(c[1]), F(c[2]), F(c[3]), F(c[4]))); m.SetFloat("_K", F(c[5]));
                break;
            case "portal": m = new Material(Shader.Find("FF/RRPortal")); portalMat = m; break;
            default:
                m = new Material(Shader.Find("FF/RRLit"));
                if (p != null)
                {
                    var d = T(p[3]); var n = T(p[4]); var a = T(p[5]);
                    if (d != null) m.SetTexture("_MainTex", d);
                    if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_HasNrm", 1f); }
                    if (a != null) { m.SetTexture("_ArmTex", a); m.SetFloat("_HasArm", float.Parse(p[6])); }
                    m.SetFloat("_UVScale", F(p[7]));
                    m.SetColor("_Tint", new Color(F(p[8]), F(p[9]), F(p[10])));
                    m.SetFloat("_Rough", F(p[11])); m.SetFloat("_Metal", F(p[12])); m.SetFloat("_SpecK", F(p[13])); m.SetFloat("_NrmK", F(p[14]));
                }
                m.SetFloat("_Dynamic", dynamic ? 1f : 0f);
                if (sh == "globe") { m.SetFloat("_EmisTex", 0f); globeMat = m; }
                break;
        }
        m.name = "RR " + key;
        mats[key] = m;
        return m;
    }

    void Build()
    {
        if (root != null) return;
        root = new GameObject("RealRoomRoot").transform;
        root.position = RoomO;
        root.gameObject.SetActive(false);
        // globals that never change
        var k = cfg["k"]; Shader.SetGlobalVector("_RRK", new Vector4(F(k[1]), F(k[2]), F(k[3]), 0));
        var pk = cfg["pk"]; Shader.SetGlobalVector("_RRPK", new Vector4(F(pk[1]), F(pk[2]), F(pk[3]), 0));
        Shader.SetGlobalTexture("_RRLMEnv", T("lm_env")); Shader.SetGlobalTexture("_RRLMLamp", T("lm_lamp")); Shader.SetGlobalTexture("_RRLMTv", T("lm_tv"));
        Shader.SetGlobalTexture("_RRPanoEnv", T("pano_env")); Shader.SetGlobalTexture("_RRPanoLamp", T("pano_lamp")); Shader.SetGlobalTexture("_RRPanoTv", T("pano_tv"));
        var b = cfg["box"];
        Shader.SetGlobalVector("_RRBoxMin", RoomO + new Vector3(F(b[1]), F(b[2]), F(b[3])));
        Shader.SetGlobalVector("_RRBoxMax", RoomO + new Vector3(F(b[4]), F(b[5]), F(b[6])));
        var pr = cfg["probe"]; Shader.SetGlobalVector("_RRProbe", RoomO + new Vector3(F(pr[1]), F(pr[2]), F(pr[3])));
        shEnv = SH("env"); shLamp = SH("lamp"); shTv = SH("tv");
        lampPos = RoomO + Pt("lamp"); tvPos = RoomO + Pt("tv");
        spawnPos = RoomO + Pt("spawn"); doorPos = RoomO + Pt("door"); switchPos = RoomO + Pt("switch"); duckSpawn = RoomO + Pt("duck");
        seatPos = RoomO + Pt("seat"); { float th = PtF("seat", 3) * Mathf.Deg2Rad; seatYaw = Mathf.Atan2(Mathf.Sin(th), -Mathf.Cos(th)) * Mathf.Rad2Deg; }
        spawnPos = RoomO + new Vector3(1.15f, 0f, -1.55f);   // 0.9 m inside the door (the leaf starts half open)
        BuildMeshes();
        BuildColliders();
        // where the froggy's own body waits while the player is inside (out of sight, no kill plane in this world)
        var park = new GameObject("RealRoomPark"); park.transform.SetParent(root.parent, false);
        park.transform.position = RoomO + new Vector3(0f, -29f, 0f);
        park.AddComponent<BoxCollider>().size = new Vector3(12f, 1f, 12f);
        BuildPlayer();
        Hands.Build(handsBin, pitchT, T("skin"), owner != null ? owner.color : new Color(0.3f, 0.75f, 0.2f));
        handsBin = null; roomBin = null;
        Debug.Log("RealRoom: built");
    }

    void BuildMeshes()
    {
        var r = new BinaryReader(new MemoryStream(roomBin));
        r.ReadBytes(4);
        int n = r.ReadInt32();
        var duckGo = new GameObject("RubberDuck"); duckGo.layer = Layer;
        duckGo.transform.SetParent(root, false);
        var doorPivot = new GameObject("DoorHinge").transform; doorPivot.SetParent(root, false);
        var dp = Pt("door", 0);
        float dx1 = PtF("door", 4);
        doorPivot.localPosition = new Vector3(dx1 - 0.003f, 0f, dp.z - 0.04f);
        door = doorPivot;
        Transform rockPivot = null;
        for (int e = 0; e < n; e++)
        {
            string oname = Str(r), mname = Str(r);
            int flags = r.ReadByte(), nv = r.ReadInt32(), nt = r.ReadInt32();
            Vector3 mn = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), ext = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var pos = new Vector3[nv]; var nrm = new Vector3[nv]; var uv0 = new Vector2[nv]; Vector2[] uv1 = (flags & 1) != 0 ? new Vector2[nv] : null;
            for (int i = 0; i < nv; i++) pos[i] = mn + Vector3.Scale(ext, new Vector3(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16()) / 65535f);
            for (int i = 0; i < nv; i++) { nrm[i] = new Vector3(r.ReadSByte(), r.ReadSByte(), r.ReadSByte()) / 127f; r.ReadSByte(); }
            for (int i = 0; i < nv; i++) uv0[i] = new Vector2(Mathf.HalfToFloat(r.ReadUInt16()), Mathf.HalfToFloat(r.ReadUInt16()));
            if (uv1 != null) for (int i = 0; i < nv; i++) uv1[i] = new Vector2(r.ReadUInt16() / 65535f, r.ReadUInt16() / 65535f);
            var tri = new int[nt * 3];
            if (nv < 65536) for (int i = 0; i < tri.Length; i++) tri[i] = r.ReadUInt16(); else for (int i = 0; i < tri.Length; i++) tri[i] = (int)r.ReadUInt32();
            bool duckPart = (flags & 2) != 0, doorPart = (flags & 4) != 0, rockPart = (flags & 8) != 0;
            Transform parent = duckPart ? duckGo.transform : doorPart ? doorPivot : root;
            if (rockPart)
            {
                Vector3 c = mn + ext * 0.5f;
                rockPivot = new GameObject("Rocker").transform; rockPivot.SetParent(root, false); rockPivot.localPosition = c;
                for (int i = 0; i < nv; i++) pos[i] -= c;
                parent = rockPivot;
            }
            else if (doorPart) for (int i = 0; i < nv; i++) pos[i] -= doorPivot.localPosition;
            var mesh = new Mesh { name = oname };
            if (nv >= 65536) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = pos; mesh.normals = nrm; mesh.uv = uv0;
            if (uv1 != null) mesh.uv2 = uv1;
            mesh.triangles = tri;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            var go = new GameObject(oname + "|" + mname); go.layer = Layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MatFor(mname, uv1 == null);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        rocker = rockPivot;
        if (rocker != null) rocker.localRotation = Quaternion.Euler(-9f, 0f, 0f);
        // the duck: rigidbody with a two-sphere body (body + head)
        duck = duckGo.AddComponent<Rigidbody>();
        duck.mass = 0.25f; duck.drag = 0.15f; duck.angularDrag = 0.4f;
        duck.interpolation = RigidbodyInterpolation.Interpolate;
        duck.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var pm = new PhysicMaterial("duck") { bounciness = 0.45f, dynamicFriction = 0.5f, staticFriction = 0.6f, bounceCombine = PhysicMaterialCombine.Maximum };
        var s1 = duckGo.AddComponent<SphereCollider>(); s1.center = new Vector3(0f, 0.062f, 0.01f); s1.radius = 0.062f; s1.material = pm;
        var s2 = duckGo.AddComponent<SphereCollider>(); s2.center = new Vector3(0f, 0.125f, -0.045f); s2.radius = 0.042f; s2.material = pm;
        duckCols.Add(s1); duckCols.Add(s2);
        duckGo.AddComponent<RRDuck>();
        // soft contact shadow
        var blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(blob.GetComponent<Collider>());
        blob.layer = Layer; blob.transform.SetParent(root, false);
        blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        blob.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("FF/RRBlob"));
        duckBlob = blob.transform;
    }

    static string Str(BinaryReader r) { int n = r.ReadByte(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }

    BoxCollider Box(Vector3 mn, Vector3 mx, string name)
    {
        var go = new GameObject("col " + name); go.layer = Layer; go.transform.SetParent(root, false);
        go.transform.localPosition = (mn + mx) * 0.5f;
        var b = go.AddComponent<BoxCollider>(); b.size = mx - mn;
        return b;
    }

    void BuildColliders()
    {
        var bx = cfg["box"];
        Vector3 a = new Vector3(F(bx[1]), F(bx[2]), F(bx[3])), z = new Vector3(F(bx[4]), F(bx[5]), F(bx[6]));
        const float T = 0.5f;
        Box(new Vector3(a.x - T, a.y - T, a.z - T), new Vector3(z.x + T, a.y, z.z + T), "floor");
        Box(new Vector3(a.x - T, z.y, a.z - T), new Vector3(z.x + T, z.y + T, z.z + T), "ceiling");
        Box(new Vector3(a.x - T, a.y, a.z - T), new Vector3(a.x, z.y, z.z + T), "west");
        Box(new Vector3(z.x, a.y, a.z - T), new Vector3(z.x + T, z.y, z.z + T), "east");
        Box(new Vector3(a.x, a.y, z.z), new Vector3(z.x, z.y, z.z + T), "north");
        Box(new Vector3(a.x, a.y, a.z - T), new Vector3(z.x, z.y, a.z), "south");
        foreach (var o in objLines)
        {
            string n = o[1];
            if (n == "rubber_duck_toy" || n == "modern_ceiling_lamp_01" || n == "hanging_picture_frame_02") continue;
            Vector3 mn = new Vector3(F(o[2]), F(o[3]), F(o[4])), mx = new Vector3(F(o[5]), F(o[6]), F(o[7]));
            if (n == "ArmChair_01") { var c = Box(mn + new Vector3(0.12f, 0f, 0.12f), mx - new Vector3(0.12f, 0.2f, 0.12f), n); chairCol = c; continue; }
            Box(mn, mx, n);
        }
        // TV panel (wall mounted) + the interactive switch / door
        Vector3 tvc = Pt("tv");
        Box(tvc + new Vector3(-0.62f, -0.36f, -0.01f), tvc + new Vector3(0.62f, 0.36f, 0.05f), "tv");
        var sw = Box(Pt("switch") + new Vector3(-0.07f, -0.09f, -0.04f), Pt("switch") + new Vector3(0.07f, 0.09f, 0.03f), "switch");
        sw.isTrigger = true; switchCol = sw;
        Vector3 dpos = Pt("door");
        var dc = Box(new Vector3(PtF("door", 3), 0f, dpos.z - 0.05f), new Vector3(PtF("door", 4), PtF("door", 5), dpos.z + 0.02f), "door");
        doorCol = dc;
    }

    void BuildPlayer()
    {
        var p = new GameObject("RealRoomPlayer"); p.layer = Layer;
        p.transform.SetParent(root, false);
        yawT = p.transform;
        body = p.AddComponent<CharacterController>();
        body.height = 1.55f; body.radius = 0.24f; body.center = new Vector3(0f, 0.8f, 0f); body.stepOffset = 0.2f; body.skinWidth = 0.02f;
        pitchT = new GameObject("Eye").transform; pitchT.SetParent(yawT, false); pitchT.localPosition = new Vector3(0f, EyeH, 0f);
        var cg = new GameObject("RealRoomCam"); cg.transform.SetParent(pitchT, false);
        cam = cg.AddComponent<Camera>();
        cam.cullingMask = 1 << Layer;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.03f; cam.farClipPlane = 40f;
        cam.fieldOfView = Screen.height > Screen.width ? 78f : 68f;
        cam.depth = 30;
        cam.allowHDR = !Lite; cam.allowMSAA = true;
        cam.useOcclusionCulling = false;
        cam.enabled = false;
        if (!Lite)
        {
            var sh = Shader.Find("Hidden/FF/RRPost");
            if (sh != null && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                post = cg.AddComponent<RRPostFx>(); post.mat = new Material(sh);
            }
            else { Lite = true; cam.allowHDR = false; Debug.Log("RealRoom: no HDR post here - shader-side tonemap"); }
        }
    }

    void PlayVideo()
    {
        if (screenMat == null) return;
        if (vp == null)
        {
            vidRT = new RenderTexture(640, 360, 0, RenderTextureFormat.ARGB32); vidRT.name = "RR TV";
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false; vp.isLooping = true;
            vp.source = VideoSource.Url; vp.url = BaseUrl() + "tv.mp4" + V;
            vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = vidRT;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.errorReceived += (p, msg) => { Debug.LogWarning("RealRoom: video " + msg); vidFallback = true; };
            vp.prepareCompleted += p => { p.Play(); Debug.Log("RealRoom: video playing"); };
            screenMat.SetTexture("_MainTex", vidRT);
            vidT0 = Time.time;
            vp.Prepare();
        }
        else vp.Play();
        try { vp.SetDirectAudioVolume(0, Sfx.Level == 0 ? 0.32f : Sfx.Level == 1 ? 0.12f : 0f); } catch { }
        StartCoroutine(VideoWatch());
    }

    IEnumerator VideoWatch()
    {
        float t = 0f;
        while (t < 9f && vp != null && !vp.isPlaying && !vidFallback) { t += Time.unscaledDeltaTime; yield return null; }
        if (vp == null || !vp.isPlaying) { vidFallback = true; Debug.Log("RealRoom: video not playing - test pattern"); }
        if (screenMat != null) screenMat.SetFloat("_Fallback", vidFallback ? 1f : 0f);
    }

    // ---------------------------------------------------------------- ?realroom=1 test spawn + demo shots
    bool urlDone;
    void UrlTest()
    {
        if (urlDone || Game.I == null || Game.I.state != Game.State.Play || Game.I.slots.Count == 0) return;
        urlDone = true;
        if (Param("realroom") == null || demoShot != null) return;
        Frog f = Game.I.frogs[Game.I.slots[0].frog];
        if (HouseWorld.I == null) return;
        HouseWorld.I.Enter(f);
        f.SendTo(WorldId.House, HouseDoorFront, 270f);
        f.Toast("REAL ROOM test: the white door is right in front of you", 4f);
        BeginLoad();
    }

    string demoShot;
    float demoT = -1f, demoExpo = -1f;
    int demoPhase;
    public static void DemoStart(Frog f, string shot)
    {
        if (I == null) return;
        I.demoShot = shot; I.demoT = 0f; I.demoPhase = 0;
        if (HouseWorld.I != null) { HouseWorld.I.Enter(f); f.SendTo(WorldId.House, HouseDoorFront, 270f); }
        I.BeginLoad();
        Debug.Log("FFDEMO realroom " + shot);
    }

    void Demo(float dt)
    {
        if (demoShot == null || Game.I == null || Game.I.slots.Count == 0) return;
        demoT += dt;
        Frog f = Game.I.frogs[Game.I.slots[0].frog];
        if (demoPhase == 0)
        {
            if (!loaded) return;
            demoPhase = 1;
            Request(f);
            if (st == St.Opening || st == St.Loading) { }
            return;
        }
        if (st == St.Intro && demoShot != "realroom-enter") { stT = Mathf.Max(stT, 6.6f); skipPressed = true; }
        if (st != St.Play && st != St.Intro) return;
        if (demoPhase == 1 && st == St.Play)
        {
            demoPhase = 2; demoT = 0f;
            Debug.Log("FFDEMO realroom play " + demoShot);
            switch (demoShot)
            {
                case "realroom": Pose(new Vector3(1.55f, 0f, -1.85f), -25f, 9f); break;
                case "realroom-dark": Pose(new Vector3(1.55f, 0f, -1.85f), -25f, 9f); lightsOn = false; if (rocker != null) rocker.localRotation = Quaternion.Euler(9f, 0f, 0f); break;
                case "realroom-tv": SitDown(); yaw = seatYaw; pitch = 6f; break;
                case "realroom-throw": Pose(new Vector3(0.9f, 0f, -0.75f), 15f, 25f); PickDuck(); break;
            }
        }
        if (demoPhase == 2)
        {
            if (demoShot == "realroom-dark") { lightsOn = false; }
            if (demoShot == "realroom-tv") { yaw = seatYaw - 8f; pitch = 5f; }
            if (demoShot == "realroom-throw")
            {
                if (demoT > 3f && holding) { Throw(0.6f); }
                if (demoT > 3f) { yaw = Mathf.Lerp(yaw, 15f, dt); pitch = Mathf.Lerp(pitch, 10f, dt); }
                if (demoT > 9f && !holding && demoT < 9.2f) { ResetDuck(); duck.transform.position = Hands.HoldPoint; PickDuck(); demoT = 0.5f; }
            }
            ApplyView();
        }
    }

    void Pose(Vector3 local, float y, float p)
    {
        body.enabled = false; yawT.position = RoomO + local; body.enabled = true;
        yaw = y; pitch = p; ApplyView();
    }
}
