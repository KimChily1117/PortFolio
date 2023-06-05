using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class Player_Health_Bar : MonoBehaviour
{
    public PlayerController player;
    public Slider p_slider;
    // Start is called before the first frame update
    void Start()
    {
        p_slider = GetComponent<Slider>();
    }

    // Update is called once per frame
    void Update()
    {
        p_slider.value = (float)player.m_Curhp / (float)player.m_Maxhp;
    }
}
