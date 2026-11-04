// UIManager.cs
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro; // keep if you use TextMeshPro; safe to leave even if unused

[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    [Header("Panels (assign in Inspector)")]
    public GameObject framePanel;
    public GameObject endScreenPanel;
    public GameObject homeScreenPanel;
    public GameObject pausePanel;

    [Header("Optional Buttons (assign if you want auto-wiring)")]
    public Button framePlayPauseButton;
    public Button endBackHomeButton;
    public Button homePlayButton;
    public Button homeSoundButton;
    public Button homeExitButton;
    public Button pauseSoundButton;
    public Button pauseRestartButton;
    public Button pauseExitButton;
    public Button pauseResumeButton;

    [Header("Optional label references")]
    public Text homeSoundText;           // legacy UI Text
    public Text pauseSoundText;          // legacy UI Text
    public TextMeshProUGUI homeSoundTMP; // TextMeshPro (optional)
    public TextMeshProUGUI pauseSoundTMP; // TextMeshPro (optional)

    [Header("Startup settings")]
    [Tooltip("If true, only the Home panel will be visible at runtime and the game will be paused (timeScale = 0).")]
    public bool ensureHomeVisibleAtStart = true;
    [Tooltip("If true and button references are assigned, UIManager will add listeners at Start.")]
    public bool autoWireButtons = false;

    void Awake()
    {
        // Defensive: warn if panels are not assigned
        if (framePanel == null) Debug.LogWarning("[UIManager] framePanel not assigned.");
        if (endScreenPanel == null) Debug.LogWarning("[UIManager] endScreenPanel not assigned.");
        if (homeScreenPanel == null) Debug.LogWarning("[UIManager] homeScreenPanel not assigned.");
        if (pausePanel == null) Debug.LogWarning("[UIManager] pausePanel not assigned.");
    }

    void Start()
    {
        if (ensureHomeVisibleAtStart)
        {
            ShowOnly(homeScreenPanel);
            // Pause gameplay until StartGame is called
            if (GameManager.Instance != null)
            {
                // Ensure GameManager is in a non-running state
                GameManager.Instance.StopGame();
            }
            else
            {
                // Fallback: pause time if no GameManager present
                Time.timeScale = 0f;
            }
        }

        if (autoWireButtons)
            WireButtonsSafely();

        // Ensure labels reflect current sound state at start
        UpdateSoundLabels();
    }

    // -------------------------
    // Panel control helpers
    // -------------------------
    void ShowOnly(GameObject panelToShow)
    {
        SafeSetActive(framePanel, framePanel == panelToShow);
        SafeSetActive(endScreenPanel, endScreenPanel == panelToShow);
        SafeSetActive(homeScreenPanel, homeScreenPanel == panelToShow);
        SafeSetActive(pausePanel, pausePanel == panelToShow);
    }

    void SafeSetActive(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active) go.SetActive(active);
    }

    public void ShowHome()
    {
        ShowOnly(homeScreenPanel);
        Debug.Log("[UIManager] ShowHome");
    }

    public void ShowFrame()
    {
        ShowOnly(framePanel);
        Debug.Log("[UIManager] ShowFrame");
    }

    public void ShowEndScreen()
    {
        ShowOnly(endScreenPanel);
        Debug.Log("[UIManager] ShowEndScreen");
    }

    public void ShowPause()
    {
        ShowOnly(pausePanel);
        Debug.Log("[UIManager] ShowPause");
    }

    // -------------------------
    // Button handlers (public - wire in Inspector)
    // -------------------------
    public void OnHomePlay()
    {
        Debug.Log("[UIManager] OnHomePlay pressed");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
        else
        {
            // fallback: unpause time
            Time.timeScale = 1f;
            Debug.LogWarning("[UIManager] GameManager.Instance not found; unpausing Time.timeScale directly.");
        }
        ShowFrame();
    }

    public void OnHomeToggleSound()
    {
        Debug.Log("[UIManager] OnHomeToggleSound pressed");
        // Toggle sound: prefer AudioManager if it exposes a ToggleSound API; otherwise fallback to AudioListener
        bool? newState = null;
        bool current = QueryIsSoundOn();
        newState = !current;
        InvokeToggleSound(newState);
        UpdateSoundLabels();
    }

    public void OnHomeExit()
    {
        Debug.Log("[UIManager] OnHomeExit pressed");
        if (GameManager.Instance != null) GameManager.Instance.ExitGame();
    }

    public void OnFramePlayPausePressed()
    {
        Debug.Log("[UIManager] OnFramePlayPausePressed pressed");
        ShowPause();
        if (GameManager.Instance != null) GameManager.Instance.PauseGame();
    }

    public void OnEndBackToHome()
    {
        Debug.Log("[UIManager] OnEndBackToHome pressed");
        ShowHome();
        if (GameManager.Instance != null) GameManager.Instance.StopGame();
    }

    public void OnPauseToggleSound()
    {
        Debug.Log("[UIManager] OnPauseToggleSound pressed");
        bool? newState = null;
        bool current = QueryIsSoundOn();
        newState = !current;
        InvokeToggleSound(newState);
        UpdateSoundLabels();
    }

    public void OnPauseRestart()
    {
        Debug.Log("[UIManager] OnPauseRestart pressed");
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        // After restart the scene reloads; UI will reinitialize
    }

    public void OnPauseExit()
    {
        Debug.Log("[UIManager] OnPauseExit pressed");
        if (GameManager.Instance != null) GameManager.Instance.ExitGame();
    }

    public void OnPauseResume()
    {
        Debug.Log("[UIManager] OnPauseResume pressed");
        if (GameManager.Instance != null) GameManager.Instance.ResumeGame();
        else Time.timeScale = 1f;
        ShowFrame();
    }

    // -------------------------
    // Helpers
    // -------------------------
    public void UpdateSoundLabels()
    {
        bool on = QueryIsSoundOn();
        string text = on ? "Sound: On" : "Sound: Off";

        if (homeSoundText != null) homeSoundText.text = text;
        if (pauseSoundText != null) pauseSoundText.text = text;
        if (homeSoundTMP != null) homeSoundTMP.text = text;
        if (pauseSoundTMP != null) pauseSoundTMP.text = text;
    }

    void WireButtonsSafely()
    {
        // Add listeners only if both button and method exist
        TryWire(framePlayPauseButton, OnFramePlayPausePressed);
        TryWire(endBackHomeButton, OnEndBackToHome);
        TryWire(homePlayButton, OnHomePlay);
        TryWire(homeSoundButton, OnHomeToggleSound);
        TryWire(homeExitButton, OnHomeExit);
        TryWire(pauseSoundButton, OnPauseToggleSound);
        TryWire(pauseRestartButton, OnPauseRestart);
        TryWire(pauseExitButton, OnPauseExit);
        TryWire(pauseResumeButton, OnPauseResume);
    }

    void TryWire(Button b, UnityAction action)
    {
        if (b == null || action == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
    }

    // -------------------------
    // Audio compatibility helpers (reflection + safe fallback)
    // -------------------------
    // Query whether sound is on. Tries AudioManager.IsSoundOn (method or property) via reflection,
    // otherwise falls back to AudioListener.volume heuristic.
    bool QueryIsSoundOn()
    {
        var am = GetAudioManagerInstance();
        if (am != null)
        {
            var t = am.GetType();

            // Try method IsSoundOn()
            var m = t.GetMethod("IsSoundOn", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (m != null && m.ReturnType == typeof(bool))
            {
                try { return (bool)m.Invoke(am, null); } catch { /* ignore and fallback */ }
            }

            // Try property IsSoundOn
            var p = t.GetProperty("IsSoundOn", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null && p.PropertyType == typeof(bool))
            {
                try { return (bool)p.GetValue(am); } catch { /* ignore and fallback */ }
            }

            // Try property or field named SfxEnabled / SoundEnabled / sfxEnabled
            var p2 = t.GetProperty("SfxEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p2 != null && p2.PropertyType == typeof(bool))
            {
                try { return (bool)p2.GetValue(am); } catch { /* ignore */ }
            }
            var f = t.GetField("sfxEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f != null && f.FieldType == typeof(bool))
            {
                try { return (bool)f.GetValue(am); } catch { /* ignore */ }
            }
        }

        // Fallback: use AudioListener.volume heuristic
        return AudioListener.volume > 0.5f;
    }

    // Invoke toggle on AudioManager if available; otherwise toggle AudioListener.volume.
    void InvokeToggleSound(bool? setTo)
    {
        var am = GetAudioManagerInstance();
        if (am != null)
        {
            var t = am.GetType();

            // Try ToggleSound(bool) or ToggleSound()
            var mBool = t.GetMethod("ToggleSound", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, new[] { typeof(bool) }, null);
            if (mBool != null)
            {
                try
                {
                    mBool.Invoke(am, new object[] { setTo.HasValue ? setTo.Value : !QueryIsSoundOn() });
                    return;
                }
                catch { /* ignore and try other options */ }
            }

            var mNoArg = t.GetMethod("ToggleSound", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, new System.Type[0], null);
            if (mNoArg != null)
            {
                try
                {
                    mNoArg.Invoke(am, null);
                    return;
                }
                catch { /* ignore */ }
            }

            // Try SetSfxEnabled(bool) or SetSoundEnabled(bool)
            var setMethod = t.GetMethod("SetSfxEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, new[] { typeof(bool) }, null)
                         ?? t.GetMethod("SetSoundEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase, null, new[] { typeof(bool) }, null);
            if (setMethod != null)
            {
                try
                {
                    setMethod.Invoke(am, new object[] { setTo.HasValue ? setTo.Value : !QueryIsSoundOn() });
                    return;
                }
                catch { /* ignore */ }
            }

            // As a last resort, try to set a public field/property named sfxEnabled
            var p = t.GetProperty("SfxEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
            {
                try { p.SetValue(am, setTo.HasValue ? setTo.Value : !QueryIsSoundOn()); return; } catch { }
            }
            var f = t.GetField("sfxEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f != null && f.FieldType == typeof(bool))
            {
                try { f.SetValue(am, setTo.HasValue ? setTo.Value : !QueryIsSoundOn()); return; } catch { }
            }
        }

        // Fallback: toggle AudioListener.volume
        if (setTo.HasValue)
            AudioListener.volume = setTo.Value ? 1f : 0f;
        else
            AudioListener.volume = AudioListener.volume > 0.5f ? 0f : 1f;
    }

    // Helper to safely get AudioManager.Instance via reflection (no compile-time dependency)
    object GetAudioManagerInstance()
    {
        // Try direct reference if available at compile time
        // (this will compile even if AudioManager type exists in project)
        var amType = typeof(object);
        try
        {
            // Attempt to find a type named "AudioManager" in loaded assemblies
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("AudioManager", false, true);
                if (t != null)
                {
                    // Try to get static Instance property
                    var prop = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                    if (prop != null)
                    {
                        var inst = prop.GetValue(null);
                        if (inst != null) return inst;
                    }

                    // Try static field Instance
                    var field = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                    if (field != null)
                    {
                        var inst2 = field.GetValue(null);
                        if (inst2 != null) return inst2;
                    }
                }
            }
        }
        catch
        {
            // ignore reflection errors and return null
        }
        return null;
    }
}
