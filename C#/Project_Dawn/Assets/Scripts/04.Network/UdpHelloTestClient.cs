using UnityEngine;
using UnityEngine.Serialization;

// Legacy manual UDP test component.
// Runtime UDP registration and movement are now owned by NetworkManager after S_Login.UdpToken.
public class UdpHelloTestClient : MonoBehaviour
{
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private int serverPort = 8081;
    [SerializeField] private string udpToken;

    [FormerlySerializedAs("target")]
    [SerializeField] private Transform targetPlayer;

    public void SetTargetPlayer(Transform playerTransform)
    {
        targetPlayer = playerTransform;
    }
}