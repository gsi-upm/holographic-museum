using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using System.Diagnostics;
using THSDK;
using UnityEngine.UI;
using TMPro;

public class AgentManager : MonoBehaviour
{
    public static AgentManager instance;

    public MUSEBorder MuseBorder;

    [Header("MUSE UI Buttons")]
    public GameObject MuseCanvas;
    public GameObject startMUSEButton;
    public GameObject stopMUSEButton;
    public GameObject stopRecordingButton;
    public GameObject title;

    [Header("Button Animation")]
    [SerializeField] private float buttonAnimDuration = 0.25f;

    private bool isRecording = false;
    private string langGraphPath;
    private string inputPath;
    private string wavPath;
    private string flagPath;
    private string searchingFlagPath;
    private AudioClip recordedClip;
    private string microphoneName;

    public AudioSource audioSource;

    [SerializeField] private RunWhisper whisperTranscriber;

    private Process pythonProcess;
    private Coroutine _waitForAudioCoroutine;
    private Coroutine _waitForSearchingCoroutine;
    private Coroutine _playWavCoroutine;

    private bool isTranscribing  = false;
    private bool isWaitingForLLM = false;
    public HolographicDevice device;


    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (instance == null)
            instance = this;

        // Initial button state: only startMUSE visible, centred
        startMUSEButton?.SetActive(false);
        stopMUSEButton?.SetActive(false);
        stopRecordingButton?.SetActive(false);
        MuseCanvas?.SetActive(false);
        title?.SetActive(false);

        langGraphPath     = Path.Combine(Application.streamingAssetsPath, "LangGraph");
        inputPath         = Path.Combine(langGraphPath, "input.txt");
        wavPath           = Path.Combine(langGraphPath, "audioguide.wav");
        flagPath          = Path.Combine(langGraphPath, "done.flag");
        searchingFlagPath = Path.Combine(langGraphPath, "searching.flag");

        UnityEngine.Debug.Log($"[AgentManager] langGraphPath : {langGraphPath}");
        UnityEngine.Debug.Log($"[AgentManager] inputPath     : {inputPath}");
        UnityEngine.Debug.Log($"[AgentManager] flagPath      : {flagPath}");

        foreach (string f in new[] { inputPath, flagPath, searchingFlagPath })
        {
            if (File.Exists(f))
            {
                File.Delete(f);
                UnityEngine.Debug.Log($"[AgentManager] Deleted stale file: {f}");
            }
        }

