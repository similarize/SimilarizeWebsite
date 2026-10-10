using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// ffu18 REAL ROOM: a photoreal room inside James's house. A white door on the living room's west wall ("REAL ROOM")
// opens into a first-person room built from CC0 scans (Poly Haven furniture + PBR materials + a moonlit-field HDRI),
// lit by lightmaps baked offline in Blender Cycles (3 groups: moonlight through the window / ceiling lamp / TV, blended
// live so the switch and the flickering TV really relight the room), box-projected reflection panoramas, an SH probe for
// moving things, ACES + bloom (desktop) - all in linear light inside the RR shaders.
// When the door shuts behind him the froggy looks down, his cartoon hands turn into real wet frog hands and a wave turns
// the whole room real. Inside: pick up / throw the rubber duck (physics), sit in the armchair, flip the light switch,
// watch the TV (Big Buck Bunny, CC-BY Blender Foundation), and leave through the door back to the cartoon house.
// Assets stream from web/realroom/ only on the way in (nothing in the Unity build): hi/ (desktop, Xbox: 2K hero
// textures, bloom) or lo/ (phones, Tesla: 1K textures, shader-side tonemap, no post). URL: ?realroom=1 spawns at the
// door (realroom=lite / full force a tier); ?ffdemo=1&ffshot=realroom-enter | realroom | realroom-dark | realroom-tv |
// realroom-throw for the probe.
public partial class RealRoom : MonoBehaviour
{
    public static RealRoom I;
    public static readonly Vector3 RoomO = new Vector3(0f, -2000f, 1400f);   // under the house world, never in view
    public const int Layer = 21;
    const string V = "?v=rr4";   // ffu23: frog.bin (hands + body + plush), rr.txt mats, night window
    const float EyeH = 1.47f;

    // the door in the house (living room west wall), HouseWorld local coordinates
    static readonly Vector3 HDoor = new Vector3(-29.84f, 0f, 15.8f);
    public static Vector3 HouseDoorFront { get { return HouseWorld.L(HDoor.x + 1.5f, 0.15f, HDoor.z); } }

    enum St { Idle, Loading, Opening, Intro, Play, Exiting }
    St st = St.Idle;
    Frog owner;
    Slot slot;
    float stT;          // time in the current state
    bool wantEnter;

    // tier / loading
    public static bool Lite;
    bool loadStarted, loaded, loadFailed;
    float progress;
    long bytesDone, bytesTotal = 1;
    string tierDir = "hi/";
    readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
    byte[] roomBin, frogBin;
    readonly Dictionary<string, string[]> cfg = new Dictionary<string, string[]>();
    readonly List<string[]> matLines = new List<string[]>(), objLines = new List<string[]>();
    float[] tvLight = new float[0];

    // scene
    Transform root, door, rocker;
    Transform houseLeaf;
    Camera cam;
    RRPostFx post;
    CharacterController body;
    Transform yawT, pitchT;
    float yaw, pitch;
    Rigidbody duck; Transform duckBlob;
    Rigidbody plush; Transform plushBlob; Vector3 plushSpawn; float plushYaw;
    readonly List<Collider> plushCols = new List<Collider>();
    Rigidbody held; float heldR = 0.06f;      // ffu23: what the right hand carries (duck or plush)
    Collider chairCol, switchCol, doorCol;
    readonly List<Collider> duckCols = new List<Collider>();
    Material screenMat, globeMat, portalMat;
    Texture2D vidTex; bool vidFallback, vidOk; float vidT0;
    Vector3 lampPos, tvPos, seatPos, spawnPos, doorPos, switchPos, duckSpawn; float seatYaw;

    // light state
    bool lightsOn = true;
    float lampK = 1f, expo = 4f;
    Vector4[] shEnv = new Vector4[9], shLamp = new Vector4[9], shTv = new Vector4[9];
    readonly Vector4[] sh = new Vector4[9], shD = new Vector4[9];
    // ffu18c: moving things (duck, rocker, door hardware) take the room-centre probe scaled per group (env / lamp / tv);
    // the probe sits 1.3 m up near the bulb, so unscaled it lit the duck far brighter than the floor it sits on
    static readonly Vector3 DynK = new Vector3(0.25f, 0.35f, 0.45f);
    // ffu18c: lamp white balance (the 2700 K bake read strongly sepia)
    static readonly Vector3 LampWB = new Vector3(0.85f, 1.0f, 1.35f);
    // light group weights; exposure is automatic from the baked wall irradiance of the current mix (shell medians of
    // the three lightmaps: lamp 0.256, tv 0.0057 per unit, night 0.0012), so lights-off reads as a dim TV-lit room
    public const float EnvW = 0.15f, TvLightK = 4f, ExpMin = 1f, ExpMax = 25f;
    const float RefLamp = 0.256f, RefTv = 0.0057f, RefEnv = 0.0012f;

    // interaction
    bool holding, sitting;
    float charge, bobT, intakeCool;
    // ffu23: reaching. An action on something out of arm's reach first walks the froggy up to it (and crouches for
    // things on the floor), then the IK hand does the work: poke the switch, grab the duck / plush, throw.
    string pending = ""; Vector3 pendingP; float pendingT, crouch, crouchT;
    Vector3 armrestR, armrestL; bool armrestOk;
    // ffu23: third person (V / Back / touch VIEW toggles, mouse wheel / Q Z zooms; below 0.45 m it snaps to first person)
    float camDist, camDistT, camOrbit; bool third;   // camOrbit: demo only (swing round to the front)
    Collider chairMeshCol;
    Vector3 standPos; float standYaw;
    string hint = "";

    // UI
    Canvas ui; Image fade, dot; Text hintT, titleT;

    public static void Create()
    {
        var go = new GameObject("RealRoom");
        I = go.AddComponent<RealRoom>();
    }

