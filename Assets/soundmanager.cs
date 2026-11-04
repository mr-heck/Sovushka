// AudioManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer (optional)")]
    public AudioMixer mixer;
    public string musicVolumeParam = "MusicVolume";
    public string sfxVolumeParam = "SFXVolume";

    [Header("Pooling")]
    public AudioSource sfxPrefab;
    public int poolSize = 12;

    [Header("Sound toggle")]
    [Tooltip("Whether SFX/music are audible. Read by UIManager's sound button; persisted across sessions.")]
    public bool sfxEnabled = true;
    const string SfxEnabledPrefKey = "audio_sfxEnabled";

    [Header("Volume (0-1, driven by the Music / SFX sliders on the home + pause panels)")]
    [Range(0f, 1f)] public float musicVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public const string MusicVolumePrefKey = "audio_musicVolume";
    public const string SfxVolumePrefKey = "audio_sfxVolume";

    /// <summary>Raised whenever the music or SFX volume changes, so every slider can stay in sync.</summary>
    public event Action OnVolumesChanged;

    Queue<AudioSource> pool = new Queue<AudioSource>();
    AudioSource musicSource;
    AudioClip currentMusicClip;
    float currentMusicBaseVolume = 1f;

    /// <summary>Current SFX volume (0-1) for sources that don't go through the pool (loops, etc).</summary>
    public static float SfxScale => Instance != null ? Instance.sfxVolume : PlayerPrefs.GetFloat(SfxVolumePrefKey, 1f);

    /// <summary>False when the home/pause "sound" button has switched sound off.</summary>
    public static bool SoundEnabled => Instance == null || Instance.sfxEnabled;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxEnabled = PlayerPrefs.GetInt(SfxEnabledPrefKey, 1) == 1;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, musicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, sfxVolume));

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.mute = !sfxEnabled;

        if (sfxPrefab == null) sfxPrefab = gameObject.AddComponent<AudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            var src = Instantiate(sfxPrefab, transform);
            src.playOnAwake = false;
            pool.Enqueue(src);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f, bool spatial = false, Transform follow = null)
    {
        if (clip == null || !sfxEnabled) return;
        AudioSource src = pool.Count > 0 ? pool.Dequeue() : Instantiate(sfxPrefab, transform);
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume) * sfxVolume;
        src.pitch = pitch;
        src.spatialBlend = spatial ? 1f : 0f;
        if (follow != null) src.transform.position = follow.position;
        src.Play();
        StartCoroutine(ReturnWhenDone(src));
    }

    IEnumerator ReturnWhenDone(AudioSource src)
    {
        // Wait until the clip really starts before waiting for it to end (avoids returning
        // a source to the pool on the same frame Play() was called).
        yield return null;
        yield return new WaitWhile(() => src != null && src.isPlaying);
        if (src == null) yield break;
        src.clip = null;
        src.spatialBlend = 0f;
        pool.Enqueue(src);
    }

    public void PlayMusic(AudioClip clip, float volume = 1f)
    {
        if (clip == null) { StopMusic(); return; }
        currentMusicClip = clip;
        currentMusicBaseVolume = volume;
        musicSource.clip = clip;
        musicSource.volume = Mathf.Clamp01(volume) * musicVolume;
        musicSource.mute = !sfxEnabled;
        musicSource.Play();
    }

    public void StopMusic()
    {
        currentMusicClip = null;
        if (musicSource != null) musicSource.Stop();
    }

    public void SetMixerVolume(string exposedName, float db) { if (mixer != null) mixer.SetFloat(exposedName, db); }

    // -------------------------
    // Sound on/off (read + driven by UIManager's reflection-based sound button)
    // -------------------------
    public bool IsSoundOn() => sfxEnabled;

    public void ToggleSound(bool on)
    {
        sfxEnabled = on;
        PlayerPrefs.SetInt(SfxEnabledPrefKey, on ? 1 : 0);
        PlayerPrefs.Save();

        if (musicSource != null) musicSource.mute = !sfxEnabled;
        foreach (var src in pool) if (src != null) src.mute = !sfxEnabled;
    }

    public void ToggleSound() => ToggleSound(!sfxEnabled);

    // -------------------------
    // Continuous volume (driven by the Music / SFX sliders)
    // -------------------------
    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, musicVolume);
        if (musicSource != null) musicSource.volume = currentMusicBaseVolume * musicVolume;
        SetMixerVolume(musicVolumeParam, VolumeToDb(musicVolume));
        OnVolumesChanged?.Invoke();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, sfxVolume);
        SetMixerVolume(sfxVolumeParam, VolumeToDb(sfxVolume));
        OnVolumesChanged?.Invoke();
    }

    public float GetMusicVolume() => musicVolume;
    public float GetSfxVolume() => sfxVolume;

    static float VolumeToDb(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;

    void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    void OnApplicationQuit() { PlayerPrefs.Save(); }
}
