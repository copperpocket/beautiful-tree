using UnityEngine;

/// <summary>
/// Keeps an object facing the camera. Upright Only turns it left/right but
/// never tilts it, so markers stay standing straight like WoW's.
/// </summary>
[DefaultExecutionOrder(1000)]   // after the camera has moved
public class Billboard : MonoBehaviour
{
    [Tooltip("On: only turn around the vertical axis. Off: tilt to face the screen exactly.")]
    public bool uprightOnly = true;

    private Camera cam;

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        if (uprightOnly)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
        else
        {
            transform.rotation = cam.transform.rotation;
        }
    }
}
