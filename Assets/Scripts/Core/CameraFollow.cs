using UnityEngine;
using OneButtonRunner.Player;

namespace OneButtonRunner.Core
{
    /// <summary>
    /// Follows the player smoothly. Keeps player on the left side of screen.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private float offsetX = 0f; // 0 = centered, positive = look ahead
        [SerializeField] private float offsetY = 0f;
        [SerializeField] private float smoothSpeed = 5f;

        [Header("Bounds")]
        [SerializeField] private float minY = -2f;
        [SerializeField] private float maxY = 2f;

        private Transform target;
        private Vector3 velocity = Vector3.zero;

        private void Start()
        {
            if (PlayerController.Instance != null)
            {
                target = PlayerController.Instance.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = new Vector3(
                target.position.x + offsetX,
                Mathf.Clamp(target.position.y + offsetY, minY, maxY),
                transform.position.z
            );

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocity,
                1f / smoothSpeed
            );
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
