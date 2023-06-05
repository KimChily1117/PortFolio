using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Bullet : MonoBehaviour
{
    PlayerController player;
    public int Damage = 10;

    private void OnTriggerEnter(Collider other)
    {
        
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerController>().TakeDamage(10);
        }
    }


}
