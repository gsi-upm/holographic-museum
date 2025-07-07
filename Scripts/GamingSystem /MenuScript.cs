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
    [SerializeField] private float slideDuration = 0.75f;

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
        StartCoroutine(RulesSlideBack());
    }

    private IEnumerator SlideToCenter()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform gameViewRect = gameView.GetComponent<RectTransform>();
        
        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(2052f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = Vector2.zero; // centro
        Vector2 startPosGameView = gameViewRect.anchoredPosition;
        Vector2 endPosGameView = new Vector2(-2052f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            gameViewRect.anchoredPosition = Vector2.Lerp(startPosGameView, endPosGameView, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        menuRect.anchoredPosition = endPosMenu;
        gameViewRect.anchoredPosition = endPosGameView;
    }

    private IEnumerator SlideBack()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform gameViewRect = gameView.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(2052f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = new Vector2(2052f, 0f);
        Vector2 startPosGameView = gameViewRect.anchoredPosition;
        Vector2 endPosGameView = Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            gameViewRect.anchoredPosition = Vector2.Lerp(startPosGameView, endPosGameView, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        menuRect.anchoredPosition = endPosMenu;
        gameViewRect.anchoredPosition = endPosGameView;
    }

    private IEnumerator RulesSlideToCenter()
    {
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform gameViewRect = gameView.GetComponent<RectTransform>();
        RectTransform rulesRect = rules.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = Vector2.zero; // centro
        Vector2 startPosGameView = gameViewRect.anchoredPosition;
        Vector2 endPosGameView = new Vector2(-4104f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = new Vector2(-2052f, 0f);
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            gameViewRect.anchoredPosition = Vector2.Lerp(startPosGameView, endPosGameView, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        menuRect.anchoredPosition = endPosMenu;
        gameViewRect.anchoredPosition = endPosGameView;
    }

    private IEnumerator RulesSlideBack()
    {
        RectTransform rulesRect = rules.GetComponent<RectTransform>();
        RectTransform menuRect = menu.GetComponent<RectTransform>();
        RectTransform gameViewRect = gameView.GetComponent<RectTransform>();

        Vector2 startPosRules = rulesRect.anchoredPosition;
        Vector2 endPosRules = new Vector2(2052f, 0f);
        Vector2 startPosGameView = gameViewRect.anchoredPosition;
        Vector2 endPosGameView = new Vector2(-2052f, 0f);
        Vector2 startPosMenu = menuRect.anchoredPosition;
        Vector2 endPosMenu = Vector2.zero;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            rulesRect.anchoredPosition = Vector2.Lerp(startPosRules, endPosRules, elapsed / slideDuration);
            menuRect.anchoredPosition = Vector2.Lerp(startPosMenu, endPosMenu, elapsed / slideDuration);
            gameViewRect.anchoredPosition = Vector2.Lerp(startPosGameView, endPosGameView, elapsed / slideDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rulesRect.anchoredPosition = endPosRules;
        menuRect.anchoredPosition = endPosMenu;
        gameViewRect.anchoredPosition = endPosGameView;
    }
}
