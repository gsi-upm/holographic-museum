// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using TMPro;
// using System.Threading.Tasks;
// using UnityEngine.UI;
// using Microsoft.MixedReality.Toolkit.Experimental.UI;
// using System;
// using UnityEngine.Windows.Speech;

// public class TriviaManager2 : MonoBehaviour
// {
//     [Serializable]
//     private class ArtworkPins
//     {
//         public string artworkName;
//         public Image goldPin;
//         public Image silverPin;
//         public Image bronzePin;
//         public Image voidPin;
//     }

//     [Header("Trivia Settings")]
//     [SerializeField] public String lang = "en"; // This variable is not used in this version of the script, but can be used for future levels or difficulties
//     [SerializeField] public bool finishGame = false; // This variable is not used in this version of the script, but can be used for future levels or difficulties
//     [SerializeField] public List<QuestionJson> questions;
//     [SerializeField] private TextMeshProUGUI questionTextField;
//     [SerializeField] private GameObject questionBox; // Reference to the question box RectTransform
//     [SerializeField] private PlayButton playButton;
//     public List<Button> answerButtons;
//     private int currentQuestionIndex;
//     [Header("Score and Ranking")]
//     public TextMeshProUGUI menuScore;
//     [SerializeField] private AudioClip correctSound;
//     [SerializeField] private AudioClip incorrectSound;
//     [SerializeField] private AudioClip endSound;
//     [SerializeField] private AudioClip finalSound;
//     [SerializeField] private TMP_InputField playerNameInput;
//     [SerializeField] private XMLManager xmlManager;
//     [SerializeField] private NonNativeKeyboard keyboard; // Reference to the TextMeshProUGUI for high scores
//     [SerializeField] private TextMeshProUGUI rankingTextField;

//     [Header("Pins")]
//     [SerializeField] private List<ArtworkPins> artworkPins = new List<ArtworkPins>();

//     private string playerName;
//     private string obraActual;
//     private int questionLevel = 0; // This variable is not used in this version of the script, but can be used for future levels or difficulties
//     private readonly string[] difficultyLevels = { "beginner", "hard", "expert" };

//     [Header("Difficulty Levels")]

//     private int score = 0;
//     private bool hasCompleted = false;
//     public bool multipleQuestions = true; // This variable is not used in this version of the script, but can be used for future levels or difficulties
//     public int maxQuestions = 3; // This variable is not used in this version of the script, but can be used for future levels or difficulties
//     private readonly Dictionary<string, ArtworkPins> artworkPinsByName = new Dictionary<string, ArtworkPins>();
//     public readonly Dictionary<string, int> artworkDifficulties = new Dictionary<string, int>();


//     private void Awake()
//     {
//         hideQuestion();
//         hideRanking();
//         BuildArtworkPinsLookup();
//         BuildArtworkDifficultyLookup();

//         foreach (var pinSet in artworkPinsByName.Values)
//         {
//             HidePin(pinSet.goldPin);
//             HidePin(pinSet.silverPin);
//             HidePin(pinSet.bronzePin);
//         }

//     }

//     private void BuildArtworkPinsLookup()
//     {
//         artworkPinsByName.Clear();

//         foreach (var pinSet in artworkPins)
//         {
//             if (pinSet == null || string.IsNullOrWhiteSpace(pinSet.artworkName))
//             {
//                 continue;
//             }

//             artworkPinsByName[pinSet.artworkName] = pinSet;
//         }
//     }

//     private bool TryGetPinsForArtwork(string obra, out ArtworkPins pinSet)
//     {
//         return artworkPinsByName.TryGetValue(obra, out pinSet);
//     }

//     private void BuildArtworkDifficultyLookup()
//     {
//         artworkDifficulties.Clear();

//         foreach (var pinSet in artworkPins)
//         {
//             if (pinSet == null || string.IsNullOrWhiteSpace(pinSet.artworkName))
//             {
//                 continue;
//             }

//             if (!artworkDifficulties.ContainsKey(pinSet.artworkName))
//             {
//                 artworkDifficulties.Add(pinSet.artworkName, 0);
//             }
//         }
//     }

//     private int GetArtworkDifficulty(string obra)
//     {
//         if (string.IsNullOrWhiteSpace(obra))
//         {
//             return 0;
//         }

//         if (!artworkDifficulties.TryGetValue(obra, out int level))
//         {
//             artworkDifficulties[obra] = 0;
//             return 0;
//         }

//         return level;
//     }

//     private int IncreaseArtworkDifficulty(string obra)
//     {
//         int currentLevel = GetArtworkDifficulty(obra);
//         int updatedLevel = Mathf.Min(currentLevel + 1, maxQuestions);
//         artworkDifficulties[obra] = updatedLevel;
//         return updatedLevel;
//     }

