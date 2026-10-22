using UnityEngine;

// ffu72 (Ben item 6): a story mech that flies to space is NOT also standing on the ranch. While its space copy flies (or waits
// in orbit for its crew) the ranch mech is stowed + inactive: no body, no shadow, no boarding, no robot can take it. It is
// back at its own spot (or beside the Starship pad when it lands at the ranch) the moment it returns.
public partial class StoryMech
{
    public bool Away72 { get; private set; }

    public void GoAway72()
    {
        if (F72.Old || Away72) return;
        Away72 = true; stowed = true;
        aimK = 0f; speed = 0f; flameK = 0f;
        gameObject.SetActive(false);
        Debug.Log("FF72 " + Title + " left Earth - hidden on the ranch until it returns");
    }

    // back on the ranch at its own spot (the safety net when no space ship carries it any more)
    public void Home72(string why)
    {
        if (!Away72) return;
        Back72();
        transform.position = spawnPos; transform.rotation = spawnRot; yaw = spawnRot.eulerAngles.y;
        rb.position = spawnPos; rb.rotation = spawnRot;
        vy = 0f; grounded = true; Altitude = 0f; speed = 0f;
        Debug.Log("FF72 " + Title + " is home at its spot (" + why + ")");
    }

    void Back72()
    {
        if (!Away72) return;
        Away72 = false; stowed = false;
        gameObject.SetActive(true);
    }

    // ffu72 (Ben item 7, shared screen): another player's mech left behind when the group flies to space parks at its spot
    public void ParkHome72()
    {
        if (F72.Old || driver != null || Away72) return;
        transform.position = spawnPos; transform.rotation = spawnRot; yaw = spawnRot.eulerAngles.y;
        rb.position = spawnPos; rb.rotation = spawnRot;
        if (!rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        vy = 0f; grounded = true; Altitude = 0f; speed = 0f; flameK = 0f; aimK = 0f;
        Debug.Log("FF72 " + Title + " parked at its spot (its pilot rides the group trip)");
    }

    public float SpawnDist72 { get { Vector3 d = transform.position - spawnPos; d.y = 0f; return d.magnitude; } }
}
