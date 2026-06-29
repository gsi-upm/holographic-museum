using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
public class LeaderboardRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject      youBadge;

    public void Populate(int rank, string playerName, int score, bool isCurrentPlayer)
    {
        rankText.text  = rank <= 3 ? new[] { "1st", "2nd", "3rd" }[rank - 1] : $"#{rank}";
        nameText.text  = playerName;
        scoreText.text = $"{score} pts";
        youBadge.SetActive(isCurrentPlayer);
    }
}