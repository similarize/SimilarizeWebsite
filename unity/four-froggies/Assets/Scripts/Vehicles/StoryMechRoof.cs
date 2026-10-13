using UnityEngine;

// ffu39: probe helper for World/RoofBreak.cs (?ffshot=roofstomp): drop the mech from a height (the top of a jump)
public partial class StoryMech
{
    public void DemoDrop(Vector3 p, float yawDeg, float vyStart = 0f)
    {
        transform.position = p; yaw = yawDeg; transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        if (rb != null) { rb.position = p; rb.rotation = transform.rotation; }
        grounded = false; vy = vyStart; atSpawn = false;
    }
    public bool DemoGrounded { get { return grounded; } }
}
