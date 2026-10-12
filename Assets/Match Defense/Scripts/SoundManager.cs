using UnityEngine;
using System.Collections.Generic;

namespace MatchDefense
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("<color=yellow>Clips</color>")]
        [SerializeField] private AudioClip bgmClip;

        [Header("<color=yellow>Audio Sources</color>")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private int initialSfxPoolSize = 5;

        private List<AudioSource> sfxPool = new List<AudioSource>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                    bgmSource.loop = true;
                    bgmSource.playOnAwake = false;
                }

                // SFX 풀 초기화
                for (int i = 0; i < initialSfxPoolSize; i++)
                {
                    CreateNewSfxSource();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            PlayBGM(bgmClip, 0.4f);
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
            foreach (var source in sfxPool)
            {
                if (!source.isPlaying) return source;
            }
            
            return CreateNewSfxSource();
        }

        public void PlayBGM(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            bgmSource.clip = clip;
            bgmSource.volume = volume;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            bgmSource.Stop();
        }

        public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            
            AudioSource source = GetAvailableSfxSource();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        public void PlayRandomSFX(AudioClip[] clips, float volume = 1f, float pitch = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            int randomIndex = Random.Range(0, clips.Length);
            PlaySFX(clips[randomIndex], volume, pitch);
        }
    }
}