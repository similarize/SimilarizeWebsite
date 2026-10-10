using UnityEngine;

// A froggy on foot: camera-relative hopping movement, swimming, knockback, and getting in / out of vehicles.
// Humans feed it a PIn each frame; open seats run the built-in wander AI.
public class Frog : MonoBehaviour
{
    public int id;
    public string nick;
    public Color color;
    public bool human;
    public CharacterController cc;
    public FrogModel model;
    public Vehicle vehicle;
    public Robot remote;          // ffu12: a ranch robot this froggy is driving from the phone
    public WorldId world = WorldId.Ranch;
    public float autoCool;        // after a doorway / world change, auto hotspots wait
    public float spaceGravity = 1f;   // low-g worlds (Callisto) set this per frame
    public Vehicle passengerOf;       // riding along (Starship) without driving
    public bool launching;            // aboard the Starship during the ranch blast-off (LaunchSeq)
    public bool netPuppet;            // ffu13: another device controls this froggy online (Net poses it; no local simulation)
    public InputKind inputKind = InputKind.Keyboard;   // ffu15: the device of this froggy's slot (control hints per device)
    public Starship jetOf;            // ffu15: jetpacking outside a mech flying in space (position = ship + jetOff)
    public Vector3 jetOff;
    public float jetT;                // seconds since jetpacking out (no instant re-board)
    bool scubaSurface;                // ffu15: swam up from the reef - floating in the pond in scuba gear
    float diveHoldT;
    public bool Swimming { get { return swimming; } }
    public bool InScuba { get { return scubaGo != null && scubaGo.activeSelf; } }
    public void SetScubaNet(bool on) { if (on || scubaGo != null) SetScuba(on); }
    public void SetChuteNet(bool on) { if (on != chute) SetChute(on); }

