using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KSY_UI
{    public class UI_Base : MonoBehaviour
    {
        Dictionary<Type, UnityEngine.Object[]> _objects = new Dictionary<Type, UnityEngine.Object[]>();

        
        /// <summary>
       /// Text들의 종류를 열거하기 위해 선언
       /// </summary>
        enum Texts
        {
            PointText,
            ScoreText,
        }
        /// <summary>
        /// Button들의 종류를 열거하기 위해 선언
        /// </summary>
        enum Buttons
        {
            PointButton
        }

        #region 변수

        public int m_testInteger;
        public Button test_btn;
        public Text text;

        #endregion 변수

        public virtual void Bind<T>(Type type) where T : UnityEngine.Object // 리플렉션으로 Enum(열거체를 매개변수로 넘김) 
        {
            string[] names = Enum.GetNames(type); // GetNames와 GetName의 차이 [] <== 배열이 왜 있는가? , 여러가지 넣을수있나?

            UnityEngine.Object[] objects = new UnityEngine.Object[names.Length];

            _objects.Add(typeof(T), objects);

            for (int i = 0; i < names.Length; i++)
            {
                objects[i] = Util.FindChild<T>(gameObject, names[i] , true);
            }

        }


        private void Start()
        {
            Bind<Button>(typeof(Buttons));
            Bind<Text>(typeof(Texts));
        }

    }
}
