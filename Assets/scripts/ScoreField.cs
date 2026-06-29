using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreField : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(false);
        
    }

    public void HideScore(){
        gameObject.SetActive(false);
    }
}