//     private void DecreaseArtworkDifficulty(string obra)
//     {
//         int currentLevel = GetArtworkDifficulty(obra);
//         artworkDifficulties[obra] = Mathf.Max(currentLevel - 1, 0);
//     }

//     private bool HaveAllArtworksReachedMaxDifficulty()
//     {
//         if (artworkDifficulties.Count == 0)
//         {
//             return false;
//         }

//         foreach (int level in artworkDifficulties.Values)
//         {
//             if (level < maxQuestions)
//             {
//                 return false;
//             }
//         }

//         return true;
//     }


//     public void StartTrivia(string obra)
//     {
//         //The comented section is for the AiQuestion script, which is not used in this version of the script
//         /*await Task.Delay(100); // Optional: Small delay to ensure async operations complete
//         currentQuestionIndex = 0;

//          Usa las preguntas generadas si están disponibles
//         if (AiQuestion.GeneratedQuestions != null && AiQuestion.GeneratedQuestions.Count > 0)
//         {
//             questions = AiQuestion.GeneratedQuestions;
//         }
//         else
//         {
//             questions = JSONtoQ.GeneratePhrases(); // Carga desde el archivo si no hay preguntas generadas
//         }*/
//         if (multipleQuestions)
//         {
//             maxQuestions = 3;
//         }
//         else
//         {
//             maxQuestions = 1;
//         }

//         obraActual = obra;
//         questionLevel = IncreaseArtworkDifficulty(obra);

//         // Usa el nivel de dificultad correspondiente (empieza en 0)
//         string dificultad = difficultyLevels[Mathf.Clamp(questionLevel - 1, 0, difficultyLevels.Length - 1)];
//         questions = JSONtoQ.GeneratePhrases(obra, dificultad, lang); // Carga desde el archivo
//         menuScore.text = "YOUR SCORE: " + score.ToString();
//         hasCompleted = false;
//         SetAnswerValues();
//     }


//     private void SetAnswerValues()
//     {
//         if (currentQuestionIndex >= questions.Count)
//         {
//             playButton.endplay = true; // Toggle the play button to stop the trivia
//             EndTrivia();
//             currentQuestionIndex = 0; // Reset the question index for the next round
//         }
//         else
//         {
//             // Set the question text
//             var question = questions[currentQuestionIndex];
//             questionTextField.text = question.question;
//             showQuestion();

//             // Set up the answer buttons
//             for (int i = 0; i < answerButtons.Count; i++)
//             {
//                 var button = answerButtons[i];
//                 var answer = question.options[i];

//                 // Limpia los listeners existentes
//                 button.onClick.RemoveAllListeners();

//                 button.GetComponentInChildren<TextMeshProUGUI>().text = answer.choice;

//                 button.gameObject.SetActive(true);
//                 bool isCorrect = answer.correct;

//                 showAnswers();
//                 button.onClick.AddListener(() => OnAnswwerSelected(isCorrect));
//             }
//         }
//     }

//     void OnAnswwerSelected(bool isCorrect)
//     {
//         if (isCorrect)
//         {
//             currentQuestionIndex++;
//             score++;
//             if (currentQuestionIndex < questions.Count)
//             {
//                 SoundManager.instance.MakeSound(correctSound, 0.5f);
//             }
//             menuScore.text = "YOUR SCORE: " + score.ToString();
//             SetAnswerValues();
//         }
//         else
//         {
//             SoundManager.instance.MakeSound(incorrectSound, 0.5f);
//         }
//     }

//     /*private List<string> RandomizeAnswers(List<string> originalList)
//     {
//         bool correctAnswerChosen = false;

//         List<string>  newList = new List<string>();

//         for(int i = 0; i < answerButtons.Count; i++)
//         {
//             // Get a random number of the remaining choices
//             int random = Random.Range(0, originalList.Count);

//             // If the random number is 0, this is the correct answer, MAKE SURE THIS IS ONLY USED ONCE
//             if(random == 0 && !correctAnswerChosen)
//             {
//                 correctAnswerChoice = i;
//                 correctAnswerChosen = true;
//             }

//             // Add this to the new list
//             newList.Add(originalList[random]);
//             //Remove this choice from the original list (it has been used)
//             originalList.RemoveAt(random);  
//         }


//         return newList;
//     }*/

//     public void EndTrivia()
//     {
//         hasCompleted = true;
//         hideAnswers();
//         hideQuestion();
//         showRanking();

//         int nivel = GetArtworkDifficulty(obraActual);
//         if (multipleQuestions)
//         {
//             if (nivel == 1)
//             {
//                 MostrarPinBronce(obraActual);
//             }
//             else if (nivel == 2)
//             {
//                 MostrarPinPlateado(obraActual);
//             }
//             else if (nivel == 3)
//             {
//                 MostrarPinDorado(obraActual);
//             }
//         }
//         else
//         {
//             MostrarPinDorado(obraActual);
//         }

