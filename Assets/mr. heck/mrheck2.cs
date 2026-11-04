using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// MrHeckAdapter
/// - Attach to MrHeck GameObject (or prefab) to provide a public OnHitByPlayer(int) handler
///   that reuses the existing MrHeckBehaviour vanish/respawn/animation logic via reflection.
/// - No changes required to MrHeckBehaviour source.
/// - Works even if MrHeckBehaviour methods/fields are private.
/// </summary>
[DisallowMultipleComponent]
public class MrHeckAdapter : MonoBehaviour
{
    [Header("Adapter debug")]
    public bool debug = true;

    // cached references
    Component mrHeckBehaviourComp;
    Type mrType;

    // fields on MrHeckBehaviour we try to read
    FieldInfo fi_heckShotL;
    FieldInfo fi_heckShotR;
    FieldInfo fi_heckSpawning;
    FieldInfo fi_aliveVisuals;
    FieldInfo fi_downVisuals;
    FieldInfo fi_protagonist;
    FieldInfo fi_isVanished;
    FieldInfo fi_isStunned;

    // methods on MrHeckBehaviour we try to call
    MethodInfo mi_PlayAnimationAndHold; // IEnumerator PlayAnimationAndHold(GameObject)
    MethodInfo mi_VanishAndScheduleRespawn; // void VanishAndScheduleRespawn()
    MethodInfo mi_SetAliveVisuals; // maybe exists (SetAliveVisuals)
    MethodInfo mi_SetDownVisuals; // maybe exists

