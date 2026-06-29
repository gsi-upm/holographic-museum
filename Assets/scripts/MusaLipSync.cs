using UnityEngine;

/// <summary>
/// Simple lipsync for MUSA.
/// Reads audio amplitude from an AudioSource and drives two blendshapes
/// (mouth_shrug_upper + mouth_drop_lower) to simulate mouth movement.
///
/// Setup:
///   1. Add this component to any GameObject (e.g. MUSA root or head).
///   2. Assign voiceSource → the AudioSource that plays MUSA's voice.
///   3. Assign headMesh   → the SkinnedMeshRenderer with the face blendshapes.
///   4. The blendshape names are preset to mouth_shrug_upper / mouth_drop_lower.
///      Change them in the Inspector if needed.
/// </summary>
public class MusaLipsync : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Mesh")]
    [SerializeField] private SkinnedMeshRenderer headMesh;

    [Header("Blendshape names")]
    [SerializeField] private string upperBlendshape = "mouth_shrug_upper";
    [SerializeField] private string lowerBlendshape = "mouth_drop_lower";

    [Header("Tuning")]
    [SerializeField] [Range(1f, 500f)] private float sensitivity  = 80f;  // Higher = more movement
    [SerializeField] [Range(0f, 100f)] private float maxWeight    = 80f;  // Cap blendshape value
    [SerializeField] [Range(0f,  1f)]  private float smoothSpeed  = 0.15f; // Lerp speed (lower = smoother)

    private int   _upperIndex = -1;
    private int   _lowerIndex = -1;
    private float _currentWeight = 0f;
    private float[] _samples = new float[256];

    private void Start()
    {
        if (headMesh == null)
        {
            Debug.LogError("[MusaLipsync] headMesh not assigned.");
            enabled = false;
            return;
        }

        _upperIndex = headMesh.sharedMesh.GetBlendShapeIndex(upperBlendshape);
        _lowerIndex = headMesh.sharedMesh.GetBlendShapeIndex(lowerBlendshape);

        if (_upperIndex < 0) Debug.LogWarning($"[MusaLipsync] Blendshape '{upperBlendshape}' not found.");
        if (_lowerIndex < 0) Debug.LogWarning($"[MusaLipsync] Blendshape '{lowerBlendshape}' not found.");
    }

    private void Update()
    {
        float targetWeight = 0f;

        if (voiceSource != null && voiceSource.isPlaying)
        {
            voiceSource.GetOutputData(_samples, 0);

            float volume = 0f;
            foreach (float s in _samples) volume += Mathf.Abs(s);

            targetWeight = Mathf.Clamp(volume * sensitivity, 0f, maxWeight);
        }

        // Smooth towards target
        _currentWeight = Mathf.Lerp(_currentWeight, targetWeight, smoothSpeed);

        // Apply to both blendshapes
        if (_upperIndex >= 0) headMesh.SetBlendShapeWeight(_upperIndex, _currentWeight);
        if (_lowerIndex >= 0) headMesh.SetBlendShapeWeight(_lowerIndex, _currentWeight);
    }
}