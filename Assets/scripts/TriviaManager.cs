using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using System.IO;
using UnityEngine.Networking;

public class TriviaManager : MonoBehaviour
{
    // =========================================================================
    // Serialized data types
    // =========================================================================

    [Serializable]
    private class ArtworkPins
    {
        public string artworkName;
        public Image  goldPin;
        public Image  silverPin;
        public Image  bronzePin;
        public Image  voidPin;
    }

    private struct RankingStats
    {
        public int   playerScore;
        public int   rank;
        public int   totalPlayers;
        public int   topPercent;
        public float averageScore;
    }

    // =========================================================================
    // Inspector fields
    // =========================================================================

    [Header("Trivia Settings")]
    [SerializeField] public  string lang           = "en";
    [SerializeField] public  bool   multipleQuestions = true;
    [SerializeField] public  int    maxQuestions    = 3;
    [SerializeField] public  bool   finishGame      = false;
    [SerializeField] public  List<QuestionJson> questions;
    [SerializeField] public  List<AudioClip> audioClips;
    [SerializeField] public  bool   adaptiveQuestions = true;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI  questionTextField;
    [SerializeField] private GameObject       questionBox;
    [SerializeField] private PlayButton       playButton;
    [SerializeField] private TMP_InputField   playerNameInput;
    [SerializeField] private NonNativeKeyboard keyboard;
    [SerializeField] private TextMeshProUGUI  rankingTextField;
    [SerializeField] public  TextMeshProUGUI  menuScore;
    [SerializeField] public  List<Button>     answerButtons;

    [Header("Audio")]
    [SerializeField] private AudioClip correctSound;
    [SerializeField] private AudioClip incorrectSound;
    [SerializeField] private AudioClip endSound;
    [SerializeField] private AudioClip finalSound;

    [Header("Scoring")]
    [SerializeField] private int easyBasePoints   = 10;
    [SerializeField] private int mediumBasePoints  = 20;
    [SerializeField] private int hardBasePoints    = 40;
    [SerializeField] private int streakBonusThreshold = 4;  // Correct answers in a row to trigger bonus
    [SerializeField] private float streakBonusMultiplier = 1.5f; // Score multiplier when on a streak

    [Header("Pins")]
    [SerializeField] private List<ArtworkPins> artworkPins = new List<ArtworkPins>();

    [Header("Data")]
    [SerializeField] private XMLManager xmlManager;
    [SerializeField] private ResultsPanel resultsPanel;

    // =========================================================================
    // Private state
    // =========================================================================

    private readonly string[] difficultyLevels = { "beginner", "hard", "expert" };

    private readonly Dictionary<string, ArtworkPins> artworkPinsByName  = new Dictionary<string, ArtworkPins>();
    public  readonly Dictionary<string, int>          artworkDifficulties = new Dictionary<string, int>();

    private string playerName;
    private string obraActual;
    private int    questionLevel;

    // Session totals
    private int    score = 0;

    // Current run tracking
    private int    currentQuestionIndex;
    private int    currentQuestionAttempt;
    private int    currentRunScore;
    private int    currentRunCorrectAnswers;
    private int    currentRunTotalQuestions;
    private string currentRunDifficulty;

    // Streak tracking
    private int    consecutiveCorrect = 0;

    private bool   hasCompleted = false;

    // =========================================================================
    // Unity lifecycle
    // =========================================================================

    public static TriviaManager instance;

    private void Awake()
    {
        instance = this;
        hideQuestion();
        hideRanking();
        BuildArtworkPinsLookup();
        BuildArtworkDifficultyLookup();
        resultsPanel.gameObject.SetActive(false);

        foreach (var pinSet in artworkPinsByName.Values)
        {
            HidePin(pinSet.goldPin);
            HidePin(pinSet.silverPin);
            HidePin(pinSet.bronzePin);
        }
    }

    // =========================================================================
    // Public interface
    // =========================================================================

