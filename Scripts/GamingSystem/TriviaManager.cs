using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.UI;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using System;
using UnityEngine.Windows.Speech;

public class TriviaManager : MonoBehaviour
{
    [SerializeField] public List<QuestionJson> questions;
    [SerializeField] private TextMeshProUGUI questionTextField;
    [SerializeField] private GameObject questionBox; // Reference to the question box RectTransform
    [SerializeField] private PlayButton playButton;
    public List<Button> answerButtons;
    private int currentQuestionIndex;
    public TextMeshProUGUI menuScore;
    [SerializeField] private AudioClip correctSound;
    [SerializeField] private AudioClip incorrectSound;
    [SerializeField] private AudioClip endSound;
    [SerializeField] private AudioClip finalSound;
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private XMLManager xmlManager;
    [SerializeField] private NonNativeKeyboard keyboard; // Reference to the TextMeshProUGUI for high scores
    [SerializeField] private TextMeshProUGUI rankingTextField;

    // Pins for the different artworks
    //Gold Pins
    [SerializeField] private Image pinBalloonGold;
    [SerializeField] private Image pinKandinskyGold;
    [SerializeField] private Image pinMeninasGold;
    [SerializeField] private Image pinDaliGold;
    [SerializeField] private Image pinKeithGold;
    //Silver Pins
    [SerializeField] private Image pinBalloonSilver;
    [SerializeField] private Image pinKandinskySilver;
    [SerializeField] private Image pinMeninasSilver;
    [SerializeField] private Image pinDaliSilver;
    [SerializeField] private Image pinKeithSilver;
    //Bronze Pins
    [SerializeField] private Image pinBalloonBronze;
    [SerializeField] private Image pinKandinskyBronze;
    [SerializeField] private Image pinMeninasBronze;
    [SerializeField] private Image pinDaliBronze;
    [SerializeField] private Image pinKeithBronze;
    //Void Pins (for when the player has not completed the artwork)
    [SerializeField] private Image pinBalloonVoid;
    [SerializeField] private Image pinKandinskyVoid;
    [SerializeField] private Image pinMeninasVoid;
    [SerializeField] private Image pinDaliVoid;
    [SerializeField] private Image pinKeithVoid;

    private string playerName;
    private string obraActual;
    private int questionLevel = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    private readonly string[] difficultyLevels = { "beginner", "hard", "expert" };
    public int meninasDificulty = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    public int kandinskyDificulty = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    public int balloonDificulty = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    public int keithDificulty = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    public int daliDificulty = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    private int score = 0;
    private bool hasCompleted = false;
    public bool multipleQuestions = true; // This variable is not used in this version of the script, but can be used for future levels or difficulties
    public int maxQuestions = 3; // This variable is not used in this version of the script, but can be used for future levels or difficulties


    private void Awake()
    {
        hideQuestion();
        hideRanking();
        pinMeninasGold.gameObject.SetActive(false);
        pinKandinskyGold.gameObject.SetActive(false);
        pinBalloonGold.gameObject.SetActive(false);
        pinKeithGold.gameObject.SetActive(false);
        pinDaliGold.gameObject.SetActive(false);
        pinMeninasSilver.gameObject.SetActive(false);
        pinKandinskySilver.gameObject.SetActive(false);
        pinBalloonSilver.gameObject.SetActive(false);
        pinKeithSilver.gameObject.SetActive(false);
        pinDaliSilver.gameObject.SetActive(false);
        pinMeninasBronze.gameObject.SetActive(false);
        pinKandinskyBronze.gameObject.SetActive(false);
        pinBalloonBronze.gameObject.SetActive(false);
        pinKeithBronze.gameObject.SetActive(false);
        pinDaliBronze.gameObject.SetActive(false);
    }


    public void StartTrivia(string obra)
    {
        //The comented section is for the AiQuestion script, which is not used in this version of the script
        /*await Task.Delay(100); // Optional: Small delay to ensure async operations complete
        currentQuestionIndex = 0;

         Usa las preguntas generadas si están disponibles
        if (AiQuestion.GeneratedQuestions != null && AiQuestion.GeneratedQuestions.Count > 0)
        {
            questions = AiQuestion.GeneratedQuestions;
        }
        else
        {
            questions = JSONtoQ.GeneratePhrases(); // Carga desde el archivo si no hay preguntas generadas
        }*/
        if (multipleQuestions)
        {
            maxQuestions = 3;
        }
        else
        {
            maxQuestions = 1;
        }

        obraActual = obra;
        switch (obra)
        {
            case "Las Meninas":
                if (meninasDificulty < maxQuestions)
                    meninasDificulty++;
                questionLevel = meninasDificulty;
                break;
            case "Composición Ocho de Kandinsky":
                if (kandinskyDificulty < maxQuestions)
                    kandinskyDificulty++;
                questionLevel = kandinskyDificulty;
                break;
            case "Balloon Dog de Jeff Koons":
                if (balloonDificulty < maxQuestions)
                    balloonDificulty++;
                questionLevel = balloonDificulty;
                break;
            case "Dali":
                if (daliDificulty < maxQuestions)
                    daliDificulty++;
                questionLevel = daliDificulty;
                break;
            case "Keith Haring":
                if (keithDificulty < maxQuestions)
                    keithDificulty++;
                questionLevel = keithDificulty;
                break;
        }

        // Usa el nivel de dificultad correspondiente (empieza en 0)
        string dificultad = difficultyLevels[Mathf.Clamp(questionLevel - 1, 0, difficultyLevels.Length - 1)];
        questions = JSONtoQ.GeneratePhrases(obra, dificultad); // Carga desde el archivo
        menuScore.text = "YOUR SCORE: " + score.ToString();
        hasCompleted = false;
        SetAnswerValues();
    }


