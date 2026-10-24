using System.Collections.Generic;
using UnityEngine;

namespace PawCollector
{
    /// <summary>Music, garden ambience and pooled one-shot SFX (clips live in Resources/Audio).</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }
        public static bool MusicOn = true, SfxOn = true, HapticsOn = true;
        AudioSource music, ambience;
        public static string AmbienceClip = "amb_garden";
        readonly List<AudioSource> pool = new();
        readonly Dictionary<string, AudioClip> clips = new();
        int next;

        void Awake()
        {
            I = this;
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.volume = 0.35f; music.playOnAwake = false;
            ambience = gameObject.AddComponent<AudioSource>(); ambience.loop = true; ambience.volume = 0.25f; ambience.playOnAwake = false;
            for (int i = 0; i < 8; i++) { var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; pool.Add(s); }
            foreach (var c in Resources.LoadAll<AudioClip>("Audio")) clips[c.name] = c;
        }

        void Start()
        {
            music.clip = Get("music_cozy"); ambience.clip = Get(AmbienceClip);
            Apply();
        }

        AudioClip Get(string n) => clips.TryGetValue(n, out var c) ? c : null;

        public void Apply()
        {
            if (MusicOn && music.clip && !music.isPlaying) music.Play();
            if (!MusicOn) music.Stop();
            if (SfxOn && ambience.clip && !ambience.isPlaying) ambience.Play();
            if (!SfxOn) ambience.Stop();
        }

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (!SfxOn) return;
            var c = Get(name); if (!c) return;
            var s = pool[next++ % pool.Count];
            s.pitch = pitch; s.PlayOneShot(c, volume);
        }

        public void Meow(float pitch) => Play("sfx_meow" + Random.Range(1, 4), 0.8f, pitch * Random.Range(0.95f, 1.05f));

        public static void Haptic()
        {
#if UNITY_IOS || UNITY_ANDROID
            if (HapticsOn) Handheld.Vibrate();
#endif
        }
    }
}
