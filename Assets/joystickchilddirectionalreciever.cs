// SpriteDirectionReceiver.cs
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteDirectionReceiver : MonoBehaviour, IDirectionalReceiver
{
    [Tooltip("Optional: assign the JoystickDirectionManager in Inspector to avoid runtime Find.")]
    public JoystickDirectionManager manager;

    [Tooltip("Optional Animator. If present, sets integer parameter 'Direction'.")]
    public Animator animator;
    public string animatorDirectionParam = "Direction";

    [Tooltip("Sprites for directions 0..7. Index mapping: 0=E,1=NE,2=N,3=NW,4=W,5=SW,6=S,7=SE")]
    public Sprite[] directionSprites = new Sprite[8];

    [Tooltip("Sprite for idle state")]
    public Sprite idleSprite;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        // Prefer inspector assignment; only search if manager is null
        if (manager == null)
        {
#if UNITY_2024_1_OR_NEWER
            // Recommended modern API
            manager = Object.FindAnyObjectByType<JoystickDirectionManager>();
#elif UNITY_2023_2_OR_NEWER
            // Available in 2023.2+; may be deprecated in newer versions, so guarded above
            manager = Object.FindAnyObjectByType<JoystickDirectionManager>();
#else
            // Fallback for older Unity versions
            manager = FindObjectOfType<JoystickDirectionManager>();
#endif
        }

        if (manager != null) manager.RegisterReceiver(this);
    }

    void OnDisable()
    {
        if (manager != null) manager.UnregisterReceiver(this);
    }

    // IDirectionalReceiver implementation
    public void OnDirectionChanged(int dir)
    {
        // If animator exists and has the parameter, set it
        if (animator != null && HasAnimatorIntParam(animator, animatorDirectionParam))
        {
            animator.SetInteger(animatorDirectionParam, dir);
            return;
        }

        // Fallback to sprite swap
        if (dir < 0)
        {
            SetIdleSprite();
            return;
        }

        if (directionSprites != null && directionSprites.Length >= 8)
        {
            var s = directionSprites[dir];
            if (s != null) sr.sprite = s;
        }
    }

    public void OnIdle()
    {
        SetIdleSprite();
    }

    void SetIdleSprite()
    {
        if (animator != null && HasAnimatorIntParam(animator, animatorDirectionParam))
        {
            animator.SetInteger(animatorDirectionParam, -1);
            return;
        }
        if (idleSprite != null) sr.sprite = idleSprite;
    }

    static bool HasAnimatorIntParam(Animator a, string paramName)
    {
        if (a == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in a.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Int) return true;
        }
        return false;
    }
}
