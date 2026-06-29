using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    [System.Serializable]
    public class AudioGuide
    {
        public string id;
        public string language;
        public bool listened = true;
        public AudioClip clip;
    }

    [Header("Audioguias")]
    [SerializeField] public bool audioGuideEnabled = true;
    [SerializeField] private List<AudioGuide> audioGuides = new List<AudioGuide>();

    public static SoundManager instance;
    [SerializeField] private Sprite soundImage;
    [SerializeField] private Sprite noSoundImage;
    [SerializeField] private Sprite musicImage;
    [SerializeField] private Sprite noMusicImage;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private AudioSource soundObject;

    [Header("MUSA Voice — assign the AudioSource on MUSA's avatar")]
    [SerializeField] public AudioSource musaVoiceSource; // Fixed — assign this to SpeechBlend too

    public static bool soundOn = true;
    [SerializeField] private AudioSource musicObject;

    // Referencia al AudioSource de la audioguia activa
    private AudioSource activeGuideSource;
    private Coroutine activeGuideCoroutine;
    private bool isGuidePlaying = false;

    public AudioClip lastClipPlayed;
    public bool lastClipWasAgent = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void MakeSound(AudioClip audioClip, float volume)
    {
        if (soundOn)
        {
            AudioSource audioSource = Instantiate(soundObject, transform.position, Quaternion.identity);
            audioSource.clip = audioClip;
            audioSource.volume = volume;
            audioSource.Play();
            Destroy(audioSource.gameObject, audioClip.length + 0.1f);
        }
    }

    public void ToggleSound()
    {
        soundOn = !soundOn;
        soundButton.GetComponent<Image>().sprite = soundOn ? soundImage : noSoundImage;
    }

    public void ToggleMusic()
    {
        if (musicObject.isPlaying)
        {
            musicObject.Pause();
            musicButton.GetComponent<Image>().sprite = noMusicImage;
        }
        else
        {
            musicObject.Play();
            musicButton.GetComponent<Image>().sprite = musicImage;
        }
    }

    [Header("Ducking")]
    [SerializeField] [Range(0f, 1f)] private float duckVolume = 0.15f;
    private float _normalVolume = -1f;

    public void DuckMusic()
    {
        if (musicObject == null) return;
        if (_normalVolume < 0f) _normalVolume = musicObject.volume;
        musicObject.volume = duckVolume;
    }

    public void RestoreMusic()
    {
        if (musicObject == null || _normalVolume < 0f) return;
        musicObject.volume = _normalVolume;
    }

    public void PlayAudioguide(string id, string lang = "en")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("SoundManager: el id de la audioguia está vacío.");
            return;
        }

        bool alreadyListened = audioGuides.Exists(g =>
            g.id == id && g.language == GetLanguage(lang) && g.listened
        );

        // Si ya se escuchó, no reproducir automáticamente.
        // Solo mostrar botón si seguimos dentro de una sala.
        if (alreadyListened)
        {
            if (moving_script.instance.exhibitionName != "")
            {
                lastClipWasAgent = false;
                moving_script.instance.audioGuide.SetActive(true);
                moving_script.instance.stopAudioGuide.SetActive(false);
            }

            return;
        }

        StartGuide(id, lang, markAsListened: true);
        lastClipWasAgent = false;
    }

    public void PlayGuide()
    {
        string id = moving_script.instance.exhibitionName;

        if (string.IsNullOrWhiteSpace(id)) return;

        if (lastClipWasAgent && lastClipPlayed != null)
        {
            // Reproducir la última audioguía del agente
            PlayClip(lastClipPlayed);
        }
        else
        {
            // Comportamiento original: buscar en Resources
            StartGuide(id, "en", markAsListened: true);
        }
    }

    private void StartGuide(string id, string lang, bool markAsListened)
    {
        if (isGuidePlaying)
        {
            Debug.Log("SoundManager: ya hay una audioguía reproduciéndose.");
            return;
        }
        activeGuideCoroutine = StartCoroutine(PlayAudioguideRoutine(id, lang, markAsListened));
    }

    private IEnumerator PlayAudioguideRoutine(string id, string lang, bool markAsListened)
    {
        string language = GetLanguage(lang);
        string path = $"Guide/{language}/{id}";

        AudioClip clip = Resources.Load<AudioClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"SoundManager: no existe una audioguía con idioma '{language}' e id '{id}'.");
            yield break;
        }

        if (markAsListened && !audioGuides.Exists(g => g.id == id && g.language == language))
        {
            audioGuides.Add(new AudioGuide
            {
                id = id,
                language = language,
                listened = true,
                clip = clip
            });
        }

        isGuidePlaying = true;

        moving_script.instance.audioGuide.SetActive(false);
        moving_script.instance.stopAudioGuide.SetActive(true);

        activeGuideSource = musaVoiceSource;
        activeGuideSource.clip = clip;
        activeGuideSource.volume = 1f;

        MUSEBorder.instance.speakingAudioSource = activeGuideSource;
        MUSEBorder.instance.SetState(MUSEBorder.MuseState.Speaking);
        AnimationTransitioner.instance.SetTalking(true);

        activeGuideSource.Play();

        DuckMusic();

        yield return new WaitForSeconds(clip.length);

        FinishAudioguide();
    }

    public void StopAudioguide()
    {
        if (activeGuideCoroutine != null)
        {
            StopCoroutine(activeGuideCoroutine);
            activeGuideCoroutine = null;
        }

        FinishAudioguide();
        AnimationTransitioner.instance.SetTalking(false);
    }

    private void FinishAudioguide()
    {
        isGuidePlaying = false;

        if (activeGuideSource != null)
        {
            activeGuideSource.Stop();
            activeGuideSource.clip = null;
            activeGuideSource = null;
        }

        RestoreMusic();

        MUSEBorder.instance.SetState(MUSEBorder.MuseState.Idle);
        AnimationTransitioner.instance.SetTalking(false);

        moving_script.instance.stopAudioGuide.SetActive(false);

        // Solo mostrar botón si seguimos dentro de una sala
        if (moving_script.instance.exhibitionName != "")
        {
            moving_script.instance.audioGuide.SetActive(true);
        }
        else
        {
            moving_script.instance.audioGuide.SetActive(false);
        }
    }

    /// <summary>
    /// Reproduce un AudioClip ya cargado activando MUSEBorder (Speaking → Idle).
    /// Interrumpe cualquier audioguía en curso antes de empezar.
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        // Parar lo que hubiera reproduciendo
        StopAudioguide();

        activeGuideCoroutine = StartCoroutine(PlayClipRoutine(clip));
    }

    private IEnumerator PlayClipRoutine(AudioClip clip)
    {
        isGuidePlaying = true;

        activeGuideSource         = musaVoiceSource;
        activeGuideSource.clip    = clip;
        activeGuideSource.volume  = 1f;

        MUSEBorder.instance.speakingAudioSource = activeGuideSource;
        MUSEBorder.instance.SetState(MUSEBorder.MuseState.Speaking);

        DuckMusic();

        activeGuideSource.Play();
        yield return new WaitForSeconds(clip.length);

        FinishAudioguide();
    }

    private string GetLanguage(string fallback = "en")
    {
        if (LanguageScript.instance == null || string.IsNullOrWhiteSpace(LanguageScript.instance.language))
            return fallback;

        return LanguageScript.instance.language;
    }

    public void RegisterAgentClip(AudioClip clip)
    {
        lastClipPlayed  = clip;
        lastClipWasAgent = true;
    }

    public void ClearAgentClip()
    {
        lastClipPlayed   = null;
        lastClipWasAgent = false;
    }
}