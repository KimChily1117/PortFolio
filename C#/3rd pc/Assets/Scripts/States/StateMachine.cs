using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class StateMachine : Boss
{

    [Header("State")]
    public State m_Curstate;
    public enum State
    {
        idle,
        missile,
        meleeAtk,
        die
    }


    [Header("Shoting")]
    public GameObject MissilePortA;
    public GameObject MissilePortB;
    public GameObject Bullet;

    public int ShotPower;

    public float m_MissileCoolTime = 7f;
    public float m_curMissile;

    [Header("MeleeArea")]

    public BoxCollider Meleearea;


    WaitForSeconds Delay250 = new WaitForSeconds(0.25f);
    WaitForSeconds Delay500 = new WaitForSeconds(0.5f);
    

    public virtual IEnumerator FSM()
    {
        while (true)
        {
            m_curMissile -= Time.deltaTime;
            Meleearea.enabled = false;
            yield return StartCoroutine(m_Curstate.ToString());
        }
    }

    protected virtual IEnumerator idle()
    {
        if (visibleTargets.Contains(NearestTarget))
        {
            if (m_curMissile <= 0)
            {
                m_Curstate = State.missile;
            }

            else
            {
                m_Curstate = State.meleeAtk;
            }
        }
        else
        {
            m_Curstate = State.idle;            
        }        
        
        yield return null;
        Debug.Log("Idle");
    }

    protected virtual IEnumerator missile()
    {
        this.gameObject.transform.LookAt(NearestTarget);

        yield return new WaitForSeconds(1.2f);

        animator.SetTrigger("doShot");
        GameObject bullet = 
        Instantiate(Bullet, MissilePortA.transform.position, MissilePortA.transform.rotation);

        Rigidbody B_rigidbody = bullet.GetComponent<Rigidbody>();
        B_rigidbody.AddForce(MissilePortA.transform.forward * ShotPower);
        yield return new WaitForSeconds(1.2f);


        animator.SetTrigger("doShot");
        GameObject bullet2 =
        Instantiate(Bullet, MissilePortB.transform.position, MissilePortB.transform.rotation);
        B_rigidbody = bullet2.GetComponent<Rigidbody>();
        B_rigidbody.AddForce(MissilePortB.transform.forward * ShotPower);
        yield return new WaitForSeconds(3f);

        m_curMissile = m_MissileCoolTime;
        m_Curstate = State.idle;

    }

    protected virtual IEnumerator meleeAtk()
    {
        animator.SetTrigger("doTaunt");

        yield return new WaitForSeconds(4.16f);

        Meleearea.enabled = true;

        Debug.Log("MeleeAtk!");

        Meleearea.enabled = false;
        m_Curstate = State.idle;
        
    }
    
   


    // Start is called before the first frame update
    void Start()
    {
        m_Curstate = State.idle;
        StartCoroutine(FSM());
    }

}
