using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    Animator anim;
    Rigidbody rigidbody;
    NavMeshAgent nav;
    BoxCollider boxCollider;
    MeshRenderer[] meshRenderer;
    MeshRenderer mesh;

    public float targetRadius = 1.5f;
    public float targetRange = 3f;


    public Transform target;
    public float m_Maxhp;
    public float m_Curhp;
    public BoxCollider meleeArea;
    public bool isAttack;
    public bool isChase;
    private Color originColor;


    void Awake()
    {        
        InitHp();
        anim = GetComponentInChildren<Animator>();
        rigidbody = GetComponent<Rigidbody>();
        nav = GetComponent<NavMeshAgent>();
        boxCollider = GetComponent<BoxCollider>();
        meshRenderer = GetComponentsInChildren<MeshRenderer>();
        mesh = GetComponentInChildren<MeshRenderer>();

        originColor = mesh.material.color;
        Invoke("ChaseStart", 2);
    }
    void ChaseStart()
    {
        isChase = true;
        anim.SetBool("isWalk", true);

    }

    IEnumerator Cheak_Dead()
    {
        if (m_Curhp <= 0)
        {
            anim.SetTrigger("doDie");
            yield return new WaitForSeconds(2f);
            Destroy(this.gameObject);
            
        }
    }

    public void TakeDamage(int damage)
    {
        // 체력이 감소되거나 피격 애니메이션이 재생되는 등의 코드를 작성
        Debug.Log(name+"이"+damage +"만큼의 데미지를 받았습니다");

        m_Curhp -= damage;
        // 색상 변경
        StartCoroutine(OnHitColor());
        StartCoroutine(Cheak_Dead());
    }

    void Targeting()
    {
       

        RaycastHit[] raycastHits =
            Physics.SphereCastAll(transform.position, targetRadius
            , transform.forward, targetRange, LayerMask.GetMask("Player"));

        if (raycastHits.Length > 0 && !isAttack)
        {
            StartCoroutine(Attack());
        }

    }

    private IEnumerator OnHitColor()
    {
        foreach (MeshRenderer meshRenderer in meshRenderer)
        {
            meshRenderer.material.color = Color.red;
        }
        yield return new WaitForSeconds(0.3f);

        foreach (MeshRenderer meshRenderer in meshRenderer)
        {
            meshRenderer.material.color = originColor;
        }

        // 색을 빨간색으로 변경한 후 0.1초 후에 원래 색상으로 변경
        //meshRenderer.material.color = Color.red;
        //meshRenderer.material.color = originColor;
    }

    private IEnumerator Attack()
    {
        isChase = false;
        isAttack = true;

        anim.SetBool("isAttack", true);

        yield return new WaitForSeconds(0.2f);
        meleeArea.enabled = true;

        yield return new WaitForSeconds(2f);
        meleeArea.enabled = false;
        
        isChase = true;
        isAttack = false;

        anim.SetBool("isAttack", false);
        yield return new WaitForSeconds(2.5f);
    }

    void FreezeVelocity()
    {
        if (isChase)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        Targeting();
        FreezeVelocity();
    }

    void InitHp()
    {

        m_Maxhp = 50f;
        m_Curhp = m_Maxhp;
    }

  

    // Update is called once per frame
    void Update()
    {       
        if (nav.enabled)
        {
            nav.SetDestination(target.position);
            nav.isStopped = !isChase;
        }
    }
}
