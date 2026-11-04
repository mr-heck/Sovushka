// AnimationSoundReceiver.cs
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AnimationSoundReceiver : MonoBehaviour
{
    [System.Serializable]
    public struct SoundEntry
    {
        public string key;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume;
        public bool spatial;
        public bool loop;
    }

    [Tooltip("List of sounds keyed by name. Use the same keys in code or Animation Events.")]
    public SoundEntry[] sounds;

    [Tooltip("If true, use a local AudioSource for looping sounds.")]
    public bool useLocalSourceForLoop = true;

    AudioSource localSource;

    void Awake()
    {
        if (useLocalSourceForLoop)
        {
            localSource = GetComponent<AudioSource>();
            if (localSource == null) localSource = gameObject.AddComponent<AudioSource>();
            localSource.playOnAwake = false;
            localSource.loop = false;
            localSource.spatialBlend = 1f;
        }
    }

    public void PlaySound(string key)
    {
        var e = FindEntry(key);
        if (e.clip == null) return;

        if (e.loop && useLocalSourceForLoop)
        {
            if (localSource == null) return;
            localSource.clip = e.clip;
            localSource.volume = e.volume * AudioManager.SfxScale;
            localSource.loop = true;
            localSource.spatialBlend = e.spatial ? 1f : 0f;
            localSource.Play();
            return;
        }

        if (TryPlayViaAudioManager(e)) return;

        if (localSource != null)
        {
            localSource.spatialBlend = e.spatial ? 1f : 0f;
            localSource.PlayOneShot(e.clip, e.volume * AudioManager.SfxScale);
            return;
        }

        AudioSource.PlayClipAtPoint(e.clip, transform.position, e.volume * AudioManager.SfxScale);
    }

    public void StopSound(string key)
    {
        var e = FindEntry(key);
        if (e.clip == null) return;

        if (useLocalSourceForLoop && localSource != null && localSource.isPlaying && localSource.clip == e.clip)
        {
            localSource.Stop();
            localSource.clip = null;
        }
    }

    public void PlayDirectionSound(int dir) => PlaySound("dir" + dir);
    public void StopDirectionSound(int dir) => StopSound("dir" + dir);

    SoundEntry FindEntry(string key)
    {
        if (sounds == null) return new SoundEntry { key = key, clip = null, volume = 1f, spatial = false, loop = false };
        foreach (var s in sounds) if (s.key == key) return s;
        return new SoundEntry { key = key, clip = null, volume = 1f, spatial = false, loop = false };
    }

    bool TryPlayViaAudioManager(SoundEntry e)
    {
        // Safe call to AudioManager if present (no compile-time dependency)
        var am = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (var asm in am)
        {
            var t = asm.GetType("AudioManager", false, true);
            if (t == null) continue;
            var prop = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase);
            if (prop == null) continue;
            var inst = prop.GetValue(null);
            if (inst == null) continue;
            var playMethod = t.GetMethod("PlaySFX", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (playMethod == null) continue;
            try
            {
                playMethod.Invoke(inst, new object[] { e.clip, e.volume, 1f, e.spatial, e.spatial ? transform : null });
                return true;
            }
            catch { return false; }
        }
        return false;
    }
}
