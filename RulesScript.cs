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

    private void Awake()
    {
        closeRulesButton.gameObject.SetActive(false);
    }

    public void ReadRules()
    {
        closeRulesButton.gameObject.SetActive(true);
        readRulesButton.gameObject.SetActive(false);
        StartCoroutine(FirstSlide());
    }

    private IEnumerator FirstSlide()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(4104f, 0f);
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