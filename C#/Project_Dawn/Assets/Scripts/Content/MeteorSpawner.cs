using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MeteorSpawner : MonoBehaviour
{
    public Meteor m_meteor;

    public Animator animator;

    private void OnEnable()
    {
        GameObject spawnEffect = GameManager.Resources.Instantiate("Effect/MeteorAreaSpawnEffect");   
        
        spawnEffect.transform.position = transform.position;
        spawnEffect.GetComponent<Animator>().Play("Bakal_Skill_AreaExplosion");
        Destroy(spawnEffect,0.5f);
        
        GameObject go = GameManager.Resources.Instantiate("Enemy/Meteor");
        go.transform.SetParent(this.transform);
        go.transform.position = new Vector2(this.transform.position.x, 6.5f);
    }


}



