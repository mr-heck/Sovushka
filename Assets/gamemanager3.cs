using System;
using UnityEngine;

/// <summary>
/// Shot energy + hit processing.
///
/// Energy model: each shot costs exactly 1 charge. The player starts with a full
/// tank (maxCharges, default 5) and the tank ONLY refills when Sovushka visits Yanam
/// (YanamProximitySaver calls RefillToFull). There is no passive recharge unless
/// passiveRecharge is explicitly turned on in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class GameManagerSimple : MonoBehaviour
{
    public static GameManagerSimple Instance { get; private set; }

    [Header("Energy (1 charge = 1 shot)")]
    [Min(1)] public int maxCharges = 5;
    [Tooltip("OFF = energy only refills when Sovushka visits Yanam. ON = also trickles back over rechargeSeconds for a full tank.")]
    public bool passiveRecharge = false;
    [Tooltip("Only used when passiveRecharge is ON: seconds to go from empty to full.")]
    public float rechargeSeconds = 5f;
    [Tooltip("Starting energy in charges. Use -1 to start with a full tank.")]
    public float initialEnergy = -1f;

    [Header("Defaults")]
    public float defaultDetectionRadius = 3f;
    public GameObject defaultHitVisualPrefab;

    [Header("Debug")]
    public bool debugLogs = false;

    [SerializeField] float _currentEnergy = 0f;

    public event Action<float> OnEnergyChanged;
    public event Action<int> OnChargesChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (maxCharges < 1) maxCharges = 1;

        if (initialEnergy >= 0f) _currentEnergy = Mathf.Clamp(initialEnergy, 0f, maxCharges);
        else _currentEnergy = maxCharges;

        BroadcastEnergy();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!passiveRecharge || rechargeSeconds <= 0f) return;
        if (_currentEnergy >= maxCharges) return;

        float ratePerSecond = maxCharges / rechargeSeconds;
        _currentEnergy = Mathf.Min(maxCharges, _currentEnergy + ratePerSecond * Time.deltaTime);
        BroadcastEnergy();
    }

    void BroadcastEnergy()
    {
        float normalized = GetNormalizedEnergy();
        OnEnergyChanged?.Invoke(normalized);
        OnChargesChanged?.Invoke(GetAvailableCharges());
        if (debugLogs) Debug.Log($"[GameManagerSimple] Energy: {_currentEnergy}/{maxCharges} (norm {normalized})");
    }

    /// <summary>Spends one charge. Returns false (and spends nothing) if the tank is empty.</summary>
    public bool TryConsumeCharge()
    {
        if (_currentEnergy >= 1f)
        {
            _currentEnergy = Mathf.Max(0f, _currentEnergy - 1f);
            BroadcastEnergy();
            if (debugLogs) Debug.Log("[GameManagerSimple] Consumed 1 charge. Remaining: " + _currentEnergy);
            return true;
        }
        if (debugLogs) Debug.Log("[GameManagerSimple] Not enough energy to consume.");
        return false;
    }

    /// <summary>Fills the tank back to maxCharges (called when Sovushka visits Yanam).</summary>
    public void RefillToFull()
    {
        if (_currentEnergy >= maxCharges) return;
        _currentEnergy = maxCharges;
        BroadcastEnergy();
        if (debugLogs) Debug.Log("[GameManagerSimple] Energy refilled to full.");
    }

    // UI helpers used by UIManager_Compat
    public int GetAvailableCharges() => Mathf.FloorToInt(Mathf.Clamp(_currentEnergy, 0f, maxCharges) + 0.0001f);

    public float GetNormalizedEnergy() => Mathf.Clamp01(_currentEnergy / Mathf.Max(1, maxCharges));

    // A vanished Mr Heck keeps its GameObject but every animation child is deactivated,
    // so "no active SpriteRenderer underneath" means he is currently gone and can't be hit.
    static bool IsHeckPresent(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;
        return t.GetComponentInChildren<SpriteRenderer>(false) != null;
    }

    // Always resolve to the Mr Heck root (the object that owns MrHeckBehaviour / MrHeckAdapter),
    // because SendMessage only reaches the exact GameObject it is sent to - a hit sent to one of
    // his child sprites would be silently ignored.
    static Transform ResolveHeckRoot(Transform t)
    {
        if (t == null) return null;
        var behaviour = t.GetComponentInParent<MrHeckBehaviour>();
        return behaviour != null ? behaviour.transform : t;
    }

    Transform FindTarget(Transform shooter, float radius, Transform explicitMrHeck)
    {
        float radiusSqr = radius * radius;
        Vector2 origin = shooter.position;

        if (explicitMrHeck != null)
        {
            float sqr = ((Vector2)explicitMrHeck.position - origin).sqrMagnitude;
            if (sqr <= radiusSqr && IsHeckPresent(explicitMrHeck)) return ResolveHeckRoot(explicitMrHeck);
            if (debugLogs) Debug.Log("[GameManagerSimple] explicitMrHeck provided but out of range or vanished.");
        }

        Transform best = null;
        float bestSqr = float.MaxValue;

        var hecks = UnityEngine.Object.FindObjectsByType<MrHeckBehaviour>();
        foreach (var h in hecks)
        {
            if (h == null || !IsHeckPresent(h.transform)) continue;
            float d = ((Vector2)h.transform.position - origin).sqrMagnitude;
            if (d <= radiusSqr && d < bestSqr) { bestSqr = d; best = h.transform; }
        }
        return best;
    }

    public void ProcessPlayerShot(Transform shooter, int damage = 1, float detectionRadius = -1f, GameObject hitVisualPrefab = null, Transform explicitMrHeck = null)
    {
        if (shooter == null) { if (debugLogs) Debug.LogWarning("[GameManagerSimple] shooter is null."); return; }

        float radius = detectionRadius > 0f ? detectionRadius : defaultDetectionRadius;
        if (debugLogs) Debug.Log($"[GameManagerSimple] ProcessPlayerShot called by {shooter.name} radius={radius} damage={damage}");

        Transform target = FindTarget(shooter, radius, explicitMrHeck);
        if (target == null)
        {
            if (debugLogs) Debug.Log("[GameManagerSimple] No MrHeck found in range.");
            return;
        }

        if (debugLogs) Debug.Log($"[GameManagerSimple] Target chosen: {target.name} at {target.position}");

        // send shooter position first so MrHeck can pick left/right animation
        try { target.gameObject.SendMessage("OnHitByPlayerWithSource", shooter.position, SendMessageOptions.DontRequireReceiver); } catch { }
        try { target.gameObject.SendMessage("OnHitByPlayer", damage, SendMessageOptions.DontRequireReceiver); } catch { }

        var prefab = hitVisualPrefab != null ? hitVisualPrefab : defaultHitVisualPrefab;
        if (prefab != null) Instantiate(prefab, target.position, Quaternion.identity);
    }
}
