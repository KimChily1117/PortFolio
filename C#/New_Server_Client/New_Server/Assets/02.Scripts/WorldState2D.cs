using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이건 snapshot을 받아서 actor 상태를 캐싱하는 곳이야.
///중요: partial snapshot이므로 기존 값을 유지하고 덮어써야 함.
/// </summary>
public class WorldState2D : MonoBehaviour
{
    private readonly Dictionary<int, ActorStateData> _actors = new Dictionary<int, ActorStateData>();

    public IReadOnlyDictionary<int, ActorStateData> Actors => _actors;

    public int LastServerTick { get; private set; }
    public uint LastAckInputSeq { get; private set; }

    public void ApplySnapshot(int serverTick, uint ackInputSeq, List<ActorStateData> incomingActors)
    {
        LastServerTick = serverTick;
        LastAckInputSeq = ackInputSeq;

        foreach (ActorStateData incoming in incomingActors)
        {
            if (_actors.TryGetValue(incoming.ActorId, out ActorStateData existing))
            {
                existing.ActorType = incoming.ActorType;
                existing.MainState = incoming.MainState;
                existing.PosX = incoming.PosX;
                existing.PosY = incoming.PosY;
                existing.Hp = incoming.Hp;
                existing.MaxHp = incoming.MaxHp;
                existing.IsDead = incoming.IsDead;
                existing.IsJumping = incoming.IsJumping;
            }
            else
            {
                _actors.Add(incoming.ActorId, incoming);
            }
        }
    }

    public bool TryGetActor(int actorId, out ActorStateData actor)
    {
        return _actors.TryGetValue(actorId, out actor);
    }
}