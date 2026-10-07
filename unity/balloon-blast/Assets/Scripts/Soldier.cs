using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// One airsoft player (human or bot). Controllers write the in* fields every frame.
public class Soldier : MonoBehaviour
{
    public int id;
    public string nick;
    public Color color;
    public bool human;
    public Slot slot;
    public int layer;

    public CharacterController cc;
    public Transform eye;
    public readonly List<Balloon> balloons = new List<Balloon>();
    public bool alive = true;
    public float yaw, pitch;
    public int pops;

    // inputs
    public Vector2 inMove, inLook;
    public bool inFire, inAds, inJump, inReload;

    // weapon
    public const int Mag = 30;
    public int ammo = Mag;
    public float reloadLeft;
    public float adsBlend;
    public float kick;
    float cooldown;

    Vector3 vel;
    GameObject capsuleBody;
    Transform modelRoot;
    Animation anim;
    string clipIdle, clipWalk, clipRun, clipSad, curClip;

    public bool Reloading { get { return reloadLeft > 0f; } }

    public int BalloonsLeft
    {
        get { int n = 0; foreach (var b in balloons) if (!b.popped) n++; return n; }
    }

    public Vector3 AimPoint()
    {
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var b in balloons) if (!b.popped) { sum += b.transform.position; n++; }
        return n > 0 ? sum / n : eye.position;
    }

    public void Build(int id, string nick, Color color, bool human, int layer)
    {
        this.id = id; this.nick = nick; this.color = color; this.human = human; this.layer = layer;
        gameObject.name = nick;

        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.38f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.45f;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.05f;

        eye = new GameObject("Eye").transform;
        eye.SetParent(transform, false);
        eye.localPosition = new Vector3(0f, 1.6f, 0f);

        // Procedural fallback body (hidden once the X-Bot model arrives)
        capsuleBody = new GameObject("CapsuleBody");
        capsuleBody.transform.SetParent(transform, false);
        Color dark = new Color(0.18f, 0.2f, 0.22f);
        Mats.Prim(PrimitiveType.Capsule, capsuleBody.transform, new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.62f, 0.45f), Mats.Lit(color), false);
        Mats.Prim(PrimitiveType.Sphere, capsuleBody.transform, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.36f, Mats.Lit(new Color(0.95f, 0.8f, 0.65f)), false);
        Mats.Prim(PrimitiveType.Cube, capsuleBody.transform, new Vector3(0f, 1.66f, 0.15f), new Vector3(0.3f, 0.1f, 0.1f), Mats.Lit(dark), false);
        Mats.Prim(PrimitiveType.Sphere, capsuleBody.transform, new Vector3(0f, 1.74f, 0f), new Vector3(0.4f, 0.18f, 0.4f), Mats.Lit(Color.Lerp(color, Color.black, 0.35f)), false);

        // Third-person rifle (follows pitch)
        var gun = new GameObject("Gun3P").transform;
        gun.SetParent(eye, false);
        gun.localPosition = new Vector3(0.22f, -0.34f, 0.32f);
        Mats.Prim(PrimitiveType.Cube, gun, Vector3.zero, new Vector3(0.07f, 0.1f, 0.6f), Mats.Lit(dark), false);
        Mats.Prim(PrimitiveType.Cube, gun, new Vector3(0f, -0.1f, 0.05f), new Vector3(0.05f, 0.14f, 0.07f), Mats.Lit(dark), false);
        Mats.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0f, 0.33f), new Vector3(0.075f, 0.075f, 0.07f), Mats.Lit(new Color(1f, 0.45f, 0.05f)), false);

        // Three balloons above the shoulders
        Vector3[] pos = { new Vector3(-0.36f, 2.2f, -0.08f), new Vector3(0f, 2.42f, -0.12f), new Vector3(0.36f, 2.2f, -0.08f) };
        Color bc = Color.Lerp(color, Color.white, 0.12f);
        for (int k = 0; k < 3; k++)
        {
            GameObject bg = Mats.Prim(PrimitiveType.Sphere, transform, pos[k], new Vector3(0.42f, 0.5f, 0.42f), Mats.Shiny(bc), true);
            bg.name = "Balloon" + k;
            ((SphereCollider)bg.GetComponent<Collider>()).radius = 0.62f;
            var b = bg.AddComponent<Balloon>();
            b.owner = this; b.index = k; b.basePos = pos[k];
            Vector3 anchor = new Vector3(pos[k].x * 0.45f, 1.45f, -0.05f);
            Vector3 bottom = pos[k] + Vector3.down * 0.25f;
            Vector3 d = bottom - anchor;
            GameObject str = Mats.Prim(PrimitiveType.Cylinder, transform, (anchor + bottom) * 0.5f, new Vector3(0.012f, d.magnitude * 0.5f, 0.012f), Mats.Lit(new Color(0.95f, 0.95f, 0.95f)), false);
            str.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            str.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.stringObj = str;
            balloons.Add(b);
        }

        Mats.SetLayer(gameObject, layer);
        ModelLoader.Request(this);
    }

    public void ResetForRound(Vector3 pos, float yawDeg)
    {
        cc.enabled = false;
        transform.position = pos + Vector3.up * 0.15f;
        cc.enabled = true;
        yaw = yawDeg; pitch = 0f;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        vel = Vector3.zero;
        alive = true;
        ammo = Mag; reloadLeft = 0f; cooldown = 0f; adsBlend = 0f; kick = 0f;
        inMove = Vector2.zero; inLook = Vector2.zero; inFire = inAds = inJump = inReload = false;
        foreach (var b in balloons)
        {
            b.popped = false;
            b.gameObject.SetActive(true);
            if (b.stringObj != null) b.stringObj.SetActive(true);
        }
        capsuleBody.transform.localRotation = Quaternion.identity;
        capsuleBody.transform.localPosition = Vector3.zero;
        if (modelRoot != null) modelRoot.localRotation = Quaternion.identity;
        curClip = null;
        PlayClip(clipIdle);
    }

    // ---------- model ----------
    public void AttachModel(GameObject holder)
    {
        StartCoroutine(FitModel(holder));
    }

    IEnumerator FitModel(GameObject holder)
    {
        Color body = Color.Lerp(color, Color.white, 0.1f);
        Color joints = new Color(0.16f, 0.17f, 0.19f);
        foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = ((mats[i] != null ? mats[i].name : "") + " " + r.gameObject.name).ToLowerInvariant();
                mats[i] = n.Contains("joint") ? Mats.Lit(joints) : Mats.Lit(body);
            }
            r.sharedMaterials = mats;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
        }
        Mats.SetLayer(holder, layer);
        anim = holder.GetComponentInChildren<Animation>();
        if (anim != null)
        {
            foreach (AnimationState st in anim)
            {
                string n = st.name.ToLowerInvariant();
                if (clipIdle == null && n.Contains("idle")) clipIdle = st.name;
                else if (clipWalk == null && n.Contains("walk")) clipWalk = st.name;
                else if (clipRun == null && n.Contains("run")) clipRun = st.name;
                else if (clipSad == null && n.Contains("sad")) clipSad = st.name;
            }
            foreach (string c in new[] { clipIdle, clipWalk, clipRun })
                if (c != null) anim[c].wrapMode = WrapMode.Loop;
            if (clipSad != null) anim[clipSad].wrapMode = WrapMode.ClampForever;
            curClip = null;
            PlayClip(alive ? clipIdle : clipSad);
        }
        yield return null;
        yield return null;
        Bounds b; bool has = Measure(holder, out b);
        if (has && b.size.y > 0.05f)
        {
            float k = 1.78f / b.size.y;
            holder.transform.localScale = holder.transform.localScale * k;
            yield return null;
            has = Measure(holder, out b);
            if (has) holder.transform.position += Vector3.up * (transform.position.y - b.min.y);
        }
        modelRoot = holder.transform;
        capsuleBody.SetActive(false);
        Debug.Log("Soldier " + nick + ": X-Bot attached, clips idle=" + clipIdle + " walk=" + clipWalk + " run=" + clipRun);
    }

    static bool Measure(GameObject g, out Bounds b)
    {
        b = new Bounds();
        bool has = false;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return has;
    }

    void PlayClip(string c)
    {
        if (anim == null || c == null || c == curClip) return;
        curClip = c;
        anim.CrossFade(c, 0.2f);
    }

    // ---------- gameplay ----------
    public void PopBalloon(Balloon b, Soldier by)
    {
        if (!alive || b.popped) return;
        b.popped = true;
        FX.Pop(b.transform.position, color);
        b.gameObject.SetActive(false);
        if (b.stringObj != null) b.stringObj.SetActive(false);
        if (by != null)
        {
            by.pops++;
            if (by.slot != null && by.slot.hud != null) by.slot.hud.ShowPop();
        }
        if (slot != null && slot.hud != null) slot.hud.ShowHit();
        if (Game.I != null) Game.I.OnPop(by, this);
        if (BalloonsLeft == 0) GoOut(by);
    }

    void GoOut(Soldier by)
    {
        alive = false;
        inFire = false;
        vel = Vector3.zero;
        if (clipSad != null && anim != null) PlayClip(clipSad);
        else
        {
            capsuleBody.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            capsuleBody.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            if (modelRoot != null && anim == null) modelRoot.localRotation = Quaternion.Euler(0f, 0f, 12f);
        }
        if (Game.I != null) Game.I.OnOut(this, by);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Game g = Game.I;
        bool playing = g != null && g.state == Game.State.Playing;
        bool frozen = g != null && g.state == Game.State.Countdown;

        if (alive && (playing || frozen))
        {
            yaw += inLook.x;
            pitch = Mathf.Clamp(pitch - inLook.y, -80f, 80f);
        }
        inLook = Vector2.zero;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        bool ads = alive && playing && inAds && !Reloading;
        adsBlend = Mathf.MoveTowards(adsBlend, ads ? 1f : 0f, dt * 6f);
        kick = Mathf.MoveTowards(kick, 0f, dt * 8f);

        // movement
        if (cc.enabled)
        {
            Vector3 wish = Vector3.zero;
            if (alive && playing)
            {
                wish = transform.right * inMove.x + transform.forward * inMove.y;
                wish = Vector3.ClampMagnitude(wish, 1f);
            }
            float spd = Mathf.Lerp(5.4f, 2.6f, adsBlend);
            Vector3 hv = new Vector3(vel.x, 0f, vel.z);
            hv = Vector3.MoveTowards(hv, wish * spd, (cc.isGrounded ? 45f : 10f) * dt);
            float vy = vel.y;
            if (cc.isGrounded)
            {
                if (vy < 0f) vy = -2f;
                if (inJump && alive && playing) vy = 6.4f;
            }
            vy -= 20f * dt;
            vel = new Vector3(hv.x, vy, hv.z);
            cc.Move(vel * dt);

            Vector3 p = transform.position;
            float gy = World.Ground(p.x, p.z);
            if (p.x < World.Min || p.x > World.Max || p.z < World.Min || p.z > World.Max || p.y < gy - 3f)
            {
                p.x = Mathf.Clamp(p.x, World.Min, World.Max);
                p.z = Mathf.Clamp(p.z, World.Min, World.Max);
                if (p.y < gy - 3f) p.y = gy + 0.5f;
                cc.enabled = false; transform.position = p; cc.enabled = true;
            }
        }
        inJump = false;

        // weapon
        cooldown -= dt;
        if (reloadLeft > 0f)
        {
            reloadLeft -= dt;
            if (reloadLeft <= 0f) { reloadLeft = 0f; ammo = Mag; }
        }
        if (inReload && ammo < Mag && reloadLeft <= 0f) reloadLeft = 1.4f;
        inReload = false;
        if (alive && playing && inFire && cooldown <= 0f && reloadLeft <= 0f)
        {
            if (ammo > 0)
            {
                Fire();
                ammo--;
                cooldown = 1f / 9f;
                if (ammo == 0) reloadLeft = 1.4f;
            }
        }

        // animation
        float speed = new Vector2(vel.x, vel.z).magnitude;
        if (!alive) PlayClip(clipSad != null ? clipSad : clipIdle);
        else if (speed > 3.8f) PlayClip(clipRun != null ? clipRun : clipWalk);
        else if (speed > 0.4f) PlayClip(clipWalk != null ? clipWalk : clipIdle);
        else PlayClip(clipIdle);

        // balloons bob
        float t = Time.time;
        foreach (var b in balloons)
        {
            if (b.popped) continue;
            b.transform.localPosition = b.basePos + new Vector3(Mathf.Sin(t * 1.7f + b.index * 2f) * 0.03f, Mathf.Sin(t * 2.1f + b.index) * 0.04f, 0f);
        }
    }

    void Fire()
    {
        float spread = Mathf.Lerp(1.5f, 0.3f, adsBlend);
        Vector2 r = Random.insideUnitCircle * spread;
        Vector3 dir = eye.rotation * Quaternion.Euler(r.y, r.x, 0f) * Vector3.forward;
        Vector3 origin = eye.position + eye.forward * 0.35f;
        if (BBs.I != null) BBs.I.Fire(origin, dir * BBs.Speed, this);
        kick = 1f;
        pitch -= 0.2f;
    }
}
