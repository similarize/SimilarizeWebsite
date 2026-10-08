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
    public string prompt = "";

    PIn input;
    float camYaw;
    Vector3 vel;          // vertical + knockback velocity
    Vector3 planar;       // smoothed walking velocity
    float yaw;
    float hopCool;
    bool swimming;
    float exitCool;

    // AI
    Vector3 aiTarget;
    float aiTimer, aiHopT;

    public const float Speed = 6.5f, SwimSpeed = 3.2f, HopV = 8.5f, Gravity = 22f;

    public Vector3 Center { get { return transform.position + Vector3.up * 0.7f; } }
    public Vector3 FocusPoint { get { return vehicle != null ? vehicle.transform.position + Vector3.up * 1.2f : transform.position + Vector3.up * 0.9f; } }
    public float HSpeed { get { return vehicle != null ? vehicle.Speed : new Vector2(planar.x, planar.z).magnitude; } }

    public void Build(int index, Vector3 pos, float yawDeg)
    {
        id = index;
        nick = Froggies.Names[index];
        color = Froggies.Color(index);
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
        model.Build(color);
        aiTarget = pos;
        aiTimer = Random.Range(1f, 4f);
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
        if (!human) Think(dt);
        exitCool -= dt;
        if (vehicle != null)
        {
            prompt = vehicle.HelpLine;
            if (input.use && exitCool <= 0f) ExitVehicle();
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
        // nearest free vehicle
        Vehicle near = Vehicle.Nearest(transform.position, this);
        prompt = "";
        if (near != null)
        {
            if (near.driver != null) prompt = near.Title + " - " + near.driver.nick + " is driving";
            else prompt = "A / E: " + near.EnterVerb;
        }
        if (input.use && near != null && near.driver == null && exitCool <= 0f)
        {
            EnterVehicle(near);
            return;
        }

        Vector3 p = transform.position;
        swimming = Layout.InPond(p.x, p.z) && p.y < Layout.WaterY - 0.35f;

        Vector3 wish = Vector3.zero;
        if (input.move.sqrMagnitude > 0.0001f)
        {
            Quaternion cy = Quaternion.Euler(0f, camYaw, 0f);
            wish = cy * new Vector3(input.move.x, 0f, input.move.y);
            if (wish.sqrMagnitude > 1f) wish.Normalize();
        }
        float spd = swimming ? SwimSpeed : Speed;
        planar = Vector3.MoveTowards(planar, wish * spd, (cc.isGrounded || swimming ? 40f : 12f) * dt);
        if (wish.sqrMagnitude > 0.01f)
        {
            float target = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
            yaw = Mathf.MoveTowardsAngle(yaw, target, 720f * dt);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        hopCool -= dt;
        if (swimming)
        {
            float wantY = Layout.WaterY - 0.55f;
            vel.y = Mathf.Lerp(vel.y, (wantY - p.y) * 4f, dt * 5f);
            if (input.hop) { vel.y = HopV * 0.8f; FX.Splash(p + Vector3.up * 0.5f, 10); }
            if (Random.value < planar.magnitude * dt * 2f) FX.Splash(p + Vector3.up * 0.4f, 1);
        }
        else
        {
            if (cc.isGrounded)
            {
                if (vel.y < -2f) vel.y = -2f;
                vel.x = Mathf.MoveTowards(vel.x, 0f, 30f * dt);
                vel.z = Mathf.MoveTowards(vel.z, 0f, 30f * dt);
                if (input.hop && hopCool <= 0f) { vel.y = HopV; hopCool = 0.25f; }
            }
            else
            {
                vel.x = Mathf.MoveTowards(vel.x, 0f, 3f * dt);
                vel.z = Mathf.MoveTowards(vel.z, 0f, 3f * dt);
            }
            vel.y -= Gravity * dt;
        }
        cc.Move((planar + vel) * dt + VehiclePush(dt));
        if (transform.position.y < -30f) Teleport(new Vector3(-6f + id * 3f, 2f, 40f));
        model.Animate(new Vector2(planar.x, planar.z).magnitude, !cc.isGrounded && !swimming, false, swimming, dt);
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
        v.OnEnter();
    }

    public void ExitVehicle()
    {
        Vehicle v = vehicle;
        if (v == null) return;
        vehicle = null;
        v.driver = null;
        v.OnExit();
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
        model.gameObject.SetActive(true);
        Vector3 spot = v.ExitPoint();
        transform.rotation = Quaternion.Euler(0f, v.transform.eulerAngles.y, 0f);
        yaw = v.transform.eulerAngles.y;
        cc.enabled = true;
        Teleport(spot);
        vel = v.Velocity * 0.5f;
        exitCool = 0.4f;
    }

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
        // blocked? hop
        if (cc.isGrounded && to.magnitude > 2f && planar.magnitude < 0.6f && Random.value < dt) i.hop = true;
        input = i;
        camYaw = 0f;
    }
}
