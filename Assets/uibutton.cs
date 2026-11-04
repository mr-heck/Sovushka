using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[RequireComponent(typeof(AudioSource))]
public class CollectReleaseUI : MonoBehaviour
{
    [Header("References")]
    public Button collectButton;
    public Button releaseButton;
    public InventoryManager inventoryManager; // optional: will fallback to InventoryManager.Instance

    [Header("Feedback")]
    public float buttonCooldown = 0.35f;
    public Color pressedColor = new Color(0.8f, 0.8f, 0.8f);
    public AudioClip clickSound;
    public bool useHaptics = false;

    [Header("Optional Events")]
    public UnityEvent OnCollectSuccess;
    public UnityEvent OnCollectFail;
    public UnityEvent OnReleaseSuccess;
    public UnityEvent OnReleaseFail;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (clickSound != null) audioSource.clip = clickSound;

        // Auto-assign InventoryManager if not set in inspector
        if (inventoryManager == null && InventoryManager.Instance != null)
            inventoryManager = InventoryManager.Instance;

        // Warn if buttons or manager are missing
        if (collectButton == null) Debug.LogWarning("[CollectReleaseUI] Collect button not assigned.");
        if (releaseButton == null) Debug.LogWarning("[CollectReleaseUI] Release button not assigned.");
        if (inventoryManager == null) Debug.LogWarning("[CollectReleaseUI] InventoryManager not assigned.");

        // Wire up button listeners safely
        if (collectButton != null)
        {
            collectButton.onClick.RemoveListener(OnCollectButton); // avoid duplicate
            collectButton.onClick.AddListener(OnCollectButton);
        }

        if (releaseButton != null)
        {
            releaseButton.onClick.RemoveListener(OnReleaseButton);
            releaseButton.onClick.AddListener(OnReleaseButton);
        }
    }

    void Start()
    {
        // final fallback for inventory manager
        if (inventoryManager == null && InventoryManager.Instance != null)
            inventoryManager = InventoryManager.Instance;
    }

    void OnDestroy()
    {
        // clean up listeners
        if (collectButton != null) collectButton.onClick.RemoveListener(OnCollectButton);
        if (releaseButton != null) releaseButton.onClick.RemoveListener(OnReleaseButton);
    }

    // Public methods to wire to Button OnClick (also used by Awake listeners)
    public void OnCollectButton()
    {
        StartCoroutine(DoButtonAction(collectButton, () =>
        {
            if (inventoryManager == null)
            {
                Debug.LogWarning("[CollectReleaseUI] No InventoryManager assigned.");
                OnCollectFail?.Invoke();
                return;
            }

            bool ok = inventoryManager.TryCollect();
            if (ok) OnCollectSuccess?.Invoke(); else OnCollectFail?.Invoke();
        }));
    }

    public void OnReleaseButton()
    {
        StartCoroutine(DoButtonAction(releaseButton, () =>
        {
            if (inventoryManager == null)
            {
                Debug.LogWarning("[CollectReleaseUI] No InventoryManager assigned.");
                OnReleaseFail?.Invoke();
                return;
            }

            bool ok = inventoryManager.TryRelease();
            if (ok) OnReleaseSuccess?.Invoke(); else OnReleaseFail?.Invoke();
        }));
    }

    IEnumerator DoButtonAction(Button btn, System.Action action)
    {
        if (btn == null || !btn.interactable) yield break;

        Image img = btn.GetComponent<Image>();
        Color original = img != null ? img.color : Color.white;
        if (img != null) img.color = pressedColor;

        btn.interactable = false;

        if (clickSound != null && audioSource != null && AudioManager.SoundEnabled)
            audioSource.PlayOneShot(clickSound, AudioManager.SfxScale);

        if (useHaptics)
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        action?.Invoke();

        yield return new WaitForSeconds(buttonCooldown);

        if (img != null) img.color = original;
        btn.interactable = true;
    }
}
