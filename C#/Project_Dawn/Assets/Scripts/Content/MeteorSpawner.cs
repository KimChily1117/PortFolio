using UnityEngine;

public class MeteorSpawner : MonoBehaviour
{
    public Meteor m_meteor;

    public Animator animator;

    private void OnEnable()
    {
        GameObject spawnEffect = GameManager.Resources.Instantiate("Effect/MeteorAreaSpawnEffect");

        spawnEffect.transform.position = transform.position;
        spawnEffect.GetComponent<Animator>().Play("Bakal_Skill_AreaExplosion");
        Destroy(spawnEffect, 0.5f);

        GameObject go = GameManager.Resources.Instantiate("Enemy/Meteor");
        go.transform.SetParent(transform);
        go.transform.position = transform.position;

        Meteor meteor = go.GetComponent<Meteor>();
        if (meteor != null)
            meteor.InitializeGroundImpact(transform.position);
        else
            Debug.LogWarning($"[METEOR][SPAWN_FAILED] Reason=MeteorComponentMissing, GroundCenter=({transform.position.x:0.00},{transform.position.y:0.00})");
    }
}
