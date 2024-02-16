using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Web;
using UnityEngine;
using UnityEngine.Networking;



// Todo : ��ȸ�� ������ �Լ��� ��� �и��Ұ�����??? ��� �غ���


public class ApiHelper : MonoBehaviour
{
    public string TEST_URL = string.Empty;

    private string APIKEY = "rEBF1IaLeT1g0PWClCmiYChe7hOIwEQ0";



    //string str = $"https://img-api.neople.co.kr/df/servers/<{HttpUtility.UrlEncode("bakal",System.Text.Encoding.UTF8)}>/characters/<characterId>?zoom=<1>";



    // URL : https://api.neople.co.kr/df/servers/<serverId>/characters?characterName=<characterName>&jobId=<jobId>&jobGrowId=<jobGrowId>&isAllJobGrow=<isAllJobGrow>&limit=<limit>&wordType=<wordType>&apikey=rEBF1IaLeT1g0PWClCmiYChe7hOIwEQ0
    // ĳ���� �˻� API 

    // URL : https://api.neople.co.kr/df/servers/<serverId>/characters/<characterId>?apikey=rEBF1IaLeT1g0PWClCmiYChe7hOIwEQ0

    // ĳ���� �⺻ ���� ��û API 

    public void MakeCharInfoURL()
    {
        //string str = $"https://img-api.neople.co.kr/df/servers/<{HttpUtility.UrlEncode("bakal",System.Text.Encoding.UTF8)}>/characters/<characterId>?zoom=<1>";
        string str = $"https://api.neople.co.kr/df/servers/{HttpUtility.UrlEncode("bakal", System.Text.Encoding.UTF8)}/characters?characterName={HttpUtility.UrlEncode("ĥ�������ݸ�", System.Text.Encoding.UTF8)}&apikey={APIKEY}";

        TEST_URL = str ;
    }



    public string MakeCharDetailInfo()
    {
        // URL : https://api.neople.co.kr/df/servers/<serverId>/characters/<characterId>?apikey=rEBF1IaLeT1g0PWClCmiYChe7hOIwEQ0
        string charid = GameManager.DataManager.CHARID;



        string Url = $"https://api.neople.co.kr/df/servers/{HttpUtility.UrlEncode("bakal", System.Text.Encoding.UTF8)}/characters/{charid}?apikey=rEBF1IaLeT1g0PWClCmiYChe7hOIwEQ0";


        return Url;
    }

    
}