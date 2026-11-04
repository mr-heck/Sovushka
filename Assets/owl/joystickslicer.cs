using UnityEngine;

[DisallowMultipleComponent]
public class JoystickOctantAnimatorManager_NoAnimator : MonoBehaviour
{
    [Tooltip("Name of the parameter conceptually used (not required for no-animator mode).")]
    public string paramName = "Direction";

    [Tooltip("Root where child receivers live. If empty, uses this GameObject.")]
    public Transform animatorsRoot;

    [Tooltip("Ignore small stick movement")]
    public float deadzone = 0.2f;

    [Tooltip("Frames the new direction must persist before switching")]
    public int framesToConfirm = 3;

    [Tooltip("Normalize input before snapping")]
    public bool normalizeInput = true;

    // internals
    IDirectionalAnimatorReceiver[] receivers;
    Vector2 currentInput = Vector2.zero;

    int lastStableDir = -1;
    int candidateDir = -1;
    int candidateFrames = 0;

    void Awake()
    {
        if (animatorsRoot == null) animatorsRoot = transform;
        // gather receivers under the root (including inactive)
        receivers = animatorsRoot.GetComponentsInChildren<IDirectionalAnimatorReceiver>(includeInactive: true);
        // initialize to idle
        foreach (var r in receivers) r?.OnDirectionChanged(-1);
    }

    void Update()
    {
        // Idle handling
        if (currentInput.sqrMagnitude < deadzone * deadzone)
        {
            candidateDir = -1;
            candidateFrames = 0;
            if (lastStableDir != -1)
            {
                lastStableDir = -1;
                SetDirectionAll(-1);
            }
            return;
        }

        Vector2 input = currentInput;
        if (normalizeInput && input.sqrMagnitude > 0.0001f) input = input.normalized;

        // compute angle 0..360
        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // snap to nearest 45 degrees and convert to index 0..7
        float snappedAngle = Mathf.Round(angle / 45f) * 45f;
        if (snappedAngle >= 360f) snappedAngle = 0f;
        int dirIndex = Mathf.RoundToInt(snappedAngle / 45f) % 8;

        // hysteresis: require framesToConfirm frames of stability before switching
        if (dirIndex != candidateDir)
        {
            candidateDir = dirIndex;
            candidateFrames = 1;
        }
        else
        {
            candidateFrames++;
        }

        if (candidateFrames >= Mathf.Max(1, framesToConfirm) && candidateDir != lastStableDir)
        {
            lastStableDir = candidateDir;
            SetDirectionAll(lastStableDir);
        }
    }

    void SetDirectionAll(int dir)
    {
        foreach (var r in receivers)
        {
            if (r == null) continue;
            try { r.OnDirectionChanged(dir); } catch { /* ignore receiver errors */ }
        }
    }

    /// <summary>
    /// Call this from your joystick OnDrag to feed the current stick vector (-1..1).
    /// Call SetInput(Vector2.zero) on pointer up to return to idle.
    /// </summary>
    public void SetInput(Vector2 v)
    {
        currentInput = v;
    }
}

/// <summary>
/// Interface children implement to receive direction changes (dir: 0..7, -1 = idle)
/// </summary>
public interface IDirectionalAnimatorReceiver
{
    void OnDirectionChanged(int dir);
}
