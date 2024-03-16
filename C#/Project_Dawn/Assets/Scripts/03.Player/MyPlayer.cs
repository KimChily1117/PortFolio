using Character;
using Google.Protobuf.Protocol;
using Unity.VisualScripting;
using UnityEngine;

public class MyPlayer : BaseCharacter
{


    // **  Input Buffer **
    InputBuffer _inputBuffer;
    // **  Input Buffer **


    // ** Combo **
    public int currentAtkCount;
    private readonly float comboTimeWindow = 0.6f; // 콤보 어택 유효 시간 (초 단위)
    [SerializeField]
    private float lastComboTime = 0f;   // 마지막 콤보 어택 시간

    private bool isActiveComboDetect;


    // ** Combo **

    protected override void InitializeStat(Stat stat)
    {
        base.InitializeStat(stat);
        // 장비에 따라 여기서 스텟 값을 바꿔준다.
    }

    


    protected override void Start()
    {    

        // _inputBuffer = new InputBuffer();

        base.Start();
        _combatSystem = this.GetOrAddComponent<CombatSystem>();
        _combatSystem.Init(this);
        _combatSystem.inLineCollider = Util.FindChild<BoxCollider2D>(this.gameObject, "Base/Shadow", true);


        lastComboTime = comboTimeWindow;
        currentAtkCount = 0;

        GameManager.Input.Clear();


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
            if (_state == PlayerState.Jump)
            {
                //CellPos += Vector2.up * _speed * Time.deltaTime;
                //_shadowObject.transform.position += Vector3.up * _speed * Time.deltaTime;
                CellPos += Vector2.up * _speed * Time.deltaTime;

                _shadowObject.transform.position = CellPos;
                return;
            }

            PositionInfo.State = PlayerState.Moving;
            _moveDir = Vector2.up;

            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.UpArrow);
            //CheckUpdatedFlag();

        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {

            if (_state == PlayerState.Jump)
            {
                CellPos += Vector2.down * _speed * Time.deltaTime;

                _shadowObject.transform.position = CellPos;
                return;
            }
            PositionInfo.State = PlayerState.Moving;


            Debug.Log($"down");
            _moveDir = Vector2.down;
            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.DownArrow);
            //CheckUpdatedFlag();
        }

        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            if (_state == PlayerState.Jump)
            {
                CellPos += Vector2.left * _speed * Time.deltaTime;
                _shadowObject.transform.position = CellPos;
                return;
            }

            PositionInfo.State = PlayerState.Moving;
            _moveDir = Vector2.left;
            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.LeftArrow);
            //CheckUpdatedFlag();
        }

        else if (Input.GetKey(KeyCode.RightArrow))
        {

            if (_state == PlayerState.Jump)
            {
                CellPos += Vector2.right * _speed * Time.deltaTime;
                _shadowObject.transform.position = CellPos;     
                return;
            }
            PositionInfo.State = PlayerState.Moving;
            _moveDir = Vector2.right;
            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.RightArrow);
            //CheckUpdatedFlag();
        }
        //else
        //{
        //    Dir = MoveDir.None;
        //}

    }


    protected override void MoveToNextPos()
    {
        if (Dir == MoveDir.None) // 키 입력이 없을 때.
        {
            PositionInfo.State = PlayerState.Idle;
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
        // TODO : 평타 로직 다시 만들어야함
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (currentAtkCount == 3)
                return;
            isActiveComboDetect = true;
            lastComboTime = comboTimeWindow;
            _state = PlayerState.Atk;
            currentAtkCount++;

            switch (currentAtkCount)
            {

                case 1:
                    {
                        C_Skill skill = new C_Skill() { Info = new SkillInfo() };

                        skill.Info.SkillId = 2;
                        GameManager.Network.Send(skill);
                        _animator.SetTrigger("Attack1");

                        GameManager.Sound.Play($"Effect/Swordman/sm_atk_01");
                        GameManager.Sound.Play($"Effect/Swordman/weapon/kata_01");

                        break;
                    }
                case 2:
                    {
                        C_Skill skill = new C_Skill() { Info = new SkillInfo() };

                        skill.Info.SkillId = 3;
                        GameManager.Network.Send(skill);

                        _animator.SetTrigger("Attack2");
                        GameManager.Sound.Play($"Effect/Swordman/sm_atk_02");
                        GameManager.Sound.Play($"Effect/Swordman/weapon/kata_02");


                        break;
                    }
                case 3:
                    {
                        C_Skill skill = new C_Skill() { Info = new SkillInfo() };


                        skill.Info.SkillId = 4;
                        GameManager.Network.Send(skill);

                        _animator.SetTrigger("Attack3");
                        GameManager.Sound.Play($"Effect/Swordman/sm_atk_03");
                        GameManager.Sound.Play($"Effect/Swordman/weapon/kata_03");

                        break;
                    }
            }

            //if (Time.time - lastComboTime > comboTimeWindow)
            //{
            //    PosInfo.State = PlayerState.Atk;
            //    lastComboTime = Time.time;
            //    if (currentAtkCount == 0)
            //    {
            //        C_Skill skill = new C_Skill() { Info = new SkillInfo() };
            //        skill.Info.SkillId = 2;
            //        GameManager.Network.Send(skill);



            //        // 첫 번째 공격
            //        _animator.SetTrigger("Attack1");
            //        //_HitBox.gameObject.SetActive(true);

            //        currentAtkCount = 1;


            //        //_coSkillCoolTime = StartCoroutine("CoInputCooltime", 0.2f);



            //    }
            //    else if (currentAtkCount == 1)
            //    {

            //        C_Skill skill = new C_Skill() { Info = new SkillInfo() };
            //        skill.Info.SkillId = 3;
            //        GameManager.Network.Send(skill);


            //        //PosInfo.State = PlayerState.Atk;

            //        //_HitBox.gameObject.SetActive(true);

            //        _animator.SetTrigger("Attack2");
            //        // 두 번째 공격
            //        currentAtkCount = 2;
            //    }
            //    else if (currentAtkCount == 2)
            //    {

            //        C_Skill skill = new C_Skill() { Info = new SkillInfo() };
            //        skill.Info.SkillId = 4;
            //        GameManager.Network.Send(skill);


            //        // 세 번째 공격 (3단 근접 공격)
            //        PosInfo.State = PlayerState.Idle;

            //        //_HitBox.gameObject.SetActive(true);

            //        _animator.SetTrigger("Attack3");
            //        currentAtkCount = 0;

            //    }
            //}
            //// 최초 Idle에서 평타키를 눌렀을 때 or 평타 대기 상태에서 평타키를 눌렀을 떄.
            //// Atk 1타 상타로 진입함.

            //if (PosInfo.State == PlayerState.Jump)
            //    // To-do : 점프 상태에서 점프 평타 모션 넣어야함
            //    return;
            //if (PosInfo.State == PlayerState.Run)
            //    // To-do : 대쉬 상태에서 대쉬 평타 모션 넣어야함
            //    return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            if (PositionInfo.State == PlayerState.Jump)
                return;

            initialPosition = _Sprite.transform.position;
            CellPos = _Sprite.transform.position;
            PositionInfo.State = PlayerState.Jump;

            //C_Skill skill = new C_Skill() { Info = new SkillInfo() };
            //skill.Info.SkillId = 1;
            //GameManager.Network.Send(skill);

            C_Jump c_Jump = new C_Jump();
            c_Jump.PosInfo = PositionInfo;
            GameManager.Network.Send(c_Jump);

            GameManager.Sound.Play($"Effect/Swordman/sm_jump");

        }

        //if (Input.GetKeyDown(KeyCode.T))
        //{
        //    C_Skill skill = new C_Skill() { Info = new SkillInfo() };
        //    skill.Info.SkillId = 1;
        //    GameManager.Network.Send(skill);
        //}
    }




    public void OnKeyUpAction()
    {
        if (PositionInfo.State != PlayerState.Jump)
        {
            if (PositionInfo.State == PlayerState.Atk)
            {
                return;
            }
            PositionInfo.State = PlayerState.Idle;
            PositionInfo.MoveDir = MoveDir.None;
            _updated = true;

            CheckUpdatedFlag();
        }

    }

    public void DoubleKeyAction()
    {
        if (_state == PlayerState.Jump)
        {
            CellPos += _moveDir * _speed * Time.deltaTime;
            _shadowObject.transform.position = CellPos;
            return;
        }
        PositionInfo.State = PlayerState.Run;

        //CheckUpdatedFlag();
    }


    #region ProcMethod

    public override void ProcIdlePlayer()
    {
        base.ProcIdlePlayer();

        _animator?.SetBool("isWalk", false);
        _animator?.SetBool("isRun", false);

    }

    public override void ProcWalkPlayer()
    {
        base.ProcWalkPlayer();
        GameManager.Sound.PlayEffectOneShot($"Effect/common/cave_walk_01");
        _animator.SetBool("isWalk", true);

        //CellPos = transform.position;

        //_HitBox.gameObject.SetActive(false);
    }

    public override void ProcRunPlayer()
    {
        base.ProcRunPlayer();
        GameManager.Sound.PlayEffectOneShot($"Effect/common/cave_walk_01");

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

        if(_updated)
        {
            C_Move movePacket = new C_Move();



            movePacket.PosInfo = new PositionInfo();

            movePacket.PosInfo.PosX = PositionInfo.PosX;
            movePacket.PosInfo.PosY = PositionInfo.PosY;
            movePacket.PosInfo.MoveDir = PositionInfo.MoveDir;
            movePacket.PosInfo.State = PositionInfo.State;


            //Debug.Log($"Current Pos? : {movePacket.PosInfo.PosX} , {movePacket.PosInfo.PosY} , {movePacket.PosInfo.State} , {movePacket.PosInfo.MoveDir}");
            GameManager.Network.Send(movePacket);
            _updated = false;
        }
    }



    private void ResetComboState()
    {
        if (isActiveComboDetect)
        {
            lastComboTime -= Time.deltaTime;
            if (lastComboTime <= 0)
            {
                isActiveComboDetect = false;
                _state = PlayerState.Idle;
                lastComboTime = comboTimeWindow;
                currentAtkCount = 0;
            }

        }
    }



    #region Unity Method


    protected override void Update()
    {
        base.Update();
        ResetComboState();
    }

    #endregion
}
