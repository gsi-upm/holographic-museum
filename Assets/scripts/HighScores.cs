// using System.Collections.Generic;
// using UnityEngine;

// public class HighScores
// {
//     [SerializeField] private XMLManager xmlManager;

    
//     public List<HighScoreEntry> scores = new List<HighScoreEntry>();
//     //public HighScoreDisplay[] highScoreDisplayArray;

//     public void TryScores()
//     {
//         // Adds some test data
//         AddNewScore("John", 4500);
//         AddNewScore("Max", 5520);
//         AddNewScore("Dave", 380);
//         AddNewScore("Steve", 6654);
//         AddNewScore("Mike", 11021);
//         AddNewScore("Teddy", 3252);
//     }

//     public void addScores()
//     {
//         scores.Sort((HighScoreEntry x, HighScoreEntry y) => y.score.CompareTo(x.score));

//         xmlManager.SaveScores(scores);
//     }

//     void AddNewScore(string entryName, int entryScore)
//     {
//         scores.Add(new HighScoreEntry { name = entryName, score = entryScore });
//     }
// }