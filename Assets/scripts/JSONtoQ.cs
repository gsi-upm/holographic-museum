/* This script loads trivia questions with a two-tier lookup:
 * 1. StreamingAssets/LangGraph/Resources/Questions/{lang}/{room}/{level}/questions.json
 *    (agent-generated, user-personalised — takes priority)
 * 2. Assets/Resources/Questions/{lang}/{room}/{level}/questions
 *    (bundled defaults — used when no personalised version exists yet)
 */

using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class JSONtoQ
{
    public List<QuestionJson> Questions { get; private set; }

    // Path inside StreamingAssets where the Python agent writes questions
    private static string StreamingAssetsQuestionsRoot =>
        Path.Combine(Application.streamingAssetsPath, "LangGraph", "Resources", "Questions");

    public static List<QuestionJson> GeneratePhrases(string roomName, string difficultyLevel, string lang)
    {
        // --- Tier 1: StreamingAssets (agent-generated, personalised) ---
        string streamingPath = Path.Combine(
            StreamingAssetsQuestionsRoot, lang, roomName, difficultyLevel, "questions.json");
        if (TriviaManager.instance.adaptiveQuestions)
        {    
            if (File.Exists(streamingPath))
            {
                try
                {
                    string json = File.ReadAllText(streamingPath);
                    QuestionList questionList = JsonUtility.FromJson<QuestionList>(json);
                    if (questionList?.trivia != null && questionList.trivia.Count > 0)
                    {
                        Debug.Log($"[JSONtoQ] Loaded from StreamingAssets: {streamingPath}");
                        return questionList.trivia;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[JSONtoQ] Failed to read StreamingAssets file: {e.Message}");
                }
            }
        }

        // --- Tier 2: Resources (bundled defaults) ---
        string resourcePath = "Questions/" + lang + "/" + roomName + "/" + difficultyLevel + "/questions";
        TextAsset jsonFile = Resources.Load<TextAsset>(resourcePath);

        if (jsonFile == null)
        {
            Debug.LogWarning($"[JSONtoQ] No questions found in StreamingAssets or Resources for " +
                             $"{lang}/{roomName}/{difficultyLevel}");
            return null;
        }

        Debug.Log($"[JSONtoQ] Loaded from Resources (default): {resourcePath}");
        QuestionList defaultList = JsonUtility.FromJson<QuestionList>(jsonFile.text);
        return defaultList?.trivia;
    }

    // ReloadQuestions is a no-op now: StreamingAssets files are read fresh from
    // disk every call, and Resources assets don't need unloading for this use case.
    public static void ReloadQuestions(string roomName, string lang)
    {
        // Nothing to do — GeneratePhrases always reads fresh from disk.
    }
}


[System.Serializable]
public class QuestionList
{
    public List<QuestionJson> trivia;
}

[System.Serializable]
public class QuestionJson
{
    public string question;
    public List<RespuestaJson> options;
}

[System.Serializable]
public class RespuestaJson
{
    public string choice;
    public bool correct;
}