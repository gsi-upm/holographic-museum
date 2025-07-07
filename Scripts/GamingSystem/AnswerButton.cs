// This script is for the buttons the answers will go on

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class AnswerButton : MonoBehaviour
{
    private bool isCorrect;
    private TextMeshProUGUI answerText;

    // To make it ask a new question after the first question
    [SerializeField] private TriviaManager triviaManager;

    public void ShowQuiz()
    {
        showAnswers();
    }

    public void Awake()
    {
        gameObject.SetActive(false);
    }

    public void SetAnswerText(string newText)
    {
        answerText.text = newText;
    }

    public void SetIsCorrect(bool newBool)
    {
        isCorrect = newBool;
    }

    public void OnClick()
    {
        if(!isCorrect)
        {
            gameObject.SetActive(false);
        }
        
    }

    public void showAnswers()
    {
        gameObject.SetActive(true);
    }
}
