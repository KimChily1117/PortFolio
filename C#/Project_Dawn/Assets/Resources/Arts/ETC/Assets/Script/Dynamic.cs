//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class Dynamic : MonoBehaviour
//{

//    public float Speed = 3.4f;
//    public float Dash_Speed = 6.42f;
//    public int Dash_Limit;
//    public GameObject objPlayer;
//    public Vector3 vDir = Vector3.right;
//    public P_Gun cGun;
//    public bool isJumping;   //점프를 하고있느냐에 대한 bool 변수 
//    public float posY;        //오브젝트의 초기 높이, 점프전에 y좌표를 저장하기 위해 만듬 
//    public float gravity;     //중력
//    public float jumpPower;   //점프력
//    public bool isWalking;

//    Vector3 vector;

//    //IEnumerator ProcTime()
//    //{
//    //    Reserv = true;
//    //    Jump();
//    //    yield return new WaitForSeconds(JumpCool);
//    //    Reserv = false;
//    //}

//    //public float JumpCool;
//    //public bool Reserv;
//    //public float jumpTime;    //점프 이후 경과시간
//    //public int JumpCnt;       //점프 횟수제한 


//    void Jump()
//    {
//        Animator animator = GetComponent<Animator>();
//        animator.SetBool("IsJump", true);

//        vector += Vector3.down * gravity; // 아래방향으로 바꿔준다. 

//        this.gameObject.transform.position += vector * Time.deltaTime;

//        if (this.gameObject.transform.position.y < posY)
//        {
//            Debug.Log("착지했다.");
//            transform.position = new Vector3(transform.position.x, posY, transform.position.z);
//            isJumping = false;
//            animator.SetBool("IsJump", false);
//        }
//    }


//    //void Jump()
//    //{
//    //    //y=-a*x+b에서 (a: 중력가속도, b: 초기 점프속도)
//    //    //적분하여 y = (-a/2)*x*x + (b*x) 공식을 얻는다.(x: 점프시간, y: 오브젝트의 높이)
//    //    //변화된 높이 height를 기존 높이 _posY에 더한다.
//    //    //Animator animator = GetComponent<Animator>();
//    //    float height = (jumpTime * jumpTime * (-gravity) / 2) + (jumpTime * jumpPower);
//    //    transform.position = new Vector3(transform.position.x, posY + height, transform.position.z);
//    //    //점프시간을 증가시킨다.
//    //    jumpTime += Time.deltaTime;
//    //    //animator.SetBool("isJumping", true);


//    //    //처음의 높이 보다 더 내려 갔을때 => 점프전 상태로 복귀한다.
//    //    if (transform.position.z <= 0.0f)
//    //    {            
//    //        isJumping = false;
//    //        jumpTime = 0.0f;
//    //        JumpCnt--;
//    //        transform.position = new Vector3(transform.position.x, posY, transform.position.z);           
//    //    }
//    //}

//    // 1. 점프를 하면 힘이 가해져서 위로 이동 => 끝없이 위로 가게됨 

//    // 2. 점프를 한힘이 매프레임마다 중력때문에 힘이 줄어들게됨  => 끝없이 밑으로 내려감

//    // 3. 땅에 닿였을때 , 플레이어를 이동시키지않는다. +- 0이 되었으니 제자리에 오게된다.


//    // Start is called before the first frame update
//    void Start()
//    {
//        Animator animator = GetComponent<Animator>();
//        isJumping = false;
//        vDir = Vector3.right;
//        animator.SetBool("SetWalk", false);
//        isWalking = false;
//    }

//    // Update is called once per frame
//    void Update()
//    {
//        Animator animator = GetComponent<Animator>();

