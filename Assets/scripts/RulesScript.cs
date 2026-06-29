using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using System;

public class RulesScript : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject gameView;
    [SerializeField] private GameObject rules;
    [SerializeField] private Button closeRulesButton;
    [SerializeField] private Button readRulesButton;
    [SerializeField] private float slideDuration = 0.75f;
    public bool rulesRead = false;
    private int currentRuleIndex = 1;

    private string language = LanguageScript.instance.language;

    public static RulesScript instance;

    private void Awake()
    {
        instance = this;
    }

    public void FinalRules()
    {
        // SoundManager.instance.PlayAudioguide("welcome");
        AgentManager.instance.startMUSEButton.SetActive(true);
        AgentManager.instance.MuseCanvas.SetActive(true);
        AgentManager.instance.title.SetActive(true);
        if(rulesRead)
        {
            closeRulesButton.gameObject.SetActive(true);
            MenuScript.instance.HideRules();
            return;
        }
        StartCoroutine(NextSlide());
        rulesRead = true;
    }

    public void NextRule()
    {
        StartCoroutine(NextSlide());
        SoundManager.instance.PlayAudioguide($"Rules{currentRuleIndex + 1}", language);
        currentRuleIndex++;
    }
    public void PrevRule()
    {
        StartCoroutine(PreviousSlide());
    }

    private IEnumerator NextSlide()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = startPosRules + new Vector2(2000f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
    }

    private IEnumerator PreviousSlide()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = startPosRules - new Vector2(2000f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
    }
}