using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapTest
{
    /*
     ���� �� ������ ���� ���� �Ұ̴ϴ�.

    �׷��ٸ� 2���� �迭�� �� ������ �����.

    �� ���� ���� ���� �ҷ��� �մϴ�. 
    
    ��Į ���� ���� 3*3 ���� , �̽���� �׷��� 
    3*3 2���� �迭�� ���� �����
     
     */

    public int[,] map;

    public void GenMap(int SizeX , int SizeY)
    {
        map = new int[SizeX, SizeY];
    }

}
