using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;
using UnityEngine.Networking;
using System.Text;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

/*public class AiQuestion : MonoBehaviour
{
    public static List<QuestionJson> GeneratedQuestions { get; private set; } // Almacena las preguntas generadas

    public static async Task GetQuestionsAsync(String obra)
    {
        AiQuestion instance = new GameObject("AiQuestionTemp").AddComponent<AiQuestion>();
        await AiQuestion.StartCoroutineAsync(instance.FetchData(obra));
        Destroy(instance.gameObject);
    }

    private IEnumerator FetchData(String obra)
    {
        Debug.Log("Fetching data from Gemini...");
        //string apiKey = "AIzaSyCXupoFq6SiA1rHx_fPFW-NFq38g7xBrNw"; //Uni
        string apiKey = "AIzaSyD8wBxW2mroNv88Fmo5vtzyNysIwtKHc30"; //Personal
        string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key=" + apiKey;
        string prompt = "Genera un JSON en texto plano con cuatro preguntas diferenes y sencillas sobre " + obra + " en el siguiente formato: cada quizz tendrá un campo pregunta, cada quizz tendrá 4 campos respuesta (La respuesta correcta será la primera) y cada respuesta tendrá un campo correcto (que solo será true si la respuesta que se señala es la correcta) y un campo texto con la respuesta escrita";

        // Create the request body
        string jsonPayload = "{ \"contents\": [{ \"parts\": [{\"text\": \"" + prompt + "\"}] }] }";

        using (UnityWebRequest webRequest = new UnityWebRequest(apiUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            webRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log("Gemini Response: " + jsonResponse);

                // Pass the response to the getJSON method
                StartCoroutine(getJSON(jsonResponse));
            }
            else
            {
                Debug.LogError("Error fetching data from Gemini: " + webRequest.error);
            }
        }
    }

    private IEnumerator getJSON(string jsonResponse)
    {
        try
        {
            // Parse the response to extract the JSON content
            JObject responseObj = JObject.Parse(jsonResponse);
            string contentText = responseObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (!string.IsNullOrEmpty(contentText))
            {
                // Remove the markdown code block markers (```json and ```)
                contentText = contentText.Replace("```json", "").Replace("```", "").Trim();

                // Wrap the JSON content in a "trivia" object
                string wrappedContent = "{ \"trivia\": " + contentText + " }";

                // Parse the wrapped JSON content into objects
                QuestionList questionList = JsonUtility.FromJson<QuestionList>(wrappedContent);
                GeneratedQuestions = questionList.trivia; // Almacena las preguntas generadas en memoria

                // Save the wrapped JSON content to a file in the Assets folder
                string filePath = Path.Combine(Application.dataPath, "Resources/Questions/questions.json").Replace("\\", "/");
                File.WriteAllText(filePath, wrappedContent);

                // Parse the wrapped JSON content for logging
                JObject wrappedObject = JObject.Parse(wrappedContent);
                JArray questionsArray = (JArray)wrappedObject["trivia"];

            }
            else
            {
                Debug.LogError("No valid content found in the Gemini response.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error parsing Gemini response: " + ex.Message);
        }

        yield return null;
    }

    // Helper extension method to await coroutines
    public static Task StartCoroutineAsync(IEnumerator coroutine)
    {
        var tcs = new TaskCompletionSource<bool>();
        CoroutineRunner.Instance.StartCoroutine(RunCoroutine(coroutine, tcs));
        return tcs.Task;
    }

    private static IEnumerator RunCoroutine(IEnumerator coroutine, TaskCompletionSource<bool> tcs)
    {
        yield return coroutine;
        tcs.SetResult(true);
    }
}

// Helper class to run coroutines from static methods
public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;
    public static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                var obj = new GameObject("CoroutineRunner");
                _instance = obj.AddComponent<CoroutineRunner>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }
}
*/