    void Awake()
    {
        // find the MrHeck behaviour component (by type name)
        mrHeckBehaviourComp = GetComponent(GetMrHeckBehaviourTypeName());
        if (mrHeckBehaviourComp == null)
        {
            // fallback: find any component that has heckShotL/heckShotR fields
            foreach (var comp in GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                var t = comp.GetType();
                if (t.GetField("heckShotL", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null &&
                    t.GetField("heckShotR", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null)
                {
                    mrHeckBehaviourComp = comp;
                    break;
                }
            }
        }

        if (mrHeckBehaviourComp == null)
        {
            if (debug) Debug.LogWarning("[MrHeckAdapter] MrHeckBehaviour not found on " + gameObject.name);
            return;
        }

        mrType = mrHeckBehaviourComp.GetType();

        // cache fields
        fi_heckShotL = mrType.GetField("heckShotL", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_heckShotR = mrType.GetField("heckShotR", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_heckSpawning = mrType.GetField("heckSpawning", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_aliveVisuals = mrType.GetField("aliveVisuals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_downVisuals = mrType.GetField("downVisuals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_protagonist = mrType.GetField("protagonist", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_isVanished = mrType.GetField("isVanished", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        fi_isStunned = mrType.GetField("isStunned", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // cache methods
        // PlayAnimationAndHold signature: IEnumerator PlayAnimationAndHold(GameObject anim)
        mi_PlayAnimationAndHold = mrType.GetMethod("PlayAnimationAndHold", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        // VanishAndScheduleRespawn signature: void VanishAndScheduleRespawn()
        mi_VanishAndScheduleRespawn = mrType.GetMethod("VanishAndScheduleRespawn", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // optional helpers
        mi_SetAliveVisuals = mrType.GetMethod("SetAliveVisuals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        mi_SetDownVisuals = mrType.GetMethod("SetDownVisuals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (debug)
        {
            Debug.Log($"[MrHeckAdapter] Attached to {gameObject.name}. Found MrHeckBehaviour: {mrHeckBehaviourComp != null}");
            Debug.Log($"[MrHeckAdapter] PlayAnim:{(mi_PlayAnimationAndHold!=null)} Vanish:{(mi_VanishAndScheduleRespawn!=null)} isStunned field found:{(fi_isStunned!=null)}");
        }
    }

    // Public API: GameManagerSimple.ProcessPlayerShot uses SendMessage("OnHitByPlayer", damage)
    public void OnHitByPlayer(int damage)
    {
        if (mrHeckBehaviourComp == null)
        {
            if (debug) Debug.LogWarning("[MrHeckAdapter] OnHitByPlayer called but MrHeckBehaviour not found.");
            return;
        }

        // if MrHeck already vanished, do nothing
        if (fi_isVanished != null)
        {
            try
            {
                var val = fi_isVanished.GetValue(mrHeckBehaviourComp);
                if (val is bool b && b)
                {
                    if (debug) Debug.Log("[MrHeckAdapter] MrHeck already vanished; ignoring hit.");
                    return;
                }
            }
            catch { /* ignore */ }
        }

        // If MrHeck is already reacting to a hit (e.g. MrHeckBehaviour's own
        // bullet-proximity check in Update() already caught this same bullet
        // and started its own HandleShotThenVanish/isStunned sequence), don't
        // double-process the same shot - doing so used to start a second,
        // overlapping animation/vanish sequence.
        if (fi_isStunned != null)
        {
            try
            {
                var val = fi_isStunned.GetValue(mrHeckBehaviourComp);
                if (val is bool b && b)
                {
                    if (debug) Debug.Log("[MrHeckAdapter] MrHeck already stunned/reacting to a hit; ignoring duplicate.");
                    return;
                }
            }
            catch { /* ignore */ }
        }

        // Stun MrHeck immediately, before playing the shot pose. MrHeckBehaviour.Update()
        // drives chase/patrol movement and animation every frame unless isStunned is true;
        // without this, Update() calls PlayExclusive() on a movement pose (since MrHeck is
        // usually still moving when hit) and immediately deactivates the shot pose GameObject
        // we're about to turn on below - so the shot animation never actually gets seen before
        // MrHeck vanishes. Setting isStunned here is what HandleShotThenVanish does on the
        // MrHeckBehaviour-driven hit path; this adapter has to do it too since it bypasses
        // that method and calls PlayAnimationAndHold directly.
        if (fi_isStunned != null)
        {
            try { fi_isStunned.SetValue(mrHeckBehaviourComp, true); }
            catch (Exception ex) { if (debug) Debug.LogWarning("[MrHeckAdapter] Failed to set isStunned: " + ex.Message); }
        }

        if (debug) Debug.Log($"[MrHeckAdapter] OnHitByPlayer invoked on {gameObject.name} damage={damage}");

        // Determine shot animation GameObject (heckShotL or heckShotR)
        GameObject shotAnim = null;
        try
        {
            // prefer protagonist position if available
            Vector3? protagonistPos = null;
            if (fi_protagonist != null)
            {
                var prot = fi_protagonist.GetValue(mrHeckBehaviourComp) as Transform;
                if (prot != null) protagonistPos = prot.position;
            }

            bool shotFromLeft = false;
            if (protagonistPos.HasValue)
                shotFromLeft = protagonistPos.Value.x < transform.position.x;
            else
            {
                // fallback: assume shot from right (so play left anim) — keep consistent with original script's logic
                shotFromLeft = false;
            }

            if (shotFromLeft)
            {
                if (fi_heckShotL != null) shotAnim = fi_heckShotL.GetValue(mrHeckBehaviourComp) as GameObject;
            }
            else
            {
                if (fi_heckShotR != null) shotAnim = fi_heckShotR.GetValue(mrHeckBehaviourComp) as GameObject;
            }
        }
        catch (Exception ex)
        {
            if (debug) Debug.LogWarning("[MrHeckAdapter] Failed to determine shotAnim: " + ex.Message);
        }

        // If PlayAnimationAndHold exists, call it and then call VanishAndScheduleRespawn
        if (mi_PlayAnimationAndHold != null)
        {
            try
            {
                // invoke PlayAnimationAndHold(anim) -> returns IEnumerator
                object[] args = new object[] { shotAnim };
                var enumeratorObj = mi_PlayAnimationAndHold.Invoke(mrHeckBehaviourComp, args) as IEnumerator;
                if (enumeratorObj != null)
                {
                    // run the returned IEnumerator on this adapter so it yields correctly
                    StartCoroutine(PlayAnimThenVanishCoroutine(enumeratorObj));
                    return;
                }
            }
            catch (Exception ex)
            {
                if (debug) Debug.LogWarning("[MrHeckAdapter] PlayAnimationAndHold invoke failed: " + ex.Message);
            }
        }

        // If PlayAnimationAndHold not available or failed, try to call Vanish directly
        if (mi_VanishAndScheduleRespawn != null)
        {
            try
            {
                mi_VanishAndScheduleRespawn.Invoke(mrHeckBehaviourComp, null);
                if (debug) Debug.Log("[MrHeckAdapter] Invoked VanishAndScheduleRespawn directly.");
                return;
            }
            catch (Exception ex)
            {
                if (debug) Debug.LogWarning("[MrHeckAdapter] VanishAndScheduleRespawn invoke failed: " + ex.Message);
            }
        }

        // Last resort: toggle alive/down visuals if fields or methods exist
        bool toggled = TryToggleVisualsFallback();
        if (!toggled && debug) Debug.LogWarning("[MrHeckAdapter] No vanish method found and visuals fallback failed.");
    }

    IEnumerator PlayAnimThenVanishCoroutine(IEnumerator animEnumerator)
    {
        // run the animation IEnumerator returned by MrHeckBehaviour
        yield return StartCoroutine(animEnumerator);

        // after animation completes, call VanishAndScheduleRespawn if available
        if (mi_VanishAndScheduleRespawn != null)
        {
            try
            {
                mi_VanishAndScheduleRespawn.Invoke(mrHeckBehaviourComp, null);
                if (debug) Debug.Log("[MrHeckAdapter] PlayAnimThenVanish: called VanishAndScheduleRespawn.");
                yield break;
            }
            catch (Exception ex)
            {
                if (debug) Debug.LogWarning("[MrHeckAdapter] PlayAnimThenVanish: Vanish invoke failed: " + ex.Message);
            }
        }

        // fallback visuals toggle
        TryToggleVisualsFallback();
    }

    bool TryToggleVisualsFallback()
    {
        try
        {
            // try methods first
            if (mi_SetAliveVisuals != null && mi_SetDownVisuals != null)
            {
                mi_SetAliveVisuals.Invoke(mrHeckBehaviourComp, new object[] { false });
                mi_SetDownVisuals.Invoke(mrHeckBehaviourComp, new object[] { true });
                if (debug) Debug.Log("[MrHeckAdapter] Toggled visuals via methods.");
                return true;
            }

            // try fields aliveVisuals/downVisuals arrays
            if (fi_aliveVisuals != null || fi_downVisuals != null)
            {
                if (fi_aliveVisuals != null)
                {
                    var aliveArr = fi_aliveVisuals.GetValue(mrHeckBehaviourComp) as GameObject[];
                    if (aliveArr != null)
                    {
                        foreach (var g in aliveArr) if (g != null) g.SetActive(false);
                    }
                }
                if (fi_downVisuals != null)
                {
                    var downArr = fi_downVisuals.GetValue(mrHeckBehaviourComp) as GameObject[];
                    if (downArr != null)
                    {
                        foreach (var g in downArr) if (g != null) g.SetActive(true);
                    }
                }
                if (debug) Debug.Log("[MrHeckAdapter] Toggled visuals via fields.");
                return true;
            }
        }
        catch (Exception ex)
        {
            if (debug) Debug.LogWarning("[MrHeckAdapter] TryToggleVisualsFallback failed: " + ex.Message);
        }
        return false;
    }

    // Helper: try to get the MrHeckBehaviour type name commonly used
    Type GetMrHeckBehaviourTypeName()
    {
        // common names to try
        string[] names = new string[] { "MrHeckBehaviour", "MrHeckController_NoAnimator", "MrHeckController", "MrHeck" };
        foreach (var n in names)
        {
            var t = Type.GetType(n);
            if (t != null) return t;
        }

        // fallback: search loaded assemblies for a type that has heckShotL/heckShotR fields
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (!typeof(Component).IsAssignableFrom(t)) continue;
                    var f1 = t.GetField("heckShotL", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var f2 = t.GetField("heckShotR", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f1 != null && f2 != null) return t;
                }
            }
            catch { }
        }
        return null;
    }
}
