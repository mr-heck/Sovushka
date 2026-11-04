using UnityEngine;

[DisallowMultipleComponent]
public class YanamFlyingEnforcer : MonoBehaviour
{
    [Tooltip("Fraction of clip remaining before we restart it (0.01 = last 1%)")]
    [Range(0f, 0.5f)]
    public float restartThreshold = 0.02f;

    [Tooltip("If true, keep the flying GameObject active even if other scripts try to deactivate it")]
    public bool forceActive = true;

    [Tooltip("If true, the enforcer will stop after a call to StopEnforcing()")]
    public bool stopAfterStopCall = false;

    Animator _anim;
    bool _enforcing = false;
    int _stateHash = 0;
    float _capturedClipLength = 0f;
    bool _stopped = false;

    void Awake()
    {
        _anim = GetComponent<Animator>();
    }

    void OnEnable()
    {
        // start enforcing when the object becomes active
        _enforcing = false; // will capture on first Update
    }

    void Update()
    {
        if (_stopped && stopAfterStopCall) return;

        if (forceActive && !gameObject.activeInHierarchy)
        {
            // ensure it stays active if requested
            gameObject.SetActive(true);
        }

        if (!gameObject.activeInHierarchy) return;

        if (!_enforcing)
        {
            CaptureCurrentState();
            _enforcing = true;
        }

        if (_anim == null || !_anim.runtimeAnimatorController) return;

        var state = _anim.GetCurrentAnimatorStateInfo(0);

        // If animator switched state, force the captured state
        if (_stateHash != 0 && state.shortNameHash != _stateHash)
        {
            ForcePlayCapturedState();
            return;
        }

        // normalizedTime may exceed 1; use fractional part
        float normalized = state.normalizedTime % 1f;

        if (normalized >= (1f - restartThreshold))
        {
            ForcePlayCapturedState();
            return;
        }
    }

    void CaptureCurrentState()
    {
        if (_anim == null || _anim.runtimeAnimatorController == null) return;

        var state = _anim.GetCurrentAnimatorStateInfo(0);
        _stateHash = state.shortNameHash;

        // try to get clip length from current clip info
        var clips = _anim.GetCurrentAnimatorClipInfo(0);
        if (clips != null && clips.Length > 0 && clips[0].clip != null)
            _capturedClipLength = clips[0].clip.length;
        else
            _capturedClipLength = 0f;
    }

    void ForcePlayCapturedState()
    {
        if (_anim == null || _stateHash == 0) return;
        try
        {
            _anim.Play(_stateHash, 0, 0f);
            _anim.Update(0f);
            _anim.speed = 1f;
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Call this to stop enforcing (optional).
    /// </summary>
    public void StopEnforcing()
    {
        _stopped = true;
    }
}
