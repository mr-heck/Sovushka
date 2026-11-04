using System.Collections;
using UnityEngine;

public class FoodDecay : MonoBehaviour
{
    [Tooltip("Seconds between each decay tick")]
    public float decayInterval = 15f;

    [Tooltip("Amount of food to reduce each tick")]
    public int decayAmount = 2;

    [Tooltip("Sovushka respawner reference")]
    public SovushkaRespawner sovushkaRespawner;

    [Tooltip("Player transform (Sovushka) to respawn if needed")]
    public Transform sovushkaTransform;

    private Coroutine decayCoroutine;

    void OnEnable()
    {
        decayCoroutine = StartCoroutine(DecayLoop());
    }

    void OnDisable()
    {
        if (decayCoroutine != null) StopCoroutine(decayCoroutine);
    }

    private IEnumerator DecayLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(decayInterval);

            if (InventoryManager.Instance == null) continue;

            InventoryManager.Instance.food = Mathf.Max(0, InventoryManager.Instance.food - decayAmount);
            Debug.Log($"[FoodDecay] Food reduced by {decayAmount}. Now: {InventoryManager.Instance.food}");

            if (InventoryManager.Instance.food <= 1)
            {
                // Respawn Sovushka
                if (sovushkaRespawner != null)
                {
                    Vector3 spawnPos = sovushkaRespawner.respawnPoint != null ? sovushkaRespawner.respawnPoint.position : Vector3.zero;
                    sovushkaRespawner.SpawnNow(spawnPos);
                    Debug.Log("[FoodDecay] Food low — Sovushka respawned.");
                }
                else if (sovushkaTransform != null)
                {
                    sovushkaTransform.gameObject.SetActive(true);
                    Debug.Log("[FoodDecay] Food low — Sovushka transform re-enabled.");
                }
            }
        }
    }
}
