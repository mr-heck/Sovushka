// GameSoundLibrary.cs
// Central place to assign every sound clip used in the game. Lives on the "Sound manager"
// GameObject alongside AudioManager. Drop AudioClip assets into these fields in the
// Inspector; every gameplay script that needs a sound looks it up here (with per-instance
// overrides still allowed where they already exist, e.g. Collectable.pickupSound).
//
// To add a new sound: add a field here (in the right section), then call
// GameSoundLibrary.Instance.<field> from the relevant script via AudioManager.Instance.PlaySFX(...).
using UnityEngine;

public class GameSoundLibrary : MonoBehaviour
{
    public static GameSoundLibrary Instance { get; private set; }

    [Header("Collecting (Wood / Gem / Food)")]
    [Tooltip("Played when a Wood collectable is picked up, unless that item has its own override clip.")]
    public AudioClip collectWood;
    [Tooltip("Played when a Gem collectable is picked up, unless that item has its own override clip.")]
    public AudioClip collectGem;
    [Tooltip("Played when a Food collectable is picked up, unless that item has its own override clip.")]
    public AudioClip collectFood;
    [Tooltip("Played when the player releases a carried item via the Release button.")]
    public AudioClip releaseItem;

    [Header("Yanam - Arrival")]
    [Tooltip("Looping sound while Yanam flies in toward the landing point.")]
    public AudioClip yanamFlyingLoop;
    [Tooltip("One-shot played the moment Yanam touches down at the landing point.")]
    public AudioClip yanamLanding;
    [Tooltip("Played as Yanam opens up / reveals Sovushka after landing.")]
    public AudioClip yanamOpening;
    [Tooltip("Played the moment Sovushka spawns out of Yanam.")]
    public AudioClip sovushkaSpawn;

    [Header("Yanam - Departure (ending sequence)")]
    [Tooltip("Played as Yanam closes up / crouches down, preparing to fly away at the end.")]
    public AudioClip yanamPreparingToFly;
    [Tooltip("Looping sound while Yanam flies away at the end of the game.")]
    public AudioClip yanamFlyingAwayLoop;

    [Header("Generator")]
    public AudioClip generatorSpawn;
    [Tooltip("Looping sound while the generator burns wood.")]
    public AudioClip generatorBurningLoop;
    public AudioClip generatorPackup;

    [Header("Mr. Heck")]
    public AudioClip mrHeckShot;
    public AudioClip mrHeckVanish;
    public AudioClip mrHeckRespawn;
    public AudioClip mrHeckEat;

    [Header("UI")]
    public AudioClip buttonClick;
    public AudioClip panelOpen;
    public AudioClip panelClose;

    [Header("Music")]
    public AudioClip backgroundMusic;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    /// <summary>Category-based fallback lookup used by Collectable when it has no per-item override.</summary>
    public AudioClip GetCollectSound(CollectableCategory category)
    {
        switch (category)
        {
            case CollectableCategory.Wood: return collectWood;
            case CollectableCategory.Gem: return collectGem;
            case CollectableCategory.Food: return collectFood;
            default: return null;
        }
    }
}
