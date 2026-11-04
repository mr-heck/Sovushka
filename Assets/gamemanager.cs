using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(-100)]
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [System.Serializable]
    public class CarriedItem
    {
        public string itemId;
        public CollectableCategory category;
        public Vector3 originPosition;
        public GameObject prefab;       // optional prefab asset
        public GameObject instanceRef;  // scene instance reference (preferred)

        public CarriedItem(string id, CollectableCategory cat, Vector3 origin, GameObject prefabAsset, GameObject instance)
        {
            itemId = id;
            category = cat;
            originPosition = origin;
            prefab = prefabAsset;
            instanceRef = instance;
        }
    }

    [Header("Carry settings")]
    public int carryCapacity = 3;
    public List<CarriedItem> carriedItems = new List<CarriedItem>();

    [Header("Stored totals")]
    public int wood = 0;
    public int food = 0;
    public int jewels = 0;

    [Header("Collection / Release settings")]
    public Transform playerTransform;
    public float collectRadius = 1.2f;
    public LayerMask collectableLayer;
    public GameObject dropPrefab; // fallback prefab asset

    [Header("Drop spawn options")]
    public bool useOriginalPositionOnRelease = false;
    public float releaseScatterRadius = 0.15f;

    [Header("Events")]
    public UnityEvent<int> OnCarriedItemsChanged;
    public UnityEvent OnStoredTotalsChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // NOTE: intentionally NOT DontDestroyOnLoad - this object holds scene references (player, collectables).
    }

    public bool CanCarryMore() => carriedItems.Count < carryCapacity;

    // Backwards-compatible wrapper required by older code
    public bool AddCarriedItemEncoded(string itemId, CollectableCategory category)
    {
        return AddCarriedItem(itemId, category);
    }

    public bool AddCarriedItem(string itemId, CollectableCategory category, Vector3 origin, GameObject prefabAsset, GameObject instance)
    {
        if (!CanCarryMore() || string.IsNullOrEmpty(itemId)) return false;
        carriedItems.Add(new CarriedItem(itemId, category, origin, prefabAsset, instance));
        OnCarriedItemsChanged?.Invoke(carriedItems.Count);
        Debug.Log($"[InventoryManager] Added carried item {itemId} ({category}) at {origin}. carriedItems={carriedItems.Count}");
        return true;
    }

    // Simple overload for older calls
    public bool AddCarriedItem(string itemId, CollectableCategory category)
        => AddCarriedItem(itemId, category, Vector3.zero, null, null);

    public void TransferCarriedToStored()
    {
        if (carriedItems == null || carriedItems.Count == 0) return;
        foreach (var ci in carriedItems)
        {
            if (ci == null) continue;
            switch (ci.category)
            {
                case CollectableCategory.Wood: wood += 1; break;
                case CollectableCategory.Food: food += 1; break;
                case CollectableCategory.Gem: jewels += 1; break;
            }
        }
        carriedItems.Clear();
        OnCarriedItemsChanged?.Invoke(carriedItems.Count);
        OnStoredTotalsChanged?.Invoke();
        Debug.Log($"[InventoryManager] Transferred to stored totals. wood={wood}, food={food}, jewels={jewels}");
    }

    public void SaveStoredTotals()
    {
        PlayerPrefs.SetInt("stored_wood", wood);
        PlayerPrefs.SetInt("stored_food", food);
        PlayerPrefs.SetInt("stored_jewels", jewels);
        PlayerPrefs.Save();
        OnStoredTotalsChanged?.Invoke();
    }

    public void LoadStoredTotals()
    {
        wood = PlayerPrefs.GetInt("stored_wood", 0);
        food = PlayerPrefs.GetInt("stored_food", 0);
        jewels = PlayerPrefs.GetInt("stored_jewels", 0);
        OnStoredTotalsChanged?.Invoke();
    }

    public bool TryCollect()
    {
        if (!CanCarryMore()) { Debug.Log("[InventoryManager] Cannot collect: capacity full."); return false; }
        if (playerTransform == null) { Debug.LogWarning("[InventoryManager] playerTransform not assigned."); return false; }

        Collider2D[] hits = Physics2D.OverlapCircleAll(playerTransform.position, collectRadius, collectableLayer);
        if (hits == null || hits.Length == 0) { Debug.Log("[InventoryManager] No collectable in range."); return false; }

        Collider2D nearest = null;
        float bestDist = float.MaxValue;
        foreach (var c in hits)
        {
            if (c == null) continue;
            float d = Vector2.SqrMagnitude((Vector2)c.transform.position - (Vector2)playerTransform.position);
            if (d < bestDist) { bestDist = d; nearest = c; }
        }
        if (nearest == null) return false;

        // NOTE: GetComponent<T>() ?? GetComponentInChildren<T>() is a known Unity/C# trap:
        // GetComponent returns Unity's "fake null" object (not a true C# null) when nothing is
        // found, so ?? never falls through to the fallback. Use explicit null checks instead.
        Collectable collectable = nearest.GetComponent<Collectable>();
        if (collectable == null) collectable = nearest.GetComponentInChildren<Collectable>();
        if (collectable == null) { Debug.LogWarning("[InventoryManager] Nearest object has no Collectable component."); return false; }

        string id = collectable.itemId;
        CollectableCategory category = collectable.category;
        Vector3 origin = collectable.transform.position;
        GameObject prefabAsset = collectable.prefabAsset;
        GameObject instance = collectable.gameObject;

        bool added = AddCarriedItem(id, category, origin, prefabAsset, instance);
        if (added)
        {
            collectable.OnCollected(); // play VFX/sound and set internal flags
            collectable.gameObject.SetActive(false); // hide but keep reference
            Debug.Log($"[InventoryManager] Collected {id} ({category}). carriedItems={carriedItems.Count}");
            return true;
        }
        else
        {
            Debug.Log("[InventoryManager] Failed to add carried item.");
            return false;
        }
    }

    public bool TryRelease() => TryRelease(playerTransform != null ? playerTransform.position : Vector3.zero);

    public bool TryRelease(Vector3 spawnPosition)
    {
        if (carriedItems == null || carriedItems.Count == 0) { Debug.Log("[InventoryManager] Nothing to release."); return false; }

        int idx = carriedItems.Count - 1;
        CarriedItem item = carriedItems[idx];
        carriedItems.RemoveAt(idx);
        OnCarriedItemsChanged?.Invoke(carriedItems.Count);

        Vector3 targetPos = spawnPosition;
        if (useOriginalPositionOnRelease && item.originPosition != Vector3.zero) targetPos = item.originPosition;
        targetPos += (Vector3)(Random.insideUnitCircle * releaseScatterRadius);

        if (AudioManager.Instance != null && GameSoundLibrary.Instance != null && GameSoundLibrary.Instance.releaseItem != null)
            AudioManager.Instance.PlaySFX(GameSoundLibrary.Instance.releaseItem);

        if (item.instanceRef != null)
        {
            GameObject go = item.instanceRef;
            if (go != null)
            {
                go.transform.position = targetPos;
                go.SetActive(true);

                SpriteRenderer[] renderers =
                    go.GetComponentsInChildren<SpriteRenderer>(true);

                foreach (SpriteRenderer sr in renderers)
                {
                    sr.enabled = true;
                }
                int layerIndex = LayerMaskToLayer(collectableLayer);
                if (layerIndex >= 0) SetLayerRecursively(go, layerIndex);
                var c = go.GetComponent<Collectable>();

                if (c == null)
                    c = go.GetComponentInChildren<Collectable>(true);

                if (c != null)
                    c.ResetAfterRelease();
                Debug.Log($"[InventoryManager] Re-enabled original instance {item.itemId} at {targetPos}");
                OnStoredTotalsChanged?.Invoke();
                return true;
            }
        }

        GameObject prefabToSpawn = item.prefab != null ? item.prefab : dropPrefab;
        if (prefabToSpawn == null)
        {
            carriedItems.Add(item);
            OnCarriedItemsChanged?.Invoke(carriedItems.Count);
            Debug.LogWarning($"[InventoryManager] Cannot release {item.itemId}: no prefab or instance available.");
            return false;
        }

        GameObject spawned = Instantiate(prefabToSpawn, targetPos, Quaternion.identity);
        int li = LayerMaskToLayer(collectableLayer);
        if (li >= 0) SetLayerRecursively(spawned, li);
        Collectable spawnedCollectable = spawned.GetComponent<Collectable>();
        if (spawnedCollectable == null) spawnedCollectable = spawned.GetComponentInChildren<Collectable>();
        if (spawnedCollectable != null)
        {
            spawnedCollectable.itemId = item.itemId;
            spawnedCollectable.category = item.category;
        }
        Debug.Log($"[InventoryManager] Instantiated fallback prefab for {item.itemId} at {targetPos}");
        OnStoredTotalsChanged?.Invoke();
        return true;
    }

    int LayerMaskToLayer(LayerMask mask)
    {
        int m = mask.value;
        for (int i = 0; i < 32; i++) if ((m & (1 << i)) != 0) return i;
        return -1;
    }

    void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursively(t.gameObject, layer);
    }
}
