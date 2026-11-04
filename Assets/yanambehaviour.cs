using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Yanam : MonoBehaviour
{
    [Header("Approach / Landing")]
    public Transform landingPoint;
    public float approachSpeed = 3f;
    public float landingRadius = 0.5f;
    public float settleTime = 0.25f;
    public bool startAtLandingPoint = false;

    [Header("Distances (units = tiles)")]
    public float owlyMoveAwayDistance = 2f; // when owly moves beyond this, play closing and start proximity logic
    public float proximityRadius = 1f;      // proximity open/close radius

    [Header("Sovushka")]
    public SovushkaRespawner sovushkaRespawner; // assign Sovushka's SovushkaRespawner component
    public bool spawnSovushkaOnLand = true;

    [Header("Animation children (assign exactly)")]
    public GameObject yanamFlying;         // "yanam flying"
    public GameObject yanamLanding;        // "yanam landing"
    public GameObject yanamOpeningOwly;    // "yanam opening owly" (reveal)
    public GameObject yanamOpening;        // "yanam opening" (used only for proximity open)
    public GameObject yanamClosing;        // "yanam closing" (used for closing and frozen last frame)

    [Header("Animation timing fallback (seconds)")]
    public float defaultClipLength = 0.8f;

    // internal state
    private Rigidbody2D rb;
    private GameObject currentAnim = null;
    private Coroutine landingCoroutine = null;
    private bool hasLanded = false;
    private bool sovushkaSpawned = false;
    private bool movedAwayTriggered = false;
    private bool proximityOpenState = false; // true when currently open (frozen on last frame)
    private Transform sovushkaTransform = null;

    // Dedicated looping source for the flying sound (AudioManager's pool is for one-shots
    // and would never reclaim a source that's set to loop, so this stays local to Yanam).
    private AudioSource _loopSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero;
        }

        _loopSource = GetComponent<AudioSource>();
        if (_loopSource == null) _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.spatialBlend = 0f;
        if (GetComponent<SfxVolumeApplier>() == null) gameObject.AddComponent<SfxVolumeApplier>();

        // Force-disable every child under Yanam so nothing plays before script runs
        DeactivateAllChildGameObjects();
    }

    void Start()
    {
        // Start with flying (or landing if configured)
        if (startAtLandingPoint && landingPoint != null)
        {
            transform.position = landingPoint.position;
            hasLanded = true;
            PlayExclusive(yanamFlying);
        }
        else
        {
            PlayExclusive(yanamFlying);
            PlayLoopSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.yanamFlyingLoop : null);
        }
    }

    void FixedUpdate()
    {
        if (hasLanded) return;
        if (landingPoint == null) return;

        Vector3 dir = landingPoint.position - transform.position;
        dir.z = 0f;
        float dist = dir.magnitude;

        if (dist > landingRadius)
        {
            Vector3 move = dir.normalized * approachSpeed * Time.fixedDeltaTime;
            rb.MovePosition(transform.position + move);
            PlayExclusive(yanamFlying);
        }
        else
        {
            rb.MovePosition(landingPoint.position);
            hasLanded = true;
            StopLoopSound();
            if (landingCoroutine != null) StopCoroutine(landingCoroutine);
            landingCoroutine = StartCoroutine(LandingAndRevealSequence());
        }
    }

    private IEnumerator LandingAndRevealSequence()
    {
        // 1) landing animation (one-shot)
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.yanamLanding : null);
        if (yanamLanding != null)
            yield return StartCoroutine(PlayAnimationAndHold(yanamLanding));
        else
            yield return null;

        // 2) SKIP yanam opening at landing (explicitly omitted)

        // 3) opening owly (one-shot) -> spawn Sovushka only after this completes
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.yanamOpening : null);
        if (yanamOpeningOwly != null)
            yield return StartCoroutine(PlayAnimationAndHold(yanamOpeningOwly));

        // spawn Sovushka now (guaranteed after opening owly)
        if (spawnSovushkaOnLand && sovushkaRespawner != null && !sovushkaSpawned)
        {
            Vector3 spawnPos = sovushkaRespawner.respawnPoint != null ? sovushkaRespawner.respawnPoint.position : transform.position;
            sovushkaRespawner.SpawnNow(spawnPos);
            sovushkaSpawned = true;
            sovushkaTransform = sovushkaRespawner.transform;
            PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.sovushkaSpawn : null);
            Debug.Log("[Yanam] Sovushka spawned at " + spawnPos);
        }

        // Keep Yanam showing the last frame of opening_owly
        PlayExclusive(yanamOpeningOwly);

        // small settle then start monitoring sovushka movement
        yield return new WaitForSeconds(settleTime);
        StartCoroutine(MonitorSovushkaMovement());
        landingCoroutine = null;
    }

    private IEnumerator MonitorSovushkaMovement()
    {
        if (sovushkaTransform == null && sovushkaRespawner != null)
            sovushkaTransform = sovushkaRespawner.transform;

        if (sovushkaTransform == null) yield break;

        while (!movedAwayTriggered)
        {
            float d = Vector3.Distance(transform.position, sovushkaTransform.position);
            if (d > owlyMoveAwayDistance)
            {
                movedAwayTriggered = true;
                // play closing animation and freeze at last frame
                if (yanamClosing != null)
                {
                    yield return StartCoroutine(PlayAnimationAndFreezeLastFrame(yanamClosing));
                }
                else
                {
                    PlayExclusive(yanamOpeningOwly);
                }

                // after closing, start proximity monitoring
                StartCoroutine(ProximityMonitorLoop());
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator ProximityMonitorLoop()
    {
        if (sovushkaTransform == null && sovushkaRespawner != null)
            sovushkaTransform = sovushkaRespawner.transform;

        if (sovushkaTransform == null) yield break;

        proximityOpenState = false; // start closed

        while (true)
        {
            float d = Vector3.Distance(transform.position, sovushkaTransform.position);

            if (!proximityOpenState && d <= proximityRadius)
            {
                // Sovushka entered proximity: play opening once and freeze at last frame
                if (yanamOpening != null)
                {
                    yield return StartCoroutine(PlayAnimationAndFreezeLastFrame(yanamOpening));
                }
                proximityOpenState = true;
            }
            else if (proximityOpenState && d > proximityRadius)
            {
                // Sovushka left proximity: play closing once and freeze at last frame
                if (yanamClosing != null)
                {
                    yield return StartCoroutine(PlayAnimationAndFreezeLastFrame(yanamClosing));
                }
                proximityOpenState = false;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Force Yanam to take off and optionally set a new landing point.
    /// </summary>
    public void ResetLanding(Transform newLandingPoint = null)
    {
        if (newLandingPoint != null) landingPoint = newLandingPoint;
        hasLanded = false;
        sovushkaSpawned = false;
        movedAwayTriggered = false;
        proximityOpenState = false;

        if (landingCoroutine != null) { StopCoroutine(landingCoroutine); landingCoroutine = null; }

        StartCoroutine(TakeoffSequence());
    }

    private IEnumerator TakeoffSequence()
    {
        if (yanamClosing != null)
            yield return StartCoroutine(PlayAnimationAndHold(yanamClosing));

        PlayExclusive(yanamFlying);
        PlayLoopSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.yanamFlyingLoop : null);
        yield return null;
    }

    // -------------------------
    // Sound helpers
    // -------------------------
    private void PlayLoopSound(AudioClip clip)
    {
        if (clip == null || _loopSource == null) return;
        if (_loopSource.clip == clip && _loopSource.isPlaying) return;
        _loopSource.clip = clip;
        _loopSource.Play();
    }

    private void StopLoopSound()
    {
        if (_loopSource != null) _loopSource.Stop();
    }

    private void PlayOneShotSound(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(clip);
    }

    // -------------------------
    // Animation helpers (force single active child)
    // -------------------------
    private void PlayExclusive(GameObject anim)
    {
        if (currentAnim == anim) return;

        // Deactivate every child under Yanam to guarantee only one active
        DeactivateAllChildGameObjects();

        ResetAnimatorSpeed(currentAnim);

        currentAnim = anim;
        if (currentAnim != null) currentAnim.SetActive(true);
    }

    // Deactivate every child GameObject under this Yanam transform
    private void DeactivateAllChildGameObjects()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            if (child.gameObject.activeSelf)
                child.gameObject.SetActive(false);
        }
        currentAnim = null;
    }

    private IEnumerator PlayAnimationAndHold(GameObject anim)
    {
        if (anim == null) yield break;

        PlayExclusive(anim);

        Animator a = anim.GetComponent<Animator>();
        float clipLength = defaultClipLength;

        if (a != null && a.runtimeAnimatorController != null)
        {
            var clips = a.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                clipLength = clips[0].length;
                string clipName = clips[0].name;
                // start from beginning and play normally
                a.Play(clipName, 0, 0f);
                // allow animator to update one frame so the clip actually starts
                a.Update(0f);
                a.speed = 1f;
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));
    }

    // Play animation once and then freeze it on its last frame
    private IEnumerator PlayAnimationAndFreezeLastFrame(GameObject anim)
    {
        if (anim == null) yield break;

        PlayExclusive(anim);

        Animator a = anim.GetComponent<Animator>();
        float clipLength = defaultClipLength;
        string clipName = null;

        if (a != null && a.runtimeAnimatorController != null)
        {
            var clips = a.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                clipLength = clips[0].length;
                clipName = clips[0].name;
                // play from start so the animation actually runs
                a.Play(clipName, 0, 0f);
                a.Update(0f);
                a.speed = 1f;
            }
        }

        // Wait for the clip to finish playing
        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));

        // Jump to the last frame and freeze there
        if (a != null && clipName != null)
        {
            a.Play(clipName, 0, 1f); // normalized time = 1 => last frame
            a.Update(0f);
            a.speed = 0f; // freeze on last frame
        }
    }

    private void ResetAnimatorSpeed(GameObject go)
    {
        if (go == null) return;
        Animator a = go.GetComponent<Animator>();
        if (a != null) a.speed = 1f;
    }

    // Editor visualization
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
        if (landingPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(landingPoint.position, landingRadius);
        }
    }
}