//         if (string.IsNullOrWhiteSpace(playerName))
//         {
//             keyboard.gameObject.SetActive(true);
//             rankingTextField.text = "You have finished the first level! Write your name to save your score:";
//             NonNativeKeyboard.Instance.CloseOnInactivity = false;
//             NonNativeKeyboard.Instance.PresentKeyboard("");
//             NonNativeKeyboard.Instance.OnTextSubmitted += OnKeyboardTextSubmitted;
//         }
//         else
//         {
//             loadScores();
//         }

//         // Si ha completado todas las obras, mover todos los pins al centro
//         if (HaveAllArtworksReachedMaxDifficulty() || finishGame)
//         {
//             SoundManager.instance.MakeSound(finalSound, 0.5f);
//             playButton.HideButton();
//             // MoveAllPinsToCenter();
//             return;
//         }
//         SoundManager.instance.MakeSound(endSound, 0.5f);

//     }

//     private void OnKeyboardTextSubmitted(object sender, EventArgs e)
//     {
//         playerName = NonNativeKeyboard.Instance.InputField.text;

//         // Si no hay nombre, no guardar
//         if (string.IsNullOrWhiteSpace(playerName))
//         {
//             questionTextField.text = "Tu puntuación es: " + score + ".\n\n";
//             return;
//         }

//         playerName = playerName.ToUpper();

//         loadScores();

//         // Cerrar teclado y desuscribir
//         NonNativeKeyboard.Instance.Close();
//         NonNativeKeyboard.Instance.OnTextSubmitted -= OnKeyboardTextSubmitted;
//     }

//     private void loadScores()
//     {
//         // Cargar y actualizar las puntuaciones
//         List<HighScoreEntry> scores = xmlManager.LoadScores() ?? new List<HighScoreEntry>();

//         var existing = scores.Find(e => e.name == playerName);
//         if (existing != null)
//         {
//             existing.score += score;
//         }
//         else
//         {
//             scores.Add(new HighScoreEntry { name = playerName, score = score });
//         }

//         // Ordenar y guardar
//         scores.Sort((a, b) => b.score.CompareTo(a.score));
//         if (scores.Count > 10) scores = scores.GetRange(0, 10);

//         xmlManager.SaveScores(scores);

//         // Mostrar el mensaje de final y las 5 mejores puntuaciones
//         string ranking = "Top 5 scores:\n";
//         for (int i = 0; i < Mathf.Min(5, scores.Count); i++)
//         {
//             var entry = scores[i];
//             ranking += $"{i + 1}. {entry.name} - {entry.score} points\n";
//         }

//         showRanking();
//         rankingTextField.text = "Your score is: " + score + ".\n\n" + ranking;
//     }

//     private IEnumerator FlipAndScaleIn(Transform target)
//     {
//         if (target.gameObject.activeSelf)
//             yield break;

//         target.gameObject.SetActive(true);
//         target.localScale = Vector3.zero;
//         target.localEulerAngles = new Vector3(-720f, 0f, 0f); // varias vueltas hacia atrás

//         float duration = 1f; // más tiempo para que se note el giro
//         float elapsed = 0f;

//         while (elapsed < duration)
//         {
//             elapsed += Time.deltaTime;
//             float t = elapsed / duration;

//             // Interpolación de escala (con suavizado)
//             target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, Mathf.SmoothStep(0f, 1f, t));

//             // Rotación con varias vueltas
//             float angleX = Mathf.Lerp(-720f, 0f, Mathf.SmoothStep(0f, 1f, t));
//             target.localEulerAngles = new Vector3(angleX, 0f, 0f);

//             yield return null;
//         }

//         // Asegura valores finales
//         target.localScale = Vector3.one;
//         target.localEulerAngles = Vector3.zero;
//     }

//     public void ForceQuit()
//     {
//         hideAnswers();
//         hideQuestion();
//         hideRanking();
//         if (!hasCompleted)
//         {
//             DecreaseArtworkDifficulty(obraActual);
//         }
//         currentQuestionIndex = 0;
//         score = score - currentQuestionIndex;
//     }


//     public void showQuestion()
//     {
//         questionBox.gameObject.SetActive(true);
//     }

//     public void hideQuestion()
//     {
//         questionBox.gameObject.SetActive(false);
//     }

//     public void hideRanking()
//     {
//         rankingTextField.gameObject.SetActive(false);
//     }

//     public void showRanking()
//     {
//         rankingTextField.gameObject.SetActive(true);
//     }

//     public void showAnswers()
//     {
//         for (int i = 0; i < answerButtons.Count; i++)
//         {
//             answerButtons[i].gameObject.SetActive(true);
//         }
//     }

