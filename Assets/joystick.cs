using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class VirtualJoystickDebug : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References (assign in Inspector)")]
    public RectTransform background;
    public RectTransform handle;

    [Header("Settings")]
    public float handleRange = 80f;
    [Range(0f, 1f)]
    public float deadZone = 0.15f;
    public bool smoothReturn = true;
    public float returnSpeed = 8f;

    [Header("Events (optional)")]
    public UnityEvent<Vector2> OnMove;
    public UnityEvent OnMoveEnd;

    [Header("Debug")]
    public bool debug = true;

    RectTransform rt;
    bool isDragging = false;
    Vector2 currentNormalized = Vector2.zero;
    const float EPS = 0.0001f;

    void Awake()
    {
        rt = GetComponent<RectTransform>();

        if (background == null) Debug.LogError("[VirtualJoystickDebug] Background not assigned on " + name);
        if (handle == null) Debug.LogError("[VirtualJoystickDebug] Handle not assigned on " + name);

        if (background != null && handle != null)
            handle.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        if (!isDragging && handle != null && handle.anchoredPosition.sqrMagnitude > EPS)
        {
            if (smoothReturn)
            {
                handle.anchoredPosition = Vector2.Lerp(handle.anchoredPosition, Vector2.zero, Mathf.Clamp01(returnSpeed * Time.deltaTime));
                Vector2 norm = handle.anchoredPosition / handleRange;
                ApplyNormalized(norm);
                if (handle.anchoredPosition.magnitude < 0.5f)
                {
                    handle.anchoredPosition = Vector2.zero;
                    ApplyNormalized(Vector2.zero);
                    OnMoveEnd?.Invoke();
                }
            }
            else
            {
                handle.anchoredPosition = Vector2.zero;
                ApplyNormalized(Vector2.zero);
                OnMoveEnd?.Invoke();
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (background == null || handle == null) return;
        isDragging = true;
        if (debug) Debug.Log($"[Joystick] OnPointerDown pos={eventData.position} camera={(eventData.pressEventCamera==null?"null":eventData.pressEventCamera.name)}");
        UpdateHandlePosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null) return;
        if (debug) Debug.Log($"[Joystick] OnDrag pos={eventData.position}");
        UpdateHandlePosition(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (background == null || handle == null) return;
        isDragging = false;
        if (debug) Debug.Log($"[Joystick] OnPointerUp pos={eventData.position}");
        if (!smoothReturn)
        {
            handle.anchoredPosition = Vector2.zero;
            ApplyNormalized(Vector2.zero);
            OnMoveEnd?.Invoke();
        }
    }

    void UpdateHandlePosition(PointerEventData eventData)
    {
        Vector2 localPoint;
        Camera cam = eventData.pressEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, cam, out localPoint))
        {
            if (debug) Debug.LogWarning("[Joystick] ScreenPointToLocalPointInRectangle returned false. Check background RectTransform and camera.");
            return;
        }

        Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);
        handle.anchoredPosition = clamped;

        Vector2 normalized = clamped / handleRange;
        if (normalized.magnitude < deadZone) normalized = Vector2.zero;

        if (debug) Debug.Log($"[Joystick] localPoint={localPoint} clamped={clamped} normalized={normalized}");
        ApplyNormalized(normalized);
    }

    void ApplyNormalized(Vector2 normalized)
    {
        currentNormalized = normalized;
        OnMove?.Invoke(currentNormalized);

        if (InputBridge.Instance != null)
            InputBridge.Instance.SetMove(currentNormalized);
    }

    public Vector2 GetValue() => currentNormalized;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (background != null)
        {
            Vector3 worldPos = background.TransformPoint(background.rect.center);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(worldPos, handleRange * (background.lossyScale.x));
        }
    }
#endif
}
