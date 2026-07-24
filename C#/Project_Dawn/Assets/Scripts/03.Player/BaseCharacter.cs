using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Character
{
    public abstract class BaseCharacter : MonoBehaviour
    {
        private const bool VerboseCharacterStateLog = false;

        public int WeaponDamage { get; private set; }
        public int ArmorDefence { get; private set; }

        private Stat _playerStat;
        public float _speed = 1.0f;


        public Vector2 _moveDir;
        public MoveDir _lastDir = MoveDir.None;
        public MoveDir _lastHorizontalDir = MoveDir.Right;

        public bool _updated = false;


        // * 점프를 위해서 따로 선언
        public float jumpHeight = 1.23f;
        // 점프력
        public float jumpDuration = 1.0f;
        // 점프 할 시간을 정함
        public bool isJumping = false;
        protected float jumpTimer = 0.0f;
        public Vector2 initialPosition;
        protected Vector3 _spriteDefaultLocalPosition;
        // 점프를 시작한 최초의 위치 백터


        //근접 공격 피격처리를 위한 Class
        protected CombatSystem _combatSystem;


        public GameObject _Sprite { get; protected set; }
        [SerializeField] public GameObject _shadowObject;
        public Animator _animator;


        public float HP = 100;
        public float MaxHP = 100;


        public int Id { set; get; }


        /// <summary>
        /// ObjectInfo에 관한 
        /// </summary>
        public ObjectInfo ObjInfo { set; get; }

        public Vector2 CellPos
        {
            get
            {
                return new Vector2(_positionInfo.PosX, _positionInfo.PosY);
            }

            set
            {
                if (_positionInfo.PosX == value.x && _positionInfo.PosY == value.y)
                    return;

                _positionInfo.PosX = value.x;
                _positionInfo.PosY = value.y;
                _updated = true;
                CheckUpdatedFlag();
            }
        }
        // 현재 위치 좌표

        protected PositionInfo _positionInfo = new PositionInfo();
        public PositionInfo PositionInfo
        {
            get { return _positionInfo; }
            set
            {
                if (_positionInfo.Equals(value))
                    return;

                PositionInfo.PosX = value.PosX;
                PositionInfo.PosY = value.PosY;
                PositionInfo.MoveDir = value.MoveDir;
                if (value.MoveDir == MoveDir.Left || value.MoveDir == MoveDir.Right)
                    _lastHorizontalDir = value.MoveDir;
                PositionInfo.State = value.State;


                _moveDir = GetVecFromDir(Dir);
                if (VerboseCharacterStateLog)
                    Debug.Log($"Dir Set , Id={Id}, Name={ObjInfo?.Name}, state {_state} , Dir {Dir} , vec : {_moveDir}");

            }
        }



        protected void EnsureAnimationEventProxy()
        {
            Animator animator = _animator != null ? _animator : GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"[CLIENT][ANIM_EVENT_PROXY_MISSING] Reason=AnimatorNotFound, Owner={ObjInfo?.Name ?? name}, OwnerType={GetType().Name}");
                return;
            }

            AnimationEventProxy proxy = animator.GetComponent<AnimationEventProxy>();
            if (proxy == null)
            {
                animator.gameObject.AddComponent<AnimationEventProxy>();
                Debug.Log($"[CLIENT][ANIM_EVENT_PROXY_ADDED] AnimatorObject={animator.gameObject.name}, Owner={ObjInfo?.Name ?? name}, OwnerType={GetType().Name}");
            }
            else
            {
                Debug.Log($"[CLIENT][ANIM_EVENT_PROXY_EXISTS] AnimatorObject={animator.gameObject.name}, Owner={ObjInfo?.Name ?? name}, OwnerType={GetType().Name}");
            }

            Debug.Log($"[CLIENT][ANIM_EVENT_PROXY_READY] AnimatorObject={animator.gameObject.name}, Owner={ObjInfo?.Name ?? name}, OwnerType={GetType().Name}");
        }
        public virtual PlayerState _state
        {
            get { return PositionInfo.State; }
            set
            {
                bool stateChanged = _positionInfo.State != value;


                if (!stateChanged)
                    return;
                _positionInfo.State = value;
                _updated = true;

                CheckUpdatedFlag();
            }
        }
        public MoveDir Dir
        {
            get { return _positionInfo.MoveDir; }
            set
            {

                if (_positionInfo.MoveDir == value)
                    return;

                _positionInfo.MoveDir = value;
                if (value != MoveDir.None)
                    _lastDir = value;
                if (value == MoveDir.Left || value == MoveDir.Right)
                    _lastHorizontalDir = value;

                //UpdateAnimation();
                _updated = true;
            }
        }

        public MoveDir GetDirFromVec(Vector2 dir)
        {
            if (dir.x > 0)
                return MoveDir.Right;
            else if (dir.x < 0)
                return MoveDir.Left;
            else if (dir.y > 0)
                return MoveDir.Up;
            else if (dir.y < 0)
                return MoveDir.Down;
            else
                return MoveDir.None;
        }

        public Vector2 GetVecFromDir(MoveDir dir)
        {

            switch (dir)
            {
                case MoveDir.None:
                    return Vector2.zero;
                case MoveDir.Up:
                    return Vector2.up;
                case MoveDir.Down:
                    return Vector2.down;
                case MoveDir.Left:
                    return Vector2.left;
                case MoveDir.Right:
                    return Vector2.right;

                default:
                    Debug.LogError("dir Error!!!");
                    return Vector2.zero;
            }
        }



        protected virtual void InitializeStat(Stat stat)
        {
            if (stat == null)
                return;

            if (_playerStat == null)
            {
                _playerStat = this.GetOrAddComponent<Stat>();
            }
            _playerStat.HP = stat.HP;
        }

        public void SyncPos()
        {
            transform.position = new Vector3(CellPos.x, CellPos.y);
        }


        protected virtual void Start()
        {
            _Sprite = this.GetComponentInChildren<SpriteRenderer>().gameObject;
            _animator = this.GetComponentInChildren<Animator>();
            EnsureAnimationEventProxy();
            _shadowObject = Util.FindChild<SpriteRenderer>(this.gameObject, "Base/Shadow", true).gameObject;
            _spriteDefaultLocalPosition = _Sprite.transform.localPosition;

            _moveDir = GetVecFromDir(Dir);

            if (_moveDir.x < 0)
                _Sprite.transform.localScale = new Vector3(-1, 1, 1);
            else if (_moveDir.x > 0)
            {
                _Sprite.transform.localScale = new Vector3(1, 1, 1);
            }

            // _inputBuffer = new InputBuffer();
            //PosInfo.State = PlayerState.Idle;

        }

        protected virtual void Update()
        {
            if (VerboseCharacterStateLog)
                Debug.Log($"Current State?? : Id={Id}, Name={ObjInfo?.Name}, State={_state}, Transform={transform.position}, Cell={CellPos}");


            if (_combatSystem != null) _combatSystem.OnUpdate();

            switch (_state)
            {
                case PlayerState.Idle:
                    ProcIdlePlayer();
                    break;
                case PlayerState.Moving:
                    ProcWalkPlayer();
                    break;
                case PlayerState.Run:
                    ProcRunPlayer();
                    break;
                case PlayerState.Jump:
                    ProcJumpPlayer();
                    break;
                case PlayerState.Atk:
                    ProcAtk();
                    break;
            }

            if ((Vector2)transform.position != CellPos)
            {

            }

        }

        public virtual void ProcIdlePlayer()
        {
            _speed = 1.0f;

            //_HitBox.gameObject.SetActive(false);

        }


        protected Vector2 ClampLocalPlayerMovement(Vector2 requested)
        {
            if (GameManager.ObjectManager == null || GameManager.ObjectManager.MyPlayer != this)
                return requested;

            if (GameManager.SCENE == null || GameManager.SCENE.CurrentScene != Define.Scenes.TOWN)
                return requested;

            TownScene townScene = TownScene.Current;
            if (townScene != null && townScene.CurrentMapState == TownMapState.SERIAROOM)
                return requested;

            if (MovementBoundsRuntime.TryClamp(GameManager.SCENE.CurrentScene, requested, out Vector2 clamped) == false)
                return requested;

            if ((clamped - requested).sqrMagnitude > 0.0001f)
            {
                Debug.Log($"[CLIENT][MOVE_BOUNDS_CLAMP] Scene={GameManager.SCENE.CurrentScene}, Requested=({requested.x:0.00},{requested.y:0.00}), Accepted=({clamped.x:0.00},{clamped.y:0.00}), Player={ObjInfo?.Name}, ObjectId={Id}");
            }

            return clamped;
        }
        public virtual void ProcWalkPlayer()
        {
            if (VerboseCharacterStateLog)
                Debug.Log($"walk State. Id={Id}, Name={ObjInfo?.Name}, Transform={transform.position}, Cell={CellPos}, MoveDir={_moveDir}");

            if (_moveDir.x < 0)
            {
                _Sprite.transform.localScale = new Vector3(-1, 1, 1);
                _shadowObject.transform.localScale = new Vector3(-1, 1, 1);
            }
            else if (_moveDir.x > 0)
            {
                _Sprite.transform.localScale = new Vector3(1, 1, 1);
                _shadowObject.transform.localScale = new Vector3(1, 1, 1);

            }

            //이전 이동 코드
            //transform.Translate(_moveDir * Time.deltaTime * _speed);
            //_Sprite.transform.Translate(_moveDir * Time.deltaTime * _speed);
            //_shadowObject.transform.position = _Sprite.transform.position;

            Vector2 requested = (Vector2)transform.position + _moveDir * Time.deltaTime * _speed;
            Vector2 accepted = ClampLocalPlayerMovement(requested);
            transform.position = new Vector3(accepted.x, accepted.y, transform.position.z);

            CellPos = accepted;
        }

        public virtual void ProcRunPlayer()
        {

            if (VerboseCharacterStateLog)
                Debug.Log($"Run State!!! Id={Id}, Name={ObjInfo?.Name}, Transform={transform.position}, Cell={CellPos}, MoveDir={_moveDir}");



            _speed = 1.8f;

            if (_moveDir.x < 0)
                _Sprite.transform.localScale = new Vector3(-1, 1, 1);
            else if (_moveDir.x > 0)
            {
                _Sprite.transform.localScale = new Vector3(1, 1, 1);
            }

            //transform.Translate(_moveDir * Time.deltaTime * _speed);
            //_Sprite.transform.Translate(_moveDir * Time.deltaTime * _speed);
            //_shadowObject.transform.position = _Sprite.transform.position;
            //_shadowObject.transform.Translate(_moveDir * Time.deltaTime * _speed);

            Vector2 requested = (Vector2)transform.position + _moveDir * Time.deltaTime * _speed;
            Vector2 accepted = ClampLocalPlayerMovement(requested);
            transform.position = new Vector3(accepted.x, accepted.y, transform.position.z);
            CellPos = accepted;
        }


        public virtual void ProcAtk()
        {

        }


        public virtual void ProcJumpPlayer(bool isMoving = false)
        {

            initialPosition = CellPos;

            jumpTimer += Time.deltaTime;
            //Debug.Log($"Jump Timer : {jumpTimer}");


            if (jumpTimer <= jumpDuration)
            {
                isJumping = true;

                float jumpProgress = jumpTimer / jumpDuration;
                float yOffset = Mathf.Sin(jumpProgress * Mathf.PI) * jumpHeight;
                _Sprite.transform.localPosition = _spriteDefaultLocalPosition + new Vector3(0, yOffset, 0);
                _animator.SetBool("isJump", true);

            }
            else
            {
                isJumping = false;
                jumpTimer = 0.0f;
                _Sprite.transform.localPosition = _spriteDefaultLocalPosition;
                CellPos = transform.position;
                _shadowObject.transform.position = CellPos;

                _animator.SetBool("isJump", false);
                _state = PlayerState.Idle;
            }
        }



        protected virtual void MoveToNextPos()
        {

        }

        /// <summary>
        /// 위치좌표를 갱신해주었는지 확인하고 Network에게 packet를 Send해주는기능을 하는 함수 
        /// </summary>
        protected virtual void CheckUpdatedFlag()
        {

        }

        Coroutine Co_spritechange;
        public virtual void TakeDamage(float Damage = 0f)
        {
            int hitAnimNumber = UnityEngine.Random.Range(1, 2);
            //TODO 피격 이펙트(팡팡 터지는거) , 피격 애니메이션 넣기 , Die까지 

            GameManager.Sound.Play($"Effect/swordman/weapon/kata_hit_0{hitAnimNumber}");

            string hitanim = "Hit" + hitAnimNumber.ToString();

            _animator.SetTrigger(hitanim);
            Debug.Log($"Take Damage : {Damage} ");

            HP -= Damage;
            Co_spritechange = StartCoroutine(hiteffect(_Sprite.GetComponent<SpriteRenderer>()));

            if (HP <= 0)
            {
                Debug.Log($"Character Die!!!");
            }

        }

        IEnumerator hiteffect(SpriteRenderer sp)
        {
            sp.color = Color.red;
            yield return new WaitForSeconds(0.3f);
            sp.color = Color.white;
        }


        public virtual void OnDead()
        {
            Invoke("AfterDeadDestroy", 8f);
        }

        public void AfterDeadDestroy()
        {
            GameManager.ObjectManager.Remove(Id);
        }



        public void RefreshAdditionalStat()
        {
            WeaponDamage = 0;
            ArmorDefence = 0;

            foreach (Item item in GameManager.Inven.Items.Values)
            {
                if (item.Equipped == false)
                    continue;

                switch (item.ItemType)
                {
                    case ItemType.Weapon:
                        WeaponDamage += ((Weapon)item).Damage;
                        break;
                    case ItemType.Armor:
                        ArmorDefence += ((Armor)item).Defence;
                        break;
                }
            }
        }
    }

}



