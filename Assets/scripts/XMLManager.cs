using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;

public class XMLManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Paths
    // -------------------------------------------------------------------------

    private string ScoresDirectory => Application.persistentDataPath + "/HighScores/";
    private string ScoresFilePath  => ScoresDirectory + "detailed_scores.xml";

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        EnsureScoresDirectory();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void SaveDetailedScores(List<ScoreEntry> scores)
    {
        EnsureScoresDirectory();

        var leaderboard = new ScoreLeaderboard { list = scores };
        var serializer  = new XmlSerializer(typeof(ScoreLeaderboard));

        using (var stream = new FileStream(ScoresFilePath, FileMode.Create))
        {
            serializer.Serialize(stream, leaderboard);
        }

        Debug.Log($"[XMLManager] Scores saved → {ScoresFilePath}");
    }

    public List<ScoreEntry> LoadDetailedScores()
    {
        EnsureScoresDirectory();

        if (!File.Exists(ScoresFilePath))
        {
            return new List<ScoreEntry>();
        }

        var serializer = new XmlSerializer(typeof(ScoreLeaderboard));

        using (var stream = new FileStream(ScoresFilePath, FileMode.Open))
        {
            var leaderboard = serializer.Deserialize(stream) as ScoreLeaderboard;
            return leaderboard?.list ?? new List<ScoreEntry>();
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void EnsureScoresDirectory()
    {
        if (!Directory.Exists(ScoresDirectory))
        {
            Directory.CreateDirectory(ScoresDirectory);
            Debug.Log($"[XMLManager] Created scores directory: {ScoresDirectory}");
        }
    }
}

// -----------------------------------------------------------------------------
// Data models
// -----------------------------------------------------------------------------

[Serializable]
public class ScoreLeaderboard
{
    public List<ScoreEntry> list = new List<ScoreEntry>();
}

[Serializable]
public class ScoreEntry
{
    public string playerName;
    public int    score;               // Points earned in this run
    public int    totalScoreAfterGame; // Cumulative score across all runs
    public string difficulty;
    public string exhibitionName;
    public int    correctAnswers;
    public int    totalQuestions;
    public string date;
}