using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Character;
public class EnemyPlayer : BaseCharacter
{
    UI_BossHpBar _bossHpBar;

    [SerializeField]
    Transform[] MeteorAreas;

    public float animTime = 0f;
    protected override void Start()
    {
        base.Start();

        Debug.Log($"Enemy Start");


        _bossHpBar = GameManager.UI._scene as UI_BossHpBar;

        if (_bossHpBar == null)
            return;
        _bossHpBar.targetChar = this;

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


    private void triggerOn()
    {
        _bossHpBar.isDecrease = true;
    }


    public void UseSkill(int skillId)
    {
        if (skillId == 4)
        {
            Debug.Log($"BAKAL SKILL EXPLOSION!!!!");
            StartCoroutine(MeteorPattern());
        }

        else
        {

        }
    }




    #region 스킬 연출

    IEnumerator MeteorPattern()
    {
        _animator.SetTrigger("SkillTrigger");

        animTime = 0f;

        yield return new WaitUntil(() => animTime >= 0.2f);

        MeteorAreas[0].gameObject.SetActive(true);
        MeteorAreas[5].gameObject.SetActive(true);

        // Todo Camera Shake


        //foreach (Transform t in MeteorAreas)
        //{
        //    t.gameObject.SetActive(true);
        //}

        yield return new WaitUntil(() => animTime >= 0.59f);

        MeteorAreas[1].gameObject.SetActive(true);
        MeteorAreas[4].gameObject.SetActive(true);

        // Todo Camera Shake


        yield return new WaitUntil(() => animTime >= 0.95f);


        MeteorAreas[2].gameObject.SetActive(true);
        MeteorAreas[3].gameObject.SetActive(true);

        // Todo Camera Shake


        yield return new WaitForSeconds(3f);

        foreach (Transform t in MeteorAreas)
        {
            t.gameObject.SetActive(false);
        }

    }
    #endregion

}








