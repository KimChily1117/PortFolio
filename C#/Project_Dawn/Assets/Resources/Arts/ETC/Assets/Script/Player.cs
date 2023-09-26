//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;


//public class Player : MonoBehaviour
//{
//    public int m_nExp = 0;
//    public int m_nLv = 1;
//    public int m_nHP = 100;
//    public int m_nMP = 100;
//    public int m_nStr = 10;
//    public int idx;
//    public int m_nMaxHP;
//    public int m_nMaxMP;

//    public Slider m_nHP_Slider;
//    public Image m_playerHp_Slider;
    

//    public int MaxHP { set { m_nMaxHP = value; } get { return m_nMaxHP; } }
         
    
// // =====================================================================================================================

//        // 몬스터 인벤토리에 넣을려고 선언한 부분, 이부분은 아이템 넣는 부분으로 수정해서 써야겠다. 
    
    
//    //public List<MonsterInfo> m_listMonsterInventory;// { get; set; }

//    //public void SetMonster(MonsterInfo monsterInfo)
//    //{
//    //    m_listMonsterInventory.Add(monsterInfo);
//    //}
//    //public MonsterInfo GetMonster(int idx)
//    //{
//    //    return m_listMonsterInventory[idx];
//    //}
       
//// ======================================================================================================================


//    //private void OnGUI()
//    //{
//    //    string msg = string.Format("Name:{0}\nHP:{1}/{2}\nMP:{3}/{4}\nStr:{5}\nLv/Exp:{6}/{7}", 
//    //                                   gameObject.name, 
//    //                                   m_nHP,m_nMaxHP, 
//    //                                   m_nMP,m_nMaxMP, 
//    //                                   m_nStr, 
//    //                                   m_nLv, m_nExp);
//    //    GUI.Box(new Rect(0, 0, 200,200), msg);       


//    //    //for(int i = 0; i < m_listMonsterInventory.Count; i++)
//    //    //{
//    //    //    GUI.Box(new Rect(Screen.width - 100, 20*i, 100, 20), m_listMonsterInventory[i].name);
//    //    //}
//    //}





//    public void Init(string name, int hp, int mp, int str)
//    {
//        this.gameObject.name = name;
//        m_nHP = hp;
//        m_nMP = mp;
//        m_nStr = str;
//        m_nMaxHP = m_nHP;
//        m_nMaxMP = m_nMP;
//    }

//    public void Attack(Player target)
//    {
//        target.m_nHP -= this.m_nStr;
//    }

//    public bool Death()
//    {
//        if (m_nHP <= 0)
//            return true;
//        else
//            return false;
//    }

//    public void StillExp(Player target)
//    {
//        this.m_nExp += target.m_nExp + (100 * m_nLv);
//    }

//    public void Lvup()
//    {
//        if (this.m_nExp >= 100)
//        {
//            m_nLv++;
//            m_nHP += 10;
//            m_nMP += 10;
//            m_nStr += 5;
//            m_nExp -= 100;
//            m_nMaxHP += 10;
//            m_nMaxMP += 10;
//        }
//    }

    


//    // Start is called before the first frame update
//    void Start()
//    {
//        //m_listMonsterInventory = new List<MonsterInfo>();
//        Init(gameObject.name, m_nHP, m_nMP, m_nStr);
       
//    }

//    // Update is called once per frame
//    void Update()
//    {
       
//        if (Death() == true)
//        { 
//            Destroy(this.gameObject);
//            m_nHP_Slider.value = 0;
           
//        }
        
//        else if (m_nHP_Slider != null && Death() == false)
//        {
//            m_nHP_Slider.maxValue = m_nMaxHP;
//            m_nHP_Slider.value = m_nHP;
            
//        }

        
        
//    }
//}