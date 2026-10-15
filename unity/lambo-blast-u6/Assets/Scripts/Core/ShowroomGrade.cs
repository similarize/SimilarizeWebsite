using UnityEngine;

// lambou6: the lambo7 showroom grade (an OnRenderImage ACES / contrast / saturation / vignette blit) is now URP post on the
// stand + thumbnail cameras with the showroom Volume (U6.ShowroomProfile on layer 20; those cameras' volume mask = layer 20).
[RequireComponent(typeof(Camera))]
public class ShowroomGrade : MonoBehaviour
{
    public float vignette = 0.6f;
    public static float Exposure = 1.0f, Contrast = 1.06f, Saturation = 0.94f;
    void Awake() { U6.ShowroomCam(GetComponent<Camera>()); }
    void OnEnable() { U6.ShowroomCam(GetComponent<Camera>()); }
}