    private void SetAnswerValues()
    {
        if (currentQuestionIndex >= questions.Count)
        {
            playButton.endplay = true; // Toggle the play button to stop the trivia
            EndTrivia();
            currentQuestionIndex = 0; // Reset the question index for the next round
        }
        else
        {
            // Set the question text
            var question = questions[currentQuestionIndex];
            questionTextField.text = question.question;
            showQuestion();

            // Set up the answer buttons
            for (int i = 0; i < answerButtons.Count; i++)
            {
                var button = answerButtons[i];
                var answer = question.options[i];

                // Limpia los listeners existentes
                button.onClick.RemoveAllListeners();

                button.GetComponentInChildren<TextMeshProUGUI>().text = answer.choice;

                button.gameObject.SetActive(true);
                bool isCorrect = answer.correct;

                showAnswers();
                button.onClick.AddListener(() => OnAnswwerSelected(isCorrect));
            }
        }
    }

    void OnAnswwerSelected(bool isCorrect)
    {
        if (isCorrect)
        {
            currentQuestionIndex++;
            score++;
            if (currentQuestionIndex < questions.Count)
            {
                SoundManager.instance.MakeSound(correctSound, 0.5f);
            }
            menuScore.text = "YOUR SCORE: " + score.ToString();
            SetAnswerValues();
        }
        else
        {
            SoundManager.instance.MakeSound(incorrectSound, 0.5f);
        }
    }

    /*private List<string> RandomizeAnswers(List<string> originalList)
    {
        bool correctAnswerChosen = false;

        List<string>  newList = new List<string>();

        for(int i = 0; i < answerButtons.Count; i++)
        {
            // Get a random number of the remaining choices
            int random = Random.Range(0, originalList.Count);

            // If the random number is 0, this is the correct answer, MAKE SURE THIS IS ONLY USED ONCE
            if(random == 0 && !correctAnswerChosen)
            {
                correctAnswerChoice = i;
                correctAnswerChosen = true;
            }

            // Add this to the new list
            newList.Add(originalList[random]);
            //Remove this choice from the original list (it has been used)
            originalList.RemoveAt(random);  
        }


        return newList;
    }*/

    public void EndTrivia()
    {
        hasCompleted = true;
        hideAnswers();
        hideQuestion();
        showRanking();

        int nivel = 0;

        switch (obraActual)
        {
            case "Las Meninas":
                nivel = meninasDificulty;
                break;
            case "Composición Ocho de Kandinsky":
                nivel = kandinskyDificulty;
                break;
            case "Balloon Dog de Jeff Koons":
                nivel = balloonDificulty;
                break;
            case "Keith Haring":
                nivel = keithDificulty;
                break;
            case "Dali":
                nivel = daliDificulty;
                break;
        }
        if (multipleQuestions)
        {
            if (nivel == 1)
            {
                MostrarPinBronce(obraActual);
            }
            else if (nivel == 2)
            {
                MostrarPinPlateado(obraActual);
            }
            else if (nivel == 3)
            {
                MostrarPinDorado(obraActual);
            }
        }
        else
        {
            MostrarPinDorado(obraActual);
        }

        if (string.IsNullOrWhiteSpace(playerName))
        {
            keyboard.gameObject.SetActive(true);
            rankingTextField.text = "¡Has terminado! Introduce tu nombre para guardar tu puntuación:";
            NonNativeKeyboard.Instance.CloseOnInactivity = false;
            NonNativeKeyboard.Instance.PresentKeyboard("");
            NonNativeKeyboard.Instance.OnTextSubmitted += OnKeyboardTextSubmitted;
        }
        else
        {
            loadScores();
        }

        // Si ha completado todas las obras, mover todos los pins al centro
        if (meninasDificulty >= maxQuestions &&
            kandinskyDificulty >= maxQuestions &&
            balloonDificulty >= maxQuestions &&
            keithDificulty >= maxQuestions &&
            daliDificulty >= maxQuestions)
        {
            SoundManager.instance.MakeSound(finalSound, 0.5f);
            playButton.HideButton();
            MoveAllPinsToCenter();
            return;
        }
        SoundManager.instance.MakeSound(endSound, 0.5f);

    }

