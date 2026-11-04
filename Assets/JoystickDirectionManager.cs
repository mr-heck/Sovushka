// JoystickDirectionManager.cs
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class JoystickDirectionManager : MonoBehaviour
{
    [Header("Input preprocessing")]
    public float deadzone = 0.15f;
    [Range(0f, 1f)] public float smoothingAlpha = 0.4f;

    [Header("Hysteresis")]
    public int framesToConfirm = 3;
    public float angleMarginDegrees = 10f;

    [Header("Receivers")]
    public Transform receiversRoot;

    Vector2 rawInput = Vector2.zero;
    Vector2 smoothInput = Vector2.zero;
    List<IDirectionalReceiver> receiversList = new List<IDirectionalReceiver>();

    int lastStableDir = -1;
    int candidateDir = -1;
    int candidateFrames = 0;

    void Awake()
    {
        if (receiversRoot == null) receiversRoot = transform;
        RefreshReceivers();
        foreach (var r in receiversList) r?.OnIdle();
    }

    void Update()
    {
        smoothInput = Vector2.Lerp(smoothInput, rawInput, smoothingAlpha);

        if (smoothInput.sqrMagnitude < deadzone * deadzone)
        {
            if (lastStableDir != -1)
            {
                lastStableDir = -1;
                NotifyIdle();
            }
            candidateDir = -1;
            candidateFrames = 0;
            return;
        }

        Vector2 dirVec = smoothInput.normalized;
        float angle = Mathf.Atan2(dirVec.y, dirVec.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        float snapped = Mathf.Round(angle / 45f) * 45f;
        if (snapped >= 360f) snapped = 0f;
        int octant = Mathf.RoundToInt(snapped / 45f) % 8;

        if (angleMarginDegrees > 0f && lastStableDir >= 0)
        {
            float lastCenter = lastStableDir * 45f;
            float distFromLast = Mathf.Abs(Mathf.DeltaAngle(lastCenter, angle));
            if (distFromLast < angleMarginDegrees) octant = lastStableDir;
        }

        if (octant != candidateDir)
        {
            candidateDir = octant;
            candidateFrames = 1;
        }
        else candidateFrames++;

        if (candidateFrames >= Mathf.Max(1, framesToConfirm) && candidateDir != lastStableDir)
        {
            lastStableDir = candidateDir;
            NotifyDirection(lastStableDir);
        }
    }

    void NotifyDirection(int dir)
    {
        foreach (var r in receiversList) r?.OnDirectionChanged(dir);
    }

    void NotifyIdle()
    {
        foreach (var r in receiversList) r?.OnIdle();
    }

    public void SetInput(Vector2 v) => rawInput = Vector2.ClampMagnitude(v, 1f);

    public void RefreshReceivers()
    {
        receiversList.Clear();
        var monos = receiversRoot.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        foreach (var m in monos)
            if (m is IDirectionalReceiver r) receiversList.Add(r);
    }

    public void RegisterReceiver(IDirectionalReceiver r)
    {
        if (r != null && !receiversList.Contains(r)) receiversList.Add(r);
    }

    public void UnregisterReceiver(IDirectionalReceiver r)
    {
        if (r != null && receiversList.Contains(r)) receiversList.Remove(r);
    }
}
