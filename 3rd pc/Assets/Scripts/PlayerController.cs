using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    enum WEAPONTYPE
    {
        WP_NONE,
        WP_Dagger,
        WP_Greatsword
    }

    Hashtable WEAPON_TYPE = new Hashtable();

    [SerializeField]
	private KeyCode jumpKeyCode = KeyCode.Space;
	[SerializeField]
	private Transform cameraTransform;
	private Player_Dynamic Player_Dynamic;
	private PlayerAnimator playerAnimator;
	MeshRenderer[] meshRenderer;
	MeshRenderer mesh;
	private Color originColor;


	public float m_Maxhp;
	public float m_Curhp;
	public bool isDamage;

    WEAPONTYPE m_eWeaponType;
   

    private void Awake()
	{
		Cursor.visible = false;                 // 마우스 커서를 보이지 않게
		Cursor.lockState = CursorLockMode.Locked;   // 마우스 커서 위치 고정
		InitHp();

       

		Player_Dynamic = GetComponent<Player_Dynamic>();
		playerAnimator = GetComponentInChildren<PlayerAnimator>();
		meshRenderer = GetComponentsInChildren<MeshRenderer>();
		mesh = GetComponentInChildren<MeshRenderer>();
		originColor = mesh.material.color; 
	}

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
	public void TakeDamage(int damage)
	{
		// 체력이 감소되거나 피격 애니메이션이 재생되는 등의 코드를 작성
		Debug.Log(damage + "만큼의 피해를 플레이어가 입었습니다.");
        if (!isDamage)
        {
			m_Curhp -= damage;
			// 색상 변경
			StartCoroutine(OnDamage());
			StartCoroutine(OnHitColor());
			Cheak_Dead();
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

    public void Swap_Weapon()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
           
        } 
    }



	private void Update()
	{
		// 방향키를 눌러 이동
		float x = Input.GetAxis("Horizontal");
		float z = Input.GetAxis("Vertical");
		// 애니메이션 파라미터 설정 (horizontal, vertical)
		playerAnimator.OnMovement(x, z);
		// 이동 속도 설정 (앞으로 이동할때만 5, 나머지는 2)
		Player_Dynamic.MoveSpeed = z > 0 ? 5.0f : 2.0f;
		// 이동 함수 호출 (카메라가 보고있는 방향을 기준으로 방향키에 따라 이동)
		Player_Dynamic.MoveTo(cameraTransform.rotation * new Vector3(x, 0, z));

		// 회전 설정 (항상 앞만 보도록 캐릭터의 회전은 카메라와 같은 회전 값으로 설정)
		transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);

		// Space키를 누르면 점프
		if (Input.GetKeyDown(jumpKeyCode))
		{
			playerAnimator.OnJump();    // 애니메이션 파라미터 설정 (onJump)
			Player_Dynamic.JumpTo();        // 점프 함수 호출
		}
		// 마우스 왼쪽 클릭시 칼 연계공격 
		if (Input.GetMouseButtonDown(0))
		{
			playerAnimator.OnWeaponAttack();
		}

        Swap_Weapon();


    }


	IEnumerator OnDamage()
	{
		isDamage = true;
		
		yield return new WaitForSeconds(2.3f);

		isDamage = false;
	}
}


