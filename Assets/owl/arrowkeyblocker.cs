using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to Sovushka (or the GameObject that has the movement script).
/// When any arrow key is held, the listed movement components will be disabled,
/// preventing arrow keys from moving the character. When arrow keys are released,
/// the components are restored to their original enabled state.
/// </summary>
public class ArrowKeyBlocker : MonoBehaviour
{
    [Tooltip("Components that control movement (e.g., OwlMovement). These will be disabled while arrow keys are pressed.")]
    public MonoBehaviour[] movementComponents;

    [Tooltip("If true and movementComponents is empty, try to auto-find a movement script on this GameObject (OwlMovement or any MonoBehaviour named 'Movement').")]
    public bool autoFindMovement = true;

    // internal state
    bool _blocked = false;
    Dictionary<MonoBehaviour, bool> _originalStates = new Dictionary<MonoBehaviour, bool>();

    void Start()
    {
        // If nothing assigned and autoFind enabled, try to find likely movement scripts
        if ((movementComponents == null || movementComponents.Length == 0) && autoFindMovement)
        {
            var list = new List<MonoBehaviour>();
            // common name check
            var candidate = GetComponent("OwlMovement") as MonoBehaviour;
            if (candidate != null) list.Add(candidate);

            // fallback: any MonoBehaviour that looks like movement (heuristic)
            if (list.Count == 0)
            {
                var all = GetComponents<MonoBehaviour>();
                foreach (var mb in all)
                {
                    if (mb == this) continue;
                    string n = mb.GetType().Name.ToLower();
                    if (n.Contains("movement") || n.Contains("move") || n.Contains("controller") || n.Contains("player"))
                        list.Add(mb);
                }
            }

            if (list.Count > 0)
                movementComponents = list.ToArray();
        }

        // record original enabled states
        if (movementComponents != null)
        {
            foreach (var mb in movementComponents)
            {
                if (mb == null) continue;
                if (!_originalStates.ContainsKey(mb))
                    _originalStates[mb] = mb.enabled;
            }
        }
    }

    void Update()
    {
        bool arrowPressed = Input.GetKey(KeyCode.UpArrow) ||
                            Input.GetKey(KeyCode.DownArrow) ||
                            Input.GetKey(KeyCode.LeftArrow) ||
                            Input.GetKey(KeyCode.RightArrow);

        if (arrowPressed && !_blocked)
        {
            // disable movement components
            _blocked = true;
            if (movementComponents != null)
            {
                foreach (var mb in movementComponents)
                {
                    if (mb == null) continue;
                    // store original state if not stored
                    if (!_originalStates.ContainsKey(mb))
                        _originalStates[mb] = mb.enabled;
                    mb.enabled = false;
                }
            }
        }
        else if (!arrowPressed && _blocked)
        {
            // restore original states
            _blocked = false;
            if (movementComponents != null)
            {
                foreach (var mb in movementComponents)
                {
                    if (mb == null) continue;
                    if (_originalStates.TryGetValue(mb, out bool orig))
                        mb.enabled = orig;
                    else
                        mb.enabled = true; // safe default
                }
            }
        }
    }

    void OnDisable()
    {
        // If this blocker is disabled while blocking, restore components
        if (_blocked)
        {
            _blocked = false;
            if (movementComponents != null)
            {
                foreach (var mb in movementComponents)
                {
                    if (mb == null) continue;
                    if (_originalStates.TryGetValue(mb, out bool orig))
                        mb.enabled = orig;
                    else
                        mb.enabled = true;
                }
            }
        }
    }
}
