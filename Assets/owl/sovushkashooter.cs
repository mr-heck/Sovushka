using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Reflection;

/// <summary>
/// Raycast shooting. One shot = one energy charge (see GameManagerSimple). On fire:
///   1. a direction is picked (where Sovushka is moving / last moved, with a little aim assist),
///   2. a thin ray finds the first wall, a circle-cast finds the first Mr Heck before it,
///   3. a bullet sprite flies along the ray and the hit lands when the bullet arrives.
/// </summary>
[DisallowMultipleComponent]
public class SovushkaShooter : MonoBehaviour
{
    [Header("Attack")]
    public int damage = 1;
    public float shotAnimDuration = 0.18f;

    [Header("Raycast shot")]
    [Tooltip("Bullet visual (sprite + RaycastBullet). If empty the hit still lands, just without a visible bullet.")]
    public GameObject bulletPrefab;
    [Tooltip("How far a shot travels, in world units.")]
    public float range = 8f;
    [Tooltip("Thickness of the shot: how far off-centre a Mr Heck can be and still get hit.")]
    public float aimRadius = 0.35f;
    [Tooltip("Bullet flight speed (world units / second).")]
    public float bulletSpeed = 20f;
    [Tooltip("If a Mr Heck is within this many degrees of the aim direction, nudge the shot onto him (0 = off).")]
    public float aimAssistAngle = 12f;
    [Tooltip("Where the bullet leaves Sovushka, relative to her pivot.")]
    public Vector2 muzzleOffset = new Vector2(0f, 0.1f);
    [Tooltip("ON = solid map colliders stop a shot. OFF = shots fly over them (they mostly mark water edges / cliffs Sovushka can't walk through).")]
    public bool wallsBlockShots = false;

    [Header("Visuals (optional)")]
    public GameObject atkUp;
    public GameObject atkUpRight;
    public GameObject atkRight;
    public GameObject atkDownRight;
    public GameObject atkDown;
    public GameObject atkDownLeft;
    public GameObject atkLeft;
    public GameObject atkUpLeft;

    [Header("Optional hit visual prefab (spawned on the Mr Heck that gets hit)")]
    public GameObject hitEffectPrefab;

    [Header("Debug")]
    public bool debugLogs = false;
    public bool drawDebugRay = false;

    Component _movementComp = null;
    FieldInfo _isShootingField = null;
    PropertyInfo _isShootingProp = null;
    bool _warnedMovementNotFound = false;

    bool _firing = false;
    InputBridge _bridge = null;
    OwlMovement _owl = null;
    Vector2 _lastAim = Vector2.right;

    void Awake()
    {
        HideAllAttackVisuals();
        CacheMovementComponent();
        _owl = GetComponent<OwlMovement>();
    }

    void Start()
    {
        // Keyboard fire (Space) is raised by InputBridge; route it through the same
        // energy-checked path as the on-screen Fire button.
        if (_bridge == null && InputBridge.Instance != null)
        {
            _bridge = InputBridge.Instance;
            _bridge.OnFire += FireOneShot;
        }
    }

    void OnDestroy()
    {
        if (_bridge != null) _bridge.OnFire -= FireOneShot;
    }

    void OnDisable()
    {
        // Sovushka gets disabled while hidden (eaten by Mr Heck / waiting to be spawned).
        // Make sure a half-finished shot can't leave her frozen after she comes back.
        if (_firing)
        {
            StopAllCoroutines();
            HideAllAttackVisuals();
            SetMovementShootingFlag(false);
            _firing = false;
        }
    }

    void Update()
    {
        // Remember the last direction Sovushka was steered in so a tap on Fire (thumb off the
        // joystick) still shoots the way she is facing.
        UpdateLastAim();
    }

