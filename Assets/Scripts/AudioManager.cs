using UnityEngine;

/// <summary>
/// Oyun genelinde ses yonetimi. DontDestroyOnLoad ile sahne gecislerinde hayatta kalir.
/// Inspector'dan AudioClip atamazsan, runtime'da basit prosedural bip sesleri uretir.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SFX Clips (Inspector'dan ata, bos birakirsan otomatik uretilir)")]
    public AudioClip sfxButtonClick;
    public AudioClip sfxDiceRoll;
    public AudioClip sfxDiceLand;
    public AudioClip sfxBuy;
    public AudioClip sfxBuild;
    public AudioClip sfxRentPay;
    public AudioClip sfxNotification;
    public AudioClip sfxBankrupt;
    public AudioClip sfxWin;
    public AudioClip sfxTurnChange;
    public AudioClip sfxCoinGain;
    public AudioClip sfxCoinLose;
    public AudioClip sfxError;

    [Header("Muzik")]
    public AudioClip musicMenu;
    public AudioClip musicGame;

    [Header("Ses Seviyeleri")]
    [Range(0f, 1f)] public float sfxVolume = 0.7f;
    [Range(0f, 1f)] public float musicVolume = 0.35f;

    private AudioSource _sfxSource;
    private AudioSource _musicSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;

        GenerateMissingClips();
    }

    // -------------------------------------------------------
    // Public API - SFX
    // -------------------------------------------------------

    public void PlayButtonClick() => PlaySfx(sfxButtonClick);
    public void PlayDiceRoll() => PlaySfx(sfxDiceRoll);
    public void PlayDiceLand() => PlaySfx(sfxDiceLand);
    public void PlayBuy() => PlaySfx(sfxBuy);
    public void PlayBuild() => PlaySfx(sfxBuild);
    public void PlayRentPay() => PlaySfx(sfxRentPay);
    public void PlayNotification() => PlaySfx(sfxNotification);
    public void PlayBankrupt() => PlaySfx(sfxBankrupt);
    public void PlayWin() => PlaySfx(sfxWin);
    public void PlayTurnChange() => PlaySfx(sfxTurnChange);
    public void PlayCoinGain() => PlaySfx(sfxCoinGain);
    public void PlayCoinLose() => PlaySfx(sfxCoinLose);
    public void PlayError() => PlaySfx(sfxError);

    public void PlaySfx(AudioClip clip)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // -------------------------------------------------------
    // Public API - Muzik
    // -------------------------------------------------------

    public void PlayMenuMusic() => PlayMusic(musicMenu);
    public void PlayGameMusic() => PlayMusic(musicGame);

    public void StopMusic()
    {
        if (_musicSource != null) _musicSource.Stop();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (_musicSource == null) return;
        if (clip == null) { StopMusic(); return; }
        if (_musicSource.clip == clip && _musicSource.isPlaying) return;
        _musicSource.clip = clip;
        _musicSource.volume = musicVolume;
        _musicSource.Play();
    }

    public void SetSfxVolume(float vol)
    {
        sfxVolume = Mathf.Clamp01(vol);
    }

    public void SetMusicVolume(float vol)
    {
        musicVolume = Mathf.Clamp01(vol);
        if (_musicSource != null) _musicSource.volume = musicVolume;
    }

    // -------------------------------------------------------
    // Prosedural ses uretimi (Inspector'dan clip atanmazsa fallback)
    // -------------------------------------------------------

    private void GenerateMissingClips()
    {
        if (sfxButtonClick == null) sfxButtonClick = GenTone(800, 0.06f);
        if (sfxDiceRoll == null) sfxDiceRoll = GenNoise(0.4f, 0.3f);
        if (sfxDiceLand == null) sfxDiceLand = GenTone(600, 0.1f);
        if (sfxBuy == null) sfxBuy = GenChord(new[] { 523f, 659f, 784f }, 0.2f);
        if (sfxBuild == null) sfxBuild = GenChord(new[] { 440f, 554f, 659f }, 0.18f);
        if (sfxRentPay == null) sfxRentPay = GenTone(350, 0.15f);
        if (sfxNotification == null) sfxNotification = GenTone(700, 0.08f);
        if (sfxBankrupt == null) sfxBankrupt = GenDescending(500, 200, 0.5f);
        if (sfxWin == null) sfxWin = GenChord(new[] { 523f, 659f, 784f, 1047f }, 0.6f);
        if (sfxTurnChange == null) sfxTurnChange = GenTone(500, 0.05f);
        if (sfxCoinGain == null) sfxCoinGain = GenAscending(600, 900, 0.12f);
        if (sfxCoinLose == null) sfxCoinLose = GenDescending(500, 300, 0.12f);
        if (sfxError == null) sfxError = GenTone(200, 0.2f);
    }

    private static AudioClip GenTone(float freq, float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var clip = AudioClip.Create("tone", samples, 1, sampleRate, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / samples;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenNoise(float duration, float vol)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var clip = AudioClip.Create("noise", samples, 1, sampleRate, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float envelope = 1f - (float)i / samples;
            data[i] = Random.Range(-1f, 1f) * envelope * vol;
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenAscending(float freqStart, float freqEnd, float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var clip = AudioClip.Create("asc", samples, 1, sampleRate, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float p = (float)i / samples;
            float freq = Mathf.Lerp(freqStart, freqEnd, p);
            float envelope = 1f - p;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenDescending(float freqStart, float freqEnd, float duration)
    {
        return GenAscending(freqStart, freqEnd, duration);
    }

    private static AudioClip GenChord(float[] freqs, float duration)
    {
        int sampleRate = 44100;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        var clip = AudioClip.Create("chord", samples, 1, sampleRate, false);
        var data = new float[samples];
        float amp = 0.4f / freqs.Length;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = 1f - (float)i / samples;
            float val = 0f;
            for (int f = 0; f < freqs.Length; f++)
                val += Mathf.Sin(2f * Mathf.PI * freqs[f] * t);
            data[i] = val * amp * envelope;
        }
        clip.SetData(data, 0);
        return clip;
    }
}
