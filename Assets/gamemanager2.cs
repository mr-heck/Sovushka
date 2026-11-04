// File: GameManager.cs
// Attach this to a persistent GameObject named "GameManager" (or similar).
// Responsible for global game state: start, pause, resume, restart, stop, exit.
// Provides UnityEvents so other systems (UI, gameplay objects) can react.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [Tooltip("True when gameplay is running (timeScale = 1).")]
    public bool IsGameRunning { get; private set; } = false;

    [Header("Gameplay Objects (optional)")]
    [Tooltip("Assign gameplay root objects that should be enabled only when the game is running.")]
    public GameObject[] gameplayRoots;

    [Header("Audio (optional)")]
    [Tooltip("Plays via AudioManager when the game starts, and stops when the game returns to the home screen. Leave empty until a music clip is added to the project.")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.6f;

    [Header("Events")]
    public UnityEvent OnGameStarted;
    public UnityEvent OnGamePaused;
    public UnityEvent OnGameResumed;
    public UnityEvent OnGameStopped;
    public UnityEvent OnGameRestarted;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // NOTE: intentionally NOT DontDestroyOnLoad - gameplayRoots etc. are scene references and must be re-created on Restart.
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Ensure game is not running at scene load by default.
        // This prevents gameplay from progressing before the player presses Play.
        IsGameRunning = false;
        Time.timeScale = 0f;

        // Optionally disable gameplay roots at start
        SetGameplayRootsActive(false);
    }

    /// <summary>
    /// Start the game: enable gameplay objects, set timeScale to 1, fire events.
    /// Call this from your UIManager.OnHomePlay or equivalent.
    /// </summary>
    public void StartGame()
    {
        if (IsGameRunning) return;

        IsGameRunning = true;
        Time.timeScale = 1f;
        SetGameplayRootsActive(true);
        if (AudioManager.Instance != null && backgroundMusic != null)
            AudioManager.Instance.PlayMusic(backgroundMusic, backgroundMusicVolume);
        OnGameStarted?.Invoke();
        Debug.Log("[GameManager] StartGame");
    }

    /// <summary>
    /// Pause the game. Keeps IsGameRunning true (game was running) but stops time.
    /// Use ResumeGame to continue.
    /// </summary>
    public void PauseGame()
    {
        if (!IsGameRunning) return;
        Time.timeScale = 0f;
        OnGamePaused?.Invoke();
        Debug.Log("[GameManager] PauseGame");
    }

    /// <summary>
    /// Resume a paused game.
    /// </summary>
    public void ResumeGame()
    {
        if (!IsGameRunning) return;
        Time.timeScale = 1f;
        OnGameResumed?.Invoke();
        Debug.Log("[GameManager] ResumeGame");
    }

    /// <summary>
    /// Restart the current scene. This reloads the scene and resets state.
    /// </summary>
    public void RestartGame()
    {
        // Ensure timeScale restored before reload
        Time.timeScale = 1f;
        OnGameRestarted?.Invoke();
        Debug.Log("[GameManager] RestartGame - reloading scene: " + SceneManager.GetActiveScene().name);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Stop the game and return to a non-playing state (useful when returning to home screen).
    /// Keeps the scene loaded but disables gameplay.
    /// </summary>
    public void StopGame()
    {
        IsGameRunning = false;
        Time.timeScale = 0f;
        SetGameplayRootsActive(false);
        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();
        OnGameStopped?.Invoke();
        Debug.Log("[GameManager] StopGame");
    }

    /// <summary>
    /// Exit the application (works in build; stops play mode in editor).
    /// </summary>
    public void ExitGame()
    {
        Debug.Log("[GameManager] ExitGame called");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Toggle pause state. If game is running and timeScale == 1 -> pause.
    /// If game is running and timeScale == 0 -> resume.
    /// If game is not running, does nothing.
    /// </summary>
    public void TogglePause()
    {
        if (!IsGameRunning) return;
        if (Mathf.Approximately(Time.timeScale, 0f)) ResumeGame();
        else PauseGame();
    }

    /// <summary>
    /// Enable or disable gameplay root GameObjects.
    /// Use this to prevent gameplay systems from running before StartGame.
    /// </summary>
    /// <param name="active"></param>
    void SetGameplayRootsActive(bool active)
    {
        if (gameplayRoots == null) return;
        for (int i = 0; i < gameplayRoots.Length; i++)
        {
            if (gameplayRoots[i] == null) continue;
            gameplayRoots[i].SetActive(active);
        }
    }

    // Optional helper: call this to register a gameplay root at runtime
    public void RegisterGameplayRoot(GameObject root)
    {
        if (root == null) return;
        // If already running, ensure it's active immediately
        if (IsGameRunning) root.SetActive(true);
    }

    // Optional helper: call this to unregister or disable a root
    public void UnregisterGameplayRoot(GameObject root)
    {
        if (root == null) return;
        root.SetActive(false);
    }
}
