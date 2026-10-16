using UnityEngine;

// ffu48: Spotty's motorcycle - the ffu45 adventure tourer (type 6 handling: long travel, hill assist) dressed in Spotty's
// colours (white tank + panniers with black spots, pink trim), a hitch arm + ball behind the rear wheel for her Carrier
// Camper, no wheelies (towing), slower top speed while towing. Not in BikeAI's list (AI froggies never take it - they'd
// drag the camper off). Canon: Spotty's own (others ask to borrow). In carrier mode it is "carried": no physics, it lies on
// its side in the stern pod (CarrierRig).
public partial class Bike
{
    public bool spotty, carried;
    public System.Func<PIn, PIn> ghostHook;   // story / demo pilot with no Frog on the bike (Spotty NPC)
    public Vector3 HitchPoint { get { return new Vector3(0f, 0.55f - bigLift, rw.mount.z - rw.r - 0.4f - bigBack); } }

    public static Bike SpawnSpotty(Vector3 pos, float yaw)
    {
        Bike b = Spawn45("Tourer motorcycle", pos, yaw, 6);
        AllBikes.Remove(b);
        b.spotty = true; b.wheelieK = 0f;
        b.ownerName = "Spotty"; b.ownerChar = 4;
        b.EnterVerb = "ride Spotty's Tourer motorcycle";
        b.DressSpotty();
        return b;
    }
    public void Rehome() { spawnPos = transform.position; spawnRot = transform.rotation; }

    void DressSpotty()
    {
        Transform L = leanT != null ? leanT : transform;
        Material white = Mats.Paint(new Color(0.97f, 0.97f, 0.95f), 0.7f), blk = Mats.Lit(new Color(0.07f, 0.07f, 0.08f)), pink = Mats.Paint(new Color(0.98f, 0.45f, 0.68f), 0.7f), steel = Mats.Steel(new Color(0.72f, 0.74f, 0.77f));
        // white "tank cover" with spots + pink stripe
        Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.12f, 0.26f), new Vector3(0.34f, 0.2f, 0.46f), white);
        Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.225f, 0.26f), new Vector3(0.08f, 0.012f, 0.46f), pink);
        foreach (var v in new[] { new Vector3(0.172f, 1.14f, 0.18f), new Vector3(-0.172f, 1.1f, 0.36f), new Vector3(0.172f, 1.06f, 0.4f) })
            Mats.Prim(PrimitiveType.Cylinder, L, v, new Vector3(0.12f, 0.01f, 0.1f), new Vector3(0f, 0f, 90f), blk);
        // white panniers with spots
        for (int s = -1; s <= 1; s += 2)
        {
            Mats.Prim(PrimitiveType.Cube, L, new Vector3(s * 0.33f, 0.82f, -0.52f), new Vector3(0.2f, 0.36f, 0.5f), white);
            Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(s * 0.432f, 0.86f, -0.48f), new Vector3(0.14f, 0.01f, 0.12f), new Vector3(0f, 0f, 90f), blk);
            Mats.Prim(PrimitiveType.Cylinder, L, new Vector3(s * 0.432f, 0.74f, -0.64f), new Vector3(0.08f, 0.01f, 0.07f), new Vector3(0f, 0f, 90f), blk);
        }
        // pink tail plate with a paw
        Mats.Prim(PrimitiveType.Cube, L, new Vector3(0f, 1.0f, -0.95f), new Vector3(0.24f, 0.14f, 0.02f), pink);
        // hitch: arm from under the seat back to a ball behind the rear tyre
        Vector3 hp = HitchPoint;
        Vector3 a = new Vector3(0f, 0.62f, -0.55f), d = hp + new Vector3(0f, 0.04f, 0.06f) - a;
        var arm = Mats.Prim(PrimitiveType.Cube, transform, (a + d * 0.5f + a) * 0.5f + d * 0.25f, new Vector3(0.07f, 0.07f, d.magnitude), steel);
        arm.transform.localPosition = a + d * 0.5f; arm.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        Mats.Prim(PrimitiveType.Sphere, transform, hp + new Vector3(0f, 0.06f, 0f), Vector3.one * 0.11f, steel);
        Mats.SetLayer(gameObject, VehicleLayer);
    }

    public override string HelpFor(InputKind k)
    {
        if (!spotty) return null;
        if (k == InputKind.Gamepad) return "L-stick::steer|RT::throttle|LT::brake / reverse|A  /  Y at the camper's tongue::hitch / unhitch the Carrier Camper|Ride into the pond (towing)::CARRIER MODE|A::get off";
        if (k == InputKind.Touch) return "Stick::steer|GAS::throttle|BRAKE::brake / reverse|A at the camper's tongue::hitch / unhitch the Carrier Camper|Ride into the pond (towing)::CARRIER MODE|A::get off";
        return "A  /  D::steer|W::throttle|S::brake / reverse|E at the camper's tongue::hitch / unhitch the Carrier Camper|Ride into the pond (towing)::CARRIER MODE|E::get off";
    }
    public override string HudStatus
    {
        get
        {
            if (!spotty) return null;
            bool tow = TrailerTow.TowedBy(this) != null;
            return "Spotty's Tourer  " + Mathf.RoundToInt(Speed * 3.6f) + " km/h" + (tow ? "  ·  towing the Carrier Camper" : "");
        }
    }
}
