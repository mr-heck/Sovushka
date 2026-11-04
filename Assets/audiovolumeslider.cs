using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Put this on a UI Slider to make it control the game's Music or SFX volume.
/// The home-screen and pause-panel sliders for the same channel stay in sync, and the
/// value is saved (via AudioManager / PlayerPrefs) so it is remembered between sessions.
/// </summary>
[RequireComponent(typeof(Slider))]
[DisallowMultipleComponent]
public class AudioVolumeSlider : MonoBehaviour, IPointerUpHandler
{
    public enum Channel { Music, Sfx }

    public Channel channel = Channel.Music;
    [Tooltip("Play a short click when the slider is released on the SFX channel, so the new level can be heard.")]
    public bool previewOnRelease = true;

    Slider _slider;
    AudioManager _hooked;

    string PrefKey => channel == Channel.Music ? AudioManager.MusicVolumePrefKey : AudioManager.SfxVolumePrefKey;

    void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.minValue = 0f;
        _slider.maxValue = 1f;
        _slider.wholeNumbers = false;
    }

    void OnEnable()
    {
        _slider.onValueChanged.AddListener(OnSliderChanged);
        Hook();
        Refresh();
    }

    void Start()
    {
        // AudioManager may not have existed yet during OnEnable on the very first frame.
        Hook();
        Refresh();
    }

    void OnDisable()
    {
        _slider.onValueChanged.RemoveListener(OnSliderChanged);
        if (_hooked != null) _hooked.OnVolumesChanged -= Refresh;
        _hooked = null;
    }

    void Hook()
    {
        if (_hooked != null || AudioManager.Instance == null) return;
        _hooked = AudioManager.Instance;
        _hooked.OnVolumesChanged += Refresh;
    }

    float CurrentVolume()
    {
        var am = AudioManager.Instance;
        if (am != null) return channel == Channel.Music ? am.GetMusicVolume() : am.GetSfxVolume();
        return PlayerPrefs.GetFloat(PrefKey, 1f);
    }

    // Move the handle to the real volume WITHOUT re-triggering OnSliderChanged.
    void Refresh()
    {
        if (_slider == null) return;
        _slider.SetValueWithoutNotify(Mathf.Clamp01(CurrentVolume()));
    }

    void OnSliderChanged(float value)
    {
        var am = AudioManager.Instance;
        if (am != null)
        {
            if (channel == Channel.Music) am.SetMusicVolume(value);
            else am.SetSfxVolume(value);
        }
        else
        {
            PlayerPrefs.SetFloat(PrefKey, Mathf.Clamp01(value));
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!previewOnRelease || channel != Channel.Sfx) return;
        var am = AudioManager.Instance;
        var lib = GameSoundLibrary.Instance;
        if (am != null && lib != null && lib.buttonClick != null) am.PlaySFX(lib.buttonClick);
    }
}
