using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossRock : Enemy_Bullet
{
    Rigidbody rigidbody;
    float angularPower = 2;
    float scaleValue = 0.1f;
    bool isShoot;

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        StartCoroutine(GainPowerTimer());
        StartCoroutine(GainPower());
    }

    // Update is called once per frame
    void Update()
    {

    }


    IEnumerator GainPowerTimer()
    {
        yield return new WaitForSeconds(2.2f);
        isShoot = true;
    }

    IEnumerator GainPower()
    {
        while (true)
        {
            angularPower += 0.02f;
            scaleValue += 0.005f;
            transform.localScale = Vector3.one * scaleValue;
            rigidbody.AddTorque(transform.right * angularPower, ForceMode.Acceleration);
            yield return null;
        }
    }


}
    