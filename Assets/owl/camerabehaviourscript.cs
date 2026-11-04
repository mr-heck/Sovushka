using UnityEngine;

[DisallowMultipleComponent]
public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Yanam transform (default target until Sovushka appears).")]
    public Transform yanam;

    [Tooltip("Optional: assign Sovushka transform in Inspector. If left empty the script will try to find a GameObject named 'Sovushka' at runtime.")]
    public Transform sovushka;

    [Header("Camera Settings")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    [Tooltip("Higher = snappier, lower = smoother")]
    public float smoothSpeed = 5f;

    [Header("Detection")]
    [Tooltip("If true, the camera will attempt to find Sovushka by name at Start if sovushka is not assigned.")]
    public bool autoFindSovushkaByName = true;
    [Tooltip("Name used when auto-finding Sovushka.")]
    public string sovushkaObjectName = "Sovushka";

    // internal
    private Transform currentTarget;

    void Awake()
    {
        // Ensure offset z is negative so camera stays behind
        if (offset.z == 0f) offset.z = -10f;

        // If sovushka not assigned and auto-find enabled, try to find it now
        if (sovushka == null && autoFindSovushkaByName && !string.IsNullOrEmpty(sovushkaObjectName))
        {
            var go = GameObject.Find(sovushkaObjectName);
            if (go != null) sovushka = go.transform;
        }

        // Start following Yanam by default
        currentTarget = yanam != null ? yanam : sovushka;
    }

    void LateUpdate()
    {
        // Choose which transform to follow this frame
        Transform follow = ChooseTarget();

        if (follow == null)
        {
            // Nothing to follow; do nothing
            return;
        }

        // Desired position = target position + offset
        Vector3 desiredPosition = follow.position + offset;

        // Smoothly interpolate camera position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, Mathf.Clamp01(smoothSpeed * Time.deltaTime));

        transform.position = smoothedPosition;
    }

    Transform ChooseTarget()
    {
        // If Sovushka exists and is visible (has at least one enabled Renderer), follow Sovushka
        if (sovushka != null && IsVisibleInHierarchy(sovushka))
        {
            currentTarget = sovushka;
            return currentTarget;
        }

        // Otherwise follow Yanam if available
        if (yanam != null)
        {
            currentTarget = yanam;
            return currentTarget;
        }

        // If Yanam missing but Sovushka visible, follow Sovushka
        if (sovushka != null && IsVisibleInHierarchy(sovushka))
        {
            currentTarget = sovushka;
            return currentTarget;
        }

        // No valid target
        return null;
    }

    bool IsVisibleInHierarchy(Transform t)
    {
        if (t == null) return false;

        // Check all Renderer components in the transform and its children (including inactive)
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            // No renderers found — treat as not visible
            return false;
        }

        foreach (var r in renderers)
        {
            if (r != null && r.enabled)
                return true;
        }

        return false;
    }

    // Public helper to force camera to follow a specific transform immediately
    public void ForceFollow(Transform newTarget)
    {
        if (newTarget == null) return;
        currentTarget = newTarget;
        transform.position = newTarget.position + offset;
    }
}