    public static bool Captures(Frog f) { return I != null && I.owner == f && I.st != St.Idle && I.st != St.Loading; }
    public static bool Busy { get { return I != null && I.st != St.Idle; } }

    // ---------------------------------------------------------------- house door
    // called by HouseWorld.Build: a white panel door with a sign + hotspot on the living room's west wall
    public static void BuildHouseDoor(Transform houseRoot)
    {
        if (I == null) return;
        Vector3 c = HouseWorld.L(HDoor.x, 0f, HDoor.z);
        var white = Mats.Lit(new Color(0.94f, 0.93f, 0.9f));
        var frameC = Mats.Lit(new Color(0.85f, 0.84f, 0.8f));
        // casing
        Mats.Prim(PrimitiveType.Cube, houseRoot, c + new Vector3(0.04f, 2.72f, 0f), new Vector3(0.1f, 0.16f, 1.86f), frameC);
        Mats.Prim(PrimitiveType.Cube, houseRoot, c + new Vector3(0.04f, 1.32f, -0.86f), new Vector3(0.1f, 2.64f, 0.14f), frameC);
        Mats.Prim(PrimitiveType.Cube, houseRoot, c + new Vector3(0.04f, 1.32f, 0.86f), new Vector3(0.1f, 2.64f, 0.14f), frameC);
        // leaf on a hinge (z = +0.78 side), opens into the house
        var hinge = new GameObject("RealRoomDoorHinge").transform;
        hinge.SetParent(houseRoot, false); hinge.position = c + new Vector3(0.05f, 0f, 0.78f);
        Mats.Prim(PrimitiveType.Cube, hinge, hinge.position + new Vector3(0.02f, 1.3f, -0.78f), new Vector3(0.06f, 2.6f, 1.56f), white);
        Mats.Prim(PrimitiveType.Cube, hinge, hinge.position + new Vector3(0.055f, 1.85f, -0.78f), new Vector3(0.02f, 1.0f, 1.1f), frameC);
        Mats.Prim(PrimitiveType.Cube, hinge, hinge.position + new Vector3(0.055f, 0.7f, -0.78f), new Vector3(0.02f, 0.9f, 1.1f), frameC);
        Mats.Prim(PrimitiveType.Sphere, hinge, hinge.position + new Vector3(0.1f, 1.25f, -1.4f), new Vector3(0.1f, 0.1f, 0.1f), Mats.Lit(new Color(0.75f, 0.72f, 0.6f)));
        I.houseLeaf = hinge;
        Ranch.Sign(c + new Vector3(0.12f, 3.15f, 0f), 90f, "REAL ROOM\n<size=16>step into the real world</size>", new Color(0.08f, 0.08f, 0.1f), 2.6f, 0.75f);
        var hs = Interact.Add(c + new Vector3(1.3f, 0.1f, 0f), 1.7f, "open the REAL ROOM door", f => I.Request(f));
        hs.enabled = f => f.world == WorldId.House && f.vehicle == null && (I.st == St.Idle || I.owner == f);
        hs.dynLabel = f => I.st == St.Loading && I.owner == f ? "REAL ROOM loading " + Mathf.RoundToInt(I.progress * 100f) + "%" : "open the REAL ROOM door";
    }

    void Request(Frog f)
    {
        if (st != St.Idle && owner != f) { f.Toast("Someone is in the REAL ROOM", 2f); return; }
        if (st != St.Idle && st != St.Loading) return;
        owner = f;
        slot = FindSlot(f);
        if (slot == null) { owner = null; return; }
        BeginLoad();
        if (loadFailed) { f.Toast("The REAL ROOM could not load (no connection?)", 3f); owner = null; return; }
        wantEnter = true;
        st = loaded ? St.Opening : St.Loading; stT = 0f;
        if (st == St.Loading) f.Toast("Opening the REAL ROOM...", 2f);
        Sfx.Play(Sfx.Door, 0.8f);
    }

    static Slot FindSlot(Frog f)
    {
        if (Game.I == null) return null;
        foreach (var s in Game.I.slots) if (Game.I.frogs[s.frog] == f) return s;
        return null;
    }

