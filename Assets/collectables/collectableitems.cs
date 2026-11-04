using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum CollectableCategory { Wood, Food, Gem }

[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class Collectable : MonoBehaviour
{
    [Header("Identity")]
    public CollectableCategory category = CollectableCategory.Wood;
    public string itemId = "wood_type";
    public string displayName = "Wood";
    public Sprite icon;

    [Header("Prefab")]
    [Tooltip("Assign the prefab asset that should be spawned when this item is released.")]
    public GameObject prefabAsset;

    [Header("Pickup Feedback")]
    public GameObject pickupVFX;
    [Tooltip("Optional per-item override. If left empty, the category sound from GameSoundLibrary (Sound manager) is used instead.")]
    public AudioClip pickupSound;
    public bool respawn = false;
    public float respawnTime = 5f;

    [Header("Events")]
    public UnityEvent OnCollectedEvent;

    Collider2D _collider;
    AudioSource _audioSource;
    bool isAvailable = true;

    void Awake()
    {
        _collider = GetComponent<Collider2D>();
        if (_collider == null) Debug.LogWarning($"[Collectable] No Collider2D found on {name}.");

        // NOTE: GetComponent<T>() ?? AddComponent<T>() is a known Unity/C# trap: when no
        // component is found, GetComponent returns Unity's "fake null" object rather than a
        // true C# null, so ?? never falls through to AddComponent. Use an explicit null check.
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    public void Collect()
    {
        if (!isAvailable) return;
        OnCollected();
    }

    public virtual void OnCollected()
    {
        if (!isAvailable) return;

        if (pickupVFX != null) Instantiate(pickupVFX, transform.position, Quaternion.identity);
        PlayPickupSound();
        OnCollectedEvent?.Invoke();

        isAvailable = false;
        if (_collider != null) _collider.enabled = false;
        SetVisualsActive(false);

        if (respawn)
            StartCoroutine(RespawnCoroutine());
        else
        {
            // Do not Destroy the GameObject so InventoryManager can reuse the instance.
            // The InventoryManager will SetActive(false) after calling OnCollected.
        }
    }

    void PlayPickupSound()
    {
        // Per-item override takes priority; otherwise fall back to the category sound
        // in the central GameSoundLibrary (on the Sound manager object), routed through
        // AudioManager's pooled SFX so it isn't cut off if this object gets deactivated.
        AudioClip clip = pickupSound;
        if (clip == null && GameSoundLibrary.Instance != null)
            clip = GameSoundLibrary.Instance.GetCollectSound(category);

        if (clip == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
        else if (_audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    void SetVisualsActive(bool active)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = active;

        foreach (var c in GetComponentsInChildren<SpriteRenderer>(true)) c.enabled = active;

        var animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = active;
    }

    IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, respawnTime));
        isAvailable = true;
        if (_collider != null) _collider.enabled = true;
        SetVisualsActive(true);
    }

    public void ResetAfterRelease()
    {
        isAvailable = true;
        if (_collider != null) _collider.enabled = true;
        SetVisualsActive(true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isAvailable) return;

        if (InventoryManager.Instance != null && InventoryManager.Instance.playerTransform != null)
        {
            if (other.transform == InventoryManager.Instance.playerTransform) TryAddToInventory();
        }
        else
        {
            if (other.CompareTag("Player")) TryAddToInventory();
        }
    }

    void TryAddToInventory()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning($"[Collectable] InventoryManager missing when collecting {name}");
            return;
        }

        bool added = InventoryManager.Instance.AddCarriedItem(itemId, category, transform.position, prefabAsset, gameObject);
        if (added) Collect();
    }
}

// Compatibility wrapper
public class CollectableItem : Collectable { }
