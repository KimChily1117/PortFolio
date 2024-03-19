using Character;
using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Meteor : MonoBehaviour
{

    float _damage = 25f;
    BoxCollider2D _boxCollider2D;

    private void OnEnable()
    {
        _boxCollider2D = GetComponent<BoxCollider2D>();
        
    }

    void Update()
    {
        this.transform.position += Vector3.down * Time.deltaTime * 2.6777f;

        if (this.transform.localPosition.y <= 0.65f)
        {
            GameObject explosionEffect = GameManager.Resources.Instantiate("Effect/ExplosionEffect");

            explosionEffect.transform.position = transform.position;

            explosionEffect.GetComponent<Animator>().Play("bakal_skill_explosion_effect");
            GameManager.Sound.Play("Sounds/mon/bakal/bakal_dragon_3phase_meteor_exp_01");
            //StartCoroutine(CameraShake.Instance.Shake(0.15f, 0.5f));
            Destroy(explosionEffect, 1.5f);
            Destroy(this.gameObject);
        }
    }



    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("OtherPlayer"))
            return;

        if (collision.CompareTag("Player"))
        {

            C_Collision c_Collision = new C_Collision();

            c_Collision.Playerinfo = new ObjectInfo();

            c_Collision.Playerinfo.ObjectId = GameManager.ObjectManager.MyPlayer.Id;
            c_Collision.Playerinfo.Damage = _damage;

            GameManager.Network.Send(c_Collision);


            //collision.transform.parent.GetComponent<BaseCharacter>().TakeDamage(100f);
            GameObject explosionEffect = GameManager.Resources.Instantiate("Effect/ExplosionEffect");

            explosionEffect.transform.position = transform.position;

            explosionEffect.GetComponent<Animator>().Play("bakal_skill_explosion_effect");
            GameManager.Sound.Play("Sounds/mon/bakal/bakal_dragon_3phase_meteor_exp_01");
            //StartCoroutine(CameraShake.Instance.Shake(0.15f, 0.5f));
            Destroy(this.gameObject);

            // Todo 서버로 충돌패킷 보내는거 추가

        }
    }


}