    // ---------------------------------------------------------------- main loop
    float nearT;
    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        stT += dt;
        UrlTest();
        Demo(dt);
        // pre-load when a human froggy walks up to the door (saves a few seconds at the door; nothing loads otherwise)
        if (!loadStarted && Game.I != null && Game.I.state == Game.State.Play && HouseWorld.I != null)
        {
            foreach (var s in Game.I.slots)
            {
                Frog f = Game.I.frogs[s.frog];
                if (f.world == WorldId.House && (f.transform.position - HouseWorld.L(HDoor.x, 0f, HDoor.z)).sqrMagnitude < 64f) { nearT += dt; if (nearT > 0.6f) BeginLoad(); }
            }
        }
        switch (st)
        {
            case St.Loading:
                if (loadFailed) { if (owner != null) owner.Toast("The REAL ROOM could not load", 3f); st = St.Idle; owner = null; break; }
                if (owner != null && owner.world != WorldId.House) { st = St.Idle; owner = null; break; }
                if (loaded) { st = St.Opening; stT = 0f; }
                break;
            case St.Opening:
                if (houseLeaf != null) houseLeaf.localRotation = Quaternion.Euler(0f, -Mathf.SmoothStep(0f, 95f, Mathf.Clamp01(stT / 0.6f)), 0f);
                SetFadeOverlay(Mathf.Clamp01((stT - 0.35f) / 0.35f), Color.black);
                if (stT > 0.75f) EnterRoom();
                break;
            case St.Intro: Intro(dt); break;
            case St.Play: Play(dt); break;
            case St.Exiting: Exiting(dt); break;
        }
        if (st != St.Opening && st != St.Idle && st != St.Loading && houseLeaf != null) houseLeaf.localRotation = Quaternion.identity;
        if (st == St.Idle && houseLeaf != null) houseLeaf.localRotation = Quaternion.Slerp(houseLeaf.localRotation, Quaternion.identity, dt * 4f);
        if (root != null && root.gameObject.activeSelf) Globals(dt);
    }

    void LateUpdate()
    {
        if (st == St.Idle || st == St.Loading || st == St.Opening)
        {
            if (st == St.Opening) return;
            if (fadeBackT > 0f) { fadeBackT -= Time.deltaTime; SetFadeOverlay(Mathf.Clamp01(fadeBackT / 0.6f), Color.white); }
            else if (fadeA > 0f || ui == null) SetFadeOverlay(0f, Color.black);
            return;
        }
        // a world change from elsewhere (online host moved, respawn ...) ends the visit cleanly
        if (owner == null || owner.world != WorldId.RealRoom) { EndVisit(false); return; }
        if (slot != null && slot.cam != null)
        {
            slot.cam.enabled = false;
            if (slot.hud != null) slot.hud.SetActive(false);
            cam.rect = slot.cam.rect;
        }
        LayoutUi();
    }

    // ---------------------------------------------------------------- enter / exit
    void EnterRoom()
    {
        if (root == null) Build();
        root.gameObject.SetActive(true);
        owner.SendTo(WorldId.RealRoom, RoomO + new Vector3(0f, -28f, 0f), 0f);
        // player at the door, facing it (the door is still swinging shut behind the froggy's back... he turns to see it)
        body.enabled = false;
        yawT.position = spawnPos; yaw = 180f; pitch = 4f;
        body.enabled = true;
        door.localRotation = Quaternion.Euler(0f, 45f, 0f);
        holding = false; sitting = false; held = null; pending = ""; crouch = 0f; crouchT = 0f;
        Hands.ResetPose();
        ResetDuck(); ResetPlush();
        camDist = camDistT = 0f; third = false; FrogBody.SetVisible(false); Hands.SetVisible(true);
        lightsOn = true; lampK = 1f; expo = 4f;
        cam.enabled = true;
        cam.rect = slot.cam.rect;
        slot.cam.enabled = false;
        Hands.SetMorph(0f);
        waveR = 0f; Shader.SetGlobalVector("_RRWaveO", new Vector4(0, 0, 0, 0f));
        if (!Lite) FFDisplay.Lobby(true);   // full pixel density on desktop while inside
        QualitySettings.antiAliasing = Lite ? 0 : 4;
        PlayVideo();
        Sfx.Play(Sfx.Door, 0.7f, 0.85f);
        st = St.Intro; stT = 0f;
        Debug.Log("RealRoom: enter (" + (Lite ? "lite" : "full") + ")");
    }

    float waveR;
    void Intro(float dt)
    {
        // 0.0-1.1 door swings shut (thud), 1.1-2.1 look down at the cartoon hands, 2.1-3.9 hands turn real,
        // 3.3-5.5 the realness wave spreads through the room, 4.6-6.4 turn round to face the room
        float t = stT;
        SetFadeOverlay(1f - Mathf.Clamp01(t / 0.35f), Color.black);
        float dk = Mathf.Clamp01(t / 1.0f);
        door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(45f, 0f, dk * dk), 0f);
        if (t - dt < 1.0f && t >= 1.0f) Sfx.Play(Sfx.Thud != null ? Sfx.Thud : Sfx.Door, 0.9f, 0.8f);
        float look = Smooth01((t - 1.1f) / 0.9f) - Smooth01((t - 4.6f) / 1.0f);
        pitch = Mathf.Lerp(4f, 42f, look);
        float turn = Smooth01((t - 4.6f) / 1.8f);
        yaw = Mathf.LerpAngle(180f, 345f, turn);
        Hands.introPose = look;
        float morph = Mathf.Clamp01((t - 2.1f) / 1.8f);
        Hands.SetMorph(morph);
        if (t - dt < 2.1f && t >= 2.1f) Sfx.Play(Sfx.Pickup, 0.8f, 0.7f);
        if (t > 3.3f)
        {
            waveR = Mathf.Pow(Mathf.Clamp01((t - 3.3f) / 2.2f), 1.6f) * 9f;
            if (t - dt < 3.3f) Sfx.Play(Sfx.Confirm != null ? Sfx.Confirm : Sfx.Win, 0.7f, 0.8f);
        }
        if (t > 5.6f) waveR = 99f;
        ApplyView();
        bool skip = t > 1.5f && skipPressed;
        skipPressed = false;
        if (t > 6.5f || skip)
        {
            Hands.SetMorph(1f); Hands.introPose = 0f; waveR = 99f;
            door.localRotation = Quaternion.identity;
            yaw = 345f; pitch = 6f; ApplyView();
            st = St.Play; stT = 0f;
        }
    }
    bool skipPressed;
    static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void StartExit()
    {
        if (holding) DropHeld(Vector3.zero);
        if (sitting) StandUp();
        pending = ""; Hands.Cancel(); camDistT = 0f;
        st = St.Exiting; stT = 0f;
        Sfx.Play(Sfx.Door, 0.8f, 1.05f);
    }

    void Exiting(float dt)
    {
        float t = stT;
        door.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(0f, 75f, Mathf.Clamp01(t / 0.9f)), 0f);
        // face the door
        Vector3 to = doorPos - yawT.position; to.y = 0f;
        yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, dt * 4f);
        pitch = Mathf.Lerp(pitch, 2f, dt * 4f);
        if (portalMat != null) portalMat.SetFloat("_Glow", 1f + 4f * Mathf.Clamp01(t / 1.0f));
        // the room turns cartoon again around the froggy, the hands too
        float back = Mathf.Clamp01((t - 0.5f) / 0.8f);
        waveR = back > 0f ? Mathf.Lerp(9f, 0f, back) : 99f;
        Hands.SetMorph(1f - back);
        SetFade(Mathf.Clamp01((t - 1.1f) / 0.4f), Color.white);
        ApplyView();
        if (t > 1.55f) EndVisit(true);
    }

    void EndVisit(bool toHouse)
    {
        if (st == St.Idle) return;
        Frog f = owner;
        st = St.Idle;
        StopVideo();
        cam.enabled = false;
        root.gameObject.SetActive(false);
        SetFade(0f, Color.white);
        if (!Lite) FFDisplay.Lobby(false);
        FFDisplay.ApplyAA();
        if (f != null && toHouse)
        {
            f.SendTo(WorldId.House, HouseDoorFront, 90f);
            fadeBackT = 0.6f;
        }
        if (slot != null && slot.cam != null && f != null && f.world != WorldId.RealRoom) { slot.cam.enabled = true; if (slot.hud != null) slot.hud.SetActive(true); }
        owner = null; wantEnter = false;
        Cursor.lockState = CursorLockMode.None;
        Debug.Log("RealRoom: exit");
    }
    float fadeBackT;

    // ---------------------------------------------------------------- input from Game (this slot's PIn)
    PIn inp;
    public static void Feed(Frog f, PIn i) { if (I != null && I.owner == f) { I.inp = i; if (i.use || i.hop || i.fire) I.skipPressed = true; } }

    void Play(float dt)
    {
        PIn i = inp; inp = new PIn();
        bool act = i.use || i.hop;
        intakeCool -= dt;
        // first / third person
        if (i.view) camDistT = camDistT > 0.3f ? 0f : 2.1f;
        if (Mathf.Abs(i.zoom) > 0.001f) { camDistT = Mathf.Clamp(camDistT + i.zoom * dt * 2.5f, 0f, 3.2f); if (camDistT < 0.45f && i.zoom < 0f) camDistT = 0f; else if (camDistT < 0.45f) camDistT = 0.9f; }
        float moveK = 0f;
        if (!sitting)
        {
            yaw += i.look.x;
            pitch = Mathf.Clamp(pitch - i.look.y, -80f, 85f);
            Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 mv = (fwd * i.move.y + right * i.move.x) * 1.45f;
            if (i.move.sqrMagnitude > 0.2f && pending != "") pending = "";   // walking away cancels a reach
            // auto-approach: walk up to what we are about to touch
            if (pending != "")
            {
                pendingT += dt;
                Vector3 to = pendingP - yawT.position; to.y = 0f;
                float stopAt = pending == "switch" ? (third ? 0.42f : 0.50f) : (third ? 0.40f : 0.42f);
                if (to.magnitude > stopAt && pendingT < 2.5f) mv += to.normalized * Mathf.Min(1.3f, (to.magnitude - stopAt) * 4f + 0.3f);
                // face it
                float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, want, 1f - Mathf.Exp(-dt * 7f));
                if (to.magnitude <= stopAt + 0.03f || pendingT > 2.5f)
                {
                    bool ok = pending == "switch" ? Hands.Poke(pendingP) : Hands.Grab(pendingP, pending == "plush" ? 0.11f : 0.075f);
                    if (ok && pending != "switch") grabRb = pending == "plush" ? plush : duck;
                    if (ok) { if (pending != "switch") crouchT = Mathf.Clamp(1.05f - (pendingP.y - RoomO.y), 0f, 0.8f); pending = ""; }
                }
            }
            moveK = new Vector2(mv.x, mv.z).magnitude / 1.45f;
            mv.y = -4f;
            body.Move(mv * dt);
            bobT += moveK * dt * 8.5f;
        }
        else
        {
            yaw = Mathf.Clamp(Mathf.DeltaAngle(seatYaw, yaw + i.look.x), -100f, 100f) + seatYaw;
            pitch = Mathf.Clamp(pitch - i.look.y, -60f, 75f);
            if (i.move.sqrMagnitude > 0.5f || i.downHeld) StandUp();
        }
        FrogBody.speed = Mathf.MoveTowards(FrogBody.speed, Mathf.Clamp01(moveK), dt * 4f);
        // crouch back up once the hand has the thing (or gave up)
        if (!Hands.Busy && pending == "") crouchT = 0f;
        crouch = Mathf.MoveTowards(crouch, crouchT, dt * 2.2f);
        // what are we looking at? (from the eyes, along the camera, in either view)
        Vector3 eyeP = pitchT.position, look = cam.transform.forward;
        Ray r = new Ray(eyeP, look);
        RaycastHit hit;
        string target = ""; Vector3 tp = Vector3.zero;
        if (Physics.Raycast(r, out hit, 2.3f, (1 << Layer) | (1 << ChairLayer), QueryTriggerInteraction.Collide))
        {
            if (duckCols.Contains(hit.collider) && held != duck) { target = "duck"; tp = duck.worldCenterOfMass; }
            else if (plushCols.Contains(hit.collider) && held != plush) { target = "plush"; tp = plush.worldCenterOfMass; }
            else if (hit.collider == switchCol) { target = "switch"; tp = SwitchFace(); }
            else if (hit.collider == doorCol) target = "door";
            else if ((hit.collider == chairCol || hit.collider == chairMeshCol) && !holding && hit.distance < 2.0f) target = "chair";
        }
        if (target == "" && !holding)
        {
            if (duck != null && Vector3.Angle(look, duck.position - eyeP) < 12f && (duck.position - eyeP).magnitude < 2.0f) { target = "duck"; tp = duck.worldCenterOfMass; }
            else if (plush != null && Vector3.Angle(look, plush.position - eyeP) < 12f && (plush.position - eyeP).magnitude < 2.0f) { target = "plush"; tp = plush.worldCenterOfMass; }
        }
        if (sitting) target = "stand";
        // anticipation: the hand drifts toward a reachable switch / toy in view
        float reachD = target == "switch" || target == "duck" || target == "plush" ? (tp - eyeP).magnitude : 9f;
        Hands.nearTarget = tp; Hands.nearW = Mathf.MoveTowards(Hands.nearW, reachD < 0.85f && !Hands.Busy ? 1f : 0f, dt * 3f);
        string pad = Badge();
        string viewHint = owner.inputKind == InputKind.Touch ? "" : owner.inputKind == InputKind.Gamepad ? "   <b>[View]</b> " + (third ? "1st" : "3rd") + " person" : "   <b>[V / wheel]</b> " + (third ? "1st" : "3rd") + " person";   // ffu23b
        if (holding) hint = pad + "  throw the " + (held == plush ? "plush frog" : "duck") + (owner.inputKind == InputKind.Touch ? "" : "   (hold to throw harder)");
        else if (target == "duck") hint = pad + "  pick up the rubber duck";
        else if (target == "plush") hint = pad + "  pick up the plush frog";
        else if (target == "switch") hint = pad + (lightsOn ? "  lights off" : "  lights on");
        else if (target == "door") hint = pad + "  leave the REAL ROOM";
        else if (target == "chair") hint = pad + "  sit in the armchair";
        else if (target == "stand") hint = pad + "  stand up";
        else hint = stT < 12f ? viewHint.Trim() : "";
        // hold-to-charge throws (RT / left mouse / A held)
        if (holding)
        {
            bool heldBtn = i.fireHeld || i.hopHeld;
            if (heldBtn) charge = Mathf.Min(1f, charge + dt * 1.2f);
            bool release = (act || i.fire) && charge < 0.05f && !heldBtn;
            if (!Hands.Busy && ((!heldBtn && charge > 0.05f) || release || (act && owner.inputKind == InputKind.Touch))) { throwK = Mathf.Max(charge, 0.25f); Hands.Throw(); Sfx.Play(Sfx.Hop, 0.5f, 1.4f); }
            Hands.charge = charge;
        }
        else if (act && intakeCool <= 0f && !Hands.Busy)
        {
            switch (target)
            {
                case "duck": pending = "duck"; pendingP = tp; pendingT = 0f; break;
                case "plush": pending = "plush"; pendingP = tp; pendingT = 0f; break;
                case "switch": pending = "switch"; pendingP = tp; pendingT = 0f; break;
                case "door": StartExit(); return;
                case "chair": SitDown(); break;
                case "stand": StandUp(); break;
            }
            intakeCool = 0.25f;
        }
        // keep the reach aimed at a toy that is still rolling
        if (pending == "duck") pendingP = duck.worldCenterOfMass; else if (pending == "plush") pendingP = plush.worldCenterOfMass;
        ApplyView();
    }
    float throwK; Rigidbody grabRb;
    public const int ChairLayer = 22;   // ffu23: armchair mesh collider (armrest raycasts, physics)

    // the switch rocker's front face (the finger pad lands here)
    Vector3 SwitchFace() { return switchPos + new Vector3(0f, -0.005f, 0.012f); }

    string Badge()
    {
        switch (owner != null ? owner.inputKind : InputKind.Keyboard)
        {
            case InputKind.Gamepad: return "<b>[A]</b>";
            case InputKind.Touch: return "<b>[tap A]</b>";
            default: return "<b>[E / Space]</b>";
        }
    }

    void ApplyView()
    {
        float bob = (st == St.Play && !sitting) ? Mathf.Sin(bobT) * 0.012f : 0f;
        yawT.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (sitting) yawT.position = Vector3.Lerp(yawT.position, seatPos, Time.deltaTime * 5f);
        float eye = sitting ? 1.08f : EyeH - crouch;
        pitchT.localPosition = new Vector3(0f, eye + bob, 0f);
        pitchT.localRotation = Quaternion.Euler(pitch + crouch * 25f, 0f, 0f);
        Hands.walk = bobT;
        // third person: orbit behind the head, pulled in by the walls
        camDist = Mathf.Lerp(camDist, camDistT, 1f - Mathf.Exp(-Time.deltaTime * 6f));
        if (camDist < 0.02f && camDistT == 0f) camDist = 0f;
        bool tp = camDist > 0.35f;
        if (tp != third) { third = tp; Hands.SetVisible(!tp); FrogBody.SetVisible(tp); }
        float d = camDist;
        if (d > 0.01f)
        {
            Vector3 piv = pitchT.position + Vector3.up * 0.08f;
            Quaternion cr = Quaternion.Euler(pitch, yaw + camOrbit, 0f);
            Vector3 want = piv + cr * new Vector3(0.18f * Mathf.Clamp01(d) * (camOrbit != 0f ? 0f : 1f), 0.04f, -d);
            Vector3 dir = want - piv; float len = dir.magnitude;
            RaycastHit h;
            if (len > 1e-3f && Physics.SphereCast(piv, 0.12f, dir / len, out h, len, (1 << Layer) | (1 << ChairLayer), QueryTriggerInteraction.Ignore) && h.collider != body)
                want = piv + dir / len * Mathf.Max(0.25f, h.distance - 0.05f);
            cam.transform.position = want;
            cam.transform.rotation = camOrbit != 0f ? Quaternion.LookRotation(piv - Vector3.up * 0.55f - want) : cr;
        }
        else { cam.transform.localPosition = Vector3.zero; cam.transform.localRotation = Quaternion.identity; }
    }

    // ---------------------------------------------------------------- interactions
    void ToggleLights()
    {
        lightsOn = !lightsOn;
        Sfx.Play(Sfx.Click != null ? Sfx.Click : Sfx.Toggle, 0.9f);
        if (rocker != null) rocker.localRotation = Quaternion.Euler(lightsOn ? -9f : 9f, 0f, 0f);
    }

    void Attach(Rigidbody rb)
    {
        held = rb; holding = true; charge = 0f;
        heldR = rb == plush ? 0.11f : 0.075f;
        Hands.holding = true; Hands.objR = heldR;
        rb.isKinematic = true;
        foreach (var c in rb == duck ? duckCols : plushCols) c.enabled = false;
        Sfx.Play(Sfx.Pickup, 0.6f, rb == plush ? 0.9f : 1.3f);
    }

    void DropHeld(Vector3 v)
    {
        if (held == null) { holding = false; Hands.holding = false; return; }
        var rb = held;
        holding = false; held = null; Hands.holding = false; Hands.charge = 0f; charge = 0f;
        Vector3 p = HeldPoint();
        // never release it inside a wall
        Vector3 eye = pitchT.position;
        RaycastHit h;
        if (Physics.Linecast(eye, p, out h, 1 << Layer, QueryTriggerInteraction.Ignore) && !duckCols.Contains(h.collider) && !plushCols.Contains(h.collider) && h.collider != body)
            p = eye + (p - eye).normalized * Mathf.Max(0.05f, h.distance - 0.14f);
        rb.transform.position = p - (rb.worldCenterOfMass - rb.transform.position);
        rb.isKinematic = false;
        foreach (var c in rb == duck ? duckCols : plushCols) c.enabled = true;
        rb.velocity = v;
        rb.angularVelocity = new Vector3(Random.Range(-6f, 6f), Random.Range(-4f, 4f), Random.Range(-6f, 6f)) * (rb == plush ? 0.5f : 1f);
    }

    // where the carried thing sits: under the first-person palm, or in the third-person body's hand
    Vector3 HeldPoint()
    {
        if (third) return FrogBody.PalmPoint(0, new Vector3(0f, -0.024f - heldR / 1.45f * 0.95f, 0.07f));
        return Hands.HoldPoint;
    }
    Quaternion HeldRot() { return third ? Quaternion.Euler(0f, yaw + 160f, 0f) : Hands.HoldRot; }

    void ResetDuck()
    {
        duck.isKinematic = false;
        duck.transform.SetPositionAndRotation(duckSpawn + Vector3.up * 0.02f, Quaternion.Euler(0f, 205f, 0f));
        duck.velocity = Vector3.zero; duck.angularVelocity = Vector3.zero;
        foreach (var c in duckCols) c.enabled = true;
    }

    void ResetPlush()
    {
        if (plush == null) return;
        plush.isKinematic = false;
        plush.transform.SetPositionAndRotation(plushSpawn + Vector3.up * 0.01f, Quaternion.Euler(0f, plushYaw, 0f));
        plush.velocity = Vector3.zero; plush.angularVelocity = Vector3.zero;
        foreach (var c in plushCols) c.enabled = true;
    }

    void SitDown()
    {
        sitting = true;
        standPos = yawT.position; standYaw = yaw;
        body.enabled = false;
        yaw = seatYaw; pitch = 8f;
        FindArmrests();
        Hands.sitting = true; FrogBody.sitting = true;
        Sfx.Play(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, 0.6f, 0.8f);
    }

    void StandUp()
    {
        sitting = false;
        yawT.position = standPos;
        body.enabled = true;
        intakeCool = 0.3f;
        Hands.sitting = false; FrogBody.sitting = false;
    }

    // the armchair's real armrest tops: ray down onto its mesh collider either side of the seat
    void FindArmrests()
    {
        armrestOk = false;
        Vector3 right = Quaternion.Euler(0f, seatYaw, 0f) * Vector3.right, fwd = Quaternion.Euler(0f, seatYaw, 0f) * Vector3.forward;
        bool okR = Armrest(right, fwd, out armrestR), okL = Armrest(-right, fwd, out armrestL);
        armrestOk = okR && okL;
        Hands.armrestR = armrestR; Hands.armrestL = armrestL; Hands.armrestOk = armrestOk;
        Debug.Log("RealRoom: armrests " + (armrestOk ? (armrestR - RoomO).ToString("F2") + " " + (armrestL - RoomO).ToString("F2") : "not found"));
    }

    bool Armrest(Vector3 side, Vector3 fwd, out Vector3 p)
    {
        p = Vector3.zero; float best = -1f;
        for (float o = 0.26f; o <= 0.46f; o += 0.02f)
            for (float f = -0.05f; f <= 0.16f; f += 0.07f)
            {
                Vector3 a = seatPos + side * o + fwd * f + Vector3.up * 1.3f;
                RaycastHit h;
                if (Physics.Raycast(a, Vector3.down, out h, 1.2f, 1 << ChairLayer, QueryTriggerInteraction.Ignore))
                {
                    float y = h.point.y - RoomO.y;
                    if (y > 0.45f && y < 0.9f && y > best + 0.01f) { best = y; p = h.point; }
                }
            }
        return best > 0f;
    }

    // duck bounce sounds
    float lastBonk;
    public void DuckHit(float speed)
    {
        if (speed < 0.8f || Time.time - lastBonk < 0.12f) return;
        lastBonk = Time.time;
        Sfx.Play(Sfx.BumpSoft != null ? Sfx.BumpSoft : Sfx.Thud, Mathf.Clamp01(speed / 6f) * 0.7f, Random.Range(1.2f, 1.5f));
    }

    // ---------------------------------------------------------------- per-frame light + shader globals
    void Globals(float dt)
    {
        lampK = Mathf.MoveTowards(lampK, lightsOn ? 1f : 0f, dt / 0.12f);
        // TV light = the average colour of the current video frame (precomputed track, 10 Hz) x brightness
        Vector3 tv = new Vector3(0.3f, 0.34f, 0.36f);
        float vt = 0f;
        VideoFrame();
        float vtm = vidOk ? VideoTime() : -1f;
        vt = vtm >= 0f ? vtm : Time.time - vidT0;
        int n = tvLight.Length / 3;
        if (n > 0 && !vidFallback)
        {
            float fi = Mathf.Repeat(vt * 10f, n);
            int a = (int)fi, b = (a + 1) % n; float f = fi - a;
            tv = Vector3.Lerp(new Vector3(tvLight[a * 3], tvLight[a * 3 + 1], tvLight[a * 3 + 2]), new Vector3(tvLight[b * 3], tvLight[b * 3 + 1], tvLight[b * 3 + 2]), f);
        }
        else if (vidFallback) { float s = Time.time * 0.15f; tv = new Vector3(0.35f + 0.1f * Mathf.Sin(s), 0.5f, 0.45f + 0.1f * Mathf.Cos(s * 1.3f)) * 0.6f; }
        tv *= TvLightK;
        Vector3 env = new Vector3(EnvW, EnvW, EnvW);
        Vector3 lamp = LampWB * lampK;
        Shader.SetGlobalVector("_RREnvCol", env);
        Shader.SetGlobalVector("_RRLampCol", lamp);
        Shader.SetGlobalVector("_RRTvCol", tv);
        for (int k = 0; k < 9; k++)
            sh[k] = new Vector4(shEnv[k].x * env.x + shLamp[k].x * lamp.x + shTv[k].x * tv.x,
                                shEnv[k].y * env.y + shLamp[k].y * lamp.y + shTv[k].y * tv.y,
                                shEnv[k].z * env.z + shLamp[k].z * lamp.z + shTv[k].z * tv.z, 0f);
        Shader.SetGlobalVectorArray("_RRSH", sh);
        for (int k = 0; k < 9; k++)
            shD[k] = new Vector4(shEnv[k].x * env.x * DynK.x + shLamp[k].x * lamp.x * DynK.y + shTv[k].x * tv.x * DynK.z,
                                 shEnv[k].y * env.y * DynK.x + shLamp[k].y * lamp.y * DynK.y + shTv[k].y * tv.y * DynK.z,
                                 shEnv[k].z * env.z * DynK.x + shLamp[k].z * lamp.z * DynK.y + shTv[k].z * tv.z * DynK.z, 0f);
        Shader.SetGlobalVectorArray("_RRSHD", shD);
        // eyes adjust: dark room -> brighter exposure over ~1.5 s
        float eref = RefLamp * lampK + RefTv * (0.2126f * tv.x + 0.7152f * tv.y + 0.0722f * tv.z) + RefEnv * EnvW;
        float target = Mathf.Clamp(1.45f / Mathf.Pow(Mathf.Max(eref, 1e-5f), 0.75f), ExpMin, ExpMax);
        expo = Mathf.Lerp(expo, target, 1f - Mathf.Exp(-dt * (target > expo ? 1.4f : 3f)));
        if (demoExpo > 0f) expo = demoExpo;
        Shader.SetGlobalFloat("_RRExposure", expo);
        Shader.SetGlobalFloat("_RRDirect", Lite ? 1f : 0f);
        Shader.SetGlobalFloat("_RRTime", Time.time);
        Shader.SetGlobalVector("_RRWaveO", new Vector4(Hands.WaveOrigin.x, Hands.WaveOrigin.y, Hands.WaveOrigin.z, waveR > 50f ? -1f : waveR));
        Shader.SetGlobalVector("_RRWaveCol", new Vector4(0.35f, 1.0f, 0.55f, 1f) * 0.6f);
        if (post != null) post.exposure = expo;
        if (globeMat != null) globeMat.SetColor("_Emis", new Color(1f, 0.86f, 0.68f, 2.2f * lampK));   // ffu18c: a touch cooler
        Hands.SetLights(lampPos, tvPos); FrogBody.SetLights(lampPos, tvPos);
        Hands.Tick(dt, st == St.Play);
        if (Hands.PokeContact) { if (demoShot == "realroom-reach") { Sfx.Play(Sfx.Click != null ? Sfx.Click : Sfx.Toggle, 0.9f); if (rocker != null) rocker.localRotation = Quaternion.Euler(9f, 0f, 0f); } else ToggleLights(); }
        if (Hands.GrabAttach && grabRb != null) { Attach(grabRb); grabRb = null; }
        if (Hands.ThrowRelease && holding)
        {
            Vector3 v = cam.transform.forward * Mathf.Lerp(3.2f, 8.5f, throwK) * (held == plush ? 0.8f : 1f) + Vector3.up * 1.2f;
            DropHeld(v); charge = 0f;
        }
        // third-person body: same hand targets as the first-person IK (wrist + palm), hanging otherwise
        if (third)
        {
            bool armR = Hands.Busy || holding || sitting || Hands.nearW > 0.01f;
            FrogBody.handT[0] = Hands.Wrist(0); FrogBody.handR[0] = Hands.HandRot(0);
            FrogBody.handW[0] = Mathf.MoveTowards(FrogBody.handW[0], armR ? 1f : 0f, dt * 5f);
            FrogBody.handT[1] = Hands.Wrist(1); FrogBody.handR[1] = Hands.HandRot(1);
            FrogBody.handW[1] = Mathf.MoveTowards(FrogBody.handW[1], sitting ? 1f : 0f, dt * 5f);
            for (int k = 0; k < 2; k++) System.Array.Copy(Hands.Curls(k), FrogBody.curl[k], 4);
            FrogBody.lookPitch = pitch;
            if (FrogBody.Root != null)
            {
                FrogBody.Root.position = sitting ? new Vector3(yawT.position.x, RoomO.y + 0.0f, yawT.position.z) : yawT.position;
                FrogBody.Root.rotation = Quaternion.Euler(0f, sitting ? seatYaw : yaw, 0f);
            }
            FrogBody.Tick(dt, bobT);
        }
        if (holding && held != null)
        {
            held.transform.rotation = HeldRot();
            held.transform.position = HeldPoint() - (held.worldCenterOfMass - held.transform.position);
        }
        if (plushBlob != null && plush != null)
        {
            Vector3 d = plush.position;
            float hgt = Mathf.Max(0f, d.y - RoomO.y);
            plushBlob.position = new Vector3(d.x, RoomO.y + 0.004f + (FloorUnderDuck(d) - RoomO.y), d.z);
            float s = Mathf.Lerp(0.42f, 0.7f, Mathf.Clamp01(hgt / 1.5f));
            plushBlob.localScale = new Vector3(s, s, 1f);
        }
        if (duckBlob != null)
        {
            Vector3 d = duck.position;
            float hgt = Mathf.Max(0f, d.y - RoomO.y);
            duckBlob.position = new Vector3(d.x, RoomO.y + 0.004f + (FloorUnderDuck(d) - RoomO.y), d.z);
            float s = Mathf.Lerp(0.36f, 0.6f, Mathf.Clamp01(hgt / 1.5f));
            duckBlob.localScale = new Vector3(s, s, 1f);
        }
    }

    float FloorUnderDuck(Vector3 p)
    {
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out h, 3f, 1 << Layer, QueryTriggerInteraction.Ignore) && !duckCols.Contains(h.collider) && !plushCols.Contains(h.collider) && h.collider != body) return h.point.y;
        return RoomO.y;
    }

    // ---------------------------------------------------------------- UI (fade, crosshair, hints)
    void MakeUi()
    {
        ui = UIK.MakeCanvas("RealRoomUI", null, 300, true);
        fade = UIK.Img(ui.transform, null, new Color(0, 0, 0, 0), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 10f);
        fade.raycastTarget = false;
        dot = UIK.Img(ui.transform, UIK.Circle, new Color(1, 1, 1, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));
        dot.raycastTarget = false;
        hintT = UIK.Label(ui.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(900f, 60f), Color.white);
        hintT.raycastTarget = false;
        if (UIK.ModernFonts) UIK.Modernize(hintT, false);
        var sh = hintT.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f); sh.effectDistance = new Vector2(1.5f, -1.5f);
        titleT = UIK.Label(ui.transform, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 80f), new Color(1, 1, 1, 0.9f));
        titleT.raycastTarget = false;
    }

    void SetFadeOverlay(float a, Color c) { if (ui == null) MakeUi(); fadeA = a; fadeC = c; if (fade != null) fade.color = new Color(c.r, c.g, c.b, a); PlaceOverHouseSlot(); }
    void SetFade(float a, Color c) { fadeA = a; fadeC = c; if (post != null) post.fade = new Color(c.r, c.g, c.b, a); if (fade != null) fade.color = new Color(c.r, c.g, c.b, Lite || post == null ? a : 0f); }
    float fadeA; Color fadeC;

    void PlaceOverHouseSlot()
    {
        if (fade == null) return;
        Rect r = slot != null && slot.cam != null ? slot.cam.rect : new Rect(0, 0, 1, 1);
        var rt = fade.rectTransform;
        rt.anchorMin = r.min; rt.anchorMax = r.max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        if (hintT != null && (st == St.Idle || st == St.Loading || st == St.Opening)) { hintT.text = ""; dot.enabled = false; titleT.text = ""; }
    }

    void LayoutUi()
    {
        if (ui == null) MakeUi();
        Rect r = cam.rect;
        var rt = fade.rectTransform; rt.anchorMin = r.min; rt.anchorMax = r.max; rt.offsetMin = rt.offsetMax = Vector2.zero;
        if (st == St.Intro && stT < 0.4f) fade.color = new Color(0, 0, 0, 1f - stT / 0.4f);
        else if (st == St.Exiting) fade.color = new Color(1, 1, 1, Lite || post == null ? fadeA : 0f);
        else fade.color = new Color(0, 0, 0, 0f);
        Vector2 c = r.center;
        dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = c;
        dot.enabled = st == St.Play && !sitting;
        dot.color = new Color(1, 1, 1, hint.Length > 0 ? 0.95f : 0.45f);
        hintT.rectTransform.anchorMin = hintT.rectTransform.anchorMax = new Vector2(c.x, r.yMin);
        bool portrait = Screen.height > Screen.width;
        hintT.rectTransform.anchoredPosition = new Vector2(0f, portrait ? 330f : 110f);
        hintT.text = st == St.Play ? hint : "";
        titleT.rectTransform.anchorMin = titleT.rectTransform.anchorMax = new Vector2(c.x, r.yMax);
        titleT.rectTransform.anchoredPosition = new Vector2(0f, portrait ? -170f : -60f);
        titleT.text = st == St.Play && stT < 4f ? "<b>REAL ROOM</b>\n<size=18>look around - the duck, the plush frog, the chair, the light switch, the door</size>" : "";
        titleT.color = new Color(1, 1, 1, Mathf.Clamp01(4f - stT) * 0.9f);
    }
}

