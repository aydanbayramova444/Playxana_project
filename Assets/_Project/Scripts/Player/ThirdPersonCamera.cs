using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Placeholder orbit camera: follows the target, rotates with mouse / right stick ("Player/Look"),
    /// and pulls in when a wall is between the camera and the target.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float distance = 5f;

        [Header("Look")]
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private InputAction _look;
        private float _yaw;
        private float _pitch = 15f;

        private void Awake()
        {
            _look = InputSystem.actions.FindAction("Player/Look", true);
            _yaw = transform.eulerAngles.y;
        }

        private void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<PlayerController>();
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            var gm = GameManager.Instance;
            if (gm == null || gm.State == GameState.Playing)
            {
                Vector2 delta = _look.ReadValue<Vector2>();
                _yaw += delta.x * sensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * sensitivity, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pivot = target.position + targetOffset;
            Vector3 back = rotation * Vector3.back;
            float actualDistance = distance;

            if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                actualDistance = hit.distance;
            }

            transform.SetPositionAndRotation(pivot + back * actualDistance, rotation);
        }
    }
}
