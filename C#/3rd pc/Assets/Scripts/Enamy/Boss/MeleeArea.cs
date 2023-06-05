using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeArea : MonoBehaviour
{
    public PlayerController target;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            target.TakeDamage(10);
        } 
    }
}
