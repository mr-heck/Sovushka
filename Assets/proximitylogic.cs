using UnityEngine;

public class YanamProximitySaver : MonoBehaviour
{
    [Tooltip("Player transform reference (Sovushka)")]
    public Transform player;

    [Tooltip("Radius in world units (1 = one tile) to trigger save")]
    public float yanamSaveRadius = 1f;

    [Tooltip("Reference to the GeneratorController to notify when wood saved")]
    public GeneratorController generatorController;

    [Tooltip("Visiting Yanam refills Sovushka's shot energy back to full.")]
    public bool refillEnergyOnVisit = true;

    private bool hasSavedThisApproach = false;

    void Update()
    {
        if (player == null) return;
        float d = Vector2.Distance(transform.position, player.position);
        if (d <= yanamSaveRadius)
        {
            if (!hasSavedThisApproach)
            {
                // Transfer carried items into stored totals
                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.TransferCarriedToStored();

                // Notify generator (if wood present)
                if (generatorController != null)
                    generatorController.OnWoodSaved();

                // Recharge the shot energy slider: 5 shots per visit
                if (refillEnergyOnVisit && GameManagerSimple.Instance != null)
                    GameManagerSimple.Instance.RefillToFull();

                hasSavedThisApproach = true;
            }
        }
        else
        {
            // Reset so next approach triggers again
            hasSavedThisApproach = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, yanamSaveRadius);
    }
}
