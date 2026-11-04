using System;
using System.Reflection;
using UnityEngine;

[AddComponentMenu("Input/Shoot Joystick (Adapter)")]
public class ShootJoystick : MonoBehaviour
{
    [Tooltip("Primary source joystick component (e.g., SimpleJoystick). If empty, adapter will try to read the component specified in Source Component Name.")]
    public MonoBehaviour sourceJoystick;

    [Tooltip("If sourceJoystick is null, adapter will try to find a component with this class name on the same GameObject (e.g., FloatingJoystick). Leave empty to skip.")]
    public string sourceComponentName;

    // reflection caches
    PropertyInfo _hProp, _vProp;
    FieldInfo _hField, _vField;

    void Awake()
    {
        if (sourceJoystick == null && !string.IsNullOrEmpty(sourceComponentName))
        {
            var comp = GetComponent(sourceComponentName);
            if (comp != null) sourceJoystick = comp as MonoBehaviour;
        }

        CacheMembers();
    }

    void CacheMembers()
    {
        if (sourceJoystick == null) return;
        Type t = sourceJoystick.GetType();
        _hProp = t.GetProperty("Horizontal") ?? t.GetProperty("horizontal");
        _vProp = t.GetProperty("Vertical") ?? t.GetProperty("vertical");
        if (_hProp == null) _hField = t.GetField("Horizontal") ?? t.GetField("horizontal");
        if (_vProp == null) _vField = t.GetField("Vertical") ?? t.GetField("vertical");
    }

    // Exposed properties (SovushkaShooter will read these via reflection)
    public float Horizontal
    {
        get
        {
            if (sourceJoystick == null) return 0f;

            // Generic reflection lookup (works for any joystick component exposing Horizontal/Vertical)
            try
            {
                if (_hProp != null)
                {
                    object val = _hProp.GetValue(sourceJoystick, null);
                    if (val is float f) return f;
                }
                else if (_hField != null)
                {
                    object val = _hField.GetValue(sourceJoystick);
                    if (val is float f) return f;
                }
            }
            catch (Exception)
            {
                // ignore and return 0
            }
            return 0f;
        }
    }

    public float Vertical
    {
        get
        {
            if (sourceJoystick == null) return 0f;

            try
            {
                if (_vProp != null)
                {
                    object val = _vProp.GetValue(sourceJoystick, null);
                    if (val is float f) return f;
                }
                else if (_vField != null)
                {
                    object val = _vField.GetValue(sourceJoystick);
                    if (val is float f) return f;
                }
            }
            catch (Exception)
            {
                // ignore and return 0
            }
            return 0f;
        }
    }

    // Convenience: expose Vector2
    public Vector2 Direction => new Vector2(Horizontal, Vertical);
}
