/* This script is for importing JSON files and creating scriptable objects
 / The format will depend on the structure of the JSON file.
 / THIS FILE MUST BE IN THE "Editor" folder you created in the assets folder. 
 / This will output data to Resources/Questions folder
*/

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

public class JSONtoQ
{
    public List<QuestionJson> Questions { get; private set; }

    public static List<QuestionJson> GeneratePhrases(string roomName, string dificultyLevel)
    {
        TextAsset jsonFile = Resources.Load<TextAsset>("Questions/"+ roomName + "/" + dificultyLevel +"/questions");
        if (jsonFile == null)
        {
            Debug.Log("Error: No se pudo encontrar el archivo questions.json en Resources/Questions/" + roomName + "/" + dificultyLevel);
            return null;
        }

        QuestionList questionList = JsonUtility.FromJson<QuestionList>(jsonFile.text);
        return questionList.trivia;
    }

    public static void ReloadQuestions(string roomName)
    {
        Resources.UnloadAsset(Resources.Load<TextAsset>("Questions/"+roomName+"/questions"));
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