    public void BoardAsPassenger(Vehicle v)
    {
        if (vehicle != null) ExitVehicle();
        passengerOf = v;
        cc.enabled = false;
        transform.SetParent(v.seat, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        model.gameObject.SetActive(false);
    }

    // ffu15: riding on a space mech's shoulder in a spacesuit (visible, scaled up to read next to the mech)
    public void RideShoulder(Transform seat, float scale)
    {
        if (transform.parent != seat) transform.SetParent(seat, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * scale;
        if (!model.gameObject.activeSelf) model.gameObject.SetActive(true);
        SetSuit(true);
        model.Animate(0f, false, true, false, Time.deltaTime);
    }

    public void LeavePassenger()
    {
        if (passengerOf == null) return;
        SetSuit(false);
        passengerOf = null;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        model.gameObject.SetActive(true);
        cc.enabled = true;
    }
    public string prompt = "";
    public string toast = "";
    public float toastT;
    public bool chute;            // parachute after bailing out of a flyer in the air
    GameObject chuteGo;

    public void Toast(string s, float secs = 2.5f) { toast = s; toastT = secs; }

    PIn input;
    float camYaw;
    Vector3 vel;          // vertical + knockback velocity
    Vector3 planar;       // smoothed walking velocity
    float yaw;
    float hopCool;
    bool swimming, wasSwim;
    float exitCool;

    // AI
    Vector3 aiTarget;
    float aiTimer, aiHopT;

    public const float Speed = 6.5f, SwimSpeed = 3.2f, HopV = 8.5f, Gravity = 22f;

    public Vector3 Center { get { return transform.position + Vector3.up * 0.7f; } }
    public Vector3 FocusPoint { get { if (remote != null) return remote.transform.position + Vector3.up * remote.height * 0.6f; Vehicle v = vehicle != null ? vehicle : passengerOf; return v != null ? v.transform.position + Vector3.up * 1.2f : transform.position + Vector3.up * 0.9f; } }
    public float HSpeed { get { return vehicle != null ? vehicle.Speed : new Vector2(planar.x, planar.z).magnitude; } }

    public void Build(int index, Vector3 pos, float yawDeg)
    {
        id = index;
        charId = index;
        nick = Roster.Name(index);
        color = Roster.Color(index);
        gameObject.name = "Frog " + nick;
        gameObject.layer = 9;
        transform.position = pos;
        yaw = yawDeg;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.1f;
        cc.radius = 0.42f;
        cc.center = new Vector3(0f, 0.56f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.45f;
        cc.skinWidth = 0.05f;
        var mg = new GameObject("Model");
        mg.transform.SetParent(transform, false);
        model = mg.AddComponent<FrogModel>();
        model.BuildChar(index);
        aiTarget = pos;
        aiTimer = Random.Range(1f, 4f);
    }

    // ffu14: which roster character this seat is (0..3 frogs, 4..7 cats, 8..9 dogs). Rebuilds the model in place.
    public int charId;
    public bool IsPet { get { return !Roster.IsFrog(charId); } }
    public void SetChar(int c)
    {
        if (!Roster.Valid(c) || (c == charId && model != null && model.charId == c)) return;
        charId = c;
        color = Roster.Color(c);
        bool vis = model == null || model.gameObject.activeSelf;
        if (model != null) { model.gameObject.SetActive(false); Destroy(model.gameObject); }
        scubaGo = null; suitGo = null; finL = finR = null;   // they hung under the old model
        if (chuteGo != null) { Destroy(chuteGo); chuteGo = null; chute = false; }
        var mg = new GameObject("Model");
        mg.transform.SetParent(transform, false);
        model = mg.AddComponent<FrogModel>();
        model.BuildChar(c);
        mg.SetActive(vis);
        gameObject.name = "Frog " + Roster.Name(c);
    }
    // a little voice: ribbit / meow / bark
    void Voice(bool mine, Vector3 p, float vol)
    {
        AudioClip c = IsPet ? Sfx.AnimalVoice(Roster.KindOf(charId) == Roster.Kind.Cat ? "Cat" : "Dog") : Sfx.Pick(Sfx.Ribbit);
        if (c == null) return;
        if (mine) Sfx.Play(c, vol, Random.Range(0.95f, 1.2f)); else Sfx.PlayAt(c, p, vol, 40f, Random.Range(0.9f, 1.15f));
    }

    public void SetInput(PIn i, float cameraYaw)
    {
        input = i;
        camYaw = cameraYaw;
    }

    public void Knock(Vector3 impulse)
    {
        if (vehicle != null) return;
        vel += impulse;
        if (vel.y < 3f) vel.y = 3f;
    }

    // move to another world (or another spot in this one) facing yawDeg
    public void SendTo(WorldId w, Vector3 p, float yawDeg)
    {
        if (remote != null) remote.ReleaseManual(false);
        if (vehicle != null) ExitVehicle();
        LeavePassenger();
        if (jetOf != null) { jetOf = null; transform.localScale = Vector3.one; }
        SetSuit(false);
        if (cc != null && !cc.enabled && vehicle == null && passengerOf == null) cc.enabled = true;
        SetChute(false);
        if (world != w) { toast = ""; toastT = 0f; }   // ffu14: an old world's toast (space) no longer lingers after the trip
        world = w;
        Teleport(p);
        yaw = yawDeg;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        autoCool = 1.5f;
        exitCool = 0.4f;
    }

    // demo / screenshot mode: hold this frog at a spot, facing yaw
    public void DemoPose(Vector3 p, float yawDeg)
    {
        if (vehicle != null || world != WorldId.Ranch) return;
        p.y = Ranch.GY(p.x, p.z) + 0.05f;
        if ((transform.position - p).sqrMagnitude > 0.01f) Teleport(p);
        yaw = yawDeg;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    public void Teleport(Vector3 p)
    {
        bool was = cc.enabled;
        cc.enabled = false;
        transform.position = p;
        cc.enabled = was;
        vel = Vector3.zero;
        planar = Vector3.zero;
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (toastT > 0f) { toastT -= dt; if (toastT <= 0f) toast = ""; }
        if (netPuppet) { input = new PIn(); return; }   // ffu13: posed by Net from the owner's device
        if (launching)
        {
            prompt = "Starship launch - A / FIRE skips";
            if (human && (input.use || input.fire || input.hop)) LaunchSeq.Skip(this);
            input = new PIn();
            return;
        }
        if (!human) Think(dt);
        exitCool -= dt;
        if (passengerOf != null)
        {
            Starship st = passengerOf as Starship;
            if (st != null && st.mechForm != null && st.BayOpen)
            {
                prompt = "Bay doors open - " + (inputKind == InputKind.Gamepad ? "A" : inputKind == InputKind.Touch ? "OUT" : "SPACE / E") + ": jetpack out into space";
                if (human && (input.hop || input.use)) { st.ExitToJet(this); input = new PIn(); return; }
            }
            else prompt = "Riding along in the " + passengerOf.Title + (passengerOf.driver != null ? " - " + passengerOf.driver.nick + " is flying" : "");
            input = new PIn();
            return;
        }
        if (jetOf != null) { Jetpack(dt); input = new PIn(); return; }
        if (remote != null && human && vehicle == null)
        {
            // ffu12: driving a robot from the phone - the froggy stands still, its input goes to the robot (Game)
            input = new PIn();
            Walk(dt);
            prompt = "Driving " + remote.robotName + " (" + remote.Pct + "): stick walk, RT run, A wave  |  LB / P / PHONE: back to auto";
            return;
        }
        if (vehicle != null)
        {
            prompt = vehicle.HelpLine;
            if (input.use && exitCool <= 0f && vehicle.CanExit(this)) ExitVehicle();
            else vehicle.Drive(input, camYaw, dt);
            model.Animate(0f, false, true, false, dt);
            input = new PIn();
            return;
        }
        Walk(dt);
        input = new PIn();
    }

    void Walk(float dt)
    {
        // nearest free vehicle, or a hotspot (door, tank, toy box, person...) if that is closer
        Vehicle near = Vehicle.Nearest(transform.position, this);
        float hd;
        Hotspot hs = Interact.Nearest(transform.position, this, out hd);
        autoCool -= dt;
        if (hs != null && hs.auto)
        {
            if (autoCool <= 0f) { autoCool = 1.5f; hs.act(this); return; }
            hs = null;
        }
        if (hs != null && near != null && near.body != null && (near.body.ClosestPoint(transform.position) - transform.position).magnitude < hd) hs = null;
        prompt = "";
        if (hs != null) prompt = "A / E: " + hs.Label(this);
        else if (near != null)
        {
            if (near.driver != null) prompt = near.Title + " - " + near.driver.nick + " is driving";
            else if (near.Wrecked && !(near is StoryMech)) prompt = near.WreckedLine;
            else if (!near.CanEnter(this)) prompt = near.DeniedLine;
            else prompt = "A / E: " + near.EnterVerb;
        }
        if (input.use && hs != null && exitCool <= 0f)
        {
            exitCool = 0.35f;
            hs.act(this);
            return;
        }
        if (input.use && hs == null && near != null && near.driver == null && exitCool <= 0f && near.CanEnter(this) && !near.Wrecked)
        {
            EnterVehicle(near);
            return;
        }

        if (world == WorldId.Underwater) { Scuba(dt); return; }
        if (world == WorldId.Space && SpaceWorld.I != null && SpaceWorld.I.ship != null) { SpaceWorld.I.ship.AdoptStray(this); return; }
        spaceGravity = world == WorldId.Mars ? 0.45f : world == WorldId.Callisto ? 0.22f : 1f;
        Vector3 p = transform.position;
        swimming = world == WorldId.Ranch && Layout.InPond(p.x, p.z) && p.y < Layout.WaterY - 0.35f;
        if (swimming && !wasSwim) { FX.Splash(p + Vector3.up * 0.5f, 14); if (human) Sfx.Play(Sfx.Splash, 0.9f); }
        wasSwim = swimming;
        // ffu15: floating at the pond surface - hold DOWN / DIVE to dive back down to the reef (underwater world)
        if (!swimming) scubaSurface = false;
        SetScuba(swimming && scubaSurface);
        if (swimming && human && UnderwaterWorld.I != null)
        {
            if (prompt.Length == 0) prompt = (inputKind == InputKind.Gamepad ? "Hold B" : inputKind == InputKind.Touch ? "Hold DIVE" : "Hold SHIFT") + ": dive down to the reef   |   swim to shore to climb out";
            diveHoldT = input.downHeld ? diveHoldT + dt : 0f;
            if (diveHoldT > 0.3f) { diveHoldT = 0f; scubaSurface = false; UnderwaterWorld.I.DiveFromPond(this); return; }
        }
        else diveHoldT = 0f;

        Vector3 wish = Vector3.zero;
        if (input.move.sqrMagnitude > 0.0001f)
        {
            Quaternion cy = Quaternion.Euler(0f, camYaw, 0f);
            wish = cy * new Vector3(input.move.x, 0f, input.move.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
        }
        float spd = swimming ? SwimSpeed : Roster.RunSpeed(charId);
        planar = Vector3.MoveTowards(planar, wish * spd, (cc.isGrounded || swimming ? 40f : 12f) * dt);
        if (wish.sqrMagnitude > 0.01f)
        {
            float target = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, target, Roster.TurnRate(charId) * dt);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        hopCool -= dt;
        if (swimming)
        {
            float wantY = Layout.WaterY - 0.55f;
            vel.y = Mathf.Lerp(vel.y, (wantY - p.y) * 4f, dt * 5f);
            if (input.hop) { vel.y = HopV * 0.8f; FX.Splash(p + Vector3.up * 0.5f, 10); if (human) Sfx.Play(Sfx.Splash, 0.8f); }
            if (Random.value < planar.magnitude * dt * 2f) FX.Splash(p + Vector3.up * 0.4f, 1);
        }
        else
        {
            if (cc.isGrounded)
            {
                if (vel.y < -7f && human) Sfx.Play(Sfx.Land, Mathf.Clamp01(-vel.y / 18f) * 0.6f, Random.Range(0.9f, 1.1f));   // ffu10 landing thump
                if (vel.y < -2f) vel.y = -2f;
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                vel.z = Mathf.MoveTowards(vel.z, 0f, 30f * dt);
                if (input.hop && hopCool <= 0f) { vel.y = Roster.JumpV(charId); hopCool = 0.25f; float hp = IsPet ? 1.35f : 1f; if (human) { Sfx.Play(Sfx.Hop, IsPet ? 0.45f : 0.6f, Random.Range(0.92f, 1.1f) * hp); if (Random.value < 0.2f) Voice(true, transform.position, 0.5f); } else Sfx.PlayAt(Sfx.Hop, transform.position, 0.35f, 25f, Random.Range(1.0f, 1.2f) * hp); }
            }
            else
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 3f * dt);
                vel.z = Mathf.MoveTowards(vel.z, 0f, 3f * dt);
            }
            vel.y -= Gravity * spaceGravity * dt;
            if (chute)
            {
                if (vel.y < -4f) vel.y = Mathf.MoveTowards(vel.y, -4f, 40f * dt);
                if (cc.isGrounded) SetChute(false);
            }
        }
        if (swimming && chute) SetChute(false);
        cc.Move((planar + vel) * dt + VehiclePush(dt));
        if (transform.position.y < Worlds.KillY(world)) Worlds.Respawn(this);
        model.Animate(new Vector2(planar.x, planar.z).magnitude, !cc.isGrounded && !swimming, false, swimming, dt);
        if (chuteGo != null && chute) chuteGo.transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2f) * 6f, 0f, Mathf.Sin(Time.time * 1.3f) * 8f);
    }

    // ---------- scuba (underwater world) ----------
    // ffu15: real scuba kit - twin yellow tanks with valves + harness, regulator hose to the mouth, framed mask with a
    // head strap, orange snorkel, big flippers that kick, and a bubble stream (plus a burst on every breath out)
    GameObject scubaGo;
    Transform finL, finR;
    float bubbleT, surfaceT, breathT;
    void SetScuba(bool on)
    {
        if (on && scubaGo == null)
        {
            // fitted to the smooth frog mesh (x1.1): eyes at (+-0.22, 0.99, 0.22), mouth ~(0, 0.72, 0.5), back ~z -0.48,
            // feet ~y 0.04; parented to the bob so the kit follows the swim tilt
            scubaGo = new GameObject("Scuba");
            Transform g = scubaGo.transform;
            g.SetParent(model.bob != null ? model.bob : model.transform, false);
            Material tank = Mats.Shiny(new Color(1f, 0.82f, 0.08f)), steel = Mats.Steel(new Color(0.75f, 0.76f, 0.78f));
            Material black = Mats.Lit(new Color(0.06f, 0.06f, 0.07f)), orange = Mats.Shiny(new Color(1f, 0.45f, 0.08f));
            Material fin = Mats.Shiny(new Color(0.1f, 0.35f, 1f));
            System.Action<Vector3, Vector3, float, Material> Tube = (A, B, r, m) =>
            {
                var c = Mats.Prim(PrimitiveType.Cylinder, g, (A + B) * 0.5f, new Vector3(r, (B - A).magnitude * 0.5f, r), m);
                c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (B - A).normalized);
            };
            for (int k = -1; k <= 1; k += 2)
            {
                Mats.Prim(PrimitiveType.Capsule, g, new Vector3(0.12f * k, 0.6f, -0.64f), new Vector3(0.21f, 0.28f, 0.21f), tank);   // twin tanks
                Mats.Prim(PrimitiveType.Cylinder, g, new Vector3(0.12f * k, 0.92f, -0.64f), new Vector3(0.07f, 0.04f, 0.07f), steel);
                Mats.Prim(PrimitiveType.Cylinder, g, new Vector3(0.12f * k, 0.6f, -0.64f), new Vector3(0.225f, 0.02f, 0.225f), black);   // tank band
            }
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0.94f, -0.64f), new Vector3(0.3f, 0.04f, 0.05f), steel);              // manifold
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0.55f, 0.535f), new Vector3(0.5f, 0.045f, 0.04f), black);             // chest strap
            // regulator hose round the right cheek to the mouthpiece
            Vector3 v0 = new Vector3(0.12f, 0.95f, -0.64f), v1 = new Vector3(0.46f, 0.86f, -0.12f), v2 = new Vector3(0.3f, 0.74f, 0.42f), v3 = new Vector3(0.06f, 0.72f, 0.54f);
            Tube(v0, v1, 0.055f, black); Tube(v1, v2, 0.055f, black); Tube(v2, v3, 0.055f, black);
            Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0f, 0.72f, 0.55f), new Vector3(0.15f, 0.1f, 0.08f), black);           // mouthpiece
            // mask: open frame round both eyes + tinted lens + head strap
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 1.15f, 0.38f), new Vector3(0.74f, 0.05f, 0.07f), black);
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0.84f, 0.38f), new Vector3(0.74f, 0.05f, 0.07f), black);
            for (int k = -1; k <= 1; k += 2) Mats.Prim(PrimitiveType.Cube, g, new Vector3(0.37f * k, 1.0f, 0.38f), new Vector3(0.05f, 0.36f, 0.07f), black);
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 1.0f, 0.4f), new Vector3(0.7f, 0.28f, 0.02f), Mats.GlassTint(new Color(0.55f, 0.85f, 1f, 0.35f)));
            for (int k = -1; k <= 1; k += 2) Mats.Prim(PrimitiveType.Cube, g, new Vector3(0.42f * k, 1.0f, 0.06f), new Vector3(0.04f, 0.06f, 0.64f), black);
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 1.0f, -0.24f), new Vector3(0.86f, 0.06f, 0.04f), black);
            Mats.Prim(PrimitiveType.Cylinder, g, new Vector3(-0.46f, 1.24f, 0.22f), new Vector3(0.06f, 0.24f, 0.06f), orange);    // snorkel
            // flippers on the feet (kick in ScubaFx)
            finL = Mats.Node(g, "FinL", new Vector3(-0.36f, 0.05f, 0.02f));
            finR = Mats.Node(g, "FinR", new Vector3(0.36f, 0.05f, 0.02f));
            foreach (Transform f in new[] { finL, finR })
            {
                Mats.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0f, 0.34f), new Vector3(0.3f, 0.03f, 0.55f), fin);
                Mats.Prim(PrimitiveType.Cube, f, new Vector3(0f, 0.025f, 0.03f), new Vector3(0.22f, 0.07f, 0.18f), black);
            }
            Mats.SetLayer(scubaGo, 9);
        }
        if (scubaGo != null && scubaGo.activeSelf != on) scubaGo.SetActive(on);
    }

    void ScubaFx(float dt, float kick)
    {
        float t = Time.time * (3f + kick * 4f) + id;
        if (finL != null) { finL.localRotation = Quaternion.Euler(Mathf.Sin(t) * 26f + 10f, 0f, 0f); finR.localRotation = Quaternion.Euler(-Mathf.Sin(t) * 26f + 10f, 0f, 0f); }
        Vector3 mouth = transform.TransformPoint(new Vector3(0f, 0.74f, 0.58f));
        bubbleT -= dt;
        if (bubbleT <= 0f) { bubbleT = Random.Range(0.12f, 0.3f); FX.Bubble(mouth, Random.Range(1, 3)); }
        breathT -= dt;
        if (breathT <= 0f) { breathT = Random.Range(1.4f, 2.0f); FX.Bubble(mouth + Vector3.up * 0.1f, 9); }
    }

    void Scuba(float dt)
    {
        SetScuba(true);
        Vector3 p = transform.position;
        Quaternion cy = Quaternion.Euler(0f, camYaw, 0f);
        Vector3 wish = cy * new Vector3(input.move.x, 0f, input.move.y);
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        float up = Mathf.Clamp(input.climb + (input.hopHeld ? 1f : 0f) - (input.downHeld ? 1f : 0f), -1f, 1f);
        planar = Vector3.MoveTowards(planar, wish * 4.6f, 7f * dt);
        float wantY = up * 3.4f + 0.22f;   // gentle buoyancy: drift up slowly with no input
        vel.x = Mathf.MoveTowards(vel.x, 0f, 6f * dt); vel.z = Mathf.MoveTowards(vel.z, 0f, 6f * dt);
        vel.y = Mathf.MoveTowards(vel.y, wantY, 5f * dt);
        float top = Worlds.UnderO.y - 0.9f;
        if (p.y > top && vel.y > 0f) vel.y = 0f;
        if (wish.sqrMagnitude > 0.01f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg, 360f * dt);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cc.Move((planar + vel) * dt + VehiclePush(dt));
        ScubaFx(dt, planar.magnitude / 4.6f);
        // ffu15: swim up to the surface and keep rising -> pop up FLOATING in the ranch pond (hold DOWN there to dive again)
        if (p.y > top - 0.3f && up > 0.3f) surfaceT += dt; else surfaceT = 0f;
        if (human && surfaceT > 0.3f && surfaceT - dt <= 0.3f) Toast("At the surface - keep rising to pop up in the pond", 1.6f);
        if (surfaceT > 1.0f && UnderwaterWorld.I != null) { surfaceT = 0f; scubaSurface = true; UnderwaterWorld.I.SurfaceSwim(this); return; }
        prompt = prompt.Length > 0 ? prompt : (inputKind == InputKind.Gamepad ? "A / RT up   B / LT down" : inputKind == InputKind.Touch ? "UP / DOWN to swim up and down" : "SPACE up   SHIFT down") + "   |   swim to the top to surface";
        model.Animate(planar.magnitude, false, false, true, dt);
    }

    // ---------- ffu15 spacesuit + jetpack (outside a mech flying in space) ----------
    GameObject suitGo;
    Transform[] jetNoz = new Transform[2];
    public void SetSuit(bool on)
    {
        if (on && suitGo == null)
        {
            suitGo = new GameObject("Spacesuit");
            Transform g = suitGo.transform;
            g.SetParent(model.bob != null ? model.bob : model.transform, false);
            Material white = Mats.Shiny(new Color(0.94f, 0.95f, 0.97f)), dark = Mats.Lit(new Color(0.12f, 0.13f, 0.15f));
            Material stripe = Mats.Shiny(new Color(1f, 0.5f, 0.1f));
            var helm = Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0f, 0.93f, 0.1f), new Vector3(1.04f, 0.95f, 1.06f), Mats.GlassTint(new Color(0.7f, 0.9f, 1f, 0.28f)));
            helm.name = "Helmet";
            Mats.Prim(PrimitiveType.Cylinder, g, new Vector3(0f, 0.6f, 0.08f), new Vector3(0.82f, 0.05f, 0.82f), white);        // collar ring
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0.62f, -0.6f), new Vector3(0.56f, 0.6f, 0.3f), white);           // life-support pack
            Mats.Prim(PrimitiveType.Cube, g, new Vector3(0f, 0.8f, -0.76f), new Vector3(0.5f, 0.08f, 0.03f), stripe);
            Mats.Prim(PrimitiveType.Sphere, g, new Vector3(0.14f, 0.5f, 0.53f), new Vector3(0.1f, 0.1f, 0.05f), Mats.Unlit(new Color(0.4f, 1f, 0.6f)));   // chest light
            for (int k = 0; k < 2; k++)
            {
                var nz = Mats.Prim(PrimitiveType.Cylinder, g, new Vector3(k == 0 ? -0.17f : 0.17f, 0.26f, -0.62f), new Vector3(0.13f, 0.1f, 0.13f), dark);
                jetNoz[k] = nz.transform;
            }
            Mats.SetLayer(suitGo, 9);
        }
        if (suitGo != null && suitGo.activeSelf != on) suitGo.SetActive(on);
    }

    void Jetpack(float dt)
    {
        Starship st = jetOf;
        if (st == null || world != WorldId.Space) { jetOf = null; SetSuit(false); transform.localScale = Vector3.one; return; }
        SetSuit(true);
        float S = Starship.RiderScale;
        if (transform.localScale.x != S) transform.localScale = Vector3.one * S;
        Vector3 wish;
        float up;
        if (human)
        {
            Quaternion cy = Quaternion.Euler(0f, camYaw, 0f);
            wish = cy * new Vector3(input.move.x, 0f, input.move.y);
            up = Mathf.Clamp(input.climb + (input.hopHeld ? 1f : 0f) - (input.downHeld ? 1f : 0f), -1f, 1f);
        }
        else
        {
            // AI / demo froggies: lazy loops around the bay
            float t = Time.time * 0.35f + id * 2.1f;
            Vector3 goal = st.BayWorld - st.transform.position + new Vector3(Mathf.Cos(t) * 9f, 3.5f + Mathf.Sin(t * 1.7f) * 2.5f, Mathf.Sin(t) * 9f);
            Vector3 d = goal - jetOff;
            wish = new Vector3(d.x, 0f, d.z) * 0.25f; up = Mathf.Clamp(d.y * 0.3f, -1f, 1f);
        }
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        planar = Vector3.MoveTowards(planar, wish * 7f, 9f * dt);
        vel.y = Mathf.MoveTowards(vel.y, up * 5f, 8f * dt);
        jetOff += (planar + Vector3.up * vel.y) * dt;
        if (jetOff.magnitude > 70f) jetOff = jetOff.normalized * 70f;   // tethered: never drift away from the mech
        transform.position = st.transform.position + jetOff;
        if (wish.sqrMagnitude > 0.01f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg, 240f * dt);
        transform.rotation = Quaternion.Euler(Mathf.Clamp(planar.magnitude * 3f, 0f, 20f), yaw, 0f);
        // jet puffs from the backpack nozzles
        float thrust = Mathf.Clamp01(planar.magnitude / 7f + Mathf.Abs(vel.y) / 5f) * 0.8f + 0.2f;
        for (int k = 0; k < 2; k++)
            if (jetNoz[k] != null && Random.value < dt * (8f + thrust * 30f))
            {
                Vector3 np = jetNoz[k].position;
                FX.Spray(np - transform.up * 0.3f * S, -transform.up * (3f + thrust * 4f) + Random.insideUnitSphere * 0.8f, 0.45f * S, 0.7f, new Color(0.95f, 0.97f, 1f, 0.6f));
                if (thrust > 0.45f) FX.Flame(np, -transform.up);
            }
        model.Animate(0.5f, true, false, false, dt);
        // re-board: fly back into the open bay
        if (st.BayOpen)
        {
            float d = (transform.position - st.BayWorld).magnitude;
            prompt = d < 8f && jetT > 2.5f ? (inputKind == InputKind.Gamepad ? "A" : inputKind == InputKind.Touch ? "A" : "SPACE / E") + ": climb back into the bay" : "Jetpack: stick move, " + (inputKind == InputKind.Gamepad ? "A up / B down" : inputKind == InputKind.Touch ? "UP / DOWN" : "SPACE up / SHIFT down") + "  |  fly back to the open bay to re-board";
            jetT += dt;
            if (jetT > 2.5f && (d < 4.5f || (d < 8f && human && input.use))) st.Reboard(this);
        }
        else prompt = "The bay is closed - wait for " + (st.driver != null ? st.driver.nick : "the pilot") + " to open it";
    }

    void SetChute(bool on)
    {
        chute = on;
        if (on && chuteGo == null)
        {
            chuteGo = new GameObject("Chute");
            chuteGo.transform.SetParent(transform, false);
            chuteGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            Color c = Color.Lerp(color, Color.white, 0.35f);
            Mats.Prim(PrimitiveType.Sphere, chuteGo.transform, new Vector3(0f, 2.4f, 0f), new Vector3(3.2f, 1.2f, 3.2f), Mats.Lit(c));
            Mats.Prim(PrimitiveType.Sphere, chuteGo.transform, new Vector3(0f, 2.15f, 0f), new Vector3(3.0f, 0.9f, 3.0f), Mats.Lit(Color.white));
            for (int k = 0; k < 4; k++)
            {
                Vector3 e = Quaternion.Euler(0f, 45f + k * 90f, 0f) * new Vector3(0f, 2.1f, 1.3f);
                var g = Mats.Prim(PrimitiveType.Cube, chuteGo.transform, e * 0.5f, new Vector3(0.03f, e.magnitude, 0.03f), Mats.Lit(Color.white));
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, e.normalized);
            }
            Mats.SetLayer(chuteGo, 9);
        }
        if (chuteGo != null) chuteGo.SetActive(on);
    }

    // Frogs and vehicles don't physically collide (layer 8/9 ignored), so keep frogs out of vehicle bodies
    // here, and bonk them into the air when a vehicle hits them at speed.
    float bonkCool;
    Vector3 VehiclePush(float dt)
    {
        bonkCool -= dt;
        Vector3 push = Vector3.zero;
        Vector3 c = Center;
        foreach (Vehicle v in Vehicle.All)
        {
            if (v == null || v.body == null || v.driver == this) continue;
            Vector3 cp = v.body.ClosestPoint(c);
            Vector3 d = c - cp;
            float dist = d.magnitude;
            if (dist > 0.55f) continue;
            if (dist < 0.001f) d = c - v.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
            d.Normalize();
            push += d * Mathf.Max(0.55f - dist, 0.05f) * 0.6f;
            if (v.Speed > 4f && bonkCool <= 0f)
            {
                bonkCool = 0.7f;
                Vector3 hv = v.Velocity; hv.y = 0f;
                vel = hv * 0.8f + d * 4f + Vector3.up * (5f + v.Speed * 0.25f);
            }
        }
        return push;
    }

    public void EnterVehicle(Vehicle v)
    {
        if (v == null || v.driver != null) return;
        vehicle = v;
        v.driver = this;
        cc.enabled = false;
        transform.SetParent(v.seat, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one * v.seatScale;
        model.gameObject.SetActive(v.showDriver);
        exitCool = 0.4f;
        planar = Vector3.zero; vel = Vector3.zero;
        SetChute(false);
        v.OnEnter();
        if (!netPuppet) Sfx.Play(Sfx.Door, 0.8f);
        if (human) Voice(true, transform.position, 0.45f);
    }

    public void ExitVehicle()
    {
        Vehicle v = vehicle;
        if (v == null) return;
        if (netPuppet && Net.I != null) Net.I.ReleaseVeh(v);   // ffu13: back to local physics first
        vehicle = null;
        v.driver = null;
        v.OnExit();
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        model.gameObject.SetActive(true);
        Vector3 spot;
        bool bail = false;
        Flyer fl = v as Flyer;
        if (fl != null && fl.AltitudeAboveGround > 2.5f)
        {
            // bail out beside the aircraft and float down under a parachute
            bail = true;
            Vector3 side = v.transform.right; side.y = 0f; side.Normalize();
            spot = v.transform.position + side * (v.body.size.x * 0.5f + 1.4f) - Vector3.up * 0.6f;
            if (Physics.CheckCapsule(spot + Vector3.up * 0.5f, spot + Vector3.up * 1.0f, 0.42f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore))
                spot = v.transform.position - Vector3.up * 2.2f;
        }
        else if (v is Submarine)
        {
            Vector3 side = v.transform.right; side.y = 0f; side.Normalize();
            spot = v.transform.position + side * 2.6f - Vector3.up * 0.4f;
            if (Physics.CheckCapsule(spot + Vector3.up * 0.5f, spot + Vector3.up * 1.0f, 0.42f, Vehicle.GroundMask, QueryTriggerInteraction.Ignore))
                spot = v.transform.position + Vector3.up * 2.2f;
        }
        else spot = v.ExitPoint();
        transform.rotation = Quaternion.Euler(0f, v.transform.eulerAngles.y, 0f);
        yaw = v.transform.eulerAngles.y;
        cc.enabled = true;
        Teleport(spot);
        vel = v.Velocity * 0.5f;
        if (bail) { vel.y = Mathf.Min(vel.y, 0f); SetChute(true); Toast("Parachute! The " + v.Title + " flies itself home.", 3f); }
        exitCool = 0.4f;
        if (!netPuppet) Sfx.Play(Sfx.Door, 0.8f);
    }

    float aiRibbitT = 5f;
    // ---------- AI wander ----------
    void Think(float dt)
    {
        var i = new PIn();
        if (vehicle != null) { input = i; return; }
        aiTimer -= dt;
        Vector3 p = transform.position;
        Vector3 to = aiTarget - p; to.y = 0f;
        if (aiTimer <= 0f || to.magnitude < 1.5f)
        {
            aiTimer = Random.Range(4f, 10f);
            for (int k = 0; k < 10; k++)
            {
                Vector3 c = Random.value < 0.6f ? new Vector3(-10f, 0f, 40f) : new Vector3(-40f, 0f, 20f);
                Vector3 t = c + new Vector3(Random.Range(-45f, 45f), 0f, Random.Range(-30f, 30f));
                if (Layout.InPond(t.x, t.z)) continue;
                if (Mathf.Abs(t.x - Layout.HouseC.x) < 25f && Mathf.Abs(t.z - Layout.HouseC.y) < 17f) continue;
                aiTarget = t; break;
            }
            if (Random.value < 0.3f) aiTarget = p; // sit a while
        }
        if (to.magnitude > 1.5f)
        {
            Vector3 d = to.normalized;
            i.move = new Vector2(d.x, d.z) * 0.75f;
        }
        aiHopT -= dt;
        if (aiHopT <= 0f) { aiHopT = Random.Range(2f, 6f); i.hop = to.magnitude > 1.5f || Random.value < 0.3f; }
        // ffu10: AI froggies ribbit now and then (heard when a player froggy is near)
        aiRibbitT -= dt;
        if (aiRibbitT <= 0f) { aiRibbitT = Random.Range(7f, 18f); Voice(false, p + Vector3.up * 0.5f, 0.55f); }
        // blocked? hop
        if (cc.isGrounded && to.magnitude > 2f && planar.magnitude < 0.6f && Random.value < dt) i.hop = true;
        input = i;
        camYaw = 0f;
    }
}
