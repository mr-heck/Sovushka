using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Non-invasive watcher that triggers the Yanam ending when a generator is fulfilled.
/// Attach this to an always-active object (Game manager, etc.). It will either
/// subscribe to a UnityEvent on the generator (if present) or poll the generator state.
/// </summary>
public class GeneratorEndingTrigger : MonoBehaviour
{
    [Tooltip("Assign the generator GameObject to watch (the existing generator).")]
    public GameObject generatorObject;

    [Tooltip("Optional: if the generator script exposes a UnityEvent named OnRequirementFulfilled, this will subscribe automatically.")]
    public string generatorEventName = "OnRequirementFulfilled";

    [Tooltip("If polling is used, how often (seconds) to check generator state.")]
    public float pollInterval = 0.25f;

    [Tooltip("If true, trigger ending when the generator GameObject becomes inactive.")]
    public bool triggerOnDeactivate = true;

    [Tooltip("Optional: if the generator has a script with 'currentAmount' and 'requiredAmount' fields, this will use them.")]
    public bool tryUseAmountFields = true;

    bool _triggered = false;
    Component _generatorComponent = null;
    UnityEvent _generatorEvent = null;

    void Start()
    {
        if (generatorObject == null)
        {
            Debug.LogWarning("[GeneratorEndingTrigger] generatorObject not assigned. Disabling watcher.");
            enabled = false;
            return;
        }

        // Try to find a UnityEvent field named generatorEventName on any component
        _generatorComponent = FindComponentWithEvent(generatorObject, generatorEventName, out _generatorEvent);

        if (_generatorEvent != null)
        {
            // Subscribe safely
            _generatorEvent.AddListener(OnGeneratorFulfilled);
            Debug.Log("[GeneratorEndingTrigger] Subscribed to generator event.");
        }
        else
        {
            // Start polling if no event found
            StartCoroutine(PollGeneratorCoroutine());
            Debug.Log("[GeneratorEndingTrigger] No generator event found; started polling.");
        }
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

            // If generator became inactive and we want to trigger on deactivate
            if (triggerOnDeactivate && !generatorObject.activeInHierarchy)
            {
                TryTriggerEnding("deactivate");
                yield break;
            }

            // Try to read currentAmount/requiredAmount via reflection if requested
            if (tryUseAmountFields && _generatorComponent == null)
            {
                // find first component that has both fields
                _generatorComponent = FindComponentWithAmountFields(generatorObject);
            }

            if (_generatorComponent != null)
            {
                int current = GetIntField(_generatorComponent, "currentAmount");
                int required = GetIntField(_generatorComponent, "requiredAmount");
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

        if (yanam != null && yanam.gameObject.activeInHierarchy && yanam.enabled)
        {
            Debug.Log($"[GeneratorEndingTrigger] Triggering Yanam ending (reason={reason}).");
            yanam.TriggerEnding();
        }
        else if (yanam != null)
        {
            // enable it first then trigger
            Debug.LogWarning("[GeneratorEndingTrigger] YanamEnding found but inactive; enabling then triggering.");
            yanam.gameObject.SetActive(true);
            yanam.enabled = true;
            yanam.TriggerEnding();
        }
        else
        {
            Debug.LogWarning("[GeneratorEndingTrigger] YanamEnding not found in scene; cannot trigger ending.");
        }
    }

    // Find any YanamEnding instance without using deprecated APIs.
    // Uses Resources.FindObjectsOfTypeAll to include inactive objects and works across Unity versions.
    YanamEnding FindAnyYanamEnding()
    {
        var all = Resources.FindObjectsOfTypeAll<YanamEnding>();
        if (all == null || all.Length == 0) return null;

        // Prefer active in hierarchy first
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.activeInHierarchy) return all[i];
        }

        // Otherwise return the first non-null instance (inactive)
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null) return all[i];
        }

        return null;
    }

    // Utility: find a component on the generator that exposes a UnityEvent field with the given name
    Component FindComponentWithEvent(GameObject go, string eventFieldName, out UnityEvent foundEvent)
    {
        foundEvent = null;
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

    // Utility: find a component that has integer fields named currentAmount and requiredAmount
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

    void OnDestroy()
    {
        // Unsubscribe if we subscribed earlier
        if (_generatorEvent != null)
            _generatorEvent.RemoveListener(OnGeneratorFulfilled);
    }
}
