// This script is for the buttons the answers will go on

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.UI;

public class PlayButton : MonoBehaviour
{
    [SerializeField] private TriviaManager triviaManager;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private Sprite stopPlay;
    [SerializeField] private Sprite play;
    public bool playing = false; // Used to determine if the trivia is currently playing
    public bool endplay = false; // Used to determine if the trivia has ended

    private string currentRoomName;

    private void Awake()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false); // Force the button to be hidden if it's active
        }
        playing = false; // Initialize playing to false
    }

    public void OnClick()
    {
        SoundManager.instance.MakeSound(buttonClickSound, 0.5f);

        if (playing)
        {
            // Estamos jugando → queremos parar
            triviaManager.ForceQuit();
            int questionLevel = 0;

            switch (currentRoomName)
            {
                case "Las Meninas":
                    questionLevel = triviaManager.meninasDificulty;
                    break;
                case "Composición Ocho de Kandinsky":
                    questionLevel = triviaManager.kandinskyDificulty;
                    break;
                case "Balloon Dog de Jeff Koons":
                    questionLevel = triviaManager.balloonDificulty;
                    break;
                case "Dali":
                    questionLevel = triviaManager.daliDificulty;
                    break;
                case "Keith Haring":
                    questionLevel = triviaManager.keithDificulty;
                    break;
            }
            if (questionLevel < triviaManager.maxQuestions)
            {
                SetToPlay(); // Cambiar a estado no jugando
            }
            else
            {
                HideButton(); // Ocultar el botón si no quedan preguntas
            }
        }
        else
        {
            // No estamos jugando → queremos empezar
            JSONtoQ.ReloadQuestions(currentRoomName);
            triviaManager.StartTrivia(currentRoomName);
            SetToStop(); // Cambiar a estado jugando
        }
    }


    public void SetToPlay()
    {
        gameObject.GetComponent<Image>().sprite = play;
        playing = false;
    }

    public void SetToStop()
    {
        gameObject.GetComponent<Image>().sprite = stopPlay;
        playing = true;
    }


    public void ShowButton()
    {
        gameObject.SetActive(true);
    }

    public void HideButton()
    {
        gameObject.SetActive(false);
    }

    public void SetRoomName(string roomName)
    {
        currentRoomName = roomName;
    }
}
