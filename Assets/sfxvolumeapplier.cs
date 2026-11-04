using UnityEngine;

/// <summary>
/// Makes a looping / directly-played AudioSource obey the SFX volume slider and the
/// sound on/off button. (One-shot sounds already go through AudioManager.PlaySFX, which
/// applies both; this covers sources that play on their own, like the Yanam and generator loops.)
/// </summary>
[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class SfxVolumeApplier : MonoBehaviour
{
    AudioSource _src;
    float _baseVolume = 1f;

    void Awake()
    {
        _src = GetComponent<AudioSource>();
        _baseVolume = _src.volume;
    }

    void Update()
    {
        if (_src == null) return;
        _src.volume = _baseVolume * AudioManager.SfxScale;
        _src.mute = !AudioManager.SoundEnabled;
    }
}
