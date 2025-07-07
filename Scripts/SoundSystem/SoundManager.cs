using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SoundManager: MonoBehaviour{
    public static SoundManager instance;
    [SerializeField] private Sprite soundImage;
    [SerializeField] private Sprite noSoundImage;
    [SerializeField] private Sprite musicImage;
    [SerializeField] private Sprite noMusicImage;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button musicButton;
    [SerializeField] private AudioSource soundObject;

    public static bool soundOn = true; // Static variable to keep track of sound state
    [SerializeField] private AudioSource musicObject;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void MakeSound(AudioClip audioClip, float voulume){
        if (soundOn){
            AudioSource audioSource = Instantiate(soundObject, transform.position, Quaternion.identity);
            audioSource.clip = audioClip;
            audioSource.volume = voulume;
            audioSource.Play();
            Destroy(audioSource.gameObject, audioClip.length + 0.1f); // Destroy the sound object after the clip has finished playing
        }
    }

    public void ToggleSound(){
        soundOn = !soundOn; // Toggle the sound state
        if(soundOn){
            soundButton.GetComponent<Image>().sprite = soundImage; // Set button image to soundImage when sound is on
        } else {
            soundButton.GetComponent<Image>().sprite = noSoundImage; // Set button image to noSoundImage when sound is off
        }
    }

    public void ToggleMusic(){
        if (musicObject.isPlaying) {
            musicObject.Pause(); // Pause the music if it's playing
            musicButton.GetComponent<Image>().sprite = noMusicImage; // Set button image to soundImage when sound is on
        } else {
            musicObject.Play(); // Play the music if it's paused
            musicButton.GetComponent<Image>().sprite = musicImage; // Set button image to soundImage when sound is on
        }
    }
}
