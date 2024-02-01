using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Web;





public class ApiHelper
{
    public string TEST_URL = string.Empty;


    public void MakeURL()
    {
        //string str = $"https://img-api.neople.co.kr/df/servers/<{HttpUtility.UrlEncode("bakal",System.Text.Encoding.UTF8)}>/characters/<characterId>?zoom=<1>";
        string str = $"https://api.neople.co.kr/df/servers/{HttpUtility.UrlEncode("bakal", System.Text.Encoding.UTF8)}/characters?characterName={HttpUtility.UrlEncode("Ä¥¸®¸ÀÃÊÄÝ¸´", System.Text.Encoding.UTF8)}&apikey=rEBF1IaLeT1g0PWClCmiYChe7hOIwEQ0";

        TEST_URL = str ;
    }

}