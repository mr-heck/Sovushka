using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIManager_Compat : MonoBehaviour
{
    public Slider energySlider;
    public Text chargesText;
    public Button fireButton;
    public SovushkaShooter shooter; // assign your player here

    void Start()
    {
        if (energySlider != null)
        {
            // The bar shows normalized energy (0..1): one fifth of the bar per shot.
            energySlider.minValue = 0f;
            energySlider.maxValue = 1f;
            // wholeNumbers would snap every in-between value to 0 or 1 (bar only ever empty/full).
            energySlider.wholeNumbers = false;
            // It is a read-only gauge: the player must not be able to drag/click it to "fill" it.
            energySlider.interactable = false;
            energySlider.transition = Selectable.Transition.None;
        }

        if (GameManagerSimple.Instance != null)
        {
            GameManagerSimple.Instance.OnEnergyChanged += OnEnergyChanged;
            GameManagerSimple.Instance.OnChargesChanged += OnChargesChanged;

            // initialize UI immediately from GameManager
            OnEnergyChanged(GameManagerSimple.Instance.GetNormalizedEnergy());
            OnChargesChanged(GameManagerSimple.Instance.GetAvailableCharges());
        }

        if (fireButton != null && shooter != null)
            fireButton.onClick.AddListener(OnFirePressed);
    }

    void OnDestroy()
    {
        if (GameManagerSimple.Instance != null)
        {
            GameManagerSimple.Instance.OnEnergyChanged -= OnEnergyChanged;
            GameManagerSimple.Instance.OnChargesChanged -= OnChargesChanged;
        }
        if (fireButton != null) fireButton.onClick.RemoveListener(OnFirePressed);
    }

    void OnFirePressed()
    {
        if (shooter != null) shooter.FireOneShot();
    }

    void OnEnergyChanged(float normalized)
    {
        if (energySlider != null) energySlider.value = Mathf.Clamp01(normalized);
    }

    void OnChargesChanged(int charges)
    {
        if (chargesText != null && GameManagerSimple.Instance != null)
            chargesText.text = $"{charges}/{GameManagerSimple.Instance.maxCharges}";
    }
}
