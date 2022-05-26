using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utils
{
    public static string GetRegion(Define.RegionType regionType)
    {
        string returnvalue = string.Empty;

        switch (regionType)
        {
            case Define.RegionType.ASIA:
                returnvalue = "asia";
                break;
            case Define.RegionType.CHINESE:
                returnvalue = "cn";
                break;
            case Define.RegionType.EUROPE:
                returnvalue = "eu";
                break;
            case Define.RegionType.JAPAN:
                returnvalue = "jp";
                break;
            case Define.RegionType.SOUTH_AFRICA:
                returnvalue = "za";
                break;
            case Define.RegionType.SOUTH_KOREA:
                returnvalue = "kr";
                break;
            case Define.RegionType.USA_EAST:
                returnvalue = "us";
                break;
            case Define.RegionType.USA_WEST:
                returnvalue = "usw";
                break;
        }
        return returnvalue;
    }
}
