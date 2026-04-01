using UnityEngine;
using Server.Protocol;

public class ClientTickRunner2D : MonoBehaviour
{
    [SerializeField] private NetworkClient2D _networkClient;

    private const float TickInterval = 1f / 30f;
    private float _accumulator;

    private int _lastMoveX;
    private int _lastMoveY;
    private bool _hasSentInitialMove;

    private void Update()
    {
        if (_networkClient == null || !_networkClient.IsConnected)
            return;

        _accumulator += Time.deltaTime;

        while (_accumulator >= TickInterval)
        {
            _accumulator -= TickInterval;
            Tick();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            _networkClient.SendAction(ActionType.ActionAttack, 1, 0);
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            _networkClient.SendAction(ActionType.ActionJump, 0, 0);
        }
    }

    private void Tick()
    {
        int moveX = 0;
        int moveY = 0;

        if (Input.GetKey(KeyCode.A)) moveX -= 1;
        if (Input.GetKey(KeyCode.D)) moveX += 1;
        if (Input.GetKey(KeyCode.S)) moveY -= 1;
        if (Input.GetKey(KeyCode.W)) moveY += 1;

        if (!_hasSentInitialMove || moveX != _lastMoveX || moveY != _lastMoveY)
        {
            _networkClient.SendMoveInput(moveX, moveY);

            _lastMoveX = moveX;
            _lastMoveY = moveY;
            _hasSentInitialMove = true;
        }
    }
}