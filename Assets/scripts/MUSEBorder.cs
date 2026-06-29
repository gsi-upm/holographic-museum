using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MUSEBorder : MonoBehaviour
{
    public static MUSEBorder instance;
    public enum MuseState { Idle, Listening, Speaking }

    [Header("Estado inicial")]
    [SerializeField] private MuseState initialState = MuseState.Idle;

    [Header("Escala base — Listening / Speaking")]
    [Tooltip("Escala Idle: se lee del Transform del Inspector (no se sobreescribe al arrancar).")]
    [SerializeField] private float listeningBaseScale = 0.75f;
    [SerializeField] private float speakingBaseScale  = 0.75f;

    [Header("Amplitud del pulso por estado")]
    [SerializeField] private float idleAmplitude      = 0.02f;
    [SerializeField] private float listeningAmplitude = 0.08f;
    [SerializeField] private float speakingAmplitude  = 0.12f;

    [Header("Velocidad del pulso (Hz)")]
    [SerializeField] private float idleFrequency      = 0.3f;
    [SerializeField] private float listeningFrequency = 0.8f;
    [SerializeField] private float speakingFrequency  = 1.5f;

    [Header("Transición entre estados")]
    [SerializeField] private float transitionDuration = 0.6f;

    [Header("Audio reactivo — Speaking (opcional)")]
    [Tooltip("AudioSource que reproduce el audio de MUSE. Sin asignar = sin reactividad.")]
    [SerializeField] public AudioSource speakingAudioSource;
    [SerializeField] [Range(0f, 5f)] private float audioReactivity = 2.0f;
    [SerializeField] private int audioSampleSize = 256;

    private RectTransform _rect;
    private MuseState     _currentState;
    private float         _time;
    private float         _idleScale; // Escala leída del Inspector en Awake

    private float _activeBase, _activeAmplitude, _activeFrequency;
    private float _fromBase,   _fromAmplitude,   _fromFrequency;
    private float _toBase,     _toAmplitude,     _toFrequency;

    private float   _transitionTimer;
    private bool    _inTransition;
    private float[] _audioSamples;

    private void Awake()
    {
        instance = this;
        _rect         = GetComponent<RectTransform>();
        _audioSamples = new float[audioSampleSize];
        _idleScale    = _rect.localScale.x; // Preserva la escala configurada en el Inspector

        GetStateParams(initialState, out _activeBase, out _activeAmplitude, out _activeFrequency);
        _currentState = initialState;
    }

    private void Update()
    {
        // 1. Interpolar parámetros durante la transición
        if (_inTransition)
        {
            _transitionTimer += Time.deltaTime;
            float t      = Mathf.Clamp01(_transitionTimer / transitionDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            _activeBase      = Mathf.Lerp(_fromBase,      _toBase,      smooth);
            _activeAmplitude = Mathf.Lerp(_fromAmplitude, _toAmplitude, smooth);
            _activeFrequency = Mathf.Lerp(_fromFrequency, _toFrequency, smooth);

            if (t >= 1f) _inTransition = false;
        }

        // 2. Reactividad de audio (solo en Speaking con AudioSource asignado)
        float audioMult = 1f;
        if (_currentState == MuseState.Speaking && speakingAudioSource != null && audioReactivity > 0f)
            audioMult = 1f + GetAudioAmplitude() * audioReactivity;

        // 3. Onda sinusoidal → escala final
        _time += Time.deltaTime;
        float wave = (Mathf.Sin(_time * _activeFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        float organicNoise = Mathf.PerlinNoise(Time.time * 1.4f, 0f);
        float organicFactor = Mathf.Lerp(0.9f, 1.1f, organicNoise);
        float scale = _activeBase + _activeAmplitude * wave * audioMult * organicFactor;

        _rect.localScale = new Vector3(scale, scale, 1f);
    }

    // -----------------------------------------------------------------------
    // API pública
    // -----------------------------------------------------------------------

    public void SetState(MuseState newState)
    {
        if (newState == _currentState && !_inTransition) return;

        _currentState  = newState;
        _fromBase      = _activeBase;
        _fromAmplitude = _activeAmplitude;
        _fromFrequency = _activeFrequency;

        GetStateParams(newState, out _toBase, out _toAmplitude, out _toFrequency);

        _transitionTimer = 0f;
        _inTransition    = true;
    }

    public void SetState(string stateName)
    {
        if (System.Enum.TryParse(stateName, true, out MuseState s))
            SetState(s);
        else
            Debug.LogWarning($"[MUSEBorder] Estado desconocido: '{stateName}'");
    }

    public MuseState CurrentState => _currentState;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void GetStateParams(MuseState state, out float baseScale, out float amplitude, out float frequency)
    {
        switch (state)
        {
            case MuseState.Listening:
                baseScale = listeningBaseScale;
                amplitude = listeningAmplitude;
                frequency = listeningFrequency;
                break;
            case MuseState.Speaking:
                baseScale = speakingBaseScale;
                amplitude = speakingAmplitude;
                frequency = speakingFrequency;
                break;
            default: // Idle
                baseScale = _idleScale; // Vuelve a la escala original del Inspector
                amplitude = idleAmplitude;
                frequency = idleFrequency;
                break;
        }
    }

    private float GetAudioAmplitude()
    {
        speakingAudioSource.GetOutputData(_audioSamples, 0);
        float sum = 0f;
        foreach (float s in _audioSamples) sum += s * s;
        return Mathf.Clamp01(Mathf.Sqrt(sum / _audioSamples.Length) * 10f);
    }
}