        // Delete all files in Resources directory
        string resourcesPath = Path.Combine(langGraphPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            try
            {
                Directory.Delete(resourcesPath, true); // true = recursivo, borra todo
                UnityEngine.Debug.Log($"[AgentManager] Deleted Resources folder: {resourcesPath}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[AgentManager] Failed to delete Resources folder: {ex.Message}");
            }
        }
        Directory.CreateDirectory(resourcesPath);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        
    }

    private void Start()
    {
        foreach (string d in Microphone.devices)
            UnityEngine.Debug.Log("Available microphone: " + d);

        if (Microphone.devices.Length > 0)
        {
            microphoneName = Microphone.devices[0];
            UnityEngine.Debug.Log("Microphone selected: " + microphoneName);
        }
        else
        {
            UnityEngine.Debug.LogWarning("No microphone detected.");
        }

        if (!Directory.Exists(langGraphPath))
            UnityEngine.Debug.LogWarning("LangGraph folder not found: " + langGraphPath);
    }

    private void Update()
    {
        var user       = device.GetUser(0);
        var controller = user.GetController(0);
        if (Input.GetKeyDown(KeyCode.R))                                            StartMUSEAgent();
        if (Input.GetKeyDown(KeyCode.S))                                            StopRecording();
        if (Input.GetKeyDown(KeyCode.P))                                            PlayRecording();
        if (Input.GetKeyDown(KeyCode.Escape))                                       StopMUSEAgent();
    }

    // -----------------------------------------------------------------------
    // MUSE button API
    // -----------------------------------------------------------------------

    /// <summary>Called by the startMUSE button. Begins recording and animates buttons.</summary>
    public void StartMUSEAgent()
    {
        SoundManager.instance.StopAudioguide();
        SoundManager.instance.DuckMusic();
        if (_playWavCoroutine          != null) { StopCoroutine(_playWavCoroutine);            _playWavCoroutine          = null; }

        if (isRecording || isWaitingForLLM) return;

        if (string.IsNullOrEmpty(microphoneName))
        {
            UnityEngine.Debug.LogWarning("Cannot record: no microphone selected.");
            return;
        }

        MuseBorder?.SetState(MUSEBorder.MuseState.Listening);
        recordedClip = Microphone.Start(microphoneName, false, 300, 16000);
        isRecording  = true;
        UnityEngine.Debug.Log("[AgentManager] Recording started.");

        // startMUSE out; stopRecording at -100; stopMUSE at +100
        startMUSEButton?.SetActive(false);

        if(LanguageScript.instance.language == "en")
            title.GetComponent<TextMeshProUGUI>().text = "Transcribing...";
        else
            title.GetComponent<TextMeshProUGUI>().text = "Transcribiendo...";

        stopRecordingButton?.SetActive(true);
    }

    /// <summary>Called by the stopRecording button. Stops mic, sends to agent, slides stopMUSE to centre.</summary>
    public void StopRecording()
    {
        if (!isRecording) return;
        SoundManager.instance.RestoreMusic();

        if (!string.IsNullOrEmpty(microphoneName) && Microphone.IsRecording(microphoneName))
            Microphone.End(microphoneName);

        isRecording = false;
        UnityEngine.Debug.Log("[AgentManager] Recording stopped — transcribing.");

        // stopRecording out; stopMUSE slides to centre
        stopRecordingButton?.SetActive(false);
        stopMUSEButton?.SetActive(true);
        if(LanguageScript.instance.language == "en")
            title.GetComponent<TextMeshProUGUI>().text = "I'm thinking...";
        else
            title.GetComponent<TextMeshProUGUI>().text = "Estoy pensando...";

        StartCoroutine(TranscribeAndSend());
    }

    /// <summary>Called by the stopMUSE button. Cancels everything and resets UI.</summary>
    public void StopMUSEAgent()
    {
        CancelAllActivity();
        ResetToStart();
        UnityEngine.Debug.Log("[AgentManager] StopMUSEAgent — all activity cancelled.");
    }

    // -----------------------------------------------------------------------
    // Cancel helper
    // -----------------------------------------------------------------------

    private void CancelAllActivity()
    {
        // 1. Stop microphone
        if (isRecording)
        {
            if (!string.IsNullOrEmpty(microphoneName) && Microphone.IsRecording(microphoneName))
                Microphone.End(microphoneName);
            isRecording = false;
        }

        // 2. Cancel coroutines
        if (_waitForAudioCoroutine     != null) { StopCoroutine(_waitForAudioCoroutine);     _waitForAudioCoroutine     = null; }
        if (_waitForSearchingCoroutine != null) { StopCoroutine(_waitForSearchingCoroutine);  _waitForSearchingCoroutine = null; }
        if (_playWavCoroutine          != null) { StopCoroutine(_playWavCoroutine);            _playWavCoroutine          = null; }

        // 3. Stop audio
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        // 4. Clean runtime files
        foreach (string f in new[] { inputPath, flagPath, searchingFlagPath, wavPath })
            if (File.Exists(f)) File.Delete(f);

        // 5. Reset flags
        isTranscribing  = false;
        isWaitingForLLM = false;

        // 6. Border idle
        MuseBorder?.SetState(MUSEBorder.MuseState.Idle);
    }

    // -----------------------------------------------------------------------
    // Recording / playback
    // -----------------------------------------------------------------------

    public void PlayRecording()
    {
        if (recordedClip == null) { UnityEngine.Debug.LogWarning("No recorded audio to play."); return; }
        if (audioSource  == null) { UnityEngine.Debug.LogWarning("No AudioSource assigned.");    return; }
        audioSource.clip = recordedClip;
        SoundManager.instance.DuckMusic();
        audioSource.Play();
    }

    // -----------------------------------------------------------------------
    // Transcription
    // -----------------------------------------------------------------------

    private IEnumerator TranscribeAndSend()
    {
        if (isTranscribing || isWaitingForLLM)
        {
            UnityEngine.Debug.LogWarning("Transcription ignored: already processing.");
            yield break;
        }

        isTranscribing = true;

        string result  = null;
        bool   done    = false;
        bool   errored = false;

        TranscribeAsync(
            r  => { result  = r;    done = true; },
            () => { errored = true; done = true; }
        );

        yield return new WaitUntil(() => done);

        isTranscribing = false;

        if (errored || string.IsNullOrWhiteSpace(result))
        {
            UnityEngine.Debug.LogWarning("[AgentManager] Empty or failed transcription — resetting.");
            ResetToStart();
            yield break;
        }

        UnityEngine.Debug.Log($"[AgentManager] Transcription: {result}");
        isWaitingForLLM = true;
        OnUserFinishedSpeaking(result);
    }

    private async void TranscribeAsync(System.Action<string> onSuccess, System.Action onError)
    {
        try
        {
            if (whisperTranscriber == null || recordedClip == null) { onError(); return; }
            string lang   = LanguageScript.instance != null ? LanguageScript.instance.language : "en";
            string result = await whisperTranscriber.TranscribeAudio(recordedClip, lang);
            onSuccess(result);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[AgentManager] Transcription error: {e.Message}");
            onError();
        }
    }

    // -----------------------------------------------------------------------
    // Communication with Python
    // -----------------------------------------------------------------------

    public void OnUserFinishedSpeaking(string transcription)
    {
        if (!Directory.Exists(langGraphPath))
        {
            UnityEngine.Debug.LogError("LangGraph folder does not exist: " + langGraphPath);
            isWaitingForLLM = false;
            ResetToStart();
            return;
        }

        if (File.Exists(flagPath)) File.Delete(flagPath);
        if (File.Exists(wavPath))  File.Delete(wavPath);

        string exhibitionName = moving_script.instance != null ? moving_script.instance.exhibitionName : "unknown";
        string activeLang     = LanguageScript.instance != null ? LanguageScript.instance.language : "en";
        string text           = $"[room:{exhibitionName}][level:beginner][lang:{activeLang}] {transcription}";

        File.WriteAllText(inputPath, text);
        UnityEngine.Debug.Log($"[AgentManager] Input written: {text}");

        if (_waitForAudioCoroutine     != null) StopCoroutine(_waitForAudioCoroutine);
        if (_waitForSearchingCoroutine != null) StopCoroutine(_waitForSearchingCoroutine);
        if (File.Exists(searchingFlagPath))     File.Delete(searchingFlagPath);

        _waitForSearchingCoroutine = StartCoroutine(WaitForSearching());
        _waitForAudioCoroutine     = StartCoroutine(WaitForAudio());
    }

    private IEnumerator WaitForAudio()
    {
        const float pollInterval = 1f;
        const float maxWait      = 120f;
        float elapsed = 0f;

        UnityEngine.Debug.Log("[AgentManager] Waiting for done.flag...");

        while (!File.Exists(flagPath))
        {
            if (elapsed >= maxWait)
            {
                UnityEngine.Debug.LogWarning("[AgentManager] Timed out waiting for done.flag.");
                isWaitingForLLM = false;
                ResetToStart();
                yield break;
            }
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
        }

        File.Delete(flagPath);
        UnityEngine.Debug.Log("[AgentManager] done.flag detected — playing WAV.");

        _waitForAudioCoroutine = null;
        isWaitingForLLM        = false;
        _playWavCoroutine      = StartCoroutine(PlayWav(wavPath, onFinished: ResetToStart));
        MuseBorder?.SetState(MUSEBorder.MuseState.Idle);
    }

    private IEnumerator WaitForSearching()
    {
        const float pollInterval = 0.2f;
        const float maxWait      = 30f;
        float elapsed = 0f;

        while (!File.Exists(searchingFlagPath))
        {
            if (elapsed >= maxWait)
            {
                UnityEngine.Debug.LogWarning("[AgentManager] Timed out waiting for searching.flag.");
                yield break;
            }
            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
        }

        string content = File.ReadAllText(searchingFlagPath).Trim();
        File.Delete(searchingFlagPath);
        _waitForSearchingCoroutine = null;

        if (content == "use=1")
        {
            string activeLang       = LanguageScript.instance != null ? LanguageScript.instance.language : "en";
            string searchingFile    = activeLang == "es" ? "searching_es.mp3" : "searching_en.mp3";
            string searchingWavPath = Path.Combine(langGraphPath, searchingFile);
            UnityEngine.Debug.Log("[AgentManager] Playing searching audio: " + searchingWavPath);
            // No onFinished — WaitForAudio owns the final ResetToStart
            _playWavCoroutine = StartCoroutine(PlayWav(searchingWavPath, onFinished: null));
        }
    }

    /// <summary>Loads and plays a WAV/MP3. Calls onFinished when playback ends (if not cancelled).</summary>
    private IEnumerator PlayWav(string path, System.Action onFinished)
    {
        SoundManager.instance.DuckMusic();
        if (audioSource == null) { UnityEngine.Debug.LogWarning("No AudioSource."); yield break; }
        if (!File.Exists(path)) { UnityEngine.Debug.LogWarning("[AgentManager] File not found: " + path); yield break; }

        string url = new System.Uri(Path.GetFullPath(path)).AbsoluteUri;
        AudioType audioType = path.EndsWith(".mp3") ? AudioType.MPEG : AudioType.WAV;

        using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            UnityEngine.Debug.LogError("[AgentManager] Error loading audio: " + req.error);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        if (clip == null) { UnityEngine.Debug.LogError("[AgentManager] Clip is null."); yield break; }

        SoundManager.instance.RegisterAgentClip(clip);

        if (MuseBorder != null)
        {
            MuseBorder.speakingAudioSource = audioSource;
            MuseBorder.SetState(MUSEBorder.MuseState.Speaking);
            AnimationTransitioner.instance.SetTalking(true);
        }

        SoundManager.instance.musaVoiceSource.clip = clip;
        SoundManager.instance.musaVoiceSource.Play();
        yield return new WaitForSeconds(clip.length);
        moving_script.instance?.audioGuide.SetActive(false);
        SoundManager.instance.RestoreMusic();
        AnimationTransitioner.instance.SetTalking(false);

        _playWavCoroutine = null;
        onFinished?.Invoke();
    }

    // -----------------------------------------------------------------------
    // UI state helpers
    // -----------------------------------------------------------------------

    /// <summary>Resets buttons to initial state: only startMUSE visible and centred.</summary>
    private void ResetToStart()
    {
        stopMUSEButton?.SetActive(false);
        stopRecordingButton?.SetActive(false);
        startMUSEButton?.SetActive(true);
        MuseBorder?.SetState(MUSEBorder.MuseState.Idle);
        UnityEngine.Debug.Log("[AgentManager] UI reset to start.");
        if(LanguageScript.instance.language == "en")
            title.GetComponent<TextMeshProUGUI>().text = "Ask MUSE!";
        else
            title.GetComponent<TextMeshProUGUI>().text = "¡Pregunta a MUSE!";
    }

    // -----------------------------------------------------------------------
    // Python process management
    // -----------------------------------------------------------------------

    public void StartPythonAgent(string language = "en")
    {
        string scriptPath = Path.Combine(langGraphPath, "agent.py");

        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogError("[AgentManager] agent.py not found at: " + scriptPath);
            return;
        }

        string interpreterPath = ResolveInterpreter();
        if (string.IsNullOrEmpty(interpreterPath))
        {
            UnityEngine.Debug.LogError("[AgentManager] No Python interpreter found.");
            return;
        }

        pythonProcess = new Process();
        pythonProcess.StartInfo.FileName               = interpreterPath;
        pythonProcess.StartInfo.Arguments              = $"\"{scriptPath}\" {language}";
        pythonProcess.StartInfo.WorkingDirectory       = langGraphPath;
        pythonProcess.StartInfo.UseShellExecute        = false;
        pythonProcess.StartInfo.CreateNoWindow         = true;
        pythonProcess.StartInfo.RedirectStandardOutput = true;
        pythonProcess.StartInfo.RedirectStandardError  = true;
        pythonProcess.OutputDataReceived += (_, e) => { };
        pythonProcess.ErrorDataReceived  += (_, e) => { };

        try
        {
            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();
            UnityEngine.Debug.Log($"[AgentManager] Python agent started. PID: {pythonProcess.Id}");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[AgentManager] Failed to start Python: {ex.Message}");
            pythonProcess.Dispose();
            pythonProcess = null;
        }
    }

    private string ResolveInterpreter()
    {
        string[] candidates = new[]
        {
            Path.GetFullPath(Path.Combine(langGraphPath,        "venv/bin/python")),
            Path.GetFullPath(Path.Combine(langGraphPath,        "venv/Scripts/python.exe")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "../venv/bin/python")),
            Path.GetFullPath(Path.Combine(Application.dataPath, "../venv/Scripts/python.exe")),
            "/usr/bin/python3",
            "/usr/local/bin/python3",
        };

        foreach (string c in candidates)
        {
            if (File.Exists(c))
            {
                UnityEngine.Debug.Log($"[AgentManager] Interpreter found: {c}");
                return c;
            }
        }

        UnityEngine.Debug.LogError("[AgentManager] Interpreter search failed. Tried:\n" + string.Join("\n", candidates));
        return null;
    }

    private void OnApplicationQuit()
    {
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
            UnityEngine.Debug.Log("[AgentManager] Python agent stopped.");
        }
    }
}