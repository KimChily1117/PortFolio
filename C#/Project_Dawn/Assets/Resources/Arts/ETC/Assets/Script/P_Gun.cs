using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class P_Gun : MonoBehaviour
{

    public GameObject Bullet;
    public float ShotPower = 145f;


    public void Shot(Vector3 vDir)
    {
        GameObject bullet = Instantiate(Bullet); // Bullet이라는 빈공간을 만들어서 

        //생성된 오브젝트를 부활 위치로 옮겨준다.
        bullet.transform.position = this.transform.position; // 거따가 총알파일을 넣어준다 


        Rigidbody2D rigidbody = bullet.GetComponent<Rigidbody2D>();
        rigidbody.AddForce(vDir * ShotPower);
               
    }




}
