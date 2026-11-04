using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GeneratorController : MonoBehaviour
{
    [Header("Animation children (assign exactly)")]
    public GameObject generatorSpawn;   // child: generator spawn animation (one-shot)
    public GameObject generatorBurning; // child: generator burning (loop)
    public GameObject generatorPackup;  // child: generator packup (one-shot)

    [Header("Timing fallback (seconds)")]
    public float defaultClipLength = 0.8f;

    [Header("References")]
    public SovushkaRespawner sovushkaRespawner; // assign Sovushka respawner
    public Transform sovushkaTransform;         // optional direct transform

    [Header("Legacy Yanam fields (unused)")]
    [Tooltip("No longer used: Yanam's ending sequence (closing -> prepping -> flying) is handled by YanamEnding, triggered via GameManagerYanamTrigger when this generator deactivates. Kept only to avoid losing existing Inspector references.")]
    public Transform yanamTransform;
    public GameObject yanamClosingOwly;
    public GameObject yanamPrepping;
    public GameObject yanamFlying;
    public Transform yanamStartPosition;

    [Header("Generator behavior")]
    public int targetWoodForCompletion = 100;

    // internal
    private Coroutine generatorCoroutine = null;
    private bool generatorActive = false;

    // Dedicated looping source for the burning sound (see Yanam.cs for why this can't
    // go through AudioManager's one-shot pool).
    private AudioSource _loopSource;

    void Awake()
    {
        _loopSource = GetComponent<AudioSource>();
        if (_loopSource == null) _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.spatialBlend = 0f;
        if (GetComponent<SfxVolumeApplier>() == null) gameObject.AddComponent<SfxVolumeApplier>();

        // Ensure all animation children are disabled initially
        DeactivateAllChildren();
    }

    // Called by YanamProximitySaver when wood is saved
    public void OnWoodSaved()
    {
        // Start generator if there is any stored wood and generator not already active
        if (InventoryManager.Instance == null) return;
        if (InventoryManager.Instance.wood > 0 && !generatorActive)
        {
            if (generatorCoroutine != null) StopCoroutine(generatorCoroutine);
            generatorCoroutine = StartCoroutine(GeneratorSequence());
        }
    }

    private IEnumerator GeneratorSequence()
    {
        generatorActive = true;

        // 1) Spawn animation (one-shot)
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.generatorSpawn : null);
        if (generatorSpawn != null)
            yield return StartCoroutine(PlayAnimationAndHold(generatorSpawn));
        else
            yield return null;

        // 2) Start burning animation (loop) until stored wood >= target
        if (generatorBurning != null)
        {
            PlayExclusive(generatorBurning);
        }
        PlayLoopSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.generatorBurningLoop : null);

        // Wait until stored wood reaches targetWoodForCompletion
        while (InventoryManager.Instance != null && InventoryManager.Instance.wood < targetWoodForCompletion)
        {
            // Optionally, you could increment progress here or show UI
            yield return null;
        }

        StopLoopSound();

        // 3) Packup animation (one-shot)
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.generatorPackup : null);
        if (generatorPackup != null)
            yield return StartCoroutine(PlayAnimationAndHold(generatorPackup));
        else
            yield return null;

        // 4) Make Sovushka disappear (disable respawner's spawned object)
        if (sovushkaRespawner != null)
        {
            // If respawner has a spawned instance, disable it
            if (sovushkaRespawner.transform != null)
            {
                sovushkaRespawner.gameObject.SetActive(false);
            }
        }
        else if (sovushkaTransform != null)
        {
            sovushkaTransform.gameObject.SetActive(false);
        }

        // 5) Make generator disappear (disable whole GameObject).
        // GameManagerYanamTrigger (on the always-active Game manager object) polls this
        // GameObject's active state and calls YanamEnding.TriggerEnding() once it goes
        // inactive, which plays Yanam's closing -> prepping -> flying sequence and moves
        // it away. Deactivating here MUST be the last thing this coroutine does: once this
        // GameObject is inactive, every coroutine running on its own components (including
        // this one) is stopped immediately, so nothing after this line would ever run.
        generatorActive = false;
        generatorCoroutine = null;
        Debug.Log("[GeneratorController] Generator finished, game end sequence triggered.");
        gameObject.SetActive(false);
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
    // Animation helpers (force single active child)
    // -------------------------
    private GameObject currentAnim = null;

    private void PlayExclusive(GameObject anim)
    {
        if (currentAnim == anim) return;
        DeactivateAllChildren();
        ResetAnimatorSpeed(currentAnim);
        currentAnim = anim;
        if (currentAnim != null) currentAnim.SetActive(true);
    }

    private void DeactivateAllChildren()
    {
        if (generatorSpawn) generatorSpawn.SetActive(false);
        if (generatorBurning) generatorBurning.SetActive(false);
        if (generatorPackup) generatorPackup.SetActive(false);
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
                a.Play(clipName, 0, 0f);
                a.Update(0f);
                a.speed = 1f;
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));
    }

    private void ResetAnimatorSpeed(GameObject go)
    {
        if (go == null) return;
        Animator a = go.GetComponent<Animator>();
        if (a != null) a.speed = 1f;
    }
}
