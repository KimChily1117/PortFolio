using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
public class EnemyPlayer : BaseCharacter
{
    UI_BakalSceneUI _bakalSceneUI;

    [SerializeField]
    Transform[] MeteorAreas;

    public float animTime = 0f;
    protected override void Start()
    {
        base.Start();

        Debug.Log($"Enemy Start");


        _bakalSceneUI = GameManager.UI._scene as UI_BakalSceneUI;

        if (_bakalSceneUI == null)
            return;
        _bakalSceneUI.targetChar = this;

        MeteorAreas = new Transform[6];

        GameObject go = GameObject.Find("Map");

        if (go == null)
            return;

        for (int i = 0; i < MeteorAreas.Length; i++)
        {
            MeteorAreas[i] = go.FindChild<Transform>($"Meteor_Area_{i + 1}");
        }

        foreach (Transform t in MeteorAreas)
        {
            t.gameObject.SetActive(false);
        }

    }

    protected override void Update()
    {
        base.Update();
        if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Bakal2p_Skill") == true)
        {
            animTime = _animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        }
    }
    public override void TakeDamage(float Damage = 0)
    {
        base.TakeDamage(Damage);
        Invoke("triggerOn", 0.2f);


    }


    public override void OnDead()
    {
        _animator.SetTrigger("DeadTrigger");
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_skill_20_2");

        base.OnDead();

    }



    private void triggerOn()
    {
        _bakalSceneUI.isDecrease = true;
    }


    public void UseSkill(int skillId)
    {
        if (skillId == 4)
        {
            Debug.Log($"BAKAL SKILL EXPLOSION!!!!");
            StartCoroutine(MeteorPattern());
        }

        //else if
        //{

        //}
    }




    #region 스킬 연출

    IEnumerator MeteorPattern()
    {
        _animator.SetTrigger("SkillTrigger");

        animTime = 0f;

        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_skill_01_1");

        yield return new WaitUntil(() => animTime >= 0.2f);
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_fire_stomp_exp_02");
        StartCoroutine(CameraShake.Instance.Shake(0.3f, 0.4f));
        MeteorAreas[0].gameObject.SetActive(true);
        MeteorAreas[5].gameObject.SetActive(true);


        //foreach (Transform t in MeteorAreas)
        //{
        //    t.gameObject.SetActive(true);
        //}

        yield return new WaitUntil(() => animTime >= 0.59f);
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_fire_stomp_exp_02");

        StartCoroutine(CameraShake.Instance.Shake(0.3f, 0.4f));

        MeteorAreas[1].gameObject.SetActive(true);
        MeteorAreas[4].gameObject.SetActive(true);

        yield return new WaitUntil(() => animTime >= 0.95f);
        GameManager.Sound.Play($"Sounds/mon/bakal/bakal_dragon_fire_stomp_exp_03");


        StartCoroutine(CameraShake.Instance.Shake(0.3f, 0.4f));
        MeteorAreas[2].gameObject.SetActive(true);
        MeteorAreas[3].gameObject.SetActive(true);

        yield return new WaitForSeconds(10f);

        foreach (Transform t in MeteorAreas)
        {
            t.gameObject.SetActive(false);
        }
    }
    #endregion

}








