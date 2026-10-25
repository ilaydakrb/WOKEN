using UnityEngine;
using UnityEngine.InputSystem;

namespace Woken.Characters
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(CharacterAnimator))]
    public sealed class PlayerWASDController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.4f;
        private Rigidbody2D body;
        private Vector2 moveInput;
        public Vector2 MoveInput => moveInput;
        public float MoveSpeed => moveSpeed;
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); body.gravityScale = 0f; body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) { moveInput = Vector2.zero; return; }
            float x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            moveInput = new Vector2(x, y).normalized;
            GetComponent<CharacterAnimator>().SetMotion(moveInput);
        }
        private void FixedUpdate() { body.MovePosition(body.position + moveInput * moveSpeed * Time.fixedDeltaTime); }
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private bool robot;
        private CharacterArtFactory.Facing facing = CharacterArtFactory.Facing.Down;
        private Vector2 motion;
        private float animationClock;
        private int frame;
        public void Initialize(bool isRobot) { robot = isRobot; spriteRenderer = GetComponent<SpriteRenderer>(); ApplySprite(); }
        private void Awake() { if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>(); }
        public void SetMotion(Vector2 direction)
        {
            motion = direction;
            if (direction.sqrMagnitude > .05f)
            {
                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) facing = direction.x < 0 ? CharacterArtFactory.Facing.Left : CharacterArtFactory.Facing.Right;
                else facing = direction.y < 0 ? CharacterArtFactory.Facing.Down : CharacterArtFactory.Facing.Up;
            }
            ApplySprite();
        }
        private void Update()
        {
            if (robot || motion.sqrMagnitude > .02f) { animationClock += Time.deltaTime; if (animationClock >= .12f) { animationClock = 0; frame ^= 1; ApplySprite(); } }
            else if (frame != 0) { frame = 0; animationClock = 0; ApplySprite(); }
        }
        private void ApplySprite()
        {
            if (spriteRenderer == null) return;
            spriteRenderer.sprite = robot ? CharacterArtFactory.GetRobot(facing, frame) : CharacterArtFactory.GetPlayer(facing, frame);
        }
    }
}