    public void StartTrivia(string obra)
    {
        lang = LanguageScript.instance.language;
        maxQuestions  = multipleQuestions ? 3 : 1;
        obraActual    = obra;
        questionLevel = IncreaseArtworkDifficulty(obra);

        int difficultyIndex    = Mathf.Clamp(questionLevel - 1, 0, difficultyLevels.Length - 1);
        currentRunDifficulty   = difficultyLevels[difficultyIndex];
        questions              = JSONtoQ.GeneratePhrases(obra, currentRunDifficulty, lang);

        audioClips = new List<AudioClip>(new AudioClip[questions != null ? questions.Count : 0]);

        currentQuestionIndex      = 0;
        currentQuestionAttempt    = 1;
        currentRunScore           = 0;
        currentRunCorrectAnswers  = 0;
        currentRunTotalQuestions  = questions != null ? questions.Count : 0;
        consecutiveCorrect        = 0;
        hasCompleted              = false;

        UpdateScoreText();

        // Cargar audios de las preguntas en segundo plano; cuando terminen, mostrar la primera
        StartCoroutine(LoadQuestionAudiosAndStart(obra, currentRunDifficulty, lang));
    }

    public void ForceQuit()
    {
        hideAnswers();
        hideQuestion();
        hideRanking();
        resultsPanel.gameObject.SetActive(false);

        if (!hasCompleted)
        {
            DecreaseArtworkDifficulty(obraActual);
        }

        currentQuestionIndex   = 0;
        currentQuestionAttempt = 1;
        UpdateScoreText();
    }

    public void MultipleQuestionsToggle(bool value)
    {
        multipleQuestions = value;
        maxQuestions = value ? 3 : 1;
    }

    // =========================================================================
    // Trivia flow
    // =========================================================================

    private void SetAnswerValues()
    {
        if (currentQuestionIndex >= questions.Count)
        {
            playButton.endplay = true;
            EndTrivia();
            SoundManager.instance.ClearAgentClip();
            currentQuestionIndex = 0;
            return;
        }

        var question = questions[currentQuestionIndex];
        currentQuestionAttempt = 1;
        questionTextField.text = question.question;
        showQuestion();

        for (int i = 0; i < answerButtons.Count; i++)
        {
            var button = answerButtons[i];
            var answer = question.options[i];

            button.onClick.RemoveAllListeners();
            button.GetComponentInChildren<TextMeshProUGUI>().text = answer.choice;
            button.gameObject.SetActive(true);

            bool isCorrect = answer.correct;
            button.onClick.AddListener(() => OnAnswerSelected(isCorrect));
        }

        showAnswers();

        // Reproducir el audio de esta pregunta si está disponible
        PlayQuestionAudio(questionIndex: currentQuestionIndex);
    }

    private void OnAnswerSelected(bool isCorrect)
    {
        if (isCorrect)
        {
            consecutiveCorrect++;

            int pointsEarned = CalculateQuestionScore(currentQuestionAttempt);
            score                   += pointsEarned;
            currentRunScore         += pointsEarned;
            if (currentQuestionAttempt == 1)
            {
                currentRunCorrectAnswers++;
            }

            currentQuestionIndex++;

            if (currentQuestionIndex < questions.Count)
            {
                SoundManager.instance.MakeSound(correctSound, 0.5f);
            }

            UpdateScoreText();
            SetAnswerValues();
        }
        else
        {
            consecutiveCorrect = 0;
            currentQuestionAttempt++;
            UpdateScoreText();
            SoundManager.instance.MakeSound(incorrectSound, 0.5f);
        }
    }

    private void EndTrivia()
    {
        hasCompleted = true;
        hideAnswers();
        hideQuestion();
        // showRanking();

        ShowPinForCurrentDifficulty();

        if (string.IsNullOrWhiteSpace(playerName))
        {
            keyboard.gameObject.SetActive(true);
            rankingTextField.text = "You finished! Enter your name to save your score:";

            string lang         = LanguageScript.instance != null ? LanguageScript.instance.language : "en";
            AudioClip leaderboardClip = Resources.Load<AudioClip>($"Guide/{lang}/leaderboard");
            if (leaderboardClip != null)
                SoundManager.instance.PlayClip(leaderboardClip);
            else
                Debug.LogWarning($"[TriviaManager] Audio 'leaderboard' no encontrado en Resources/Guide/{lang}/");

            NonNativeKeyboard.Instance.CloseOnInactivity = false;
            NonNativeKeyboard.Instance.PresentKeyboard("");
            NonNativeKeyboard.Instance.OnTextSubmitted += OnKeyboardTextSubmitted;
        }
        else
        {
            SaveAndShowResults();
        }

        if (HaveAllArtworksReachedMaxDifficulty() || finishGame)
        {
            SoundManager.instance.MakeSound(finalSound, 0.5f);
            playButton.HideButton();
            return;
        }

        SoundManager.instance.MakeSound(endSound, 0.5f);
    }

