using System.Collections;
using UnityEngine;

public class EnamyController : MonoBehaviour
{
	

	public float m_Maxhp;
	public float m_Curhp;

	private Animator animator;
	private MeshRenderer[] meshRenderer;
	
	private Color originColor;
	void InitHp()
	{
		
		m_Maxhp = 100f;
		m_Curhp = m_Maxhp;
	}

	void Cheak_Dead()
	{
		if (m_Curhp <= 0)
		{
			Destroy(this.gameObject);

		}
	}
	private void Awake()
	{
		animator = GetComponent<Animator>();
		meshRenderer = GetComponentsInChildren<MeshRenderer>();
		InitHp();
	}
	public void TakeDamage(int damage)
	{
		// 체력이 감소되거나 피격 애니메이션이 재생되는 등의 코드를 작성
		Debug.Log(damage + "의 체력이 감소합니다.");

        m_Curhp -= damage;
		// 색상 변경
		StartCoroutine("OnHitColor");

		Cheak_Dead();
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
}

