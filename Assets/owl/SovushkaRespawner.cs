using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SovushkaRespawner : MonoBehaviour
{
    [Tooltip("If set, this transform will be used as the respawn point. Otherwise the current position is used.")]
    public Transform respawnPoint;

    [Tooltip("When HideAndRespawn is called, wait this long before restoring (seconds).")]
    public float defaultRespawnDelay = 15f;

    [Tooltip("If true, Sovushka will be hidden at Start and only spawned by Yanam or SpawnNow.")]
    public bool startHidden = true;

    // internal saved state
    private List<Behaviour> savedBehaviours = new List<Behaviour>();
    private List<Renderer> savedRenderers = new List<Renderer>();
    private List<Collider2D> savedColliders = new List<Collider2D>();
    private Rigidbody2D rb;
    private bool isHidden = false;

    /// <summary>True while Sovushka is hidden (not spawned yet / being eaten).</summary>
    public bool IsHidden { get { return isHidden; } }
    private Coroutine respawnCoroutine = null;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (startHidden)
        {
            HideVisualsAndControls();
            isHidden = true;
        }
    }

    /// <summary>
    /// Public helper to hide visuals and controls immediately (keeps GameObject active).
    /// Use this when another system (like MrHeck) will call SpawnNow() explicitly later.
    /// </summary>
    public void HideVisualsAndControlsPublic()
    {
        // Cancel any pending respawn (we're explicitly hiding now)
        if (respawnCoroutine != null) { StopCoroutine(respawnCoroutine); respawnCoroutine = null; }
        HideVisualsAndControls();
        isHidden = true;
    }

    private void HideVisualsAndControls()
    {
        // Save and disable renderers
        savedRenderers.Clear();
        var rends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            savedRenderers.Add(r);
            r.enabled = false;
        }

        // Save and disable colliders
        savedColliders.Clear();
        var cols = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
        {
            savedColliders.Add(c);
            c.enabled = false;
        }

        // Save and disable other behaviours (control scripts) except this component
        savedBehaviours.Clear();
        var behaviours = GetComponents<Behaviour>();
        foreach (var b in behaviours)
        {
            if (b == this) continue;
            savedBehaviours.Add(b);
            b.enabled = false;
        }

        // Freeze physics if present
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    /// <summary>
    /// Hide Sovushka safely (do not SetActive(false)) and schedule a respawn after delaySeconds.
    /// Use this when you want a delayed respawn (other systems rely on the delay).
    /// </summary>
    public void HideAndRespawn(float delaySeconds = -1f, Vector3? forcedRespawnPosition = null)
    {
        if (delaySeconds <= 0f) delaySeconds = defaultRespawnDelay;

        // If already hidden, restart the scheduled respawn with the new parameters
        if (isHidden)
        {
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            respawnCoroutine = StartCoroutine(RespawnCoroutine(delaySeconds, forcedRespawnPosition));
            return;
        }

        HideVisualsAndControls();
        isHidden = true;
        respawnCoroutine = StartCoroutine(RespawnCoroutine(delaySeconds, forcedRespawnPosition));
    }

    /// <summary>
    /// Immediately spawn (restore) Sovushka now. Cancels any pending respawn.
    /// </summary>
    public void SpawnNow(Vector3? forcedRespawnPosition = null)
    {
        // Cancel any scheduled respawn so we don't double-restore later
        if (respawnCoroutine != null) { StopCoroutine(respawnCoroutine); respawnCoroutine = null; }
        StartCoroutine(ImmediateRestoreCoroutine(forcedRespawnPosition));
    }

    private IEnumerator RespawnCoroutine(float delaySeconds, Vector3? forcedRespawnPosition)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delaySeconds));
        yield return StartCoroutine(ImmediateRestoreCoroutine(forcedRespawnPosition));
    }

    private IEnumerator ImmediateRestoreCoroutine(Vector3? forcedRespawnPosition)
    {
        Vector3 respawnPos = forcedRespawnPosition ?? (respawnPoint != null ? respawnPoint.position : transform.position);

        // small frame delay
        yield return null;

        transform.position = respawnPos;

        // restore renderers
        foreach (var r in savedRenderers)
        {
            if (r != null) r.enabled = true;
        }

        // restore colliders
        foreach (var c in savedColliders)
        {
            if (c != null) c.enabled = true;
        }

        // restore behaviours
        foreach (var b in savedBehaviours)
        {
            if (b != null) b.enabled = true;
        }

        // restore physics
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        isHidden = false;
        respawnCoroutine = null;
        yield break;
    }
}