    private void OnKeyboardTextSubmitted(object sender, EventArgs e)
    {
        playerName = NonNativeKeyboard.Instance.InputField.text?.Trim().ToUpper();

        if (string.IsNullOrWhiteSpace(playerName))
        {
            rankingTextField.text = $"Your score: {score} points.";
            return;
        }

        SaveAndShowResults();

        NonNativeKeyboard.Instance.Close();
        NonNativeKeyboard.Instance.OnTextSubmitted -= OnKeyboardTextSubmitted;
    }

    // =========================================================================
    // Score calculation
    // =========================================================================

    /// <summary>
    /// Base points scaled by difficulty, halved on each retry, with a streak
    /// bonus applied when the player answers correctly several times in a row.
    /// </summary>
    private int CalculateQuestionScore(int attemptNumber)
    {
        int   basePoints      = GetBasePointsForCurrentDifficulty();
        float attemptMultiplier = GetAttemptMultiplier(attemptNumber);

        if (attemptMultiplier <= 0f)
        {
            return 0;
        }

        float streakMultiplier = (consecutiveCorrect > 0 && consecutiveCorrect % streakBonusThreshold == 0)
            ? streakBonusMultiplier
            : 1f;

        return Mathf.RoundToInt(basePoints * attemptMultiplier * streakMultiplier);
    }

    private int GetBasePointsForCurrentDifficulty()
    {
        return questionLevel switch
        {
            1 => easyBasePoints,
            2 => mediumBasePoints,
            3 => hardBasePoints,
            _ => easyBasePoints,
        };
    }

    private float GetAttemptMultiplier(int attemptNumber)
    {
        return attemptNumber switch
        {
            1 => 1.00f,
            2 => 0.50f,
            3 => 0.25f,
            _ => 0.00f,
        };
    }

    private void UpdateScoreText()
    {
        if (menuScore != null)
        {
            menuScore.text = "SCORE: " + score;
        }
    }

    // =========================================================================
    // Save & ranking display
    // =========================================================================

