using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class YanamEnding : MonoBehaviour
{
    [Header("Animation children (assign exactly like in Yanam)")]
    public GameObject yanamClosing;        // "yanam closing" (play first; hide protagonist when starts)
    public GameObject yanamPrepping;       // "yanam prepping" (play second)
    public GameObject yanamFlying;         // "yanam flying"   (play third; move upward while playing)

    [Header("References")]
    [Tooltip("Protagonist GameObject (owly) to hide when closing starts)")]
    public GameObject protagonist;

    [Tooltip("End game UI panel to enable when sequence finishes")]
    public GameObject endGamePanel;

    [Header("Movement while flying")]
    [Tooltip("World units to move Yanam upward during flying")]
    public float flyMoveDistance = 6f;

    [Tooltip("If > 0, use this as flying duration; otherwise use animation clip length or defaultClipLength")]
    public float flyingDurationOverride = 0f;

    [Header("Animation timing fallback (seconds)")]
    public float defaultClipLength = 0.8f;

    [Header("Options")]
    [Tooltip("If true, automatically find Animator on child animations and use clip lengths when available")]
    public bool autoDetectClipLengths = true;

    [Tooltip("Optional event invoked when ending sequence completes")]
    public UnityEvent OnEndingComplete;

    // internal
    private GameObject currentAnim = null;
    private bool _endingStarted = false;
    private Rigidbody2D rb;

    // Dedicated looping source for the "flying away" sound (see Yanam.cs for why this
    // can't go through AudioManager's one-shot pool).
    private AudioSource _loopSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.linearVelocity = Vector2.zero; // updated: use linearVelocity
        }

        _loopSource = GetComponent<AudioSource>();
        if (_loopSource == null) _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.spatialBlend = 0f;
        if (GetComponent<SfxVolumeApplier>() == null) gameObject.AddComponent<SfxVolumeApplier>();

        // Ensure nothing plays before we start
        DeactivateAllChildGameObjects();

        if (endGamePanel != null)
            endGamePanel.SetActive(false);
    }

    /// <summary>
    /// External trigger: call this when the generator becomes inactive / requirement fulfilled.
    /// </summary>
    public void TriggerEnding()
    {
        if (_endingStarted) return;
        _endingStarted = true;
        StartCoroutine(EndingSequenceCoroutine());
    }

    private IEnumerator EndingSequenceCoroutine()
    {
        // 1) Play closing animation; hide protagonist as soon as it starts; freeze last frame after it finishes
        if (yanamClosing != null)
        {
            yield return StartCoroutine(PlayAnimationAndFreezeLastFrame(yanamClosing, hideProtagonistOnStart: true));
        }
        else
        {
            if (protagonist != null) protagonist.SetActive(false);
        }

        // 2) Play prepping animation and wait for it to finish (hold last frame)
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.yanamPreparingToFly : null);
        if (yanamPrepping != null)
        {
            yield return StartCoroutine(PlayAnimationAndHold(yanamPrepping));
        }

        // 3) Play flying animation and move Yanam upward while it plays
        float animFlyLength = GetClipLengthFromGameObject(yanamFlying);
        float moveDuration = flyingDurationOverride > 0f ? flyingDurationOverride : (animFlyLength > 0f ? animFlyLength : defaultClipLength);

        if (yanamFlying != null)
        {
            // Start flying animation (play normally)
            PlayExclusive(yanamFlying);
            Animator a = yanamFlying.GetComponent<Animator>();
            if (a != null && a.runtimeAnimatorController != null)
            {
                var clips = a.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0)
                {
                    a.Play(clips[0].name, 0, 0f);
                    a.Update(0f);
                    a.speed = 1f;
                }
            }
        }

        PlayLoopSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.yanamFlyingAwayLoop : null);

        // Move upward smoothly over moveDuration
        yield return StartCoroutine(MoveUpWhile(moveDuration));

        StopLoopSound();

        // Ensure final position and wait a tiny bit to let animation finish if needed
        yield return new WaitForSeconds(0.05f);

        // 4) Show end game panel
        if (endGamePanel != null) endGamePanel.SetActive(true);

        OnEndingComplete?.Invoke();
    }

    // Move Yanam upward by flyMoveDistance over duration (uses Rigidbody2D if available)
    private IEnumerator MoveUpWhile(float duration)
    {
        if (duration <= 0f)
        {
            ApplyFinalPosition(transform.position + Vector3.up * flyMoveDistance);
            yield break;
        }

        Vector3 start = transform.position;
        Vector3 target = start + Vector3.up * flyMoveDistance;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            Vector3 pos = Vector3.Lerp(start, target, alpha);
            ApplyPosition(pos);
            yield return null;
        }

        ApplyFinalPosition(target);
    }

    private void ApplyPosition(Vector3 pos)
    {
        if (rb != null)
        {
            rb.MovePosition(pos);
        }
        else
        {
            transform.position = pos;
        }
    }

    private void ApplyFinalPosition(Vector3 pos)
    {
        if (rb != null)
        {
            rb.MovePosition(pos);
            rb.linearVelocity = Vector2.zero; // updated: clear linear velocity using new API
        }
        else
        {
            transform.position = pos;
        }
    }

    // -------------------------
    // Sound helpers
    // -------------------------
    private void PlayLoopSound(AudioClip clip)
    {
        if (clip == null || _loopSource == null) return;
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
    // Animation helpers (same style as your Yanam script)
    // -------------------------
    private void PlayExclusive(GameObject anim)
    {
        if (currentAnim == anim) return;

        DeactivateAllChildGameObjects();
        ResetAnimatorSpeed(currentAnim);

        currentAnim = anim;
        if (currentAnim != null) currentAnim.SetActive(true);
    }

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

        if (autoDetectClipLengths && a != null && a.runtimeAnimatorController != null)
        {
            var clips = a.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                clipLength = clips[0].length;
                string clipName = clips[0].name;
                a.Play(clipName, 0, 0f);
                a.Update(0f);
                a.speed = 1f;
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));
    }

    // Play animation once and freeze on last frame. Optionally hide protagonist when the animation starts.
    private IEnumerator PlayAnimationAndFreezeLastFrame(GameObject anim, bool hideProtagonistOnStart = false)
    {
        if (anim == null)
        {
            if (hideProtagonistOnStart && protagonist != null) protagonist.SetActive(false);
            yield break;
        }

        PlayExclusive(anim);

        if (hideProtagonistOnStart && protagonist != null)
            protagonist.SetActive(false);

        Animator a = anim.GetComponent<Animator>();
        float clipLength = defaultClipLength;
        string clipName = null;

        if (autoDetectClipLengths && a != null && a.runtimeAnimatorController != null)
        {
            var clips = a.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                clipLength = clips[0].length;
                clipName = clips[0].name;
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

    // Utility: get clip length from a child GameObject's Animator (first clip) or 0
    private float GetClipLengthFromGameObject(GameObject go)
    {
        if (!autoDetectClipLengths || go == null) return 0f;
        Animator a = go.GetComponent<Animator>();
        if (a == null || a.runtimeAnimatorController == null) return 0f;
        var clips = a.runtimeAnimatorController.animationClips;
        if (clips != null && clips.Length > 0 && clips[0] != null) return clips[0].length;
        return 0f;
    }
}
