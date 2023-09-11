using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Character
{
    public abstract class BaseCharacter : MonoBehaviour
    {
    
        private Stat _playerStat;
        private float _speed = 1.0f;


        public Vector3 _moveDir;
        public MoveDir _lastDir = MoveDir.None;

        public bool _updated = false;


        // * 점프를 위해서 따로 선언

        public float jumpHeight = 2f;
        // 점프력
        public float jumpDuration = 1.0f;
        // 점프 할 시간을 정함

        private bool isJumping = false;
        private float jumpTimer = 0.0f;
        public Vector2 initialPosition;
        // 점프를 시작한 최초의 위치 백터



        [Header("Player HitBox")]
        public BoxCollider2D _HitBox;



        public SpriteRenderer _SpriteRenderer;
        public Animator _animator;


        public int Id { set; get; }


        public Vector2 CellPos
        {
            get
            {
                return new Vector2(PosInfo.PosX, PosInfo.PosY);
            }

            set
            {
                if (PosInfo.PosX == value.x && PosInfo.PosY == value.y)
                    return;

                PosInfo.PosX = value.x;
                PosInfo.PosY = value.y;
                _updated = true;
            }
        }
        // 현재 위치 좌표

        PositionInfo _positionInfo = new PositionInfo();
        public PositionInfo PosInfo 
        {
            get { return _positionInfo; }
            set
            {
                if (_positionInfo.Equals(value))
                    return;
                
                _state = value.State;
                Dir = value.MoveDir;

                _moveDir = GetVecFromDir(Dir);
                Debug.Log($"Dir Set , state {_state} , Dir {Dir} , vec : {_moveDir}");
                //ProcWalkPlayer();
            }
        }



        public virtual PlayerState _state
        {
            get { return PosInfo.State; }
            set
            {
                if (PosInfo.State == value)
                    return;

                PosInfo.State = value;
            }
        }








        public MoveDir Dir
        {
            get { return PosInfo.MoveDir; }
            set
            {

                if (PosInfo.MoveDir == value)
                    return;

                PosInfo.MoveDir = value;
                if (value != MoveDir.None)
                    _lastDir = value;

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

        public Vector3 GetVecFromDir(MoveDir dir)
        {
            Vector3 _dirvec = _moveDir;

            switch (_lastDir)
            {
                case MoveDir.None:
                    _dirvec = Vector3.zero;
                    break;
                case MoveDir.Up:
                    _dirvec = Vector3.up;

                    break;
                case MoveDir.Down:
                    _dirvec = Vector3.down;

                    break;
                case MoveDir.Left:
                    _dirvec = Vector3.left;

                    break;
                case MoveDir.Right:
                    _dirvec = Vector3.right;

                    break;
            }
            return _dirvec;
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



        protected virtual void Start()
        {
            Debug.Log($"Start Test");

            // _inputBuffer = new InputBuffer();


            //PosInfo.State = PlayerState.Idle;

            _moveDir = Vector2.zero;
            _SpriteRenderer = this.GetOrAddComponent<SpriteRenderer>();
            _animator = this.GetOrAddComponent<Animator>();

            _HitBox = Util.FindChild<BoxCollider2D>(this.gameObject, "Hitbox");

            _HitBox.gameObject.SetActive(false);

        }

        void Update() 
        {
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
        }

        public virtual void ProcIdlePlayer()
        {
            _speed = 1.0f;
            
            //_HitBox.gameObject.SetActive(false);

        }


        public virtual void ProcWalkPlayer()
        {
            Debug.Log($"walk State");
            

            if (_moveDir.x < 0)
                transform.localScale = new Vector3(-1, 1, 1);
            else if (_moveDir.x > 0)
            {
                transform.localScale = new Vector3(1, 1, 1);
            }


            //이전 이동 코드
            transform.Translate(_moveDir * Time.deltaTime * _speed);


            //// 수정된 코드

            ////Vector3 destPos = Managers.Map.CurrentGrid.CellToWorld(CellPos) + new Vector3(0.5f, 0.5f);
            ////Vector3 moveDir = destPos - transform.position;

            //// 도착 여부 체크
            //float dist = _moveDir.magnitude;
            //if (dist < _speed * Time.deltaTime)
            //{
            //    transform.position = CellPos;
            //    MoveToNextPos();
            //}
            //else
            //{
            //    transform.position += _moveDir.normalized * _speed * Time.deltaTime;
            //    PosInfo.State = PlayerState.Moving;
            //}



        }

        public virtual void ProcRunPlayer()
        {
            _speed = 2.25f;


            if (_moveDir.x < 0)
                transform.localScale = new Vector3(-1, 1, 1);
            else if (_moveDir.x > 0)
            {
                transform.localScale = new Vector3(1, 1, 1);

            }

            transform.Translate(_moveDir * Time.deltaTime * _speed);
        }


        public virtual void ProcAtk()
        {

        }


        public virtual void ProcJumpPlayer()
        {

            jumpTimer += Time.deltaTime;
            Debug.Log($"Jump Timer : {jumpTimer}");


            if (jumpTimer <= jumpDuration)
            {
                float jumpProgress = jumpTimer / jumpDuration;
                float yOffset = Mathf.Sin(jumpProgress * Mathf.PI) * jumpHeight;
                transform.position = initialPosition + new Vector2(0, yOffset);

                // To-do 여기다가 키보드 입력시 x축으로 움직이는 code 추가필요
                _animator.SetBool("isJump", true);
            }
            else
            {
                isJumping = false;
                jumpTimer = 0.0f;
                transform.position = initialPosition;
                _animator.SetBool("isJump", false);
                PosInfo.State = PlayerState.Idle;

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

    }

}

