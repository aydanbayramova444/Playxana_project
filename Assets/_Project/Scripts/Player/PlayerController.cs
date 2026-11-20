using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// First-person movement on a CharacterController: walk / sprint / jump relative to where the player faces.
    /// Turning is done by <see cref="FirstPersonLook"/> on the child camera.
    /// Input comes from project-wide actions (Assets/InputSystem_Actions.inputactions, map "Player").
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;

        [Header("Jump & Gravity")]
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private InputAction _move;
        private InputAction _jump;
        private InputAction _sprint;
        private float _verticalVelocity;

        public bool IsGrounded => _controller.isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            var actions = InputSystem.actions;
            _move = actions.FindAction("Player/Move", true);
            _jump = actions.FindAction("Player/Jump", true);
            _sprint = actions.FindAction("Player/Sprint", true);
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.State != GameState.Playing) return;

            Vector2 input = _move.ReadValue<Vector2>();
            Vector3 direction = transform.forward * input.y + transform.right * input.x;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            float speed = _sprint.IsPressed() ? sprintSpeed : moveSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f; // keeps the controller snapped to the ground

            if (_controller.isGrounded && _jump.WasPressedThisFrame())
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * speed + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
