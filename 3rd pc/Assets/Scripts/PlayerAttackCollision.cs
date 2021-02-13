using System.Collections;
using UnityEngine;

public class PlayerAttackCollision : MonoBehaviour
{
	public int Damage = 10;


	private void OnEnable()
	{
		StartCoroutine("AutoDisable");
	}

	private void OnTriggerEnter(Collider other)
	{
		// 플레이어가 타격하는 대상의 태그, 컴포넌트, 함수는 바뀔 수 있다
		
		if (other.CompareTag("Enamy"))
		{
			other.GetComponent<Enemy>().TakeDamage(Damage);
		}

        if (other.CompareTag("Boss"))
        {
			other.GetComponent<Boss>().TakeDamage(Damage);
        }
        
	}

	private IEnumerator AutoDisable()
	{
		// 0.1초 후에 오브젝트가 사라지도록 한다
		yield return new WaitForSeconds(0.1f);
		gameObject.SetActive(false);
	}
}