    /// <summary>
    /// Fires one shot. Costs exactly one energy charge; does nothing (and costs nothing)
    /// when the tank is empty, Sovushka is hidden, the game is paused, or a shot is
    /// already in progress.
    /// </summary>
    public void FireOneShot()
    {
        if (!isActiveAndEnabled) return;      // hidden / eaten / not spawned yet
        if (Time.timeScale <= 0f) return;     // paused or on the home screen
        if (_firing) return;                  // one shot at a time

        if (GameManagerSimple.Instance == null)
        {
            Debug.LogWarning("[SovushkaShooter] GameManagerSimple not found in scene.");
            return;
        }

        // Spend a charge first. Empty tank = no shot.
        if (!GameManagerSimple.Instance.TryConsumeCharge())
        {
            if (debugLogs) Debug.Log("[SovushkaShooter] Out of energy - visit Yanam to refill.");
            return;
        }

        StartCoroutine(FireRoutine());
    }

    IEnumerator FireRoutine()
    {
        _firing = true;
        SetMovementShootingFlag(true);

        Vector2 origin = (Vector2)transform.position + muzzleOffset;
        Vector2 dir = ApplyAimAssist(origin, _lastAim);

        var visual = Play8WayAttackVisual(dir);

        Transform target;
        Vector2 end;
        ResolveShot(origin, dir, out target, out end);

        if (debugLogs) Debug.Log($"[SovushkaShooter] Shot dir={dir} end={end} target={(target != null ? target.name : "none")}");
        if (drawDebugRay) Debug.DrawLine(origin, end, target != null ? Color.red : Color.yellow, 0.6f);

        Vector3 shooterPos = transform.position;
        System.Action onArrive = () => ApplyHit(target, shooterPos);

        if (bulletPrefab != null)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var go = Instantiate(bulletPrefab, new Vector3(origin.x, origin.y, transform.position.z), Quaternion.Euler(0f, 0f, angle));
            go.SetActive(true);
            var rb = go.GetComponent<RaycastBullet>();
            if (rb != null) rb.Launch(end, bulletSpeed, onArrive);
            else { Destroy(go, 0.5f); onArrive(); }
        }
        else
        {
            onArrive();
        }

        yield return new WaitForSeconds(shotAnimDuration);

