using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public sealed class PlayerAttack : MonoBehaviour
{
    private const float DefaultChargeDuration = 0.75f;
    private const float DefaultCooldown = 0.15f;
    private const float DefaultDiagonalInputGrace = 0.12f;
    private const float DefaultProjectileLifetime = 3f;

    [Header("Charge")]
    [Min(0f)]
    [SerializeField] private float minimumChargeDuration = 0.1f;
    [Min(0.01f)]
    [SerializeField] private float chargeDuration = DefaultChargeDuration;
    [Min(0f)]
    [SerializeField] private float cooldown = DefaultCooldown;
    [Tooltip("대각선 WASD 입력에서 한 키가 먼저 떨어져도 마지막 대각선 방향을 유지하는 시간")]
    [Min(0f)]
    [SerializeField] private float diagonalInputGrace = DefaultDiagonalInputGrace;

    [Header("Projectile")]
    [SerializeField] private Sprite projectileSprite;
    [Min(0.01f)]
    [SerializeField] private float minimumProjectileSpeed = 6f;
    [Min(0.01f)]
    [SerializeField] private float projectileSpeed = 12f;
    [Min(0f)]
    [SerializeField] private float projectileDeceleration = 5f;
    [Min(0.01f)]
    [SerializeField] private float projectileLifetime = DefaultProjectileLifetime;
    [Min(0f)]
    [SerializeField] private float returnAcceleration = 18f;
    [Min(0f)]
    [SerializeField] private float returnDeceleration = 20f;
    [Min(0.01f)]
    [SerializeField] private float maximumReturnSpeed = 14f;
    [Min(0f)]
    [SerializeField] private float spawnDistance = 0.65f;
    [Min(0)]
    [SerializeField] private int damage = 1;

    [Header("Attack Poses")]
    [SerializeField] private Sprite attackLeft;
    [SerializeField] private Sprite attackLeftUp;
    [SerializeField] private Sprite attackUp;
    [SerializeField] private Sprite attackRightUp;
    [SerializeField] private Sprite attackRight;
    [SerializeField] private Sprite attackLeftDown;
    [SerializeField] private Sprite attackDown;
    [SerializeField] private Sprite attackRightDown;

    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector2 aimDirection = Vector2.left;
    private float chargeElapsed;
    private float cooldownRemaining;
    private float singleDirectionElapsed;
    private bool isCharging;
    private bool hasArrow = true;
    private bool waitForChargeButtonRelease;
    private PlayerProjectile activeProjectile;

    public float ChargeProgress =>
        isCharging ? Mathf.Clamp01(chargeElapsed / chargeDuration) : 0f;
    public bool IsCharging => isCharging;
    public bool HasArrow => hasArrow;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);

        Vector2 aimInput = ReadAimInput();
        if (aimInput.sqrMagnitude > 0f)
        {
            aimDirection = ResolveAimDirection(aimInput);
        }

        bool isChargeButtonHeld = IsChargeButtonHeld();
        if (!hasArrow)
        {
            activeProjectile?.SetReturning(isChargeButtonHeld);
            return;
        }

        if (waitForChargeButtonRelease)
        {
            if (!isChargeButtonHeld)
            {
                waitForChargeButtonRelease = false;
            }

            return;
        }

        if (isChargeButtonHeld)
        {
            if (!isCharging && hasArrow && cooldownRemaining <= 0f)
            {
                BeginCharge();
            }

            if (isCharging)
            {
                chargeElapsed += Time.deltaTime;
                spriteRenderer.sprite = GetAttackPose(aimDirection);
            }

            return;
        }

        if (!isCharging)
        {
            return;
        }

        if (chargeElapsed >= minimumChargeDuration)
        {
            float chargePower = Mathf.InverseLerp(
                minimumChargeDuration,
                Mathf.Max(minimumChargeDuration + 0.01f, chargeDuration),
                chargeElapsed);
            Fire(chargePower);
        }

        EndCharge();
    }

    private void OnDisable()
    {
        if (isCharging)
        {
            EndCharge();
        }
    }

    private void BeginCharge()
    {
        isCharging = true;
        chargeElapsed = 0f;
        singleDirectionElapsed = 0f;
        animator.enabled = false;
        spriteRenderer.sprite = GetAttackPose(aimDirection);
    }

    private void EndCharge()
    {
        isCharging = false;
        chargeElapsed = 0f;
        singleDirectionElapsed = 0f;
        cooldownRemaining = cooldown;
        animator.enabled = true;
        animator.Update(0f);
    }

    private void Fire(float chargePower)
    {
        if (projectileSprite == null)
        {
            Debug.LogWarning("Projectile sprite is not assigned.", this);
            return;
        }

        GameObject projectileObject = new("Player Projectile");
        projectileObject.transform.position =
            transform.position + (Vector3)(aimDirection * spawnDistance);

        SpriteRenderer projectileRenderer =
            projectileObject.AddComponent<SpriteRenderer>();
        projectileRenderer.sprite = projectileSprite;
        projectileRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        projectileRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;

        PlayerProjectile projectile =
            projectileObject.AddComponent<PlayerProjectile>();
        float chargedProjectileSpeed = Mathf.Lerp(
            minimumProjectileSpeed,
            projectileSpeed,
            chargePower);
        hasArrow = false;
        activeProjectile = projectile;
        projectile.Initialize(
            aimDirection,
            chargedProjectileSpeed,
            projectileDeceleration,
            projectileLifetime,
            returnAcceleration,
            returnDeceleration,
            maximumReturnSpeed,
            damage,
            gameObject,
            this);
    }

    public void RecoverArrow(PlayerProjectile projectile)
    {
        if (projectile == null || projectile != activeProjectile || hasArrow)
        {
            return;
        }

        hasArrow = true;
        activeProjectile = null;
        waitForChargeButtonRelease = true;
        cooldownRemaining = 0f;
        Destroy(projectile.gameObject);
    }

    private Sprite GetAttackPose(Vector2 direction)
    {
        int x = Mathf.RoundToInt(direction.x);
        int y = Mathf.RoundToInt(direction.y);

        if (x < 0 && y > 0) return attackLeftUp;
        if (x > 0 && y > 0) return attackRightUp;
        if (x < 0 && y < 0) return attackLeftDown;
        if (x > 0 && y < 0) return attackRightDown;
        if (x < 0) return attackLeft;
        if (x > 0) return attackRight;
        if (y > 0) return attackUp;
        return attackDown;
    }

    private Vector2 ResolveAimDirection(Vector2 input)
    {
        bool isDiagonalInput = input.x != 0f && input.y != 0f;
        bool wasAimingDiagonally =
            aimDirection.x != 0f && aimDirection.y != 0f;

        if (isDiagonalInput || !wasAimingDiagonally || !isCharging)
        {
            singleDirectionElapsed = 0f;
            return input.normalized;
        }

        singleDirectionElapsed += Time.deltaTime;
        if (singleDirectionElapsed <= diagonalInputGrace)
        {
            return aimDirection;
        }

        return input.normalized;
    }

    private static bool IsChargeButtonHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.cKey.isPressed;
    }

    private static Vector2 ReadAimInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        return new Vector2(
            (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
            (keyboard.leftArrowKey.isPressed ? 1f : 0f),
            (keyboard.upArrowKey.isPressed ? 1f : 0f) -
            (keyboard.downArrowKey.isPressed ? 1f : 0f));
    }
}
