using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
public struct ResultsData
{
    public string playerName;
    public int    runScore;
    public int    totalScore;
    public int    correctAnswers;
    public int    totalQuestions;
    public string difficulty;
    public string exhibitionName;
    public bool   hasStreak;
    public int    streakBonus;
    public List<ScoreEntry> allScores; // para construir el top 5
}