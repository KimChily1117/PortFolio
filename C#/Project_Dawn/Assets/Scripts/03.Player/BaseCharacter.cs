using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Character
{
    public abstract class BaseCharacter : MonoBehaviour
    {
        private Stat _playerStat;

        public Vector2 _moveDir;
        public SpriteRenderer _SpriteRenderer;
        public Animator _animator;
        
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

    }

}