//     public void hideAnswers()
//     {
//         for (int i = 0; i < answerButtons.Count; i++)
//         {
//             answerButtons[i].gameObject.SetActive(false);
//         }
//     }

//     private void MostrarPinDorado(string obra)
//     {
//         if (!TryGetPinsForArtwork(obra, out ArtworkPins pinSet))
//         {
//             return;
//         }

//         HidePin(pinSet.silverPin);
//         if (pinSet.goldPin != null)
//         {
//             StartCoroutine(FlipAndScaleIn(pinSet.goldPin.transform));
//         }
//     }

//     private void MostrarPinPlateado(string obra)
//     {
//         if (!TryGetPinsForArtwork(obra, out ArtworkPins pinSet))
//         {
//             return;
//         }

//         if (pinSet.silverPin != null)
//         {
//             StartCoroutine(FlipAndScaleIn(pinSet.silverPin.transform));
//         }

//         HidePin(pinSet.bronzePin);
//     }

//     private void MostrarPinBronce(string obra)
//     {
//         if (!TryGetPinsForArtwork(obra, out ArtworkPins pinSet))
//         {
//             return;
//         }

//         if (pinSet.bronzePin != null)
//         {
//             StartCoroutine(FlipAndScaleIn(pinSet.bronzePin.transform));
//         }
//     }

//     private void MoveAllPinsToCenter()
//     {
//         float spacing = 260f;
//         float centerX = -820f;
//         float centerY = -475f;
//         Vector3 targetScale = Vector3.one * 1.2f;

//         List<ArtworkPins> validPinSets = new List<ArtworkPins>();
//         RectTransform sharedParent = null;

//         foreach (ArtworkPins pinSet in artworkPins)
//         {
//             if (pinSet == null)
//             {
//                 continue;
//             }

//             if (pinSet.goldPin == null && pinSet.voidPin == null)
//             {
//                 continue;
//             }

//             if (sharedParent == null)
//             {
//                 Image referencePin = pinSet.goldPin != null ? pinSet.goldPin : pinSet.voidPin;
//                 if (referencePin != null)
//                 {
//                     sharedParent = referencePin.rectTransform.parent as RectTransform;
//                 }
//             }

//             validPinSets.Add(pinSet);
//         }

//         int count = validPinSets.Count;
//         if (count == 0 || sharedParent == null)
//         {
//             return;
//         }

//         float centerIndex = (count - 1f) / 2f;

//         for (int i = 0; i < count; i++)
//         {
//             ArtworkPins pinSet = validPinSets[i];
//             float x = centerX + ((i - centerIndex) * spacing);
//             Vector2 targetPosition = new Vector2(x, centerY);

//             if (pinSet.goldPin != null)
//             {
//                 StartCoroutine(MoveAndScalePin(pinSet.goldPin, targetPosition, targetScale, sharedParent));
//             }

//             if (pinSet.voidPin != null)
//             {
//                 StartCoroutine(MoveAndScalePin(pinSet.voidPin, targetPosition, targetScale, sharedParent));
//             }

//             HidePin(pinSet.silverPin);
//             HidePin(pinSet.bronzePin);
//         }
//     }


//     private IEnumerator MoveAndScalePin(Image pin, Vector2 targetPosition, Vector3 targetScale, RectTransform sharedParent)
//     {
//         if (pin == null)
//         {
//             yield break;
//         }

//         float duration = 1f;
//         float elapsed = 0f;

//         RectTransform pinRect = pin.rectTransform;

//         if (sharedParent != null && pinRect.parent != sharedParent)
//         {
//             pinRect.SetParent(sharedParent, true);
//         }

//         pinRect.anchorMin = new Vector2(0.5f, 0.5f);
//         pinRect.anchorMax = new Vector2(0.5f, 0.5f);
//         pinRect.pivot = new Vector2(0.5f, 0.5f);

//         Vector2 startPosition = pinRect.anchoredPosition;
//         Vector3 startScale = pinRect.localScale;

//         pin.gameObject.SetActive(true);

//         while (elapsed < duration)
//         {
//             elapsed += Time.deltaTime;
//             float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

//             pinRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
//             pinRect.localScale = Vector3.Lerp(startScale, targetScale, t);

//             yield return null;
//         }

//         pinRect.anchoredPosition = targetPosition;
//         pinRect.localScale = targetScale;
//     }

//     public void MultipleQuestionsToggle(bool value)
//     {
//         multipleQuestions = value;
//         if (value)
//         {
//             maxQuestions = 3; // Set to 3 for multiple questions
//         }
//         else
//         {
//             maxQuestions = 1; // Set to 1 for single question mode
//         }
//     }

//     private void HidePin(Image pin)
//     {
//         if (pin != null)
//         {
//             pin.gameObject.SetActive(false);
//         }
//     }





// }

