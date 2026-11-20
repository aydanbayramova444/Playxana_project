using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Placeholder third-person movement: camera-relative walk / sprint / jump on a CharacterController.
    /// Input comes from project-wide actions (Assets/InputSystem_Actions.inputactions, map "Player").
    /// Replace or extend once the game concept is decided.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float rotationSpeed = 12f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        [Header("References")]
        [Tooltip("Movement is relative to this camera. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        private CharacterController _controller;
        private InputAction _move;
        private InputAction _jump;
        private InputAction _sprint;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            var actions = InputSystem.actions;
            _move = actions.FindAction("Player/Move", true);
            _jump = actions.FindAction("Player/Jump", true);
            _sprint = actions.FindAction("Player/Sprint", true);
        }

        private void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;

            Vector3 direction = GetMoveDirection(_move.ReadValue<Vector2>());
            float speed = _sprint.IsPressed() ? sprintSpeed : moveSpeed;

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, rotationSpeed * Time.deltaTime);
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f; // keeps the controller snapped to the ground

            if (_controller.isGrounded && _jump.WasPressedThisFrame())
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private Vector3 GetMoveDirection(Vector2 input)
        {
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }

            Vector3 direction = forward * input.y + right * input.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }
    }
}