    private void SaveAndShowResults()
    {
        List<ScoreEntry> scores = xmlManager.LoadDetailedScores();

        var newEntry = new ScoreEntry
        {
            playerName        = playerName,
            score             = currentRunScore,
            totalScoreAfterGame = score,
            difficulty        = currentRunDifficulty,
            exhibitionName    = obraActual,
            correctAnswers    = currentRunCorrectAnswers,
            totalQuestions    = currentRunTotalQuestions,
            date              = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        scores.Add(newEntry);
        xmlManager.SaveDetailedScores(scores);

        RankingStats globalStats    = CalculateGlobalStats(scores, playerName);
        RankingStats levelStats     = CalculateFilteredBestScoreStats(scores, playerName, currentRunDifficulty, filterByDifficulty: true);
        RankingStats exhibitionStats = CalculateFilteredBestScoreStats(scores, playerName, obraActual, filterByDifficulty: false);

        // showRanking();
        // rankingTextField.text = BuildResultsText(globalStats, levelStats, exhibitionStats, scores);
        resultsPanel.Populate(new ResultsData
        {
            playerName       = playerName,
            runScore         = currentRunScore,
            totalScore       = score,
            correctAnswers   = currentRunCorrectAnswers,
            totalQuestions   = currentRunTotalQuestions,
            difficulty       = currentRunDifficulty,
            exhibitionName   = obraActual,
            hasStreak        = consecutiveCorrect >= streakBonusThreshold,
            streakBonus      = consecutiveCorrect >= streakBonusThreshold ? Mathf.RoundToInt(currentRunScore * (streakBonusMultiplier - 1f)) : 0,
            allScores        = scores,
        });
    }

    // =========================================================================
    // Ranking text builder
    // =========================================================================

    private string BuildResultsText(
        RankingStats globalStats,
        RankingStats levelStats,
        RankingStats exhibitionStats,
        List<ScoreEntry> allScores)
    {
        string medal(int rank) => rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{rank}" };

        // ── Header ────────────────────────────────────────────────────────────
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("RESULTS");

        // ── This run summary ──────────────────────────────────────────────────
        sb.AppendLine($"{FormatDifficultyName(currentRunDifficulty).ToUpper()} · {obraActual}");
        sb.AppendLine($"Points:   {currentRunScore}");
        sb.AppendLine();

        // ── Top 5 global leaderboard ──────────────────────────────────────────
        sb.AppendLine("TOP 5 ALL EXHIBITIONS");
        sb.AppendLine("──────────────────────────");

        var topGlobal = BuildTop5(allScores, filterByDifficulty: false, filterValue: null);
        bool playerInTop5 = false;

        for (int i = 0; i < topGlobal.Count; i++)
        {
            var (name, pts) = topGlobal[i];
            bool isPlayer   = name == playerName;
            if (isPlayer) playerInTop5 = true;

            string row = $"{name,-12} {pts,5} pts";
            sb.AppendLine(isPlayer ? "▶" + row.TrimStart() : row);
        }

        // Show player's position if they didn't make the top 5
        if (!playerInTop5)
        {
            sb.AppendLine($"  {medal(globalStats.rank)}  {playerName,-12} {globalStats.playerScore,5} pts  ◀ You");
        }

        sb.AppendLine();

        // ── Personal stats card ───────────────────────────────────────────────
        sb.AppendLine("YOUR STATS");
        sb.AppendLine("──────────────────────────");
        sb.AppendLine($"  Global rank:     {globalStats.rank} / {globalStats.totalPlayers}  (top {globalStats.topPercent}%)");
        sb.AppendLine($"  Global score:    {globalStats.playerScore} pts  (avg {Mathf.RoundToInt(globalStats.averageScore)})");
        sb.AppendLine($"  {FormatDifficultyName(currentRunDifficulty)} rank:      {levelStats.rank} / {levelStats.totalPlayers}");
        sb.AppendLine($"  Best this level: {levelStats.playerScore} pts");
        sb.AppendLine($"  {obraActual} rank: {exhibitionStats.rank} / {exhibitionStats.totalPlayers}");
        sb.AppendLine($"  Best this room:  {exhibitionStats.playerScore} pts");
        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━");

        return sb.ToString();
    }

    /// <summary>Returns the top 5 players by total score, optionally filtered.</summary>
    private List<(string name, int score)> BuildTop5(
        List<ScoreEntry> scores,
        bool filterByDifficulty,
        string filterValue)
    {
        var totals = new Dictionary<string, int>();

        foreach (var entry in scores)
        {
            if (string.IsNullOrWhiteSpace(entry.playerName)) continue;

            if (filterValue != null)
            {
                bool matches = filterByDifficulty
                    ? entry.difficulty == filterValue
                    : entry.exhibitionName == filterValue;
                if (!matches) continue;
            }

            if (!totals.ContainsKey(entry.playerName))
            {
                totals[entry.playerName] = 0;
            }
            totals[entry.playerName] += entry.score;
        }

        var sorted = new List<(string name, int score)>();
        foreach (var kv in totals)
        {
            sorted.Add((kv.Key, kv.Value));
        }
        sorted.Sort((a, b) => b.score.CompareTo(a.score));

        return sorted.Count > 5 ? sorted.GetRange(0, 5) : sorted;
    }

    private string FormatDifficultyName(string difficulty)
    {
        return difficulty switch
        {
            "beginner" => "Easy",
            "hard"     => "Medium",
            "expert"   => "Hard",
            _          => difficulty,
        };
    }

    // =========================================================================
    // Ranking stat helpers
    // =========================================================================

    private RankingStats CalculateGlobalStats(List<ScoreEntry> scores, string targetPlayer)
    {
        var totalByPlayer = new Dictionary<string, int>();

        foreach (var entry in scores)
        {
            if (string.IsNullOrWhiteSpace(entry.playerName)) continue;
            if (!totalByPlayer.ContainsKey(entry.playerName)) totalByPlayer[entry.playerName] = 0;
            totalByPlayer[entry.playerName] += entry.score;
        }

        int playerScore = totalByPlayer.ContainsKey(targetPlayer) ? totalByPlayer[targetPlayer] : 0;
        return BuildRankingStats(totalByPlayer, playerScore);
    }

    private RankingStats CalculateFilteredBestScoreStats(
        List<ScoreEntry> scores,
        string targetPlayer,
        string filterValue,
        bool filterByDifficulty)
    {
        var bestByPlayer = new Dictionary<string, int>();

        foreach (var entry in scores)
        {
            if (string.IsNullOrWhiteSpace(entry.playerName)) continue;

            bool matches = filterByDifficulty
                ? entry.difficulty    == filterValue
                : entry.exhibitionName == filterValue;
            if (!matches) continue;

            if (!bestByPlayer.ContainsKey(entry.playerName) || entry.score > bestByPlayer[entry.playerName])
            {
                bestByPlayer[entry.playerName] = entry.score;
            }
        }

        int playerScore = bestByPlayer.ContainsKey(targetPlayer) ? bestByPlayer[targetPlayer] : currentRunScore;
        return BuildRankingStats(bestByPlayer, playerScore);
    }

    private RankingStats BuildRankingStats(Dictionary<string, int> scoresByPlayer, int playerScore)
    {
        var stats = new RankingStats { playerScore = playerScore };
        stats.totalPlayers = scoresByPlayer.Count;

        if (stats.totalPlayers == 0)
        {
            stats.rank       = 1;
            stats.topPercent = 100;
            return stats;
        }

        int betterPlayers = 0;
        int totalScore    = 0;

        foreach (int value in scoresByPlayer.Values)
        {
            if (value > playerScore) betterPlayers++;
            totalScore += value;
        }

        stats.rank         = betterPlayers + 1;
        stats.averageScore = (float)totalScore / stats.totalPlayers;
        stats.topPercent   = Mathf.CeilToInt((float)stats.rank / stats.totalPlayers * 100f);

        return stats;
    }

    // =========================================================================
    // Difficulty progression
    // =========================================================================

    private int GetArtworkDifficulty(string obra)
    {
        if (string.IsNullOrWhiteSpace(obra)) return 0;
        return artworkDifficulties.TryGetValue(obra, out int level) ? level : 0;
    }

    private int IncreaseArtworkDifficulty(string obra)
    {
        int updated = Mathf.Min(GetArtworkDifficulty(obra) + 1, maxQuestions);
        artworkDifficulties[obra] = updated;
        return updated;
    }

    private void DecreaseArtworkDifficulty(string obra)
    {
        artworkDifficulties[obra] = Mathf.Max(GetArtworkDifficulty(obra) - 1, 0);
    }

    private bool HaveAllArtworksReachedMaxDifficulty()
    {
        if (artworkDifficulties.Count == 0) return false;

        foreach (int level in artworkDifficulties.Values)
        {
            if (level < maxQuestions) return false;
        }

        return true;
    }

    // =========================================================================
    // Pins
    // =========================================================================

    private void ShowPinForCurrentDifficulty()
    {
        int nivel = GetArtworkDifficulty(obraActual);

        if (!multipleQuestions)
        {
            MostrarPinDorado(obraActual);
            return;
        }

        if      (nivel == 1) MostrarPinBronce(obraActual);
        else if (nivel == 2) MostrarPinPlateado(obraActual);
        else if (nivel >= 3) MostrarPinDorado(obraActual);
    }

    private void MostrarPinDorado(string obra)
    {
        if (!TryGetPinsForArtwork(obra, out var pinSet)) return;
        HidePin(pinSet.silverPin);
        if (pinSet.goldPin != null) StartCoroutine(FlipAndScaleIn(pinSet.goldPin.transform));
    }

    private void MostrarPinPlateado(string obra)
    {
        if (!TryGetPinsForArtwork(obra, out var pinSet)) return;
        if (pinSet.silverPin != null) StartCoroutine(FlipAndScaleIn(pinSet.silverPin.transform));
        HidePin(pinSet.bronzePin);
    }

    private void MostrarPinBronce(string obra)
    {
        if (!TryGetPinsForArtwork(obra, out var pinSet)) return;
        if (pinSet.bronzePin != null) StartCoroutine(FlipAndScaleIn(pinSet.bronzePin.transform));
    }

    private void HidePin(Image pin)
    {
        if (pin != null) pin.gameObject.SetActive(false);
    }

    private bool TryGetPinsForArtwork(string obra, out ArtworkPins pinSet)
    {
        return artworkPinsByName.TryGetValue(obra, out pinSet);
    }

    private void BuildArtworkPinsLookup()
    {
        artworkPinsByName.Clear();
        foreach (var pinSet in artworkPins)
        {
            if (pinSet != null && !string.IsNullOrWhiteSpace(pinSet.artworkName))
            {
                artworkPinsByName[pinSet.artworkName] = pinSet;
            }
        }
    }

    private void BuildArtworkDifficultyLookup()
    {
        artworkDifficulties.Clear();
        foreach (var pinSet in artworkPins)
        {
            if (pinSet != null && !string.IsNullOrWhiteSpace(pinSet.artworkName))
            {
                artworkDifficulties.TryAdd(pinSet.artworkName, 0);
            }
        }
    }

    // =========================================================================
    // Pin animations
    // =========================================================================

    private IEnumerator FlipAndScaleIn(Transform target)
    {
        if (target.gameObject.activeSelf) yield break;

        target.gameObject.SetActive(true);
        target.localScale        = Vector3.zero;
        target.localEulerAngles  = new Vector3(-720f, 0f, 0f);

        float duration = 1f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            target.localScale       = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            target.localEulerAngles = new Vector3(Mathf.Lerp(-720f, 0f, t), 0f, 0f);

            yield return null;
        }

        target.localScale       = Vector3.one;
        target.localEulerAngles = Vector3.zero;
    }

