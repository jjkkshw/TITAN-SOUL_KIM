using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerMove : MonoBehaviour
{
    private const float DefaultSpeed = 5f;
    private static readonly Vector2 DefaultIdleDirection = Vector2.left;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int LastMoveXHash = Animator.StringToHash("LastMoveX");
    private static readonly int LastMoveYHash = Animator.StringToHash("LastMoveY");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    [Min(0f)]
    [SerializeField] private float speed = DefaultSpeed;

    private Animator animator;
    private PlayerAttack playerAttack;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerAttack = GetComponent<PlayerAttack>();

        // 시작할 때 왼쪽 Idle이 선택되도록 기본 방향을 지정한다.
        SetIdleDirection(DefaultIdleDirection);
    }

    private void Update()
    {
        bool isMovementLocked = playerAttack != null &&
            (playerAttack.IsCharging || playerAttack.IsRecalling);
        Vector2 moveInput = isMovementLocked ? Vector2.zero : ReadMoveInput();
        transform.position +=
            (Vector3)(moveInput * speed * Time.deltaTime);

        bool isMoving = moveInput.sqrMagnitude > 0f;
        animator.SetFloat(MoveXHash, moveInput.x);
        animator.SetFloat(MoveYHash, moveInput.y);
        animator.SetBool(IsMovingHash, isMoving);

        if (isMoving)
        {
            animator.SetFloat(LastMoveXHash, moveInput.x);
            animator.SetFloat(LastMoveYHash, moveInput.y);
        }
    }

    private static Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = new(
            (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
            (keyboard.leftArrowKey.isPressed ? 1f : 0f),
            (keyboard.upArrowKey.isPressed ? 1f : 0f) -
            (keyboard.downArrowKey.isPressed ? 1f : 0f));

        return input.normalized;
    }

    private void SetIdleDirection(Vector2 direction)
    {
        animator.SetFloat(LastMoveXHash, direction.x);
        animator.SetFloat(LastMoveYHash, direction.y);
    }
}