        if (visual != null) visual.SetActive(false);
        SetMovementShootingFlag(false);
        _firing = false;
    }

    // -------------------------
    // Aiming
    // -------------------------
    void UpdateLastAim()
    {
        if (InputBridge.Instance == null) return;
        Vector2 m = InputBridge.Instance.Move;
        if (m.sqrMagnitude <= 0.04f) return;

        // Same isometric squash the movement uses, so she shoots the way she visibly travels.
        float iso = _owl != null ? _owl.isometricAngle : 0.5f;
        Vector2 world = new Vector2(m.x, m.y * iso);
        if (world.sqrMagnitude > 0.0001f) _lastAim = world.normalized;
    }

    Vector2 ApplyAimAssist(Vector2 origin, Vector2 aim)
    {
        if (aimAssistAngle <= 0f) return aim;

        Vector2 best = aim;
        float bestAngle = aimAssistAngle;
        foreach (var h in Object.FindObjectsByType<MrHeckBehaviour>())
        {
            if (h == null || !IsHeckPresent(h.transform)) continue;
            Vector2 to = (Vector2)h.transform.position - origin;
            if (to.sqrMagnitude < 0.0001f || to.magnitude > range) continue;
            float ang = Vector2.Angle(aim, to);
            if (ang < bestAngle) { bestAngle = ang; best = to.normalized; }
        }
        return best;
    }

    // -------------------------
    // Raycast resolution
    // -------------------------
    static bool IsWall(Collider2D c)
    {
        return c is CompositeCollider2D || c is TilemapCollider2D;
    }

    // A vanished Mr Heck keeps his GameObject but every animation child is deactivated.
    static bool IsHeckPresent(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        return t.GetComponentInChildren<SpriteRenderer>(false) != null;
    }

    void ResolveShot(Vector2 origin, Vector2 dir, out Transform target, out Vector2 end)
    {
        // 1) thin ray: where does the first wall stop the shot?
        float reach = range;
        var wallHits = Physics2D.RaycastAll(origin, dir, range);
        foreach (var h in wallHits)
        {
            var c = h.collider;
            if (c == null || c.isTrigger) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (wallsBlockShots && IsWall(c)) { reach = h.distance; break; }   // results are sorted nearest-first
        }

        // 2) fat ray: nearest Mr Heck before that wall
        target = null;
        float bestDist = float.MaxValue;
        var heckHits = Physics2D.CircleCastAll(origin, aimRadius, dir, reach);
        foreach (var h in heckHits)
        {
            var c = h.collider;
            if (c == null) continue;
            var heck = c.GetComponentInParent<MrHeckBehaviour>();
            if (heck == null || !IsHeckPresent(heck.transform)) continue;

            float d = Vector2.Distance(origin, heck.transform.position);
            if (d < bestDist) { bestDist = d; target = heck.transform; }
        }

        end = target != null ? (Vector2)target.position : origin + dir * reach;
    }

    void ApplyHit(Transform heck, Vector3 shooterPos)
    {
        if (heck == null || !IsHeckPresent(heck)) return;   // he vanished while the bullet was in flight

        // SendMessage only reaches this exact GameObject, so it has to be the Mr Heck root.
        heck.gameObject.SendMessage("OnHitByPlayerWithSource", shooterPos, SendMessageOptions.DontRequireReceiver);
        heck.gameObject.SendMessage("OnHitByPlayer", damage, SendMessageOptions.DontRequireReceiver);

        if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, heck.position, Quaternion.identity);
    }

    // -------------------------
    // Attack pose
    // -------------------------
    GameObject Play8WayAttackVisual(Vector2 dir)
    {
        HideAllAttackVisuals();
        if (dir.sqrMagnitude < 0.0001f) { if (atkRight) { atkRight.SetActive(true); return atkRight; } return null; }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        angle = (angle + 90f + 360f) % 360f;
        int index = Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;

        GameObject chosen = index switch
        {
            0 => atkUp,
            1 => atkUpRight,
            2 => atkRight,
            3 => atkDownRight,
            4 => atkDown,
            5 => atkDownLeft,
            6 => atkLeft,
            7 => atkUpLeft,
            _ => atkRight
        };

        if (chosen != null) chosen.SetActive(true);
        return chosen;
    }

    void HideAllAttackVisuals()
    {
        if (atkUp) atkUp.SetActive(false);
        if (atkUpRight) atkUpRight.SetActive(false);
        if (atkRight) atkRight.SetActive(false);
        if (atkDownRight) atkDownRight.SetActive(false);
        if (atkDown) atkDown.SetActive(false);
        if (atkDownLeft) atkDownLeft.SetActive(false);
        if (atkLeft) atkLeft.SetActive(false);
        if (atkUpLeft) atkUpLeft.SetActive(false);
    }

    void CacheMovementComponent()
    {
        foreach (var comp in GetComponents<Component>())
        {
            if (comp == null) continue;
            var t = comp.GetType();
            if (t == this.GetType()) continue;
            var f = t.GetField("isShooting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var p = t.GetProperty("IsShooting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
                    t.GetProperty("isShooting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null || p != null)
            {
                _movementComp = comp;
                _isShootingField = f;
                _isShootingProp = p;
                return;
            }
        }
    }

    void SetMovementShootingFlag(bool value)
    {
        if (_movementComp == null)
        {
            CacheMovementComponent();
            if (_movementComp == null)
            {
                if (!_warnedMovementNotFound)
                {
                    Debug.LogWarning("[SovushkaShooter] Movement component with isShooting not found. Movement won't be auto-frozen.", this);
                    _warnedMovementNotFound = true;
                }
                return;
            }
        }

        try
        {
            if (_isShootingField != null) { _isShootingField.SetValue(_movementComp, value); return; }
            if (_isShootingProp != null && _isShootingProp.CanWrite) { _isShootingProp.SetValue(_movementComp, value, null); return; }

            var t = _movementComp.GetType();
            var pubField = t.GetField("isShooting", BindingFlags.Instance | BindingFlags.Public);
            if (pubField != null) { pubField.SetValue(_movementComp, value); return; }
            var pubProp = t.GetProperty("IsShooting", BindingFlags.Instance | BindingFlags.Public) ?? t.GetProperty("isShooting", BindingFlags.Instance | BindingFlags.Public);
            if (pubProp != null && pubProp.CanWrite) { pubProp.SetValue(_movementComp, value, null); return; }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[SovushkaShooter] Failed to set movement isShooting flag: " + ex.Message, this);
        }
    }
}