    private void MoveAllPinsToCenter()
    {
        const float spacing   = 260f;
        const float centerX   = -820f;
        const float centerY   = -475f;
        var         targetScale = Vector3.one * 1.2f;

        var validPinSets = new List<ArtworkPins>();
        RectTransform sharedParent = null;

        foreach (var pinSet in artworkPins)
        {
            if (pinSet == null) continue;
            if (pinSet.goldPin == null && pinSet.voidPin == null) continue;

            if (sharedParent == null)
            {
                var reference = pinSet.goldPin != null ? pinSet.goldPin : pinSet.voidPin;
                if (reference != null) sharedParent = reference.rectTransform.parent as RectTransform;
            }

            validPinSets.Add(pinSet);
        }

        if (validPinSets.Count == 0 || sharedParent == null) return;

        float centerIndex = (validPinSets.Count - 1f) / 2f;

        for (int i = 0; i < validPinSets.Count; i++)
        {
            var    pinSet   = validPinSets[i];
            float  x        = centerX + ((i - centerIndex) * spacing);
            var    target   = new Vector2(x, centerY);

            if (pinSet.goldPin != null)  StartCoroutine(MoveAndScalePin(pinSet.goldPin,  target, targetScale, sharedParent));
            if (pinSet.voidPin != null)  StartCoroutine(MoveAndScalePin(pinSet.voidPin,  target, targetScale, sharedParent));

            HidePin(pinSet.silverPin);
            HidePin(pinSet.bronzePin);
        }
    }

