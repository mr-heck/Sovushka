using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class OwlMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Isometric Settings")]
    public float isometricAngle = 0.5f;

    [Header("Input deadzone")]
    [Tooltip("Below this input magnitude the owl is considered idle/stopped. Keeps tiny joystick residue from reading as movement.")]
    public float moveDeadzone = 0.03f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool isMoving;
    private bool wasMoving = false;

    [Header("Animation")]
    [Tooltip("The single Animator that drives all owl poses (consolidated controller: Assets/owl/OwlMovement.controller).")]
    public Animator anim;

    // Per-state local position offsets, taken from each pose's original hand-placed pivot.
    // Keyed by Animator state name.
    private Dictionary<string, Vector3> poseOffsets;

    [Header("Legacy shooting animation refs (unused by current shooting path; kept to avoid breaking old serialized data)")]
    public GameObject owlyShootRDown;
    public GameObject owlyShootRUp;
    public GameObject owlyShootLDown;
    public GameObject owlyShootLUp;

    [Header("Beam Settings")]
    public GameObject beamPrefab;
    public float tileSize = 1f;
    public int beamLength = 10;
    public float beamSpeed = 10f;
    public float shootAnimDuration = 0.25f;

    // When true, the owl is in a shooting state: it will not move, but WSAD will change the beam direction
    private bool isShooting = false;

    private enum IdleState { Flap1, Flap2, Blink }
    private IdleState currentIdleState = IdleState.Flap1;

    private float idleTimer = 0f;
    private float flapDuration = 1f;
    private float blinkDuration = 1f;

    private string currentMoveState = null;
    private bool idleAnimShown = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        if (anim == null) anim = GetComponentInChildren<Animator>();

        BuildPoseOffsets();

        flapDuration = GetClipLength("owly flap");
        blinkDuration = GetClipLength("owly blink");

        SetIdleState(IdleState.Flap1);
    }

    void BuildPoseOffsets()
    {
        // These match each original pose GameObject's hand-tuned local position,
        // preserved here so the consolidated single-sprite setup still looks identical.
        poseOffsets = new Dictionary<string, Vector3>
        {
            { "FlyLeft",       new Vector3(0.01f, -0.18f, 0f) },
            { "FlyRight",      new Vector3(0.00f, -0.12f, 0f) },
            { "FlyBack",       new Vector3(0.02f, -0.16f, 0f) },
            { "FlyFront",      new Vector3(0.02f, -0.19f, 0f) },
            { "DiagUpLeft",    new Vector3(0.08f, -0.17f, 0f) },
            { "DiagUpRight",   new Vector3(0.06f, -0.17f, 0f) },
            { "DiagDownLeft",  new Vector3(0.06f, -0.18f, 0f) },
            { "DiagDownRight", new Vector3(0.00f, -0.19f, 0f) },
            { "Blink",         new Vector3(0.03f, -0.25f, 0f) },
            { "Flap",          new Vector3(0.05f, -0.25f, 0f) },
        };
    }

    void Update()
    {
        // Read movement from InputBridge (works for joystick and keyboard fallback)
        if (InputBridge.Instance != null)
            moveInput = InputBridge.Instance.Move;
        else
            moveInput = Vector2.zero;

        // When shooting, we don't want the character to move; WSAD only affects beam direction.
        isMoving = !isShooting && moveInput.sqrMagnitude > (moveDeadzone * moveDeadzone);

        if (isMoving)
        {
            if (!wasMoving)
            {
                idleTimer = 0f;
                idleAnimShown = false;
                SetIdleState(IdleState.Flap1);
            }
            wasMoving = true;
            HandleMovementAnimation();
        }
        else
        {
            if (wasMoving)
            {
                currentMoveState = null;
                idleAnimShown = false;
                SetIdleState(IdleState.Flap1);
            }
            wasMoving = false;
            if (!isShooting) // only run idle cycle when not shooting
                HandleIdleCycle();
        }
    }

    void FixedUpdate()
    {
        // If shooting, freeze movement (velocity zero). WSAD will not move the owl.
        if (isShooting)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isMoving)
        {
            Vector2 isoDir = new Vector2(
                moveInput.x,
                moveInput.y * isometricAngle
            );

            // Preserve how far the joystick is actually pushed (0..1) instead of always
            // snapping to full speed. This is what makes the owl stop the instant the
            // joystick is released/returns to center, rather than coasting at full
            // speed until the handle finally settles back to (0,0).
            float pushAmount = Mathf.Clamp01(moveInput.magnitude);
            rb.linearVelocity = isoDir.normalized * moveSpeed * pushAmount;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    void HandleMovementAnimation()
    {
        string targetState = DirectionStateForInput(moveInput);

        if (targetState != currentMoveState)
        {
            currentMoveState = targetState;
            if (currentMoveState != null)
                PlayState(currentMoveState);
        }
    }

    // Picks one of the 8 movement poses from the raw input vector using angle
    // snapping instead of raw sign checks. Sign checks (x<0 && y>0, etc.) only
    // ever land on a cardinal pose when one axis is *exactly* zero - keyboard
    // input naturally gives you that, but an analog joystick almost never
    // does, so every joystick push used to read as diagonal. Angle snapping
    // fixes that for both input sources, and giving the 4 cardinal directions
    // a wider capture window than the diagonals (60 degrees vs 30) makes them
    // easy to hit from a joystick, since players rarely push perfectly on-axis.
    string DirectionStateForInput(Vector2 input)
    {
        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        const float cardinalHalfWidth = 30f;  // cardinal windows are 60 degrees wide
        const float diagonalHalfWidth = 15f;  // diagonal windows are 30 degrees wide

        if (AngleWithin(angle, 0f, cardinalHalfWidth)) return "FlyRight";
        if (AngleWithin(angle, 90f, cardinalHalfWidth)) return "FlyBack";
        if (AngleWithin(angle, 180f, cardinalHalfWidth)) return "FlyLeft";
        if (AngleWithin(angle, 270f, cardinalHalfWidth)) return "FlyFront";

        if (AngleWithin(angle, 45f, diagonalHalfWidth)) return "DiagUpRight";
        if (AngleWithin(angle, 135f, diagonalHalfWidth)) return "DiagUpLeft";
        if (AngleWithin(angle, 225f, diagonalHalfWidth)) return "DiagDownLeft";
        if (AngleWithin(angle, 315f, diagonalHalfWidth)) return "DiagDownRight";

        return null; // shouldn't happen - the windows above tile the full circle
    }

    bool AngleWithin(float angle, float center, float halfWidth)
    {
        return Mathf.Abs(Mathf.DeltaAngle(angle, center)) <= halfWidth;
    }

    void HandleIdleCycle()
    {
        idleTimer += Time.deltaTime;

        switch (currentIdleState)
        {
            case IdleState.Flap1:
                if (!idleAnimShown)
                {
                    PlayState("Flap");
                    idleAnimShown = true;
                }
                if (idleTimer >= flapDuration)
                    SetIdleState(IdleState.Flap2);
                break;

            case IdleState.Flap2:
                if (!idleAnimShown)
                {
                    PlayState("Flap");
                    idleAnimShown = true;
                }
                if (idleTimer >= flapDuration)
                    SetIdleState(IdleState.Blink);
                break;

            case IdleState.Blink:
                if (!idleAnimShown)
                {
                    PlayState("Blink");
                    idleAnimShown = true;
                }
                if (idleTimer >= blinkDuration)
                    SetIdleState(IdleState.Flap1);
                break;
        }
    }

    /// <summary>
    /// Plays a state on the single consolidated Animator and restores that pose's
    /// hand-tuned local position offset (each original sprite was placed a few
    /// hundredths of a unit differently to line up its art).
    /// </summary>
    void PlayState(string stateName)
    {
        if (anim == null) return;
        anim.Play(stateName, 0, 0f);
        if (poseOffsets != null && poseOffsets.TryGetValue(stateName, out var offset))
            anim.transform.localPosition = offset;
    }

    float GetClipLength(string clipName)
    {
        if (anim == null) return 1f;
        RuntimeAnimatorController ac = anim.runtimeAnimatorController;
        if (ac == null) return 1f;
        foreach (AnimationClip clip in ac.animationClips)
        {
            if (clip.name == clipName) return clip.length;
        }
        return 1f;
    }

    void SetIdleState(IdleState state)
    {
        currentIdleState = state;
        idleTimer = 0f;
        idleAnimShown = false;
    }

    // ---------------------------------------------------------------------
    // Legacy shooting coroutines below are DEAD CODE in the current build:
    // nothing calls StartShootingWithDynamicDirection / StartDirectionalShooting
    // (both are private and unreferenced - the active shooting path is
    // SovushkaShooter.cs). Left untouched/unused to avoid changing behavior
    // that wasn't part of this fix; safe to remove later if confirmed unused.
    // ---------------------------------------------------------------------

    IEnumerator StartShootingWithDynamicDirection()
    {
        if (isShooting) yield break;

        GameObject shootAnim = null;
        Vector2 initialDir = Vector2.zero;

        if (moveInput.x > 0 && moveInput.y > 0)
        {
            shootAnim = owlyShootRUp;
            initialDir = new Vector2(1f, 1f);
        }
        else if (moveInput.x > 0 && moveInput.y < 0)
        {
            shootAnim = owlyShootRDown;
            initialDir = new Vector2(1f, -1f);
        }
        else if (moveInput.x < 0 && moveInput.y > 0)
        {
            shootAnim = owlyShootLUp;
            initialDir = new Vector2(-1f, 1f);
        }
        else if (moveInput.x < 0 && moveInput.y < 0)
        {
            shootAnim = owlyShootLDown;
            initialDir = new Vector2(-1f, -1f);
        }
        else
        {
            shootAnim = owlyShootRUp;
            initialDir = new Vector2(1f, 1f);
        }

        isShooting = true;
        if (shootAnim != null)
        {
            shootAnim.SetActive(true);
            StartCoroutine(DisableAfterDelay(shootAnim, shootAnimDuration));
        }

        GameObject beam = null;
        if (beamPrefab != null)
        {
            beam = Instantiate(beamPrefab, transform.position, Quaternion.identity);
            float angle = Mathf.Atan2(initialDir.y, initialDir.x) * Mathf.Rad2Deg;
            beam.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (beam != null)
        {
            float distanceTravelled = 0f;
            float maxDistance = beamLength * tileSize;
            Vector2 currentDir = initialDir.normalized;

            while (beam != null && distanceTravelled < maxDistance)
            {
                Vector2 input = moveInput;
                if (input.sqrMagnitude > 0.001f)
                {
                    Vector2 candidate = input.normalized;
                    if (Mathf.Approximately(Mathf.Abs(candidate.x), 0f) || Mathf.Approximately(Mathf.Abs(candidate.y), 0f))
                    {
                        float sx = Mathf.Sign(candidate.x) != 0 ? Mathf.Sign(candidate.x) : Mathf.Sign(currentDir.x);
                        float sy = Mathf.Sign(candidate.y) != 0 ? Mathf.Sign(candidate.y) : Mathf.Sign(currentDir.y);
                        candidate = new Vector2(sx, sy).normalized;
                    }
                    currentDir = candidate;
                    float newAngle = Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg;
                    beam.transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
                }

                float step = beamSpeed * Time.deltaTime;
                beam.transform.position += (Vector3)(currentDir * step);
                distanceTravelled += step;
                yield return null;
            }

            if (beam != null)
                Destroy(beam);
        }

        isShooting = false;
    }

    IEnumerator StartDirectionalShooting(GameObject shootAnim, Vector2 dir)
    {
        if (isShooting) yield break;

        isShooting = true;
        if (shootAnim != null)
        {
            shootAnim.SetActive(true);
            StartCoroutine(DisableAfterDelay(shootAnim, shootAnimDuration));
        }

        GameObject beam = null;
        if (beamPrefab != null)
        {
            beam = Instantiate(beamPrefab, transform.position, Quaternion.identity);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            beam.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (beam != null)
        {
            float distanceTravelled = 0f;
            float maxDistance = beamLength * tileSize;
            Vector2 currentDir = dir.normalized;

            while (beam != null && distanceTravelled < maxDistance)
            {
                float step = beamSpeed * Time.deltaTime;
                beam.transform.position += (Vector3)(currentDir * step);
                distanceTravelled += step;
                yield return null;
            }

            if (beam != null)
                Destroy(beam);
        }

        isShooting = false;
    }

    IEnumerator DisableAfterDelay(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null)
            go.SetActive(false);
    }
}