// HDR post for the room camera (desktop tier): bloom + exposure + ACES + vignette + fade
public class RRPostFx : MonoBehaviour
{
    public Material mat;
    public float exposure = 1.5f, bloom = 0.9f;
    public Color fade = new Color(0, 0, 0, 0);
    readonly RenderTexture[] chain = new RenderTexture[6];
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null) { Graphics.Blit(src, dst); return; }
        int w = src.width / 2, h = src.height / 2, n = 0;
        var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.Default;
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Threshold", 1.1f);
        for (; n < chain.Length && w >= 8 && h >= 8; n++, w /= 2, h /= 2) chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
        Graphics.Blit(src, chain[0], mat, 0);
        for (int i = 1; i < n; i++) Graphics.Blit(chain[i - 1], chain[i], mat, 1);
        for (int i = n - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], mat, 2);
        mat.SetTexture("_Bloom", chain[0]);
        mat.SetFloat("_BloomK", bloom * 0.12f);
        mat.SetFloat("_Vignette", 0.32f);
        mat.SetFloat("_Grain", 0.004f);
        mat.SetColor("_Fade", fade);
        Graphics.Blit(src, dst, mat, 3);
        for (int i = 0; i < n; i++) { RenderTexture.ReleaseTemporary(chain[i]); chain[i] = null; }
    }
}

public class RRDuck : MonoBehaviour
{
    void OnCollisionEnter(Collision c) { if (RealRoom.I != null) RealRoom.I.DuckHit(c.relativeVelocity.magnitude); }
}
