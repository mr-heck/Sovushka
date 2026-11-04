using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerInteraction : MonoBehaviour
{
    [Tooltip("How close the player must be to collect (1 = one tile)")]
    public float collectRadius = 1f;

    [Tooltip("Layer mask for collectable objects")]
    public LayerMask collectableLayer;

    [Tooltip("Maximum distance to consider when picking nearest")]
    public float maxSearchDistance = 1.5f;

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.E))
            OnCollectButtonPressed();

        if (Input.GetKeyDown(KeyCode.Q))
            OnReleaseButtonPressed();
#endif
    }

    // Called from UI button OnClick or keyboard fallback
    public void OnCollectButtonPressed()
    {
        TryCollectNearest();
    }

    // Called from UI button OnClick or keyboard fallback
    public void OnReleaseButtonPressed()
    {
        Debug.Log("[PlayerInteraction] Release requested");
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[PlayerInteraction] InventoryManager missing");
            return;
        }

        // TryRelease should remove one carried item and spawn the drop prefab near the player.
        // This assumes InventoryManager.TryRelease(Vector3 playerPosition) exists.
        bool released = InventoryManager.Instance.TryRelease(transform.position);
        Debug.Log($"[PlayerInteraction] TryRelease returned {released}");
    }

    // Attempts to find the nearest collectable and add it to inventory
    public bool TryCollectNearest()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[PlayerInteraction] InventoryManager missing");
            return false;
        }

        if (!InventoryManager.Instance.CanCarryMore())
        {
            Debug.Log("[PlayerInteraction] Inventory full");
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, collectRadius, collectableLayer);
        Debug.Log($"[PlayerInteraction] Overlap hits: {(hits == null ? 0 : hits.Length)}");
        if (hits == null || hits.Length == 0) return false;

        // Find nearest collectable component (Collectable)
        Collectable nearest = null;
        float bestDist = float.MaxValue;

        foreach (var c in hits)
        {
            if (c == null) continue;
            Collectable item = c.GetComponent<Collectable>();
            if (item == null) continue;

            float d = Vector2.Distance(transform.position, c.transform.position);
            if (d < bestDist && d <= maxSearchDistance)
            {
                bestDist = d;
                nearest = item;
            }
        }

        if (nearest == null)
        {
            Debug.Log("[PlayerInteraction] No collectable within maxSearchDistance");
            return false;
        }

        // Add to carried inventory (encoded string)
        bool added = InventoryManager.Instance.AddCarriedItem(
            nearest.itemId,
            nearest.category,
            nearest.transform.position,
            nearest.prefabAsset,
            nearest.gameObject
        );
        Debug.Log($"[PlayerInteraction] AddCarriedItemEncoded returned {added} for {nearest.itemId}");
        if (added)
        {
            // Ensure the item vanishes immediately and trigger any pickup logic
            nearest.OnCollected();
            Debug.Log($"[PlayerInteraction] Collected: {nearest.itemId} ({nearest.category})");
            return true;
        }
        else
        {
            Debug.Log("[PlayerInteraction] Could not add item");
            return false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collectRadius);
    }
}
