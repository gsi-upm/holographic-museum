using UnityEngine;

public class SpeechBlendTest : MonoBehaviour
{
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioClip testClip;

    private void Update()
    {
        // Pulsa T para reproducir el clip de prueba
        if (Input.GetKeyDown(KeyCode.T))
        {
            voiceSource.clip = testClip;
            voiceSource.Play();
            Debug.Log($"[Test] Playing: {testClip.name} | isPlaying={voiceSource.isPlaying}");
        }
    }
}