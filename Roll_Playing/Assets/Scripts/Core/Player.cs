using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GameManager.Input._keyAction -= OnKeycode;
        GameManager.Input._keyAction += OnKeycode;

    }

    public void OnKeycode()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log($"KSY PortFolio : KEYDOWN : W");
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            Debug.Log($"KSY PortFolio : KEYDOWN : A");
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.Log($"KSY PortFolio : KEYDOWN : S");
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log($"KSY PortFolio : KEYDOWN : D");
        }

    }


}