    private IEnumerator MoveAndScalePin(Image pin, Vector2 targetPosition, Vector3 targetScale, RectTransform sharedParent)
    {
        if (pin == null) yield break;

        RectTransform pinRect = pin.rectTransform;

        if (sharedParent != null && pinRect.parent != sharedParent)
        {
            pinRect.SetParent(sharedParent, worldPositionStays: true);
        }

        pinRect.anchorMin = new Vector2(0.5f, 0.5f);
        pinRect.anchorMax = new Vector2(0.5f, 0.5f);
        pinRect.pivot     = new Vector2(0.5f, 0.5f);

        Vector2 startPosition = pinRect.anchoredPosition;
        Vector3 startScale    = pinRect.localScale;

        pin.gameObject.SetActive(true);

        float duration = 1f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            pinRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            pinRect.localScale       = Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        pinRect.anchoredPosition = targetPosition;
        pinRect.localScale       = targetScale;
    }

    // =========================================================================
    // UI visibility helpers
    // =========================================================================
    public string GetDifficultyString(string roomName)
    {
        int index = artworkDifficulties.TryGetValue(roomName, out int level)
            ? Mathf.Clamp(level, 0, difficultyLevels.Length - 1)
            : 0;
        return difficultyLevels[index];
    }
    public void showQuestion()  => questionBox.gameObject.SetActive(true);
    public void hideQuestion()  => questionBox.gameObject.SetActive(false);
    public void showRanking()   => rankingTextField.gameObject.SetActive(true);
    public void showResultsPanel() => resultsPanel.gameObject.SetActive(true);
    public void hideResultsPanel() => resultsPanel.gameObject.SetActive(false);
    public void hideRanking()   => rankingTextField.gameObject.SetActive(false);

