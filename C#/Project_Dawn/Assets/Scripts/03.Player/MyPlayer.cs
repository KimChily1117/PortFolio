using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
using Unity.VisualScripting;
using Google.Protobuf.Protocol;

public class MyPlayer : BaseCharacter
{


    // **  Input Buffer **

    InputBuffer _inputBuffer;

    public int currentAtkCount;
    private float comboTimeWindow = 0.2f; // 콤보 어택 유효 시간 (초 단위)
    private float lastComboTime = 0f;   // 마지막 콤보 어택 시간
    /// 






    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // 장비에 따라 여기서 스텟 값을 바꿔준다.
    }

    protected override void Start()
    {
        Debug.Log($"Start Test");

        // _inputBuffer = new InputBuffer();

        base.Start();

        GameManager.Input.KeyDownAction -= OnKeyDownMoveAction;
        GameManager.Input.KeyDownAction += OnKeyDownMoveAction;


        GameManager.Input.KeyDownAction -= OnKeyAction;
        GameManager.Input.KeyDownAction += OnKeyAction;


        GameManager.Input.KeyUpAction -= OnKeyUpAction;
        GameManager.Input.KeyUpAction += OnKeyUpAction;


        GameManager.Input.DoubleKeyAction -= DoubleKeyAction;
        GameManager.Input.DoubleKeyAction += DoubleKeyAction;
    }



    public void OnKeyDownMoveAction()
    {

        if (Input.GetKey(KeyCode.UpArrow))
        {
            PosInfo.State = PlayerState.Moving;
            Debug.Log($"up");
            _moveDir = Vector2.up;

            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.UpArrow);
            CheckUpdatedFlag();

        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            PosInfo.State = PlayerState.Moving;
            

            Debug.Log($"down");
            _moveDir = Vector2.down;            
            Dir = GetDirFromVec(_moveDir);
           
            GameManager.Input.SetInputKeyCode(KeyCode.DownArrow);
            CheckUpdatedFlag();
        }

        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            PosInfo.State = PlayerState.Moving;
            Debug.Log($"left");
            _moveDir = Vector2.left;
            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.LeftArrow);
            CheckUpdatedFlag();
        }

        else if(Input.GetKey(KeyCode.RightArrow))
        {
            PosInfo.State = PlayerState.Moving;

            Debug.Log($"right");
            _moveDir = Vector2.right;
            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.RightArrow);
            CheckUpdatedFlag();
        }
        else
        {
            Dir = MoveDir.None;
        }

    }


    protected override void MoveToNextPos()
    {
        if (Dir == MoveDir.None) // 키 입력이 없을 때.
        {
            PosInfo.State = PlayerState.Idle;
            CheckUpdatedFlag();
            return;
        }

        Vector2 destPos = CellPos;

        switch (Dir)
        {
            case MoveDir.Up:
                destPos += Vector2.up;
                break;
            case MoveDir.Down:
                destPos += Vector2.down;
                break;
            case MoveDir.Left:
                destPos += Vector2.left;
                break;
            case MoveDir.Right:
                destPos += Vector2.right;
                break;
        }

        //if (Managers.Map.CanGo(destPos))
        //{
        //    if (Managers.Object.Find(destPos) == null)
        //    {
        //        CellPos = destPos;
        //    }
        //}

        CheckUpdatedFlag();
    }






    private void OnKeyAction()
    {
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (Time.time - lastComboTime > comboTimeWindow)
            {
                lastComboTime = Time.time;
                if (currentAtkCount == 0)
                {
                    C_Skill skill = new C_Skill() { Info = new SkillInfo() };
                    skill.Info.SkillId = 2;
                    GameManager.Network.Send(skill);



                    // 첫 번째 공격
                    PosInfo.State = PlayerState.Atk;
                    _animator.SetTrigger("Attack1");
                    _HitBox.gameObject.SetActive(true);

                    currentAtkCount = 1;

                    
                    //_coSkillCoolTime = StartCoroutine("CoInputCooltime", 0.2f);



                }
                else if (currentAtkCount == 1)
                {

                    C_Skill skill = new C_Skill() { Info = new SkillInfo() };
                    skill.Info.SkillId = 3;
                    GameManager.Network.Send(skill);


                    PosInfo.State = PlayerState.Atk;

                    _HitBox.gameObject.SetActive(true);

                    _animator.SetTrigger("Attack2");
                    // 두 번째 공격
                    currentAtkCount = 2;
                }
                else if (currentAtkCount == 2)
                {

                    C_Skill skill = new C_Skill() { Info = new SkillInfo() };
                    skill.Info.SkillId = 4;
                    GameManager.Network.Send(skill);


                    // 세 번째 공격 (3단 근접 공격)
                    PosInfo.State = PlayerState.Idle;

                    _HitBox.gameObject.SetActive(true);

                    _animator.SetTrigger("Attack3");
                    currentAtkCount = 0;

                }
            }
            // 최초 Idle에서 평타키를 눌렀을 때 or 평타 대기 상태에서 평타키를 눌렀을 떄.
            // Atk 1타 상타로 진입함.

            if (PosInfo.State == PlayerState.Jump)
                // To-do : 점프 상태에서 점프 평타 모션 넣어야함
                return;
            if (PosInfo.State == PlayerState.Run)
                // To-do : 대쉬 상태에서 대쉬 평타 모션 넣어야함
                return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {

            C_Skill skill = new C_Skill() { Info = new SkillInfo() };
            skill.Info.SkillId = 1;
            GameManager.Network.Send(skill);



            if (PosInfo.State == PlayerState.Jump)
                return;

            initialPosition = transform.position;

            PosInfo.State = PlayerState.Jump;



        }
    }




    public void OnKeyUpAction()
    {
        if (PosInfo.State != PlayerState.Jump)
        {
            if (PosInfo.State.ToString().Contains("ATK"))
            {
                return;
            }
            PosInfo.State = PlayerState.Idle;
            PosInfo.MoveDir = MoveDir.None;
            CheckUpdatedFlag();
        }

    }

    public void DoubleKeyAction()
    {
        CheckUpdatedFlag();
        PosInfo.State = PlayerState.Run;

    }


    #region ProcMethod

    public override void ProcIdlePlayer()
    {
        base.ProcIdlePlayer();

        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        //_HitBox.gameObject.SetActive(false);

    }

    public override void ProcWalkPlayer()
    {
        base.ProcWalkPlayer();

        _animator.SetBool("isWalk", true);

        //_HitBox.gameObject.SetActive(false);
    }

    public override void ProcRunPlayer()
    {
        base.ProcRunPlayer();
        _animator.SetBool("isRun", true);

        //_HitBox.gameObject.SetActive(false);

    }



    public override void ProcAtk()
    {
        base.ProcAtk();
        _animator.SetBool("isWalk", false);
        _animator.SetBool("isRun", false);
        _animator.SetBool("isAtkIdle", true);
    }

    #endregion


    protected override void CheckUpdatedFlag()
    {
        //if(_updated == true)
        //{
        //    C_Move movePacket = new C_Move();
        //    movePacket.PosInfo = PosInfo;

        //    GameManager.Network.Send(movePacket);

        //    _updated = false;
        //}

        C_Move movePacket = new C_Move();
        movePacket.PosInfo = PosInfo;

        GameManager.Network.Send(movePacket);

        //_updated = false;

    }

}
