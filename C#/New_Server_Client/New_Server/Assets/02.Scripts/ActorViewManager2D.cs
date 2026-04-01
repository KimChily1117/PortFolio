using System.Collections.Generic;
using UnityEngine;
using Server.Protocol;

public class ActorViewManager2D : MonoBehaviour
{
    [SerializeField] private WorldState2D _worldState;
    [SerializeField] private ActorView2D _playerPrefab;
    [SerializeField] private ActorView2D _enemyPrefab;
    [SerializeField] private Transform _actorsRoot;

    private readonly Dictionary<int, ActorView2D> _views = new Dictionary<int, ActorView2D>();

    private void LateUpdate()
    {
        foreach (KeyValuePair<int, ActorStateData> pair in _worldState.Actors)
        {
            ActorStateData state = pair.Value;

            if (!_views.TryGetValue(state.ActorId, out ActorView2D view))
            {
                view = CreateView(state);
                _views.Add(state.ActorId, view);
            }

            view.Apply(state);
        }
    }

    private ActorView2D CreateView(ActorStateData state)
    {
        ActorView2D prefab = state.ActorType == ActorType.Player
            ? _playerPrefab
            : _enemyPrefab;

        ActorView2D view = Instantiate(prefab, _actorsRoot);
        view.Initialize(state.ActorId);
        return view;
    }
}