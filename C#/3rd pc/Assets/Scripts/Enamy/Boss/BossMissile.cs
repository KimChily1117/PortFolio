using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BossMissile : MonoBehaviour
{
    public Transform target;
    public PlayerController player;

    
    public int damage = 5;
    public bool isMelee;

   

    void Start()
    {
       
        //player = GetComponent<PlayerController>();
        this.gameObject.transform.LookAt(target);

    }

    // Update is called once per frame
    void Update()
    {
        this.gameObject.transform.LookAt(target);
        //nav.SetDestination(target.position);

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            player.TakeDamage(damage);
            Destroy(this.gameObject);
        }
        
    }
}
