using System.Collections.Generic;
using UnityEngine;
using Server.Protocol;

public class CombatEventPresenter2D : MonoBehaviour
{
    [SerializeField] private PatternWarningPresenter2D _patternWarningPresenter;

    public void Present(int serverTick, List<CombatEventData> events)
    {
        foreach (CombatEventData ev in events)
        {
            Debug.Log($"[CombatEvent] tick={serverTick} type={ev.EventType} attacker={ev.AttackerId} target={ev.TargetId} action={ev.ActionType} value={ev.Value}");

            if (ev.EventType == CombatEventType.CombatEventSkill)
            {
                if (_patternWarningPresenter == null)
                    continue;

                if (ev.ActionType == ActionType.ActionSkill1)
                {
                    _patternWarningPresenter.ShowShockwaveWarning(ev.AttackerId);
                }
            }
        }
    }

    public void PresentPatternZones(int serverTick, int ownerEnemyId, ActionType actionType, List<PatternZoneData> zones, int durationTick)
    {
        Debug.Log($"[PatternZones] tick={serverTick} owner={ownerEnemyId} action={actionType} zoneCount={zones.Count}");

        if (_patternWarningPresenter == null)
            return;

        if (actionType == ActionType.ActionSkill2)
        {
            _patternWarningPresenter.ShowLightZoneWarning(zones, serverTick, durationTick);
        }
    }
}