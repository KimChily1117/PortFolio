using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapTest
{
    /*
     던전 앤 파이터 맵을 구현 할겁니다.

    그렇다면 2차원 배열로 방 구조를 만들고.

    그 내부 방을 구현 할려고 합니다. 
    
    바칼 조우 기준 3*3 맵임 , 이스핀즈도 그렇고 
    3*3 2차원 배열로 맵을 만든다
     
     */

    public int[,] map;

    public void GenMap(int SizeX , int SizeY)
    {
        map = new int[SizeX, SizeY];
    }

}
