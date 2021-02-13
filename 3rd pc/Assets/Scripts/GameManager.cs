using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public Boss boss;
    public BossMissile bossmissile;
    public MeleeArea bossmelee;

    public PlayerController player;



    static GameManager Instance;
    public static GameManager GetInstance()
    { return Instance; }


    private void Awake()
    {
        Instance = this;
    }

 

    private void Update()
    {
        ProcTarget();
    }

    void ProcTarget()
    {

        bossmissile.player = player.GetComponent<PlayerController>();
        bossmelee.target = player.GetComponent<PlayerController>() ;
        bossmissile.target = player.transform;
    }
}
