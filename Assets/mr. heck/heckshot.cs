using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MrHeckController_NoAnimator : MonoBehaviour
{
    [Header("Respawn")]
    public float respawnDelay = 5f;
    public Transform spawnPoint; // optional

    [Header("Visuals")]
    public GameObject[] aliveVisuals;
    public GameObject[] downVisuals;

    [Header("Shot Animations (assign child GameObjects)")]
    public GameObject heckShotL;
    public GameObject heckShotR;

    [Header("Shot/Respawn Prefabs (optional)")]
    public GameObject shotVisualPrefab;
    public GameObject respawnVisualPrefab;

    [Header("Animation / timing fallback")]
    public float defaultClipLength = 0.12f;

    [Header("Debug")]
    public bool debugLogs = true;

    bool _isDown = false;
    Coroutine _respawnCoroutine;
    Coroutine _shotCoroutine;

    Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        SetAliveVisuals(true);
        SetDownVisuals(false);
    }

    // Legacy entry used by GameManagerSimple.SendMessage("OnHitByPlayer", damage)
    public void OnHitByPlayer(int damage)
    {
        Vector3 assumedShooterPos = transform.position + Vector3.right;
        OnHitByPlayerWithSource(assumedShooterPos);
    }

    // Preferred entry used by GameManagerSimple.SendMessage("OnHitByPlayerWithSource", shooterPosition)
    public void OnHitByPlayerWithSource(Vector3 shooterWorldPosition)
    {
        if (_isDown) return;
        if (_shotCoroutine != null) return;
        if (debugLogs) Debug.Log($"[MrHeck] OnHitByPlayerWithSource called on {name} shooterPos={shooterWorldPosition}");
        _shotCoroutine = StartCoroutine(ShotThenVanishRoutine(shooterWorldPosition));
    }

    IEnumerator ShotThenVanishRoutine(Vector3 shooterWorldPosition)
    {
        // 1) Stop physics movement immediately
        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        // 2) Disable other MonoBehaviours on this GameObject to prevent AI from moving/chasing
        var disabledComps = new List<Behaviour>();
        var comps = GetComponents<Behaviour>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            if (c == this) continue;
            if (c.enabled)
            {
                c.enabled = false;
                disabledComps.Add(c);
            }
        }

        // 3) Spawn optional shot visual prefab
        if (shotVisualPrefab != null)
        {
            try { Instantiate(shotVisualPrefab, transform.position, Quaternion.identity); }
            catch { }
        }

        // 4) Determine left/right shot animation GameObject
        bool shotFromLeft = shooterWorldPosition.x < transform.position.x;
        GameObject shotAnim = shotFromLeft ? heckShotL : heckShotR;

        // Hide alive visuals so shot animation is visible
        SetAliveVisuals(false);
        SetDownVisuals(false);

        // 5) Play shot animation (Animator or fallback)
        if (shotAnim != null)
        {
            if (debugLogs) Debug.Log($"[MrHeck] Playing shot animation {(shotFromLeft ? "Left" : "Right")} for {name}");
            SafeSetActive(shotAnim, true);
            yield return StartCoroutine(PlayAnimationAndHold(shotAnim));
            SafeSetActive(shotAnim, false);
        }
        else
        {
            if (debugLogs) Debug.Log("[MrHeck] No shot animation assigned; using fallback delay.");
            yield return new WaitForSeconds(defaultClipLength);
        }

        // 6) Vanish and schedule respawn
        SetAliveVisuals(false);
        SetDownVisuals(true);
        _isDown = true;

        if (_respawnCoroutine != null) StopCoroutine(_respawnCoroutine);
        _respawnCoroutine = StartCoroutine(RespawnCoroutine());

        // 7) Re-enable previously disabled components (they will resume after vanish; if you want them disabled during vanish keep them disabled)
        foreach (var c in disabledComps) if (c != null) c.enabled = true;

        _shotCoroutine = null;
    }

    IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (spawnPoint != null) transform.position = spawnPoint.position;

        if (respawnVisualPrefab != null)
        {
            try { Instantiate(respawnVisualPrefab, transform.position, Quaternion.identity); }
            catch { }
        }

        SetDownVisuals(false);
        SetAliveVisuals(true);
        _isDown = false;

        if (debugLogs) Debug.Log($"[MrHeck] Respawned {name}");
        _respawnCoroutine = null;
    }

    void SetAliveVisuals(bool on)
    {
        if (aliveVisuals == null) return;
        foreach (var g in aliveVisuals) if (g != null) g.SetActive(on);
    }

    void SetDownVisuals(bool on)
    {
        if (downVisuals == null) return;
        foreach (var g in downVisuals) if (g != null) g.SetActive(on);
    }

    private IEnumerator PlayAnimationAndHold(GameObject anim)
    {
        if (anim == null) yield break;

        SafeSetActive(anim, true);

        Animator a = anim.GetComponent<Animator>();
        float clipLength = defaultClipLength;

        if (a != null && a.runtimeAnimatorController != null)
        {
            var clips = a.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                clipLength = clips[0].length;
                string clipName = clips[0].name;
                try
                {
                    a.Play(clipName, 0, 0f);
                    a.Update(0f);
                    a.speed = 1f;
                }
                catch { /* ignore animator play errors */ }
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));
        SafeSetActive(anim, false);
    }

    private void SafeSetActive(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf == active) return;
        go.SetActive(active);
    }
}
