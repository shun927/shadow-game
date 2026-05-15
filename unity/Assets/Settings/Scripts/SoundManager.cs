using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Start Button")]
    [SerializeField] private AudioClip startButtonHoldClip;
    [SerializeField] private float startButtonHoldVolume = 1f;

    [Header("Countdown")]
    [SerializeField] private AudioClip countdownTickClip;
    [SerializeField] private float countdownTickVolume = 1f;

    [Header("BGM")]
    [SerializeField] private AudioClip gameplayBgmClip;
    [SerializeField] private AudioClip resultBgmClip;
    [SerializeField] private float gameplayBgmVolume = 0.6f;
    [SerializeField] private float resultBgmVolume = 0.6f;

    private AudioSource sfxSource;
    private AudioSource startButtonHoldSource;
    private AudioSource gameplayBgmSource;
    private AudioSource resultBgmSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureAudioSources();
    }

    public static SoundManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        SoundManager existing = FindFirstObjectByType<SoundManager>();
        if (existing != null)
            return existing;

        GameObject soundManagerObject = new GameObject("SoundManager");
        return soundManagerObject.AddComponent<SoundManager>();
    }

    public void PlayStartButtonHold(float progress)
    {
        if (startButtonHoldClip == null)
            return;

        EnsureAudioSources();

        float normalizedProgress = Mathf.Clamp01(progress);
        startButtonHoldSource.clip = startButtonHoldClip;
        startButtonHoldSource.loop = false;
        startButtonHoldSource.volume = startButtonHoldVolume;
        startButtonHoldSource.timeSamples = GetStartButtonHoldSample(normalizedProgress);

        if (!startButtonHoldSource.isPlaying)
            startButtonHoldSource.Play();
    }

    public void StopStartButtonHold()
    {
        if (startButtonHoldSource == null)
            return;

        startButtonHoldSource.Stop();
        startButtonHoldSource.timeSamples = 0;
    }

    public void PlayCountdownTick()
    {
        if (countdownTickClip == null)
            return;

        EnsureAudioSources();
        sfxSource.PlayOneShot(countdownTickClip, countdownTickVolume);
    }

    public void PlayGameplayBgm()
    {
        PlayBgm(ref gameplayBgmSource, "Gameplay BGM Source", gameplayBgmClip, gameplayBgmVolume);

        if (resultBgmSource != null)
            resultBgmSource.Stop();
    }

    public void PlayResultBgm()
    {
        PlayBgm(ref resultBgmSource, "Result BGM Source", resultBgmClip, resultBgmVolume);

        if (gameplayBgmSource != null)
            gameplayBgmSource.Stop();
    }

    public void StopBgms()
    {
        if (gameplayBgmSource != null)
            gameplayBgmSource.Stop();

        if (resultBgmSource != null)
            resultBgmSource.Stop();
    }

    public void StopAllManagedSounds()
    {
        StopStartButtonHold();
        StopBgms();

        if (sfxSource != null)
            sfxSource.Stop();
    }

    private void PlayBgm(ref AudioSource source, string sourceName, AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        source = EnsureAudioSource(source, sourceName, true);

        source.clip = clip;
        source.loop = true;
        source.volume = volume;

        if (!source.isPlaying)
            source.Play();
    }

    private void EnsureAudioSources()
    {
        sfxSource = EnsureAudioSource(sfxSource, "SFX Source", false);
        startButtonHoldSource = EnsureAudioSource(startButtonHoldSource, "Start Button Hold Source", true);
        gameplayBgmSource = EnsureAudioSource(gameplayBgmSource, "Gameplay BGM Source", true);
        resultBgmSource = EnsureAudioSource(resultBgmSource, "Result BGM Source", true);
    }

    private AudioSource EnsureAudioSource(AudioSource source, string sourceName, bool loop)
    {
        if (source != null)
            return source;

        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource createdSource = sourceObject.AddComponent<AudioSource>();
        createdSource.playOnAwake = false;
        createdSource.loop = loop;
        return createdSource;
    }

    private int GetStartButtonHoldSample(float progress)
    {
        if (startButtonHoldClip == null || startButtonHoldClip.samples <= 1)
            return 0;

        return Mathf.Clamp(
            Mathf.RoundToInt(progress * (startButtonHoldClip.samples - 1)),
            0,
            startButtonHoldClip.samples - 1);
    }
}
