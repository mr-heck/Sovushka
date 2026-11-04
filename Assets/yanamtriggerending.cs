using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to Game manager. Watches a generator GameObject and triggers YanamEnding safely
/// when the generator requirement is fulfilled or the generator becomes inactive.
/// Does not modify the generator script; uses event subscription if available, otherwise polls.
/// </summary>
public class GameManagerYanamTrigger : MonoBehaviour
{
    [Tooltip("Assign the existing generator GameObject to watch.")]
    public GameObject generatorObject;

    [Tooltip("Name of a UnityEvent field on the generator to subscribe to (optional).")]
    public string generatorEventName = "OnRequirementFulfilled";

    [Tooltip("If true, trigger ending when the generator GameObject becomes inactive.")]
    public bool triggerOnDeactivate = true;

    [Tooltip("If true, try to read 'currentAmount' and 'requiredAmount' fields on the generator via reflection.")]
    public bool tryUseAmountFields = true;

    [Tooltip("Polling interval (seconds) when no event is available.")]
    public float pollInterval = 0.25f;

    [Tooltip("If true, deactivate the generator GameObject after triggering the ending.")]
    public bool deactivateGeneratorAfterTrigger = false;

    bool _triggered = false;
    Component _generatorComponentWithEvent = null;
    UnityEvent _generatorEvent = null;
    Component _generatorComponentWithAmounts = null;

    void Start()
    {
        if (generatorObject == null)
        {
            Debug.LogWarning("[GameManagerYanamTrigger] generatorObject not assigned. Disabling watcher.");
            enabled = false;
            return;
        }

        // Try to find a UnityEvent field on any component of the generator
        _generatorComponentWithEvent = FindComponentWithEvent(generatorObject, generatorEventName, out _generatorEvent);

        if (_generatorEvent != null)
        {
            _generatorEvent.AddListener(OnGeneratorFulfilled);
            Debug.Log("[GameManagerYanamTrigger] Subscribed to generator event '" + generatorEventName + "'.");
        }
        else
        {
            // If no event found, start polling
            StartCoroutine(PollGeneratorCoroutine());
            Debug.Log("[GameManagerYanamTrigger] No generator event found; started polling.");
        }
    }

    void OnDestroy()
    {
        if (_generatorEvent != null)
            _generatorEvent.RemoveListener(OnGeneratorFulfilled);
    }

    // Called when subscribed event fires
    void OnGeneratorFulfilled()
    {
        TryTriggerEnding("event");
    }

    IEnumerator PollGeneratorCoroutine()
    {
        while (!_triggered)
        {
            if (generatorObject == null)
                yield break;

            // Trigger when generator becomes inactive (if enabled)
            if (triggerOnDeactivate && !generatorObject.activeInHierarchy)
            {
                TryTriggerEnding("deactivate");
                yield break;
            }

            // Try to find a component with amount fields if requested
            if (tryUseAmountFields && _generatorComponentWithAmounts == null)
                _generatorComponentWithAmounts = FindComponentWithAmountFields(generatorObject);

            if (_generatorComponentWithAmounts != null)
            {
                int current = GetIntField(_generatorComponentWithAmounts, "currentAmount");
                int required = GetIntField(_generatorComponentWithAmounts, "requiredAmount");
                if (required > 0 && current >= required)
                {
                    TryTriggerEnding("poll_amounts");
                    yield break;
                }
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, pollInterval));
        }
    }

    void TryTriggerEnding(string reason)
    {
        if (_triggered) return;
        _triggered = true;

        YanamEnding yanam = FindAnyYanamEnding();
        if (yanam != null)
        {
            if (yanam.gameObject.activeInHierarchy && yanam.enabled)
            {
                Debug.Log($"[GameManagerYanamTrigger] Triggering Yanam ending (reason={reason}).");
                yanam.TriggerEnding();
            }
            else
            {
                Debug.LogWarning("[GameManagerYanamTrigger] YanamEnding found but inactive; enabling then triggering.");
                yanam.gameObject.SetActive(true);
                yanam.enabled = true;
                yanam.TriggerEnding();
            }
        }
        else
        {
            Debug.LogWarning("[GameManagerYanamTrigger] YanamEnding not found in scene; cannot trigger ending.");
        }

        if (deactivateGeneratorAfterTrigger && generatorObject != null)
        {
            generatorObject.SetActive(false);
            Debug.Log("[GameManagerYanamTrigger] Deactivated generator after triggering ending.");
        }
    }

    // Finds any YanamEnding instance without using deprecated APIs.
    // Uses Resources.FindObjectsOfTypeAll to include inactive objects and works across Unity versions.
    YanamEnding FindAnyYanamEnding()
    {
        var all = Resources.FindObjectsOfTypeAll<YanamEnding>();
        if (all != null && all.Length > 0)
        {
            // prefer active in hierarchy
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.activeInHierarchy) return all[i];

            // otherwise return first non-null
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) return all[i];
        }
        return null;
    }

    // Reflection helpers
    Component FindComponentWithEvent(GameObject go, string eventFieldName, out UnityEvent foundEvent)
    {
        foundEvent = null;
        if (string.IsNullOrEmpty(eventFieldName)) return null;

        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            var field = c.GetType().GetField(eventFieldName);
            if (field != null && typeof(UnityEvent).IsAssignableFrom(field.FieldType))
            {
                foundEvent = field.GetValue(c) as UnityEvent;
                return c;
            }
        }
        return null;
    }

    Component FindComponentWithAmountFields(GameObject go)
    {
        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (c == null) continue;
            var t = c.GetType();
            var f1 = t.GetField("currentAmount");
            var f2 = t.GetField("requiredAmount");
            if (f1 != null && f2 != null && f1.FieldType == typeof(int) && f2.FieldType == typeof(int))
                return c;
        }
        return null;
    }

    int GetIntField(Component comp, string fieldName)
    {
        if (comp == null) return 0;
        var f = comp.GetType().GetField(fieldName);
        if (f == null) return 0;
        object val = f.GetValue(comp);
        if (val is int i) return i;
        return 0;
    }
}
