using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHpSlider : MonoBehaviour
{
    [SerializeField]
    public Boss boss;
    public Slider slider;
    // Start is called before the first frame update
    void Awake()
    {
       
        slider = GetComponent<Slider>();
       
    }

    // Update is called once per frame
    void Update()
    {

        slider.value = (float)boss.m_Curhp / (float)boss.m_Maxhp;

    }
}
