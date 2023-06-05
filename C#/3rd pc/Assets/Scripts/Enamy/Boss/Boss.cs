using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Boss : MonoBehaviour
{
    
    #region Variables

    [Header("Sight Settings")]
    public float viewRadius = 45f;
    [Range(0, 360)]
    public float viewAngle = 90f;

    [Header("Find Settings")]
    public float delay = 0.2f;

    public LayerMask targetMask;
    

    public List<Transform> visibleTargets = new List<Transform>();
    private Transform nearestTarget;

    private float distanceToTarget = 0.0f;
    public Collider[] targetsInViewRadius;

    public Animator animator;

    
    public float m_Maxhp;
    public float m_Curhp;




    #endregion Variables

    #region Properties

    public List<Transform> VisibleTargets => visibleTargets;
    public Transform NearestTarget => nearestTarget;
    public float DistanceToTarget => distanceToTarget;

    #endregion Properties
    public void FindVisibleTargets()
    {
        distanceToTarget = 0.0f;
        nearestTarget = null;
        visibleTargets.Clear();

        targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            
            Vector3 dirToTarget = (target.position - transform.position).normalized;

            if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
            {
                float dstToTarget = Vector3.Distance(transform.position, target.position);

                if (!Physics.Raycast(transform.position, dirToTarget, dstToTarget))
                {
                    visibleTargets.Add(target);

                    if (nearestTarget == null || (distanceToTarget > dstToTarget))
                    {
                        nearestTarget = target;
                    }
                    distanceToTarget = dstToTarget;
                }
            }
        }
    }

    IEnumerator FindTargetsWithDelay(float delay)
    {
        while (true)
        {
            yield return new WaitForSeconds(delay);
            FindVisibleTargets();
        }
    }

    IEnumerator Cheak_Dead()
    {
        if (m_Curhp <= 0)
        {
            animator.SetTrigger("doDie");
            yield return new WaitForSeconds(2f);
            Destroy(this.gameObject);

        }
    }

    void InitHp()
    {

        m_Maxhp = 100f;
        m_Curhp = m_Maxhp;
    }

    public void TakeDamage(int damage)
    {
        // 체력이 감소되거나 피격 애니메이션이 재생되는 등의 코드를 작성
        Debug.Log(name + "이" + damage + "만큼의 데미지를 받았습니다");
        m_Curhp -= damage;
        // 색상 변경       
        StartCoroutine(Cheak_Dead());
    }




    // Start is called before the first frame update
    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        InitHp();
        StartCoroutine("FindTargetsWithDelay",2.3f);
    }


    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

}
