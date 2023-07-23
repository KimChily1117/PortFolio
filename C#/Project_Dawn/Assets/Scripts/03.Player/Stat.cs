using System.Collections;
using System.Collections.Generic;
using UnityEngine;



namespace Character
{
    /// <summary>
    /// Mono를 상속 받아 , Monster와 Player에게 붙여준다.
    /// </summary>
    public class Stat : MonoBehaviour
    {
        [SerializeField] private int _hp;
        [SerializeField] private int _mp;
        [SerializeField] private int _moveSpeed;
        [SerializeField] private int _atkSpeed;
        [SerializeField] private int _atkAbllity;

        public int HP
        {
            get { return _hp; }
            set { _hp = value; }
        }
        public int MP
        {
            get { return _mp; }
            set { _mp = value; }
        } 
        public int MOVESPEED
        {
            get { return _moveSpeed; }
            set { _moveSpeed = value; }
        } 
        public int ATKSPEED
        {
            get { return _atkSpeed; }
            set { _atkSpeed = value; }
        }
        public int ATKABLLITY
        {
            get { return _atkAbllity; }
            set { _atkAbllity = value; }
        }
    }
    
}

