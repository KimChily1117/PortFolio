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
    private int _currentBasicAttackSkillId;
    private int _currentBasicAttackComboIndex;
    [SerializeField] private float basicAttackSameSkillCooldown = 1.25f;
    [SerializeField] private float basicAttackSendGuard = 0.05f;
    [SerializeField] private float basicAttackHitFrameFallbackTime = 0.35f;
    [SerializeField] private float basicAttackBufferedInputWindow = 0.45f;
    private float _nextBasicAttackSendTime;
    private readonly float[] _basicAttackSkillReadyAt = new float[5];
    private bool _basicAttackHitFramePending;
    private bool _basicAttackQueuedAfterHit;
    private bool _processingBufferedBasicAttack;
    private float _basicAttackHitFrameFallbackAt;
    private float _basicAttackQueuedUntil;
    private bool _isJoystickRunInputActive;

    [SerializeField] private float runStateGraceTime = 0.15f;
    private float _runStateLockUntil;

    [SerializeField] private float udpMoveSendInterval = 0.05f;
    [SerializeField] private float udpMoveMinDistance = 0.01f;

    private float _nextUdpMoveSendTime;
    private Vector2 _lastUdpMoveSentPos;
    private PlayerState _lastUdpMoveSentState;
    private MoveDir _lastUdpMoveSentDir;
    private bool _hasLastUdpMoveSent;
    private bool _inputBound;

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

        BindInput();

        if (GameManager.SCENE.CurrentScene == Define.Scenes.BAKAL)
        {
            _BakalSceneUI = GameManager.UI._scene as UI_BakalSceneUI;

            if (_BakalSceneUI)
            {
                _BakalSceneUI.HUD.targetChar = this;
                _BakalSceneUI.HUD._invenBtn.onClick.RemoveAllListeners();
                _BakalSceneUI.HUD._StatBtn.onClick.RemoveAllListeners();


                _BakalSceneUI.HUD._invenBtn.onClick.AddListener(OnClickInvenUIButton);
                _BakalSceneUI.HUD._StatBtn.onClick.AddListener(OnClickStatUIButton);
            }
        }

        else if (GameManager.SCENE.CurrentScene == Define.Scenes.TOWN)
        {
            GameManager.UI._scene.GetComponent<UI_HUD>().targetChar = this;
            GameManager.UI._scene.GetComponent<UI_HUD>()._invenBtn.onClick.RemoveAllListeners();
            GameManager.UI._scene.GetComponent<UI_HUD>()._StatBtn.onClick.RemoveAllListeners();

            GameManager.UI._scene.GetComponent<UI_HUD>()._invenBtn.onClick.AddListener(OnClickInvenUIButton);
            GameManager.UI._scene.GetComponent<UI_HUD>()._StatBtn.onClick.AddListener(OnClickStatUIButton);

        }
    }



    public void TeleportAndSync(Vector3 position, MoveDir facing = MoveDir.Right)
    {
        if (!IsInputOwnerValid())
            return;

        transform.position = position;
        if (_shadowObject != null)
            _shadowObject.transform.position = position;

        _positionInfo.PosX = position.x;
        _positionInfo.PosY = position.y;
        _positionInfo.State = PlayerState.Idle;
        _positionInfo.MoveDir = facing;
        _moveDir = Vector2.zero;
        _hasLastUdpMoveSent = false;
        _updated = true;
        CheckUpdatedFlag();

        Debug.Log($"[CLIENT][TELEPORT_SYNC] Player={ObjInfo?.Name}, ObjectId={Id}, Pos=({position.x:0.00},{position.y:0.00}), Facing={facing}");
    }
    public void OnKeyUIAction()
    {
        if (!IsInputOwnerValid())
            return;
        if(Input.GetKeyDown(KeyCode.I))
        {
            if (_isInventory == false)
            {
                if(!InvenUI)
                    InvenUI = GameManager.UI.ShowPopupUI<UI_Inventory>("UI_Inventory");
                InvenUI.RefreshUI();
                _isInventory = true;
                InvenUI.gameObject.SetActive(_isInventory);
            }
            else
            {
                _isInventory = false;
                InvenUI.gameObject.SetActive(_isInventory);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ChannelSelectionOverlay.Toggle();
            return;
        }

        if(Input.GetKeyDown(KeyCode.M))
        {
            if(_isStatInfo == false) 
            {
                if (!StatUI)
                    StatUI = GameManager.UI.ShowPopupUI<UI_StatInfo>("UI_StatInfo");
                _isStatInfo = true;
                StatUI.gameObject.SetActive(_isStatInfo);

            }
            else
            {
                _isStatInfo = false;
                StatUI.gameObject.SetActive(_isStatInfo);
            }
        }
    }


    public void OnClickInvenUIButton()
    {
        if (!IsInputOwnerValid())
            return;
        if (_isInventory == false)
        {
            if (!InvenUI)
                InvenUI = GameManager.UI.ShowPopupUI<UI_Inventory>("UI_Inventory");
            InvenUI.RefreshUI();
            _isInventory = true;
            InvenUI.gameObject.SetActive(_isInventory);
        }
        else
        {
            _isInventory = false;
            InvenUI.gameObject.SetActive(_isInventory);
        }
    }

    public void OnClickStatUIButton()
    {
        if (!IsInputOwnerValid())
            return;
        if (_isStatInfo == false)
        {
            if (!StatUI)
                StatUI = GameManager.UI.ShowPopupUI<UI_StatInfo>("UI_StatInfo");
            _isStatInfo = true;
            StatUI.gameObject.SetActive(_isStatInfo);

        }
        else
        {
            _isStatInfo = false;
            StatUI.gameObject.SetActive(_isStatInfo);
        }
    }



    private void BindInput()
    {
        if (_inputBound || GameManager.Input == null)
            return;

        GameManager.Input.ClearPlayerInputCallbacks();

        switch (GameManager.PlayerPlatformType)
        {
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
        }

        _inputBound = true;
        Debug.Log($"[INPUT][BIND] MyPlayer={ObjInfo?.Name}, ObjectId={Id}");
    }

    public void UnbindInputForSceneChange()
    {
        UnbindInput();
    }

    private void UnbindInput()
    {
        if (_inputBound == false || GameManager.Input == null)
            return;

        GameManager.Input.TouchAction -= OnJoystickMoveAction;
        GameManager.Input.EndDragAction -= OnJoystickEndDragAction;
        GameManager.Input.TouchAttackAction -= OnClickAtkBtn;
        GameManager.Input.TouchJumpAction -= OnClickJumpBtn;

        GameManager.Input.KeyDownAction -= OnKeyDownMoveAction;
        GameManager.Input.KeyDownAction -= OnKeyAction;
        GameManager.Input.KeyDownAction -= OnKeyUIAction;
        GameManager.Input.KeyUpAction -= OnKeyUpAction;
        GameManager.Input.DoubleKeyAction -= DoubleKeyAction;

        _inputBound = false;
        Debug.Log($"[INPUT][UNBIND] MyPlayer={ObjInfo?.Name}, ObjectId={Id}");
    }

    private bool IsInputOwnerValid()
    {
        if (this == null)
            return false;

        if (isActiveAndEnabled == false)
            return false;

        if (GameManager.ObjectManager == null)
            return false;

        if (GameManager.ObjectManager.MyPlayer != this)
            return false;

        return true;
    }
    private bool ShouldKeepRunMoveState()
    {
        return _state == PlayerState.Run ||
               GameManager.Input.DoublePressed ||
               _isJoystickRunInputActive ||
               Time.time < _runStateLockUntil;
    }

    private PlayerState GetInputMoveState()
    {
        return ShouldKeepRunMoveState() ? PlayerState.Run : PlayerState.Moving;
    }

    private PlayerState GetUdpMoveState(PlayerState currentState)
    {
        if (currentState == PlayerState.Moving && ShouldKeepRunMoveState())
            return PlayerState.Run;

        return currentState;
    }

    private void MoveDuringJump(Vector2 direction)
    {
        if (!IsInputOwnerValid())
            return;

        Vector2 requested = (Vector2)transform.position + direction * _speed * Time.deltaTime;
        Vector2 accepted = ClampLocalPlayerMovement(requested);
        transform.position = new Vector3(accepted.x, accepted.y, transform.position.z);
        CellPos = accepted;
        _shadowObject.transform.position = CellPos;
    }

    public void OnKeyDownMoveAction()
    {
        if (!IsInputOwnerValid())
            return;
        PlayerState moveState = GetInputMoveState();

        if (Input.GetKey(KeyCode.UpArrow))
        {
            if (_state == PlayerState.Jump)
            {
                MoveDuringJump(Vector2.up);
                return;
            }

            _state = moveState;
            _moveDir = Vector2.up;

            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.UpArrow);
            //CheckUpdatedFlag();

        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {

            if (_state == PlayerState.Jump)
            {
                MoveDuringJump(Vector2.down);
                return;
            }
            _state = moveState;


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
                MoveDuringJump(Vector2.left);
                return;
            }

            _state = moveState;
            _moveDir = Vector2.left;
            Dir = GetDirFromVec(_moveDir);

            GameManager.Input.SetInputKeyCode(KeyCode.LeftArrow);
            //CheckUpdatedFlag();
        }

        else if (Input.GetKey(KeyCode.RightArrow))
        {

            if (_state == PlayerState.Jump)
            {
                MoveDuringJump(Vector2.right);     
                return;
            }
            _state = moveState;
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
        if (!IsInputOwnerValid())
            return;
        _isJoystickRunInputActive = false;
        _runStateLockUntil = 0f;

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
        if (!IsInputOwnerValid())
            return;
        //Debug.Log($"OnJoystickMoveAction : {h}, {v}");

        Vector2 controllerDir = new Vector2(h, v);
        _isJoystickRunInputActive = controllerDir.sqrMagnitude > 0.0001f;

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


    private bool IsDashInputPendingThisFrame()
    {
        bool movementKeyDown = Input.GetKeyDown(KeyCode.UpArrow) ||
                               Input.GetKeyDown(KeyCode.DownArrow) ||
                               Input.GetKeyDown(KeyCode.LeftArrow) ||
                               Input.GetKeyDown(KeyCode.RightArrow);

        return movementKeyDown && Time.time - GameManager.Input.lastInputElapsed < GameManager.Input.doubleInputThreshold;
    }

    private bool ShouldIgnoreBasicAttack(out PlayerState ignoredState)
    {
        PlayerState currentState = PositionInfo.State;

        if (_state == PlayerState.Jump || currentState == PlayerState.Jump)
        {
            ignoredState = PlayerState.Jump;
            return true;
        }

        if (_state == PlayerState.Run ||
            currentState == PlayerState.Run ||
            GameManager.Input.DoublePressed ||
            IsDashInputPendingThisFrame() ||
            _isJoystickRunInputActive)
        {
            ignoredState = PlayerState.Run;
            return true;
        }

        ignoredState = currentState;
        return false;
    }
    private void TrySendBasicAttack()
    {
        if (ShouldIgnoreBasicAttack(out PlayerState ignoredState))
        {
            Debug.Log($"[SKILL] Basic attack ignored. State={ignoredState}");
            return;
        }

        if (_basicAttackHitFramePending && !_processingBufferedBasicAttack)
        {
            if (currentAtkCount < 3)
            {
                _basicAttackQueuedAfterHit = true;
                _basicAttackQueuedUntil = Time.time + basicAttackBufferedInputWindow;
                Debug.Log($"[CLIENT][ATTACK_INPUT_BUFFERED] CurrentSkillId={_currentBasicAttackSkillId}, CurrentComboIndex={currentAtkCount}");
            }
            else
            {
                Debug.Log("[CLIENT][ATTACK_START_SKIP] Reason=ComboMaxWhileHitFramePending");
            }
            return;
        }

        if (currentAtkCount == 3)
            return;

        int nextComboIndex = currentAtkCount + 1;
        int skillId = GetBasicAttackSkillId(nextComboIndex);
        if (skillId <= 0)
            return;

        float now = Time.time;
        if (now < _nextBasicAttackSendTime)
        {
            Debug.Log($"[CLIENT][ATTACK_START_SKIP] Reason=SendGuard, SkillId={skillId}, RemainingMs={(_nextBasicAttackSendTime - now) * 1000f:0}");
            return;
        }

        if (skillId < _basicAttackSkillReadyAt.Length && now < _basicAttackSkillReadyAt[skillId])
        {
            Debug.Log($"[CLIENT][ATTACK_START_SKIP] Reason=LocalCooldown, SkillId={skillId}, RemainingMs={(_basicAttackSkillReadyAt[skillId] - now) * 1000f:0}");
            return;
        }

        isActiveComboDetect = true;
        lastComboTime = comboTimeWindow;
        _state = PlayerState.Atk;
        currentAtkCount = nextComboIndex;

        _currentBasicAttackSkillId = skillId;
        _currentBasicAttackComboIndex = currentAtkCount;
        _combatSystem?.BeginBasicAttack(skillId, currentAtkCount);

        C_Skill skill = new C_Skill() { Info = new SkillInfo { SkillId = skillId } };
        GameManager.Network.Send(skill);
        _nextBasicAttackSendTime = now + basicAttackSendGuard;
        if (skillId < _basicAttackSkillReadyAt.Length)
            _basicAttackSkillReadyAt[skillId] = now + basicAttackSameSkillCooldown;
        _basicAttackHitFramePending = true;
        _basicAttackHitFrameFallbackAt = now + basicAttackHitFrameFallbackTime;

        MoveDir facing = PositionInfo != null ? PositionInfo.MoveDir : MoveDir.None;
        if (facing != MoveDir.Left && facing != MoveDir.Right)
            facing = _lastHorizontalDir;

        Vector2 attackPos = _shadowObject != null ? (Vector2)_shadowObject.transform.position : (Vector2)transform.position;
        Debug.Log($"[CLIENT][ATTACK_START] SkillId={skillId}, ComboIndex={currentAtkCount}, Facing={facing}, Pos=({attackPos.x:0.00},{attackPos.y:0.00})");

        switch (currentAtkCount)
        {
            case 1:
                _animator.SetTrigger("Attack1");
                GameManager.Sound.Play($"Effect/Swordman/sm_atk_01");
                GameManager.Sound.Play($"Effect/Swordman/weapon/kata_01");
                break;
            case 2:
                _animator.SetTrigger("Attack2");
                GameManager.Sound.Play($"Effect/Swordman/sm_atk_02");
                GameManager.Sound.Play($"Effect/Swordman/weapon/kata_02");
                break;
            case 3:
                _animator.SetTrigger("Attack3");
                GameManager.Sound.Play($"Effect/Swordman/sm_atk_03");
                GameManager.Sound.Play($"Effect/Swordman/weapon/kata_03");
                break;
        }
    }

    private int GetBasicAttackSkillId(int comboIndex)
    {
        switch (comboIndex)
        {
            case 1:
                return 2;
            case 2:
                return 3;
            case 3:
                return 4;
            default:
                return 0;
        }
    }

    public void AnimEvent_BasicAttackHitFrame()
    {
        if (!IsInputOwnerValid())
        {
            Debug.Log("[CLIENT][ATTACK_HIT_FRAME_IGNORED] Reason=InvalidOwner");
            return;
        }

        if (_state != PlayerState.Atk)
        {
            Debug.Log($"[CLIENT][ATTACK_HIT_FRAME_IGNORED] Reason=NotAtkState, State={_state}");
            return;
        }

        if (_currentBasicAttackSkillId <= 0)
        {
            Debug.Log("[CLIENT][ATTACK_HIT_FRAME_IGNORED] Reason=NoCurrentSkill");
            return;
        }

        _combatSystem?.TrySendBasicAttackHitFrameCandidates();
        CompleteBasicAttackHitFrame("AnimEvent");
    }

    private void CompleteBasicAttackHitFrame(string source)
    {
        if (!_basicAttackHitFramePending)
        {
            Debug.Log($"[CLIENT][ATTACK_HIT_FRAME_COMPLETE_SKIP] Reason=NoPendingHitFrame, Source={source}");
            return;
        }

        _basicAttackHitFramePending = false;
        Debug.Log($"[CLIENT][ATTACK_HIT_FRAME_COMPLETE] Source={source}, SkillId={_currentBasicAttackSkillId}, ComboIndex={currentAtkCount}, Buffered={_basicAttackQueuedAfterHit}");

        if (!_basicAttackQueuedAfterHit)
            return;

        _basicAttackQueuedAfterHit = false;
        if (Time.time > _basicAttackQueuedUntil)
        {
            Debug.Log("[CLIENT][ATTACK_BUFFER_DROP] Reason=Expired");
            return;
        }

        _processingBufferedBasicAttack = true;
        TrySendBasicAttack();
        _processingBufferedBasicAttack = false;
    }
#region touchAction

    public void OnClickAtkBtn()
    {
        if (!IsInputOwnerValid())
            return;
        TrySendBasicAttack();
    }

    public void OnClickJumpBtn()
    {
        if (!IsInputOwnerValid())
            return;
        if (_state == PlayerState.Jump)
            return;

        initialPosition = transform.position;
        CellPos = transform.position;
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
        if (!IsInputOwnerValid())
            return;
        // TODO : 평타 로직 다시 만들어야함
        if (Input.GetKeyDown(KeyCode.X))
        {
            TrySendBasicAttack();
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            if (PositionInfo.State == PlayerState.Jump)
                return;

            initialPosition = transform.position;
            CellPos = transform.position;
            _state = PlayerState.Jump;

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
        if (!IsInputOwnerValid())
            return;
        if (_state != PlayerState.Jump)
        {
            if (_state == PlayerState.Atk)
            {
                return;
            }
            _runStateLockUntil = 0f;
            _state = PlayerState.Idle;
            _positionInfo.MoveDir = MoveDir.None;
            _updated = true;

            CheckUpdatedFlag();
        }

    }

    public void DoubleKeyAction()
    {
        if (!IsInputOwnerValid())
            return;
        if (_state == PlayerState.Jump)
        {
            MoveDuringJump(_moveDir);
            return;
        }
        _runStateLockUntil = Time.time + Mathf.Max(0f, runStateGraceTime);
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
        if (!IsInputOwnerValid())
            return;
        base.ProcWalkPlayer();
        GameManager.Sound.PlayEffectOneShot($"Effect/common/cave_walk_01");
        _animator.SetBool("isWalk", true);

        //CellPos = transform.position;

        //_HitBox.gameObject.SetActive(false);
    }

    public override void ProcRunPlayer()
    {
        if (!IsInputOwnerValid())
            return;
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


    private bool ShouldSendUdpMove(Vector2 currentPos, PlayerState state, MoveDir moveDir)
    {
        if (!_hasLastUdpMoveSent)
            return true;

        if (_lastUdpMoveSentState != state || _lastUdpMoveSentDir != moveDir)
            return true;

        if (Time.time < _nextUdpMoveSendTime)
            return false;

        float minDistance = Mathf.Max(0f, udpMoveMinDistance);
        return (currentPos - _lastUdpMoveSentPos).sqrMagnitude >= minDistance * minDistance;
    }

    private void MarkUdpMoveSent(Vector2 currentPos, PlayerState state, MoveDir moveDir)
    {
        _hasLastUdpMoveSent = true;
        _lastUdpMoveSentPos = currentPos;
        _lastUdpMoveSentState = state;
        _lastUdpMoveSentDir = moveDir;
        _nextUdpMoveSendTime = Time.time + Mathf.Max(0f, udpMoveSendInterval);
    }

    protected override void CheckUpdatedFlag()
    {
        if (!IsInputOwnerValid())
            return;
        if (_updated)
        {
            C_Move movePacket = new C_Move();

            movePacket.PosInfo = new PositionInfo();

            movePacket.PosInfo.PosX = _positionInfo.PosX;
            movePacket.PosInfo.PosY = _positionInfo.PosY;
            movePacket.PosInfo.MoveDir = _positionInfo.MoveDir;
            movePacket.PosInfo.State = _positionInfo.State;

            if (GameManager.Network.DebugUdpMovementLog)
                Debug.Log($"Current Pos? : {movePacket.PosInfo.PosX} , {movePacket.PosInfo.PosY} , {movePacket.PosInfo.State} , {movePacket.PosInfo.MoveDir}");

            if (GameManager.Network.UseUdpMovement)
            {
                Vector2 udpPosition2D = new Vector2(_positionInfo.PosX, _positionInfo.PosY);
                PlayerState udpState = GetUdpMoveState(_positionInfo.State);
                MoveDir udpMoveDir = _positionInfo.MoveDir;
                if (!ShouldSendUdpMove(udpPosition2D, udpState, udpMoveDir))
                    return;

                Vector3 udpPosition = new Vector3(_positionInfo.PosX, _positionInfo.PosY, transform.position.z);
                if (GameManager.Network.DebugUdpMovementLog)
                    Debug.Log($"[UDP] Move send selected. Pos=({udpPosition.x},{udpPosition.y}), State={udpState}, MoveDir={udpMoveDir}");
                if (GameManager.Network.SendUdpMove(udpPosition, udpState, udpMoveDir))
                    MarkUdpMoveSent(udpPosition2D, udpState, udpMoveDir);
            }
            else
            {
                GameManager.Network.Send(movePacket);
            }

            _updated = false;
        }
    }



    private void ResetComboState()
    {
        if (_basicAttackHitFramePending && Time.time >= _basicAttackHitFrameFallbackAt)
        {
            Debug.Log($"[CLIENT][ATTACK_HIT_FRAME_FALLBACK] SkillId={_currentBasicAttackSkillId}, ComboIndex={currentAtkCount}");
            CompleteBasicAttackHitFrame("Fallback");
        }

        if (isActiveComboDetect)
        {
            if (_basicAttackHitFramePending)
                return;

            lastComboTime -= Time.deltaTime;
            if (lastComboTime <= 0)
            {
                isActiveComboDetect = false;
                _state = PlayerState.Idle;
                lastComboTime = comboTimeWindow;
                currentAtkCount = 0;
                _currentBasicAttackSkillId = 0;
                _currentBasicAttackComboIndex = 0;
                _basicAttackHitFramePending = false;
                _basicAttackQueuedAfterHit = false;
                _processingBufferedBasicAttack = false;
                _combatSystem?.ClearCurrentAttack();
            }

        }
    }

    public override void TakeDamage(float Damage = 0)
    {
        base.TakeDamage(Damage);

        //if (HP <= 0)
        //{
        //    _animator.SetTrigger("Die");
        //    _shadowObject.SetActive(false);
        //    GameManager.Sound.Play("Effect/Swordman/sm_die");
        //}
    }

    public override void OnDead()
    {
        _animator.SetTrigger("Die");
        _shadowObject.SetActive(false);
        GameManager.Sound.Play("Effect/Swordman/sm_die");
        base.OnDead();
    }


    #region Unity Method


    protected override void Update()
    {
        if (!IsInputOwnerValid())
            return;
        base.Update();
        ResetComboState();
    }


    private void OnDisable()
    {
        UnbindInput();
    }

    private void OnDestroy()
    {
        UnbindInput();
    }
    #endregion
}








