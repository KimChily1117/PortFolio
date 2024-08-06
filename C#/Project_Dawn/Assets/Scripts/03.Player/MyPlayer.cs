using Character;
using Google.Protobuf.Protocol;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class MyPlayer : BaseCharacter
{
    UI_BakalSceneUI _BakalSceneUI;

    // **  Input Buffer **
    InputBuffer _inputBuffer;
    // **  Input Buffer **

    #region UI
    public UI_Inventory InvenUI;
    bool _isInventory = false;


    public UI_StatInfo StatUI;
    bool _isStatInfo = false;
    #endregion UI


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

        switch (GameManager.PlayerPlatformType)
        {
            case Enums.PlatformType.NONE:
                break;
            case Enums.PlatformType.MOBILE:
                GameManager.Input.TouchAction -= OnJoystickMoveAction;
                GameManager.Input.TouchAction += OnJoystickMoveAction;

                GameManager.Input.EndDragAction -= OnJoystickEndDragAction;
                GameManager.Input.EndDragAction += OnJoystickEndDragAction;

                GameManager.Input.TouchAttackAction -= OnClickAtkBtn;
                GameManager.Input.TouchAttackAction += OnClickAtkBtn;

                GameManager.Input.TouchJumpAction -= OnClickJumpBtn;
                GameManager.Input.TouchJumpAction += OnClickJumpBtn;



                break;
            case Enums.PlatformType.DESKTOP:

                //Test Code

                GameManager.Input.TouchAction -= OnJoystickMoveAction;
                GameManager.Input.TouchAction += OnJoystickMoveAction;
                GameManager.Input.EndDragAction -= OnJoystickEndDragAction;
                GameManager.Input.EndDragAction += OnJoystickEndDragAction;
                GameManager.Input.TouchAttackAction -= OnClickAtkBtn;
                GameManager.Input.TouchAttackAction += OnClickAtkBtn;
                GameManager.Input.TouchJumpAction -= OnClickJumpBtn;
                GameManager.Input.TouchJumpAction += OnClickJumpBtn;

                //Test Code


                GameManager.Input.KeyDownAction -= OnKeyDownMoveAction;
                GameManager.Input.KeyDownAction += OnKeyDownMoveAction;


                GameManager.Input.KeyDownAction -= OnKeyAction;
                GameManager.Input.KeyDownAction += OnKeyAction;


                GameManager.Input.KeyUpAction -= OnKeyUpAction;
                GameManager.Input.KeyUpAction += OnKeyUpAction;


                GameManager.Input.KeyDownAction -= OnKeyUIAction;
                GameManager.Input.KeyDownAction += OnKeyUIAction;


                GameManager.Input.DoubleKeyAction -= DoubleKeyAction;
                GameManager.Input.DoubleKeyAction += DoubleKeyAction;
                break;
            default:
                break;
        }
        _BakalSceneUI = GameManager.UI._scene as UI_BakalSceneUI;

        if (_BakalSceneUI)
        {
            _BakalSceneUI.HUD.targetChar = this;
        }
    }



    public void OnKeyUIAction()
    {
        if(Input.GetKeyDown(KeyCode.I))
        {
            if (_isInventory == false)
            {
                InvenUI = GameManager.UI.ShowPopupUI<UI_Inventory>("UI_Inventory");

                InvenUI.RefreshUI();
                _isInventory = true;
            }
            else
            {
                _isInventory = false;
                InvenUI.gameObject.SetActive(_isInventory);
            }
        }

        if(Input.GetKeyDown(KeyCode.M))
        {
            if(_isStatInfo == false) 
            {
                StatUI = GameManager.UI.ShowPopupUI<UI_StatInfo>("UI_StatInfo");
                _isStatInfo = true;
            }
            else
            {
                _isStatInfo = false;
                StatUI.gameObject.SetActive(_isStatInfo);
            }
        }
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

            _state = PlayerState.Moving;
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
            _state = PlayerState.Moving;


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

            _state = PlayerState.Moving;
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
            _state = PlayerState.Moving;
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

    public void OnJoystickEndDragAction(PointerEventData evt)
    {
        if (PositionInfo.State == PlayerState.Run)
        {
            Debug.Log($"Vector2 is Zero : Drag end");
            _state = PositionInfo.State = PlayerState.Idle;
            _updated = true;
            CheckUpdatedFlag();
        }
    }

    public void OnJoystickMoveAction(float h, float v)
    {
        //Debug.Log($"OnJoystickMoveAction : {h}, {v}");

        Vector2 controllerDir = new Vector2(h, v);

        //if (controllerDir.magnitude <= 0.00001f)
        //{
        //    if (PositionInfo.State == PlayerState.Run)
        //    {
        //        Debug.Log($"Vector2 is zero {controllerDir.magnitude}");
        //        _state = PositionInfo.State = PlayerState.Idle;
        //        _updated = true;
        //        CheckUpdatedFlag();
        //        return;
        //    }
        //}

        if (h < 0)
        {
            _state = PlayerState.Run;
            _moveDir = controllerDir;

            Dir = GetDirFromVec(_moveDir);
            _positionInfo.MoveDir = Dir;
        }
        else if(h > 0) 
        {
            _state = PlayerState.Run;
            _moveDir = controllerDir;


            Dir = GetDirFromVec(_moveDir);
            _positionInfo.MoveDir = Dir;

        }

        else if(v > 0)
        {
            _state = PlayerState.Run;
            _moveDir = controllerDir;

            Dir = GetDirFromVec(_moveDir);
            _positionInfo.MoveDir = Dir;

        }

        else if (v < 0)
        {
            _state = PlayerState.Run;
            _moveDir = controllerDir;
            Dir = GetDirFromVec(_moveDir);
            _positionInfo.MoveDir = Dir;

        }
    }

    protected override void MoveToNextPos()
    {
        //if (Dir == MoveDir.None) // 키 입력이 없을 때.
        //{
        //    PositionInfo.State = PlayerState.Idle;
        //    CheckUpdatedFlag();
        //    return;
        //}

        //Vector2 destPos = CellPos;

        //switch (Dir)
        //{
        //    case MoveDir.Up:
        //        destPos += Vector2.up;
        //        break;
        //    case MoveDir.Down:
        //        destPos += Vector2.down;
        //        break;
        //    case MoveDir.Left:
        //        destPos += Vector2.left;
        //        break;
        //    case MoveDir.Right:
        //        destPos += Vector2.right;
        //        break;
        //}

        ////if (Managers.Map.CanGo(destPos))
        ////{
        ////    if (Managers.Object.Find(destPos) == null)
        ////    {
        ////        CellPos = destPos;
        ////    }
        ////}

        //CheckUpdatedFlag();
    }


#region touchAction

    public void OnClickAtkBtn()
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

    public void OnClickJumpBtn()
    {
        if (_state == PlayerState.Jump)
            return;

        initialPosition = _Sprite.transform.position;
        CellPos = _Sprite.transform.position;
        _state = PlayerState.Jump;

        //C_Skill skill = new C_Skill() { Info = new SkillInfo() };
        //skill.Info.SkillId = 1;
        //GameManager.Network.Send(skill);

        C_Jump c_Jump = new C_Jump();
        c_Jump.PosInfo = PositionInfo;
        GameManager.Network.Send(c_Jump);

        GameManager.Sound.Play($"Effect/Swordman/sm_jump");
    }


    #endregion


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
        if (_state != PlayerState.Jump)
        {
            if (_state == PlayerState.Atk)
            {
                return;
            }
            _state = PlayerState.Idle;
            _positionInfo.MoveDir = MoveDir.None;
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
        _state = PlayerState.Run;

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


    public override void ProcJumpPlayer(bool isMoving = false)
    {
        base.ProcJumpPlayer(isMoving);
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

            movePacket.PosInfo.PosX = _positionInfo.PosX;
            movePacket.PosInfo.PosY = _positionInfo.PosY;
            movePacket.PosInfo.MoveDir = _positionInfo.MoveDir;
            movePacket.PosInfo.State = _positionInfo.State;


            Debug.Log($"Current Pos? : {movePacket.PosInfo.PosX} , {movePacket.PosInfo.PosY} , {movePacket.PosInfo.State} , {movePacket.PosInfo.MoveDir}");
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

    public override void TakeDamage(float Damage = 0)
    {
        base.TakeDamage(Damage);

        if(HP <= 0)
        {
            _animator.SetTrigger("Die");
            _shadowObject.SetActive(false);
            GameManager.Sound.Play("Effect/Swordman/sm_die");
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
