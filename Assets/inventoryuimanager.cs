using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro; // remove if not using TextMeshPro

public class InventoryUIManager : MonoBehaviour
{
    [Header("References")]
    public InventoryManager inventoryManager; // optional: will fallback to InventoryManager.Instance
    public Slider woodSlider;
    public Slider gemSlider;
    public TextMeshProUGUI woodLabel; // optional, replace with Text if not using TMP
    public TextMeshProUGUI gemLabel;  // optional, replace with Text if not using TMP

    [Header("Slider Settings")]
    public int woodMax = 50;
    public int gemMax = 50;
    public bool wholeNumbers = true;

    void Awake()
    {
        if (inventoryManager == null && InventoryManager.Instance != null)
            inventoryManager = InventoryManager.Instance;

        if (woodSlider == null || gemSlider == null)
            Debug.LogWarning("[InventoryUIManager] Assign sliders in inspector.");

        // configure sliders
        if (woodSlider != null)
        {
            woodSlider.maxValue = woodMax;
            woodSlider.wholeNumbers = wholeNumbers;
            woodSlider.minValue = 0;
        }
        if (gemSlider != null)
        {
            gemSlider.maxValue = gemMax;
            gemSlider.wholeNumbers = wholeNumbers;
            gemSlider.minValue = 0;
        }
    }

    void OnEnable()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnStoredTotalsChanged?.AddListener(UpdateAll);
            inventoryManager.OnCarriedItemsChanged?.AddListener(OnCarriedItemsChanged);
        }
        UpdateAll();
    }

    void OnDisable()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnStoredTotalsChanged?.RemoveListener(UpdateAll);
            inventoryManager.OnCarriedItemsChanged?.RemoveListener(OnCarriedItemsChanged);
        }
    }

    void UpdateAll()
    {
        if (inventoryManager == null) return;
        UpdateWood(inventoryManager.wood);
        UpdateGems(inventoryManager.jewels);
    }

    void OnCarriedItemsChanged(int count)
    {
        // optional: refresh stored totals in case TransferCarriedToStored was called
        UpdateAll();
    }

    public void UpdateWood(int value)
    {
        if (woodSlider != null) woodSlider.value = Mathf.Clamp(value, 0, (int)woodSlider.maxValue);
        if (woodLabel != null) woodLabel.text = $"{value}";
    }

    public void UpdateGems(int value)
    {
        if (gemSlider != null) gemSlider.value = Mathf.Clamp(value, 0, (int)gemSlider.maxValue);
        if (gemLabel != null) gemLabel.text = $"{value}";
    }

    // Optional runtime helpers
    public void SetWoodMax(int max)
    {
        woodMax = max;
        if (woodSlider != null) woodSlider.maxValue = max;
        UpdateAll();
    }

    public void SetGemMax(int max)
    {
        gemMax = max;
        if (gemSlider != null) gemSlider.maxValue = max;
        UpdateAll();
    }
}
