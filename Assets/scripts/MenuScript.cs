using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using System;

public class MenuScript : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject gameView;
    [SerializeField] private GameObject rules;
    [SerializeField] private GameObject languageSettings;
    [SerializeField] private float slideDuration = 0.75f;
    public static MenuScript instance;
    public bool firstTime = true;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void Showmenu()
    {
        StartCoroutine(SlideToCenter());
    }

    public void HideMenu()
    {
        StartCoroutine(SlideBack());
    }
    
    public void ShowRules()
    {
        StartCoroutine(RulesSlideToCenter());
    }
    public void HideRules()
    {
        if (firstTime)
        {
            firstTime = false;
            StartCoroutine(RulesCloseFirst());
        }
        else{
            StartCoroutine(RulesSlideBack());
        }
    }

    public void LangSettings()
    {
        StartCoroutine(SlideToLanguageSettings());
    }

    private IEnumerator SlideToCenter()
    {
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform gameViewRect = gameView.GetComponent<RectTransform>();
        
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = Vector2.zero; // centro
        Vector2 startPosGameView = gameViewRect.anchoredPosition;
        Vector2 endPosGameView = new Vector2(-2000f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            gameViewRect.anchoredPosition = Vector2.Lerp(startPosGameView, endPosGameView, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        menuRect.anchoredPosition = endPosMenu;
        gameViewRect.anchoredPosition = endPosGameView;
    }

    private IEnumerator SlideBack()
    {
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform gameViewRect = gameView.GetComponent<RectTransform>();

        Vector2 endPosRules = new Vector2(2000f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = new Vector2(2000f, 0f);
        Vector2 startPosGameView = gameViewRect.anchoredPosition;
        Vector2 endPosGameView = Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            gameViewRect.anchoredPosition = Vector2.Lerp(startPosGameView, endPosGameView, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        menuRect.anchoredPosition = endPosMenu;
        gameViewRect.anchoredPosition = endPosGameView;
    }

    private IEnumerator RulesSlideToCenter()
    {
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform rulesRect = rules.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(-2000f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = new Vector2(-2000f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        menuRect.anchoredPosition = endPosMenu;
    }

    private IEnumerator RulesSlideBack()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();
        RectTransform menuRect = menu.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(4000f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        menuRect.anchoredPosition = endPosMenu;
    }
    
    private IEnumerator RulesCloseFirst()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(4000f, 0f);
        Vector2 endPosMenu = Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        SoundManager.instance.StopAudioguide();
        AgentManager.instance.startMUSEButton.SetActive(true);
        AgentManager.instance.MuseCanvas.SetActive(true);
        AgentManager.instance.title.SetActive(true);
        RulesScript.instance.rulesRead = true;
    }

    private IEnumerator SlideToLanguageSettings()
    {
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform languageSettingsRect = languageSettings.GetComponent<RectTransform>();

        Vector2 startPosLang = languageSettingsRect.anchoredPosition;
        Vector2 endPosLang = Vector2.zero; // centro
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = new Vector2(-2000f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            languageSettingsRect.anchoredPosition = Vector2.Lerp(startPosLang, endPosLang, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        languageSettingsRect.anchoredPosition = endPosLang;
        menuRect.anchoredPosition = endPosMenu;
    }
}
