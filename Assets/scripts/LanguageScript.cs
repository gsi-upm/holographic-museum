using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using System;

public class LanguageScript : MonoBehaviour
{

    public static LanguageScript instance;
    [SerializeField] public string language;
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject gameView;
    [SerializeField] private GameObject rules;
    [SerializeField] private GameObject rulesEn;
    [SerializeField] private GameObject rulesEs;
    [SerializeField] private GameObject languageScene;
    [SerializeField] private Button closeButton;
    [SerializeField] private List<Button> languageButtons;
    [SerializeField] private float slideDuration = 0.75f;
    [SerializeField] private AgentManager agentManager;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        closeButton.gameObject.SetActive(false);
    }

    public void SetLanguage(string lang)
    {
        language = lang;
        closeButton.gameObject.SetActive(true);
        StartCoroutine(FirstSlide());
        rulesEn.SetActive(lang == "en");
        rulesEs.SetActive(lang == "es");
        SoundManager.instance.PlayAudioguide("Rules1", language);
        AgentManager.instance.StartPythonAgent(language);
        AgentManager.instance.title.GetComponent<TMPro.TMP_Text>().text = language == "en" ? "Ask MUSE!" : "¡Pregunta a MUSE!";
    }

    private IEnumerator FirstSlide()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();
        RectTransform languageRect = languageScene.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(-2000f, 0f);
        Vector2 startPosLang = languageRect.anchoredPosition;
        Vector2 endPosLang = new Vector2(4104f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            languageRect.anchoredPosition = Vector2.Lerp(startPosLang, endPosLang, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        languageRect.anchoredPosition = endPosLang;
    }
}