using UnityEngine;

// ffu14: the playable cats and dogs, ported from Froggy Hop Racing's makeAnimal (games/froggy-hop-racing): chibi
// quadrupeds with a big round head, white eyes with pupils + shine, blush cheeks, a little smile, cone ears, whiskers
// (cats), snout + tongue (dogs). Coats from the shared roster: Spotty white with dark spots, Tigy orange with dark
// stripes, Kitty lilac, Little White Socks charcoal with white socks / chest / tail tip, Germy tan with a black saddle
// and black pointed ears, Daisy a long low red-brown dachshund with floppy ears. Built from primitives (FF/Skin) and
// merged per animated part. Same rig names as the frog (armL/armR = front legs, legL/legR = hind legs), ~1.3 m tall.
public partial class FrogModel
{
    public bool pet;
    public int charId = -1;
    Transform tailT;
    float petPhase;
    const float PS = 0.58f;   // hop-game units -> metres

    static Mesh coneMesh;
    static Mesh Cone()
    {
        if (coneMesh != null) return coneMesh;
        const int n = 20;
        var v = new Vector3[n + 2]; var tri = new int[n * 6];
        v[n] = new Vector3(0f, 0.5f, 0f); v[n + 1] = new Vector3(0f, -0.5f, 0f);
        for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; v[i] = new Vector3(Mathf.Cos(a), -0.5f, Mathf.Sin(a)); }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            tri[i * 6] = n; tri[i * 6 + 1] = j; tri[i * 6 + 2] = i;
            tri[i * 6 + 3] = n + 1; tri[i * 6 + 4] = i; tri[i * 6 + 5] = j;
        }
        coneMesh = new Mesh { name = "PetCone" };
        coneMesh.vertices = v; coneMesh.triangles = tri;
        coneMesh.RecalculateNormals(); coneMesh.RecalculateBounds();
        return coneMesh;
    }

    // hop-game sp(): a unit-radius sphere scaled by semi-axes (Unity primitives are diameter 1 -> x2)
    static GameObject Sp(Transform par, Material m, float x, float y, float z, float sx, float sy = -1f, float sz = -1f)
    {
        if (sy < 0f) sy = sx; if (sz < 0f) sz = sx;
        return Mats.Prim(PrimitiveType.Sphere, par, new Vector3(x, y, z), new Vector3(sx * 2f, sy * 2f, sz * 2f), m);
    }
    static GameObject ConeAt(Transform par, Material m, float x, float y, float z, float r, float h, float rz, float flat = 1f)
    {
        var g = new GameObject("Ear");
        g.transform.SetParent(par, false);
        g.transform.localPosition = new Vector3(x, y, z);
        g.transform.localScale = new Vector3(r, h, r * flat);
        g.transform.localRotation = Quaternion.Euler(0f, 0f, rz * Mathf.Rad2Deg);
        g.AddComponent<MeshFilter>().sharedMesh = Cone();
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }
    static Transform Geo(Transform node) { return Mats.Node(node, "Geo", Vector3.zero); }

    public void BuildChar(int c)
    {
        charId = c;
        if (Roster.IsFrog(c)) { pet = false; Build(Roster.Color(c)); return; }
        pet = true;
        BuildPet(Roster.All[c]);
    }

    void Face(Transform g, Material white, Material pupil, Material shine, Material blush, Material dark, float ex, float ey, float ez, float er, float cy, float cz, float cx, float sy, float sz, float sr)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            Sp(g, white, s * ex, ey, ez, er, er, er * 0.85f);
            Sp(g, pupil, s * ex, ey - er * 0.05f, ez + er * 0.38f, er * 0.7f, er * 0.74f, er * 0.5f);
            Sp(g, shine, s * ex + er * 0.25f, ey + er * 0.3f, ez + er * 0.66f, er * 0.2f);
            Sp(g, blush, s * cx, cy, cz, 0.13f, 0.08f, 0.04f);
        }
        // smile: a small arc of dark beads (the hop game uses a half torus)
        for (int k = 0; k <= 6; k++)
        {
            float a = Mathf.PI * (0.15f + 0.7f * k / 6f);
            Sp(g, dark, Mathf.Cos(a) * sr * 1.6f, sy - Mathf.Sin(a) * sr * 0.9f, sz, sr * 0.32f);
        }
    }

    void BuildPet(Roster.Def d)
    {
        seed = Random.value * 10f;
        Color col = Mats.Hex(d.hex);
        Color mkC = d.markHex != null ? Mats.Hex(d.markHex) : col;
        bool cat = d.kind == Roster.Kind.Cat, shep = d.look == Roster.Look.Shepherd, dachs = d.look == Roster.Look.Dachshund;
        Material mat = Mats.Skin(col, 0.3f), light = Mats.Skin(Color.Lerp(col, new Color(1f, 1f, 0.94f), 0.55f), 0.3f), mk = Mats.Skin(mkC, 0.3f);
        Material white = Mats.Paint(Color.white, 0.85f), pupil = Mats.Paint(new Color(0.07f, 0.07f, 0.07f), 0.9f), shine = Mats.Unlit(Color.white);
        Material blush = Mats.Skin(new Color(1f, 0.56f, 0.69f), 0.3f), pink = Mats.Skin(new Color(1f, 0.5f, 0.66f), 0.35f), dark = Mats.Lit(new Color(0.16f, 0.1f, 0.1f));

        bob = Mats.Node(transform, "Bob", Vector3.zero);
        Transform root = Mats.Node(bob, "Pet", Vector3.zero);
        root.localScale = Vector3.one * PS;
        // dachshund: long low body, short legs, head pushed forward (the hop game's post-transform, applied up front)
        float dy = dachs ? -0.25f : 0f, bodyZ = dachs ? 1.7f : 1f, bodyY = dachs ? 0.82f : 1f, headZ = dachs ? 0.6f : 0f;

        Transform body = Mats.Node(root, "Body", Vector3.zero);
        Transform bg = Geo(body);
        Sp(bg, mat, 0f, 0.68f + dy, -0.05f, 0.6f, 0.52f * bodyY, 0.75f * bodyZ);
        Sp(bg, light, 0f, 0.62f + dy, 0.25f * (dachs ? 1.4f : 1f), 0.42f, 0.38f * bodyY, 0.45f * bodyZ);
        Vector3 bc = new Vector3(0f, 0.68f + dy, -0.05f), br = new Vector3(0.6f, 0.52f * bodyY, 0.75f * bodyZ);

        // legs: hip groups at (+-0.3, 0.4, +-0.35); dachshund hips lower, wider apart, legs squashed
        for (int i = 0; i < 4; i++)
        {
            float s = (i % 2 == 0) ? -1f : 1f, z = (i < 2 ? 0.35f : -0.35f) * (dachs ? 1.7f : 1f);
            Transform hip = Mats.Node(root, "Leg", new Vector3(s * 0.3f, dachs ? 0.24f : 0.4f, z));
            if (dachs) hip.localScale = new Vector3(1f, 0.6f, 1f);
            Sp(hip, mat, 0f, -0.2f, 0f, 0.17f, 0.24f, 0.17f);
            if (d.look == Roster.Look.Socks) Sp(hip, mk, 0f, -0.32f, 0f, 0.19f, 0.13f, 0.19f);
            else Sp(hip, Mats.Skin(Color.Lerp(col, Color.white, 0.12f), 0.3f), 0f, -0.36f, 0.04f, 0.18f, 0.09f, 0.21f);   // paw
            if (i == 0) armL = hip; else if (i == 1) armR = hip; else if (i == 2) legL = hip; else legR = hip;
        }

        // head group (pivot at the neck)
        Vector3 hp = new Vector3(0f, 1.1f + dy, 0.3f + headZ);
        head = Mats.Node(root, "Head", hp);
        Transform hg = Geo(head);
        System.Func<float, float, float, Vector3> H = (x, y, z) => new Vector3(x, y + dy, z + headZ) - hp;
        Vector3 hc = H(0f, 1.45f, 0.35f);
        Sp(hg, mat, hc.x, hc.y, hc.z, 0.72f, 0.65f, 0.66f);
        Transform tail = Mats.Node(root, "Tail", new Vector3(0f, 0.75f + dy, -0.7f * bodyZ));
        tailT = tail;
        Transform tg = Geo(tail);
        System.Func<float, float, float, Vector3> T = (x, y, z) => new Vector3(x, y + dy, z - (dachs ? 0.45f : 0f)) - tail.localPosition;
        Vector3 v;
        if (cat)
        {
            Vector3 f0 = H(0f, 0f, 0f);
            Face(hg, white, pupil, shine, blush, dark, 0.27f, 1.52f + f0.y, 0.84f + f0.z, 0.2f, 1.28f + f0.y, 0.88f + f0.z, 0.42f, 1.22f + f0.y, 0.98f + f0.z, 0.07f);
            for (int s = -1; s <= 1; s += 2)
            {
                v = H(s * 0.38f, 2.02f, 0.3f); ConeAt(hg, mat, v.x, v.y, v.z, 0.22f, 0.45f, -s * 0.3f);
                v = H(s * 0.37f, 1.99f, 0.39f); ConeAt(hg, pink, v.x, v.y, v.z, 0.13f, 0.28f, -s * 0.3f, 0.45f);
                for (int k = 0; k < 2; k++)
                {
                    v = H(s * 0.5f, 1.31f - k * 0.07f, 0.86f);
                    Mats.Prim(PrimitiveType.Cube, hg, v, new Vector3(0.42f, 0.015f, 0.015f), new Vector3(0f, 0f, s * (k == 0 ? 0.12f : -0.1f) * Mathf.Rad2Deg), Mats.Lit(col.grayscale > 0.6f ? new Color(0.25f, 0.22f, 0.22f) : new Color(0.95f, 0.95f, 0.95f)));
                }
            }
            v = H(0f, 1.33f, 1.0f); Sp(hg, pink, v.x, v.y, v.z, 0.06f, 0.045f, 0.045f);
            float[][] tp = { new[] { 0.75f, -0.72f }, new[] { 0.95f, -0.86f }, new[] { 1.15f, -0.9f }, new[] { 1.33f, -0.82f } };
            foreach (var q in tp) { v = T(0f, q[0], q[1]); Sp(tg, d.look == Roster.Look.Socks && q[1] < -0.85f ? mk : mat, v.x, v.y, v.z, 0.11f); }
            if (d.look == Roster.Look.Spots)
            {
                Decals(bg, mk, bc, br, new[] { new Vector3(0.6f, 0.7f, -0.3f), new Vector3(-0.5f, 0.8f, 0.1f), new Vector3(0.2f, 0.9f, -0.7f), new Vector3(-0.8f, 0.3f, -0.5f), new Vector3(0.9f, 0.2f, 0.2f) }, 0.15f);
                Decals(hg, mk, hc, new Vector3(0.72f, 0.65f, 0.66f), new[] { new Vector3(0.5f, 0.8f, 0.2f), new Vector3(-0.7f, 0.5f, -0.2f), new Vector3(0.1f, 0.6f, -0.8f) }, 0.12f);
            }
            else if (d.look == Roster.Look.Stripes)
            {
                foreach (float z in new[] { -0.45f, -0.2f, 0.05f, 0.3f })
                {
                    float k = Mathf.Sqrt(1f - (z / 0.75f) * (z / 0.75f));
                    for (int a = -2; a <= 2; a++)
                    {
                        float ang = a * 0.55f;   // over the back and down the sides
                        Vector3 dir = new Vector3(Mathf.Sin(ang) * k, Mathf.Cos(ang) * k, z / 0.75f);
                        Decal(bg, mk, bc, br * 1.0f, dir, new Vector3(0.24f, 0.07f, 0.05f));
                    }
                }
                for (int x = -1; x <= 1; x++) { v = H(x * 0.16f, 2.0f, 0.42f); Mats.Prim(PrimitiveType.Cube, hg, v, new Vector3(0.05f, 0.03f, 0.3f), new Vector3(28f, 0f, 0f), mk); }
            }
            else if (d.look == Roster.Look.Socks) { Sp(bg, mk, 0f, 0.88f, 0.52f, 0.3f, 0.32f, 0.18f); }
        }
        else
        {
            v = H(0f, 1.28f, shep ? 1.0f : 0.92f); Sp(hg, shep ? mk : light, v.x, v.y, v.z, 0.3f, 0.22f, shep ? 0.4f : 0.22f);
            Vector3 f0 = H(0f, 0f, 0f);
            Face(hg, white, pupil, shine, blush, dark, 0.28f, 1.6f + f0.y, 0.82f + f0.z, 0.2f, 1.3f + f0.y, 0.86f + f0.z, 0.46f, 1.16f + f0.y, 1.1f + f0.z + (shep ? 0.1f : 0f), 0.07f);
            v = H(0f, 1.36f, shep ? 1.38f : 1.12f); Sp(hg, pupil, v.x, v.y, v.z, 0.09f, 0.07f, 0.07f);
            v = H(0f, 1.06f, shep ? 1.2f : 1.04f); Sp(hg, pink, v.x, v.y, v.z, 0.08f, 0.1f, 0.05f);
            if (shep)
            {
                for (int s = -1; s <= 1; s += 2) { v = H(s * 0.36f, 2.05f, 0.3f); ConeAt(hg, mk, v.x, v.y, v.z, 0.2f, 0.48f, -s * 0.2f, 0.7f); }
                Sp(bg, mk, 0f, 0.98f, -0.12f, 0.5f, 0.26f, 0.64f);   // black saddle
                v = T(0f, 0.88f, -0.9f); Mats.Prim(PrimitiveType.Sphere, tg, v, new Vector3(0.34f, 0.34f, 0.84f), new Vector3(-28.6f, 0f, 0f), mat);
                v = T(0f, 0.66f, -1.24f); Sp(tg, mk, v.x, v.y, v.z, 0.12f, 0.12f, 0.16f);
            }
            else
            {
                Material dk = Mats.Skin(new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f), 0.3f);
                for (int s = -1; s <= 1; s += 2) { v = H(s * 0.66f, 1.3f, 0.3f); Mats.Prim(PrimitiveType.Sphere, hg, v, new Vector3(0.4f, 1.2f, 0.2f), new Vector3(0f, 0f, s * 17f), dk); }
                v = T(0f, 0.9f, -0.78f); Mats.Prim(PrimitiveType.Sphere, tg, v, new Vector3(0.2f, 0.2f, 0.52f), new Vector3(-40f, 0f, 0f), mat);
            }
        }
        // fewer draw calls: merge each rigid part (geo holders have no animated children)
        foreach (Transform g in GetComponentsInChildren<Transform>())
            if (g.name == "Geo") MeshMerge.Merge(g, !Look.Mobile);
        foreach (Transform l in new[] { armL, armR, legL, legR }) MeshMerge.Merge(l, !Look.Mobile);
        Mats.SetLayer(gameObject, 9);
    }

    static void Decal(Transform par, Material m, Vector3 c, Vector3 r, Vector3 dir, Vector3 size)
    {
        dir.Normalize();
        Vector3 p = c + new Vector3(dir.x * r.x, dir.y * r.y, dir.z * r.z);
        Vector3 up = Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up;
        var g = Mats.Prim(PrimitiveType.Sphere, par, p, size * 2f, m);
        g.transform.localRotation = Quaternion.LookRotation(dir, up);
    }
    static void Decals(Transform par, Material m, Vector3 c, Vector3 r, Vector3[] dirs, float s)
    {
        foreach (var d in dirs) Decal(par, m, c, r, d, new Vector3(s, s * 0.8f, s * 0.3f));
    }

    void OnDestroy()
    {
        // merged meshes are runtime objects: free them with the model (lobby turntables rebuild models often)
        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            if (mf != null && mf.sharedMesh != null && mf.gameObject.name.StartsWith("Merged ")) Destroy(mf.sharedMesh);
    }

    // trot: diagonal pairs swing together; gallop-stretch in the air; dog-paddle when swimming; sit when seated
    void AnimatePet(float speed, bool air, bool seated, bool swim, float dt)
    {
        float t = Time.time + seed;
        if (seated)
        {
            bob.localPosition = new Vector3(0f, 0.05f, -0.1f);
            bob.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            legL.localRotation = legR.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            armL.localRotation = armR.localRotation = Quaternion.Euler(10f, 0f, 0f);
            head.localRotation = Quaternion.Euler(10f + Mathf.Sin(t * 0.7f) * 3f, Mathf.Sin(t * 0.5f) * 14f, 0f);
            if (tailT != null) tailT.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 3f) * 20f, 0f);
            return;
        }
        float move = Mathf.Clamp01(speed / 5f);
        petPhase += dt * (6f + speed * 1.6f);
        float s = Mathf.Sin(petPhase);
        float swing = 38f * move;
        float a = s * swing, b = -s * swing;
        if (air) { armL.localRotation = armR.localRotation = Quaternion.Euler(-45f, 0f, 0f); legL.localRotation = legR.localRotation = Quaternion.Euler(45f, 0f, 0f); }
        else if (swim)
        {
            float p = Mathf.Sin(t * 9f) * 50f;
            armL.localRotation = Quaternion.Euler(-30f + p, 0f, 0f); armR.localRotation = Quaternion.Euler(-30f - p, 0f, 0f);
            legL.localRotation = Quaternion.Euler(30f - p * 0.5f, 0f, 0f); legR.localRotation = Quaternion.Euler(30f + p * 0.5f, 0f, 0f);
        }
        else
        {
            armL.localRotation = Quaternion.Euler(a, 0f, 0f); legR.localRotation = Quaternion.Euler(a, 0f, 0f);
            armR.localRotation = Quaternion.Euler(b, 0f, 0f); legL.localRotation = Quaternion.Euler(b, 0f, 0f);
        }
        float y = air ? 0f : Mathf.Abs(Mathf.Cos(petPhase)) * 0.07f * move;
        float tilt = air ? -8f : swim ? -18f : 0f;
        if (swim) y = Mathf.Sin(t * 3f) * 0.05f - 0.15f;
        bob.localPosition = new Vector3(0f, y, 0f);
        float idle = 1f - move;
        bob.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2.4f) * 0.015f * idle, 1f);
        bob.localRotation = Quaternion.Euler(tilt + Mathf.Sin(t * 1.1f) * 1.5f * idle, 0f, Mathf.Sin(petPhase) * 3f * move);
        head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 4f - move * 4f, Mathf.Sin(t * 0.6f) * 12f * idle, Mathf.Sin(t * 0.8f) * 4f * idle);
        if (tailT != null)
        {
            bool dog = Roster.KindOf(charId) == Roster.Kind.Dog;
            float wag = dog ? Mathf.Sin(t * 14f) * 28f : Mathf.Sin(t * (2f + move * 4f)) * (14f + move * 10f);
            tailT.localRotation = Quaternion.Euler(dog ? 0f : -6f, wag, 0f);
        }
    }

    // lobby turntable: a happy idle (head looks around, tail wags; frogs breathe and blink-bob)
    public void ShowIdle(float t)
    {
        if (bob == null) return;
        if (pet) { AnimatePet(0f, false, false, false, Time.deltaTime); return; }
        Animate(0f, false, false, false, Time.deltaTime);
    }
}
