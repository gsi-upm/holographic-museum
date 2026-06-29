using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ResultsPanel : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image      difficultyBadge;

    [Header("Metrics")]
    [SerializeField] private TextMeshProUGUI runScoreText;
    [SerializeField] private TextMeshProUGUI runScore;
    [SerializeField] private TextMeshProUGUI accuracyText;
    [SerializeField] private TextMeshProUGUI accuracy;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private TextMeshProUGUI totalScore;

    [Header("Leaderboard")]
    [SerializeField] private Image           firstPlace;
    [SerializeField] private Image           secondPlace;
    [SerializeField] private Image           thirdPlace;
    [SerializeField] private Image           yourPlace;
    [SerializeField] private GameObject      youTag;

    private TextMeshProUGUI firstPlaceName;
    private TextMeshProUGUI firstPlaceScore;
    private TextMeshProUGUI secondPlaceName;
    private TextMeshProUGUI secondPlaceScore;
    private TextMeshProUGUI thirdPlaceName;
    private TextMeshProUGUI thirdPlaceScore;
    private TextMeshProUGUI yourPlaceName;
    private TextMeshProUGUI yourPlaceScore;

    private static readonly Color32 EasyBg     = new Color32(93,  202, 165, 46);  // 0.18 × 255
    private static readonly Color32 EasyBorder = new Color32(93,  202, 165, 89);  // 0.35 × 255
    private static readonly Color32 EasyText   = new Color32(159, 225, 203, 255); // #9FE1CB

    private static readonly Color32 MediumBg     = new Color32(239, 159, 39,  46);
    private static readonly Color32 MediumBorder = new Color32(239, 159, 39,  89);
    private static readonly Color32 MediumText   = new Color32(250, 199, 117, 255); // #FAC775

    private static readonly Color32 HardBg     = new Color32(212, 83,  126, 46);
    private static readonly Color32 HardBorder = new Color32(212, 83,  126, 89);
    private static readonly Color32 HardText   = new Color32(244, 192, 209, 255); // #F4C0D1

    // -------------------------------------------------------------------------

    private void Awake()
    {
        var firstPlaceTexts  = firstPlace?.GetComponentsInChildren<TextMeshProUGUI>();
        var secondPlaceTexts = secondPlace?.GetComponentsInChildren<TextMeshProUGUI>();
        var thirdPlaceTexts  = thirdPlace?.GetComponentsInChildren<TextMeshProUGUI>();
        var yourPlaceTexts   = yourPlace?.GetComponentsInChildren<TextMeshProUGUI>();

        firstPlaceName  = firstPlaceTexts[0];
        firstPlaceScore = firstPlaceTexts[1];
        secondPlaceName  = secondPlaceTexts[0];
        secondPlaceScore = secondPlaceTexts[1];
        thirdPlaceName  = thirdPlaceTexts[0];
        thirdPlaceScore = thirdPlaceTexts[1];
        yourPlaceName   = yourPlaceTexts[0];
        yourPlaceScore  = yourPlaceTexts[1];

        gameObject.SetActive(false);
    }
    public void Populate(ResultsData data)
    {
        PopulateHeader(data);
        PopulateMetrics(data);
        PopulateLeaderboard(data);

        gameObject.SetActive(true);
    }

    // -------------------------------------------------------------------------
    // Header
    // -------------------------------------------------------------------------

    private void PopulateHeader(ResultsData data)
    {
        if (titleText    != null) titleText.text   = data.exhibitionName;
        difficultyBadge.GetComponentInChildren<TMP_Text>().text = FormatDifficulty(data.difficulty);
        SetDifficultyBadge(data.difficulty);
    }

    // -------------------------------------------------------------------------
    // Metrics
    // -------------------------------------------------------------------------

    private void PopulateMetrics(ResultsData data)
    {
        int pct = data.totalQuestions > 0
            ? Mathf.RoundToInt((float)data.correctAnswers / data.totalQuestions * 100f)
            : 0;

        if (runScore    != null) runScore.text    = data.runScore.ToString();
        if (accuracy    != null) accuracy.text    = $"{pct}%";
        if (accuracyText != null) accuracyText.text = $"\n\n\n\n\n\n {data.correctAnswers} out of {data.totalQuestions} correct";
        if (totalScore  != null) totalScore.text  = data.totalScore.ToString();
    }

    // -------------------------------------------------------------------------
    // Leaderboard
    // -------------------------------------------------------------------------

    private void PopulateLeaderboard(ResultsData data)
    {
        List<(string name, int score)> top3 = BuildTop3(data.allScores);

        // Rellenar las tres filas fijas (vaciar si no hay suficientes entradas)
        SetRow(firstPlaceName, firstPlaceScore,  top3, 0);
        SetRow(secondPlaceName, secondPlaceScore, top3, 1);
        SetRow(thirdPlaceName, thirdPlaceScore,  top3, 2);

        // Buscar si el jugador actual está entre los tres primeros
        int playerRank = -1;
        for (int i = 0; i < top3.Count; i++)
        {
            if (top3[i].name == data.playerName)
            {
                playerRank = i; // 0-based
                break;
            }
        }

        if (playerRank >= 0)
        {
            // El jugador está en el top 3: ocultar fourPlaceText, mover youTag a su fila
            if (yourPlace != null) yourPlace.gameObject.SetActive(false);
            MoveYouTagToRow(playerRank, top3.Count);
        }
        else
        {
            // El jugador no está en el top 3: mostrar su fila extra
            if (yourPlaceName != null && yourPlaceScore != null)
            {
                int playerTotal = GetPlayerTotal(data.allScores, data.playerName);
                int playerGlobalRank = GetGlobalRank(data.allScores, data.playerName);
                yourPlaceName.text = $"#{playerGlobalRank + 1}  {data.playerName}";
                yourPlaceScore.text = $"{playerTotal} pts";
                yourPlaceName.gameObject.SetActive(true);
                yourPlaceScore.gameObject.SetActive(true);
            }

            // youTag va a la fila extra (yourPlaceText)
            // MoveYouTagToTarget(yourPlaceText?.rectTransform);
        }
    }

    /// <summary>
    /// Mueve el youTag a la posición Y de la fila del ranking que corresponda.
    /// rankIndex es 0-based (0 = primero, 1 = segundo, 2 = tercero).
    /// </summary>
    private void MoveYouTagToRow(int rankIndex, int top3Count)
    {

        int yMove = rankIndex switch
        {
            0 => 133,
            1 => 33,
            2 => -67,
            _ => -167,
        };
        RectTransform youRect = youTag.GetComponent<RectTransform>();

        Vector2 pos = youRect.anchoredPosition;
        pos.y = yMove;
        youRect.anchoredPosition = pos;

        Debug.Log($"[ResultsPanel] YouTag moved to rank {rankIndex + 1} (top3Count={top3Count})");
        Debug.Log($"[ResultsPanel] YouTag new position: {youRect.anchoredPosition}");
    }

    private void MoveYouTagToTarget(RectTransform target)
    {
        if (youTag == null || target == null) return;

        youTag.SetActive(true);

        RectTransform youRect = youTag.GetComponent<RectTransform>();
        if (youRect == null) return;

        // Copiar solo la posición Y, mantener X propia del tag
        Vector2 pos = youRect.anchoredPosition;
        pos.y = target.anchoredPosition.y;
        youRect.anchoredPosition = pos;
    }

    // -------------------------------------------------------------------------
    // Helpers — filas
    // -------------------------------------------------------------------------

    private void SetRow(TextMeshProUGUI nameField, TextMeshProUGUI scoreField, List<(string name, int score)> top3, int index)
    {
        if (nameField == null || scoreField == null) return;
            Debug.Log($"No hay campo para rank {index + 1}");

        if (index < top3.Count)
        {
            nameField.text  = $"#{index + 1}  {top3[index].name}";
        scoreField.text = $"{top3[index].score} pts";
            nameField.gameObject.transform.parent.gameObject.SetActive(true);
        }
        else
        {
            nameField.gameObject.transform.parent.gameObject.SetActive(false);
        }
    }

    private static string FormatRow(int rank, string name, int score)
        => $"#{rank}  {name}  \t\t\t\t  {score} pts";

    // -------------------------------------------------------------------------
    // Helpers — datos
    // -------------------------------------------------------------------------

    private static List<(string name, int score)> BuildTop3(List<ScoreEntry> scores)
    {
        var totals = new Dictionary<string, int>();

        foreach (var entry in scores)
        {
            if (string.IsNullOrWhiteSpace(entry.playerName)) continue;
            totals.TryAdd(entry.playerName, 0);
            totals[entry.playerName] += entry.score;
        }

        var sorted = new List<(string name, int score)>();
        foreach (var kv in totals) sorted.Add((kv.Key, kv.Value));
        sorted.Sort((a, b) => b.score.CompareTo(a.score));

        return sorted.Count > 3 ? sorted.GetRange(0, 3) : sorted;
    }

    private static int GetPlayerTotal(List<ScoreEntry> scores, string playerName)
    {
        int total = 0;
        foreach (var e in scores)
            if (e.playerName == playerName) total += e.score;
        return total;
    }

    private static int GetGlobalRank(List<ScoreEntry> scores, string playerName)
    {
        var totals = new Dictionary<string, int>();
        foreach (var e in scores)
        {
            if (string.IsNullOrWhiteSpace(e.playerName)) continue;
            totals.TryAdd(e.playerName, 0);
            totals[e.playerName] += e.score;
        }

        int playerTotal   = totals.TryGetValue(playerName, out int v) ? v : 0;
        int betterPlayers = 0;
        foreach (int s in totals.Values)
            if (s > playerTotal) betterPlayers++;

        return betterPlayers + 1;
    }

    private static string FormatDifficulty(string difficulty) => difficulty switch
    {
        "beginner" => "Easy",
        "hard"     => "Medium",
        "expert"   => "Hard",
        _          => difficulty,
    };

    // -------------------------------------------------------------------------
    // Helpers — UI
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets the difficulty badge based on the provided difficulty level.
    /// </summary>
    /// <param name="difficulty">The difficulty level.</param>
    private void SetDifficultyBadge(string difficulty)
    {
        var (bg, border, text) = difficulty switch
        {
            "beginner" => (EasyBg,   EasyBorder,   EasyText),
            "hard"     => (MediumBg, MediumBorder, MediumText),
            "expert"   => (HardBg,   HardBorder,   HardText),
            _          => (EasyBg,   EasyBorder,   EasyText),
        };

        difficultyBadge.GetComponent<Image>().color          = bg;
        difficultyBadge.GetComponent<Outline>().effectColor  = border; // si usas Outline component
        difficultyBadge.GetComponentInChildren<TMP_Text>().color = text;
    }
}