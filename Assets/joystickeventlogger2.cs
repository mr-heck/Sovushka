// JoystickEventLogger.cs
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class JoystickEventLogger : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public JoystickDirectionManager manager;
    public float radius = 80f;

    RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (manager == null) Debug.LogWarning("JoystickEventLogger: manager not assigned.", this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Vector2 v = ScreenToJoystickVector(eventData);
        manager?.SetInput(v);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 v = ScreenToJoystickVector(eventData);
        manager?.SetInput(v);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        manager?.SetInput(Vector2.zero);
    }

    Vector2 ScreenToJoystickVector(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out localPoint);
        Vector2 v = localPoint / radius;
        return Vector2.ClampMagnitude(v, 1f);
    }
}
