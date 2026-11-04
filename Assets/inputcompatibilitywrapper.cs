// InputBridge.cs
using UnityEngine;
using System;

public class InputBridge : MonoBehaviour
{
    public static InputBridge Instance { get; private set; }

    public Vector2 Move { get; private set; }
    public bool FirePressed { get; private set; }
    public bool CollectPressed { get; private set; }
    public bool ReleasePressed { get; private set; }

    public event Action OnFire;
    public event Action OnCollect;
    public event Action OnRelease;

    // Tracks whether the keyboard is the source currently driving Move, so we
    // know it's safe to zero Move out on release without stomping a joystick
    // drag that might be happening at the same time.
    private bool keyboardDrivingMove = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        // Old Input fallback (works when Active Input Handling = Both).
        bool anyMoveKey =
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ||
            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ||
            Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);

        if (anyMoveKey)
        {
            Vector2 move = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            Move = move.normalized;
            keyboardDrivingMove = true;
        }
        else if (keyboardDrivingMove)
        {
            // No movement key is held anymore. Since it was the keyboard that
            // last set Move, stop the character. We only do this when the
            // keyboard was the active source, so a joystick drag in progress
            // (which calls SetMove itself every frame) is never stomped.
            Move = Vector2.zero;
            keyboardDrivingMove = false;
        }

        // Buttons
        if (Input.GetKeyDown(KeyCode.Space)) TriggerFire();
        if (Input.GetKeyDown(KeyCode.E)) TriggerCollect();
        if (Input.GetKeyDown(KeyCode.Q)) TriggerRelease();
    }

    // Called by UI components (joystick/button) to set movement or trigger actions
    public void SetMove(Vector2 v)
    {
        Move = v;
        // The joystick is now the active source; let it own Move until the
        // keyboard is used again.
        keyboardDrivingMove = false;
    }
    public void TriggerFire()
    {
        FirePressed = true;
        OnFire?.Invoke();
        FirePressed = false;
    }
    public void TriggerCollect()
    {
        CollectPressed = true;
        OnCollect?.Invoke();
        CollectPressed = false;
    }
    public void TriggerRelease()
    {
        ReleasePressed = true;
        OnRelease?.Invoke();
        ReleasePressed = false;
    }
}
