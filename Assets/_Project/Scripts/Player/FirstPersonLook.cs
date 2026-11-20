using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// First-person mouse look. Lives on the camera, which is a child of the player.
    /// Horizontal input (yaw) turns the whole player body, vertical input (pitch) tilts only the camera.
    /// Input: project-wide action "Player/Look".
    /// </summary>
    public class FirstPersonLook : MonoBehaviour
    {
        [Tooltip("Object rotated left/right. Defaults to the parent PlayerController.")]
        [SerializeField] private Transform body;

        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] private float sensitivity = 0.1f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private InputAction _look;
        private float _pitch;

        private void Awake()
        {
            _look = InputSystem.actions.FindAction("Player/Look", true);

            if (body == null)
            {
                var player = GetComponentInParent<PlayerController>();
                body = player != null ? player.transform : transform.parent;
            }

            _pitch = NormalizeAngle(transform.localEulerAngles.x);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;

            Vector2 delta = _look.ReadValue<Vector2>() * sensitivity;

            if (body != null)
                body.Rotate(0f, delta.x, 0f, Space.Self);

            _pitch = Mathf.Clamp(_pitch - delta.y, minPitch, maxPitch);
            transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
    }
}