    public void showAnswers()
    {
        foreach (var b in answerButtons) b.gameObject.SetActive(true);
    }

    public void hideAnswers()
    {
        foreach (var b in answerButtons) b.gameObject.SetActive(false);
    }



    // =========================================================================
    // Question audio loading & playback
    // =========================================================================

    /// <summary>
    /// Carga en segundo plano los MP3 numerados (1.mp3 … N.mp3) para la combinación
    /// obra / nivel / idioma desde StreamingAssets. Cuando todos están listos (o han
    /// fallado) muestra la primera pregunta.
    ///
    /// Ruta esperada:
    ///   StreamingAssets/LangGraph/Resources/Questions/{lang}/{obra}/{difficulty}/{n}.mp3
    /// </summary>
    private IEnumerator LoadQuestionAudiosAndStart(string obra, string difficulty, string language)
    {
        int count = questions != null ? questions.Count : 0;

        for (int i = 0; i < count; i++)
        {
            string fileName = $"{i + 1}.wav";
            audioClips[i] = null;

            // --- Tier 1: StreamingAssets (solo grupo experimental) ---
            if (adaptiveQuestions)
            {
                string streamingPath = Path.Combine(
                    Application.streamingAssetsPath, "LangGraph", "Resources", "Questions",
                    language, obra, difficulty, fileName);

                string url = new System.Uri(Path.GetFullPath(streamingPath)).AbsoluteUri;
                using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    audioClips[i] = DownloadHandlerAudioClip.GetContent(req);
                    Debug.Log($"[TriviaManager] Audio cargado (StreamingAssets): {streamingPath}");
                    continue;
                }
            }

            // --- Tier 2: Resources (bundled, siempre disponible) ---
            string resourcePath = $"Questions/{language}/{obra}/{difficulty}/{i + 1}";
            AudioClip bundled   = Resources.Load<AudioClip>(resourcePath);

            if (bundled != null)
            {
                audioClips[i] = bundled;
                Debug.Log($"[TriviaManager] Audio cargado (Resources): {resourcePath}");
            }
            else
            {
                Debug.LogWarning($"[TriviaManager] Audio no encontrado: {fileName}");
            }
        }

        // Todos los clips procesados → mostrar primera pregunta
        SetAnswerValues();
    }

    /// <summary>
    /// Reproduce el clip de la pregunta indicada si existe y hay un AudioSource asignado.
    /// Si questionAudioSource es null intenta usar el AudioSource principal de SoundManager.
    /// </summary>
    private void PlayQuestionAudio(string name = null, int questionIndex = 0)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            // Tier 1: buscar en los clips ya cargados
            AudioClip clip = audioClips?.Find(c => c != null && c.name == name);

            // Tier 2: cargar desde Resources/Guide/{lang}/{name}
            if (clip == null)
            {
                string lang         = LanguageScript.instance != null ? LanguageScript.instance.language : "en";
                string resourcePath = $"Guide/{lang}/{name}";
                clip                = Resources.Load<AudioClip>(resourcePath);
                if (clip != null)
                    Debug.Log($"[TriviaManager] Audio cargado desde Resources: {resourcePath}");
                else
                    Debug.LogWarning($"[TriviaManager] No se encontró '{name}' en audioClips ni en Resources/{resourcePath}");
            }

            if (clip != null)
            {
                SoundManager.instance.PlayClip(clip);
                Debug.Log($"[TriviaManager] Reproduciendo audio '{name}'");
            }
            return;
        }

        // Sin nombre → reproducir por índice de pregunta
        if (audioClips == null || questionIndex >= audioClips.Count) return;

        AudioClip indexedClip = audioClips[questionIndex];
        if (indexedClip == null) return;

        SoundManager.instance.PlayClip(indexedClip);
        Debug.Log($"[TriviaManager] Reproduciendo audio pregunta {questionIndex + 1}");
    }
}