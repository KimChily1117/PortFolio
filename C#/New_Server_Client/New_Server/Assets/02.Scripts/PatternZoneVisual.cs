using UnityEngine;

public class PatternZoneVisual : MonoBehaviour
{
    public WorldState2D WorldState;
    public int EndServerTick;

    private void Update()
    {
        if (WorldState == null)
            return;

        if (WorldState.LastServerTick >= EndServerTick)
        {
            Destroy(gameObject);
        }
    }
}