using System.Collections.Generic;
using Musketeers.Data;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Every sound in the game goes through here: SFX from a small pool with ±5% pitch, two voice channels
    // (announcer, characters) that never pile up, and two music sources that crossfade between screens.
    // Sounds are looked up by file name in the AudioLibrary; a missing sound is silently skipped, so the
    // game works without any audio. Volumes (Music / SFX / Voice) live in PlayerPrefs.
    public sealed class AudioService : MonoBehaviour
    {
        public const string MuteKey = "Musketeers.Volume.Muted";
        public bool Muted { get; private set; }

        public const string MusicKey = "Musketeers.Volume.Music";
        public const string SfxKey = "Musketeers.Volume.Sfx";
        public const string VoiceKey = "Musketeers.Volume.Voice";
        const int Pool = 10;
        const float PitchJitter = 0.05f;
        const float RepeatGuard = 0.045f;    // the same key twice within this window plays once

        [SerializeField] private AudioLibrary _library;

        public static AudioService Instance { get; private set; }

        AudioSource[] _sfx;
        AudioSource _announcer, _character;
        AudioSource _musicA, _musicB;
        bool _aIsCurrent = true;
        string _musicKey;
        float _fade = 1f, _fadeTime = 1f;
        int _next;
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();

        const float RampSeconds = 0.15f;     // full 0..1 sweep of an applied volume (no zipper noise on slider drags)

        // Target (slider) values. What is actually applied ramps toward these in Update.
        public float MusicVolume { get; private set; } = 0.48f;
        public float SfxVolume { get; private set; } = 0.8f;
        public float VoiceVolume { get; private set; } = 0.72f;
        float _musicApplied, _sfxApplied, _voiceApplied;
        public string CurrentMusic => _musicKey;

        void Awake()
        {
            Instance = this;
            _sfx = new AudioSource[Pool];
            for (int i = 0; i < Pool; i++)
                _sfx[i] = Source("Sfx" + i, false);
            _announcer = Source("Announcer", false);
            _character = Source("CharacterVoice", false);
            _musicA = Source("MusicA", true);
            _musicB = Source("MusicB", true);
            MusicVolume = Read(MusicKey, MusicVolume);
            SfxVolume = Read(SfxKey, SfxVolume);
            VoiceVolume = Read(VoiceKey, VoiceVolume);
            _musicApplied = MusicVolume;
            _sfxApplied = SfxVolume;
            _voiceApplied = VoiceVolume;
            SetMuted(PlayerPrefs.GetInt(MuteKey, 0) != 0, false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        static float Read(string key, float fallback)
        {
            try { return Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback)); }
            catch { return fallback; }
        }

        // ---------- settings ----------

        // Applies at once; save = write PlayerPrefs too (Settings saves once on close, not per slider tick:
        // on Web every Save writes IndexedDB).
        public void SetVolumes(float music, float sfx, float voice, bool save = true)
        {
            MusicVolume = Mathf.Clamp01(music);
            SfxVolume = Mathf.Clamp01(sfx);
            VoiceVolume = Mathf.Clamp01(voice);
            if (save)
                SaveVolumes();
        }

        // Muting sources preserves the three independently saved slider levels.
        public void SetMuted(bool muted, bool save = true)
        {
            Muted = muted;
            if (_sfx != null) foreach (var source in _sfx) source.mute = muted;
            if (_musicA != null) _musicA.mute = muted;
            if (_musicB != null) _musicB.mute = muted;
            if (_announcer != null) _announcer.mute = muted;
            if (_character != null) _character.mute = muted;
            if (save) SaveVolumes();
        }

        public void SaveVolumes()
        {
            PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            PlayerPrefs.SetFloat(VoiceKey, VoiceVolume);
            PlayerPrefs.Save();
        }

        // ---------- playback ----------

        // "SFX/actions/musket_fire" picks one of its variants. volume is relative (0..1).
        public void Sfx(string key, float volume = 1f)
        {
            if (_library == null || _sfxApplied <= 0f || Repeated(key))
                return;
            var clip = _library.Pick(key);
            if (clip == null)
                return;
            var s = _sfx[_next];
            _next = (_next + 1) % _sfx.Length;
            s.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
            s.PlayOneShot(clip, _sfxApplied * volume);
        }

        // Announcer lines replace each other; they never stack.
        public void Announce(string clip)
        {
            if (string.IsNullOrEmpty(clip))
                return;
            PlayVoice(_announcer, _library != null ? _library.Pick("Voice/announcer/" + clip) : null);
        }

        // A musketeer's own line ("hurt", "death", "victory", "taunt"): Voice/<character>/<line>.
        public void Line(string characterId, string line, int variant = -1)
        {
            if (_library == null || string.IsNullOrEmpty(characterId))
                return;
            string key = $"Voice/{characterId}/{line}";
            var clip = variant >= 0 ? _library.Variant(key, variant) : _library.Pick(key);
            PlayVoice(_character, clip);
        }

        void PlayVoice(AudioSource channel, AudioClip clip)
        {
            if (clip == null || (VoiceVolume <= 0f && _voiceApplied <= 0f))
                return;
            channel.Stop();
            channel.clip = clip;
            channel.volume = _voiceApplied;
            channel.pitch = 1f;
            channel.Play();
        }

        bool Repeated(string key)
        {
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(key, out float t) && now - t < RepeatGuard)
                return true;
            _lastPlayed[key] = now;
            return false;
        }

        // Crossfades to a looping track ("Music/battle"); the same track keeps playing.
        public void Music(string key, float fadeSeconds = 1.2f)
        {
            if (_library == null || key == _musicKey)
                return;
            var clip = _library.Pick(key);
            if (clip == null)
                return;
            _musicKey = key;
            // A change while a crossfade is running: keep the louder of the two as the track fading out
            // and drop the quieter one, so nothing jumps to full volume or cuts out.
            if (_fade < 1f && _fade < 0.5f)
                (_aIsCurrent ? _musicA : _musicB).Stop();
            else
            {
                if (_fade < 1f)
                    (_aIsCurrent ? _musicB : _musicA).Stop();
                _aIsCurrent = !_aIsCurrent;
            }
            var cur = _aIsCurrent ? _musicA : _musicB;
            cur.clip = clip;
            cur.volume = 0f;
            cur.Play();
            _fade = 0f;
            _fadeTime = Mathf.Max(0.05f, fadeSeconds);
        }

        public void StopMusic(float fadeSeconds = 1f)
        {
            _musicKey = null;
            _aIsCurrent = !_aIsCurrent;
            (_aIsCurrent ? _musicA : _musicB).Stop();
            _fade = 0f;
            _fadeTime = Mathf.Max(0.05f, fadeSeconds);
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / RampSeconds;
            _musicApplied = Mathf.MoveTowards(_musicApplied, MusicVolume, step);
            _sfxApplied = Mathf.MoveTowards(_sfxApplied, SfxVolume, step);
            _voiceApplied = Mathf.MoveTowards(_voiceApplied, VoiceVolume, step);
            _announcer.volume = _voiceApplied;
            _character.volume = _voiceApplied;
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / _fadeTime);
                if (_fade >= 1f)
                    (_aIsCurrent ? _musicB : _musicA).Stop();
            }
            ApplyMusicVolume();
        }

        void ApplyMusicVolume()
        {
            var cur = _aIsCurrent ? _musicA : _musicB;
            var old = _aIsCurrent ? _musicB : _musicA;
            cur.volume = _musicApplied * _fade;
            old.volume = _musicApplied * (1f - _fade);
        }
    }
}