    private void OnKeyboardTextSubmitted(object sender, EventArgs e)
    {
        playerName = NonNativeKeyboard.Instance.InputField.text;

        // Si no hay nombre, no guardar
        if (string.IsNullOrWhiteSpace(playerName))
        {
            questionTextField.text = "Tu puntuación es: " + score + ".\n\n";
            return;
        }

        playerName = playerName.ToUpper();

        loadScores();

        // Cerrar teclado y desuscribir
        NonNativeKeyboard.Instance.Close();
        NonNativeKeyboard.Instance.OnTextSubmitted -= OnKeyboardTextSubmitted;
    }

    private void loadScores()
    {
        // Cargar y actualizar las puntuaciones
        List<HighScoreEntry> scores = xmlManager.LoadScores() ?? new List<HighScoreEntry>();

        var existing = scores.Find(e => e.name == playerName);
        if (existing != null)
        {
            existing.score += score;
        }
        else
        {
            scores.Add(new HighScoreEntry { name = playerName, score = score });
        }

        // Ordenar y guardar
        scores.Sort((a, b) => b.score.CompareTo(a.score));
        if (scores.Count > 10) scores = scores.GetRange(0, 10);

        xmlManager.SaveScores(scores);

        // Mostrar el mensaje de final y las 5 mejores puntuaciones
        string ranking = "Top 5 puntuaciones:\n";
        for (int i = 0; i < Mathf.Min(5, scores.Count); i++)
        {
            var entry = scores[i];
            ranking += $"{i + 1}. {entry.name} - {entry.score} puntos\n";
        }

        showRanking();
        rankingTextField.text = "Tu puntuación es: " + score + ".\n\n" + ranking;
    }

    private IEnumerator FlipAndScaleIn(Transform target)
    {
        if (target.gameObject.activeSelf)
            yield break;

        target.gameObject.SetActive(true);
        target.localScale = Vector3.zero;
        target.localEulerAngles = new Vector3(-720f, 0f, 0f); // varias vueltas hacia atrás

        float duration = 1f; // más tiempo para que se note el giro
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Interpolación de escala (con suavizado)
            target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, Mathf.SmoothStep(0f, 1f, t));

            // Rotación con varias vueltas
            float angleX = Mathf.Lerp(-720f, 0f, Mathf.SmoothStep(0f, 1f, t));
            target.localEulerAngles = new Vector3(angleX, 0f, 0f);

