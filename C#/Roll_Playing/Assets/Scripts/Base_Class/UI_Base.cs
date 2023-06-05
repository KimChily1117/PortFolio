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
       /// Text���� ������ �����ϱ� ���� ����
       /// </summary>
        enum Texts
        {
            PointText,
            ScoreText,
        }
        /// <summary>
        /// Button���� ������ �����ϱ� ���� ����
        /// </summary>
        enum Buttons
        {
            PointButton
        }

        #region ����

        public int m_testInteger;
        public Button test_btn;
        public Text text;

        #endregion ����

        public virtual void Bind<T>(Type type) where T : UnityEngine.Object // ���÷������� Enum(����ü�� �Ű������� �ѱ�) 
        {
            string[] names = Enum.GetNames(type); // GetNames�� GetName�� ���� [] <== �迭�� �� �ִ°�? , �������� �������ֳ�?

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
