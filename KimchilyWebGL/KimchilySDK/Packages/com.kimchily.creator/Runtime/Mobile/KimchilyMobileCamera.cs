using UnityEngine;

namespace Kimchily.Creator.Mobile
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("")]
    public sealed class KimchilyMobileCamera : MonoBehaviour
    {
        private KimchilyMobilePlayer player;
        private float yaw;
        private float pitch = 20;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        public void Initialize(KimchilyMobilePlayer target)
        {
            player = target; yaw = target.transform.eulerAngles.y;
            Camera camera = GetComponent<Camera>(); camera.nearClipPlane = .1f; camera.farClipPlane = 500; camera.fieldOfView = 60;
            SnapToTarget();
        }
        private void LateUpdate()
        {
            if (player == null) return;
            Vector2 delta = player.Controls.InputState.ConsumeLook();
            float scale = 180 / Mathf.Max(200, Mathf.Min(player.Controls.Layout.SafeArea.width, player.Controls.Layout.SafeArea.height));
            yaw += delta.x * scale;
            pitch = Mathf.Clamp(pitch - delta.y * scale, -10, 65);
            Place(false);
        }
        public void SnapToTarget() { Place(true); }
        private void Place(bool snap)
        {
            if (player == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 pivot = player.transform.position + Vector3.up * 1.35f;
            Vector3 direction = rotation * Vector3.back;
            float distance = 5;
            int count = Physics.SphereCastNonAlloc(pivot, .18f, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(player.transform)) distance = Mathf.Min(distance, Mathf.Max(.4f, hits[i].distance - .1f));
            Vector3 position = pivot + direction * distance;
            transform.position = snap ? position : Vector3.Lerp(transform.position, position, 1 - Mathf.Exp(-16 * Time.deltaTime));
            transform.rotation = rotation;
        }
    }
}
