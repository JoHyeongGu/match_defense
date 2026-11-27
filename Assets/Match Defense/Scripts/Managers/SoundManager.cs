using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace MatchDefense
{
    public enum SfxType
    {
        ButtonClick,
        TabSwitch,
        MatchPop,
        UnitPlace,
        EnemyDie,
        CoinTick,
        QuestClaim,
        GachaDrumroll,
        GachaReveal,
        UpgradeSuccess,
        StageClear,
        GameOver
    }

    [Serializable]
    public class SfxEntry
    {
        public SfxType type;
        public AudioClip[] clips;
        [Range(0f, 1.5f)] public float volumeScale = 1f;
    }

    [Serializable]
    public class SceneBgmEntry
    {
        [Tooltip("Scene name (e.g., Lobby, Stage_01). Leave empty for default BGM")]
        public string sceneName;
        public AudioClip bgmClip;
        [Range(0f, 1f)] public float volume = 0.4f;
    }

    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("<color=yellow>BGM (Scene Auto-Switch)</color>")]
        [SerializeField] private AudioClip defaultBgmClip;
        [SerializeField] private float defaultBgmVolume = 0.4f;
        [SerializeField] private float bgmCrossFadeDuration = 0.5f;
        [SerializeField] private List<SceneBgmEntry> sceneBgms = new List<SceneBgmEntry>();

        [Header("<color=yellow>SFX Table (Enum-Based)</color>")]
        [SerializeField] private List<SfxEntry> sfxTable = new List<SfxEntry>();

        [Header("<color=yellow>Audio Sources</color>")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private int initialSfxPoolSize = 8;

        private readonly Dictionary<SfxType, SfxEntry> sfxLookup = new Dictionary<SfxType, SfxEntry>();
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();

        private float masterBgmVolume = 1f;
        private float masterSfxVolume = 1f;
        private float currentBgmBaseVolume = 0.4f;
        private Tween bgmFadeTween;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent == null)
                    DontDestroyOnLoad(gameObject);

                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                    bgmSource.loop = true;
                    bgmSource.playOnAwake = false;
                }

                BuildSfxLookup();
                for (int i = 0; i < initialSfxPoolSize; i++)
                    CreateNewSfxSource();

                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                bgmFadeTween?.Kill();
                Instance = null;
            }
        }

        private void Start()
        {
            if (ProgramManager.Instance != null)
            {
                masterBgmVolume = ProgramManager.Instance.BgmVolume;
                masterSfxVolume = ProgramManager.Instance.SfxVolume;
            }

            PlayBgmForScene(SceneManager.GetActiveScene().name);
        }

        private void BuildSfxLookup()
        {
            sfxLookup.Clear();
            if (sfxTable == null) return;
            for (int i = 0; i < sfxTable.Count; i++)
            {
                if (sfxTable[i] != null)
                    sfxLookup[sfxTable[i].type] = sfxTable[i];
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PlayBgmForScene(scene.name);
        }

        private void PlayBgmForScene(string sceneName)
        {
            AudioClip targetClip = defaultBgmClip;
            float targetVol = defaultBgmVolume;

            if (sceneBgms != null)
            {
                for (int i = 0; i < sceneBgms.Count; i++)
                {
                    if (sceneBgms[i] != null && sceneBgms[i].bgmClip != null &&
                        string.Equals(sceneBgms[i].sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetClip = sceneBgms[i].bgmClip;
                        targetVol = sceneBgms[i].volume;
                        break;
                    }
                }
            }

            if (targetClip == null) return;
            if (bgmSource.isPlaying && bgmSource.clip == targetClip) return;

            PlayBGM(targetClip, targetVol);
        }

        private AudioSource CreateNewSfxSource()
        {
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            newSource.loop = false;
            newSource.playOnAwake = false;
            sfxPool.Add(newSource);
            return newSource;
        }

        private AudioSource GetAvailableSfxSource()
        {
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying) return sfxPool[i];
            }
            return CreateNewSfxSource();
        }

        public void SetMasterBgmVolume(float volume)
        {
            masterBgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
                bgmSource.volume = currentBgmBaseVolume * masterBgmVolume;
        }

        public void SetMasterSfxVolume(float volume)
        {
            masterSfxVolume = Mathf.Clamp01(volume);
        }

        public void PlayBGM(AudioClip clip, float volume = 0.4f)
        {
            if (clip == null || bgmSource == null) return;

            currentBgmBaseVolume = volume;
            float targetVol = currentBgmBaseVolume * masterBgmVolume;
            bgmFadeTween?.Kill();

            if (!bgmSource.isPlaying || bgmCrossFadeDuration <= 0.05f)
            {
                bgmSource.clip = clip;
                bgmSource.volume = targetVol;
                bgmSource.Play();
                return;
            }

            bgmFadeTween = DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, 0f, bgmCrossFadeDuration * 0.5f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    bgmSource.clip = clip;
                    bgmSource.Play();
                    bgmFadeTween = DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, currentBgmBaseVolume * masterBgmVolume, bgmCrossFadeDuration * 0.5f)
                        .SetUpdate(true);
                });
        }

        public void PlayBGMImmediate(AudioClip clip, float volume = 0.4f)
        {
            if (clip == null || bgmSource == null) return;
            bgmFadeTween?.Kill();
            currentBgmBaseVolume = volume;
            bgmSource.Stop();
            bgmSource.clip = clip;
            bgmSource.volume = currentBgmBaseVolume * masterBgmVolume;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            bgmFadeTween?.Kill();
            if (bgmSource != null) bgmSource.Stop();
        }

        public void FadeOutBGM(float duration = 0.5f)
        {
            if (bgmSource == null || !bgmSource.isPlaying) return;
            bgmFadeTween?.Kill();
            if (duration <= 0.01f)
            {
                bgmSource.Stop();
                return;
            }
            bgmFadeTween = DOTween.To(() => bgmSource.volume, v => bgmSource.volume = v, 0f, duration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (bgmSource != null) bgmSource.Stop();
                });
        }

        public void PauseBGM()
        {
            if (bgmSource != null && bgmSource.isPlaying) bgmSource.Pause();
        }

        public void ResumeBGM()
        {
            if (bgmSource != null) bgmSource.UnPause();
        }

        public void PlaySFX(SfxType type, float pitch = 1f, float volumeMultiplier = 1f)
        {
            if (!sfxLookup.TryGetValue(type, out SfxEntry entry)) return;
            if (entry.clips == null || entry.clips.Length == 0) return;

            AudioClip clip = entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)];
            PlaySFX(clip, entry.volumeScale * volumeMultiplier, pitch);
        }

        public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetAvailableSfxSource();
            source.clip = clip;
            source.volume = volume * masterSfxVolume;
            source.pitch = pitch;
            source.Play();
        }

        public void PlayMatchComboSFX(int matchCount)
        {
            StartCoroutine(MatchComboRoutine(Mathf.Max(1, matchCount)));
        }

        private IEnumerator MatchComboRoutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                PlaySFX(SfxType.MatchPop, pitch: 1f + (i * 0.08f));
                if (i < count - 1)
                {
                    for (float t = 0f; t < 0.05f; t += Time.unscaledDeltaTime)
                        yield return null;
                }
            }
        }
    }
}