//        if (Input.GetKey(KeyCode.RightArrow))
//        {
//            isWalking = true;
//            animator.SetBool("SetWalk", true);
//        }
//        if (Input.GetKeyUp(KeyCode.RightArrow))
//        {
//            isWalking = false;
//            Speed = 3.4f;
//            Speed -= Dash_Speed;
//            if (Dash_Limit > 0)
//            {
//                Dash_Limit--;
//            }
//            if (Dash_Limit < 0)
//            {
//                Dash_Limit = 0;
//            }
//            animator.SetBool("SetWalk", false);
//            animator.SetBool("IsDash", false);
//        }
//        if (Input.GetKey(KeyCode.LeftArrow))
//        {
//            isWalking = true;
//            animator.SetBool("SetWalk", true);
//        }
//        if (Input.GetKeyUp(KeyCode.LeftArrow))
//        {
//            isWalking = false;
//            animator.SetBool("SetWalk", false);
//            animator.SetBool("IsDash", false);           
//            Speed -= Dash_Speed;
//            if (Dash_Limit > 0)
//            {
//                Dash_Limit--;
//            }
//            if (Dash_Limit < 0)
//            {
//                Dash_Limit = 0;
//            }
//            Speed = 3.4f;
//        }

//        if (isWalking == true)
//        {
//            if (Input.GetKey(KeyCode.LeftArrow))
//            {
//                transform.position += Vector3.left * Speed * Time.deltaTime;

//                vDir = Vector3.left;

//                transform.localRotation = Quaternion.Euler(0, 180, 0);

//                if (Input.GetKeyDown(KeyCode.X)) // 공격부 
//                {
//                    Speed = 0;
//                    cGun.Shot(vDir);
//                }


//                if (Input.GetKeyDown(KeyCode.LeftShift)) // 특정 키를 입력받았을때 대쉬하는 부분 
//                {
//                    Dash_Limit++;
//                    if (Dash_Limit < 3)
//                    {
//                        Speed += Dash_Speed;
//                    }
//                    animator.SetBool("IsDash", true);
                    
//                }

//                if (Input.GetKeyUp(KeyCode.LeftShift))
//                {
//                    Dash_Limit--;                   
//                }


//            }

//            if (Input.GetKey(KeyCode.RightArrow))
//            {
//                transform.position += Vector3.right * Speed * Time.deltaTime;

//                vDir = Vector3.right;

//                transform.localRotation = Quaternion.Euler(0, 0, 0);

//                if (Input.GetKeyDown(KeyCode.X)) // 공격부 
//                {
//                    Speed = 0;
//                    cGun.Shot(vDir);
//                }

//                if (Input.GetKeyDown(KeyCode.LeftShift)) // 특정 키를 입력받았을때 대쉬하는 부분 
//                {
//                    Dash_Limit++;
//                    if (Dash_Limit < 3)
//                    {
//                        Speed += Dash_Speed;
//                    }
//                    animator.SetBool("IsDash", true);
                    
//                }

//                if (Input.GetKeyUp(KeyCode.LeftShift))
//                {
//                    if (Dash_Limit > 0)
//                    {
//                        Dash_Limit--;
//                    }
//                    if (Dash_Limit < 0)
//                    {
//                        Dash_Limit = 0;
//                    }
//                }
//            }
//        }
//        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


//        if (Input.GetKey(KeyCode.UpArrow))
//        {
//            transform.position += Vector3.up * Speed * Time.deltaTime;  // 좌측으로 이동시키는 코드 
//                                                                        // x값에 1을 더한다는 뜻 
//            animator.SetBool("SetWalk", true);
//        }

//        if (Input.GetKey(KeyCode.DownArrow))
//        {
//            transform.position += Vector3.down * Speed * Time.deltaTime;  // 좌측으로 이동시키는 코드 
//                                                                          // x값에 1을 더한다는 뜻 
//            animator.SetBool("SetWalk", true);
//        }


//        if (Input.GetKeyUp(KeyCode.DownArrow))
//        {
//            animator.SetBool("SetWalk", false);
//        }

//        if (Input.GetKeyUp(KeyCode.UpArrow))
//        {
//            animator.SetBool("SetWalk", false);

//        }

//        if (Input.GetKeyDown(KeyCode.C)) // 점프입력
//        {           

//            if (isJumping == false)
//            {
//                posY = transform.position.y;
//                Debug.Log("점프했다");
//                vector = new Vector3(0, jumpPower, 0);
//                isJumping = true;                
//            }
//        }
//        if (isJumping == true)
//        {
//            Jump();
//        }

      

//        if (Input.GetKeyDown(KeyCode.X)) // 공격부 
//        {
//            transform.position += Vector3.zero;
//            cGun.Shot(vDir);
//        }




//    }
//}