            yield return null;
        }

        // Asegura valores finales
        target.localScale = Vector3.one;
        target.localEulerAngles = Vector3.zero;
    }

    public void ForceQuit()
    {
        hideAnswers();
        hideQuestion();
        hideRanking();
        if (!hasCompleted)
        {
            switch (obraActual)
            {
                case "Las Meninas":
                    meninasDificulty -= 1;
                    break;
                case "Composición Ocho de Kandinsky":
                    kandinskyDificulty -= 1;
                    break;
                case "Balloon Dog de Jeff Koons":
                    balloonDificulty -= 1;
                    break;
                case "Keith Haring":
                    keithDificulty -= 1;
                    break;
                case "Dali":
                    daliDificulty -= 1;
                    break;
            }
        }
        currentQuestionIndex = 0;
        score = score - currentQuestionIndex;
    }


    public void showQuestion()
    {
        questionBox.gameObject.SetActive(true);
    }

    public void hideQuestion()
    {
        questionBox.gameObject.SetActive(false);
    }

    public void hideRanking()
    {
        rankingTextField.gameObject.SetActive(false);
    }

    public void showRanking()
    {
        rankingTextField.gameObject.SetActive(true);
    }

    public void showAnswers()
    {
        for (int i = 0; i < answerButtons.Count; i++)
        {
            answerButtons[i].gameObject.SetActive(true);
        }
    }

    public void hideAnswers()
    {
        for (int i = 0; i < answerButtons.Count; i++)
        {
            answerButtons[i].gameObject.SetActive(false);
        }
    }

    private void MostrarPinDorado(string obra)
    {
        switch (obra)
        {
            case "Las Meninas":
                pinMeninasSilver.gameObject.SetActive(false);
                StartCoroutine(FlipAndScaleIn(pinMeninasGold.transform));
                break;
            case "Composición Ocho de Kandinsky":
                pinKandinskySilver.gameObject.SetActive(false);
                StartCoroutine(FlipAndScaleIn(pinKandinskyGold.transform));
                break;
            case "Balloon Dog de Jeff Koons":
                pinBalloonSilver.gameObject.SetActive(false);
                StartCoroutine(FlipAndScaleIn(pinBalloonGold.transform));
                break;
            case "Keith Haring":
                pinKeithSilver.gameObject.SetActive(false);
                StartCoroutine(FlipAndScaleIn(pinKeithGold.transform));
                break;
            case "Dali":
                pinDaliSilver.gameObject.SetActive(false);
                StartCoroutine(FlipAndScaleIn(pinDaliGold.transform));
                break;
        }
    }

    private void MostrarPinPlateado(string obra)
    {
        switch (obra)
        {
            case "Las Meninas":
                StartCoroutine(FlipAndScaleIn(pinMeninasSilver.transform));
                pinMeninasBronze.gameObject.SetActive(false);
                break;
            case "Composición Ocho de Kandinsky":
                StartCoroutine(FlipAndScaleIn(pinKandinskySilver.transform));
                pinKandinskyBronze.gameObject.SetActive(false);
                break;
            case "Balloon Dog de Jeff Koons":
                StartCoroutine(FlipAndScaleIn(pinBalloonSilver.transform));
                pinBalloonBronze.gameObject.SetActive(false);
                break;
            case "Keith Haring":
                StartCoroutine(FlipAndScaleIn(pinKeithSilver.transform));
                pinKeithBronze.gameObject.SetActive(false);
                break;
            case "Dali":
                StartCoroutine(FlipAndScaleIn(pinDaliSilver.transform));
                pinDaliBronze.gameObject.SetActive(false);
                break;
        }
    }
    private void MostrarPinBronce(string obra)
    {
        switch (obra)
        {
            case "Las Meninas":
                StartCoroutine(FlipAndScaleIn(pinMeninasBronze.transform));
                break;
            case "Composición Ocho de Kandinsky":
                StartCoroutine(FlipAndScaleIn(pinKandinskyBronze.transform));
                break;
            case "Balloon Dog de Jeff Koons":
                StartCoroutine(FlipAndScaleIn(pinBalloonBronze.transform));
                break;
            case "Keith Haring":
                StartCoroutine(FlipAndScaleIn(pinKeithBronze.transform));
                break;
            case "Dali":
                StartCoroutine(FlipAndScaleIn(pinDaliBronze.transform));
                break;
        }
    }

    private void MoveAllPinsToCenter()
    {
        float spacing = 400f;
        float centerX = -820f;
        float centerY = -475f;
        Vector3 targetScale = Vector3.one * 2f;

        StartCoroutine(MoveAndScalePin(pinMeninasGold, new Vector3(centerX - 2 * spacing, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinKandinskyGold, new Vector3(centerX - spacing, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinBalloonGold, new Vector3(centerX, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinKeithGold, new Vector3(centerX + spacing, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinDaliGold, new Vector3(centerX + 2 * spacing, centerY, 0f), targetScale));

        StartCoroutine(MoveAndScalePin(pinMeninasVoid, new Vector3(centerX - 2 * spacing, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinKandinskyVoid, new Vector3(centerX - spacing, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinBalloonVoid, new Vector3(centerX, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinKeithVoid, new Vector3(centerX + spacing, centerY, 0f), targetScale));
        StartCoroutine(MoveAndScalePin(pinDaliVoid, new Vector3(centerX + 2 * spacing, centerY, 0f), targetScale));
        HidePin(pinMeninasSilver);
        HidePin(pinKandinskySilver);
        HidePin(pinBalloonSilver);
        HidePin(pinKeithSilver);
        HidePin(pinDaliSilver);
        HidePin(pinMeninasBronze);
        HidePin(pinKandinskyBronze);
        HidePin(pinBalloonBronze);
        HidePin(pinKeithBronze);
        HidePin(pinDaliBronze);
    }


    private IEnumerator MoveAndScalePin(Image pin, Vector3 targetPosition, Vector3 targetScale)
    {
        float duration = 1f;
        float elapsed = 0f;

        Vector3 startPosition = pin.rectTransform.anchoredPosition;
        Vector3 startScale = pin.rectTransform.localScale;

        pin.gameObject.SetActive(true);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            pin.rectTransform.anchoredPosition = Vector3.Lerp(startPosition, targetPosition, t);
            pin.rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        pin.rectTransform.anchoredPosition = targetPosition;
        pin.rectTransform.localScale = targetScale;
    }

    public void MultipleQuestionsToggle(bool value)
    {
        multipleQuestions = value;
        if (value)
        {
            maxQuestions = 3; // Set to 3 for multiple questions
        }
        else
        {
            maxQuestions = 1; // Set to 1 for single question mode
        }
    }

    private void HidePin(Image pin)
    {
        pin.gameObject.SetActive(false);
    }





}

