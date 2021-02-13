using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss_Missile_Port : MonoBehaviour
{
    public Transform missile;
    // Update is called once per frame
    void Awake()
    {
        Instantiate(missile, this.gameObject.transform);
    }

   
}
