using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class EyeCubeBoss : MonoBehaviour, IProjectileDamageReceiver
{
    private enum BossState
    {
        Closed,
        Moving,
        Aiming,
        Telegraph,
        Firing,
        Dead
    }

    private enum LaserMode
    {
        Side,
        Sky,
        Ground
    }

    [Header("Pattern")]
    [Min(0f)] [SerializeField] private float closedDuration = 1.5f;
    [Min(1)] [SerializeField] private int rollsPerAttack = 4;
    [Min(0.1f)] [SerializeField] private float rollDistance = 1.3f;
    [Min(0.05f)] [SerializeField] private float rollDuration = 0.3f;
    [Min(0f)] [SerializeField] private float pauseBetweenRolls = 0.08f;
    [Min(0f)] [SerializeField] private float aimingDuration = 1.25f;
    [Min(0f)] [SerializeField] private float telegraphDuration = 0.75f;
    [Min(0.01f)] [SerializeField] private float laserDuration = 0.35f;
    [Min(0.1f)] [SerializeField] private float laserRange = 20f;
    [Min(0.1f)] [SerializeField] private float skyLaserVisualRange = 4f;
    [Min(0.01f)] [SerializeField] private float laserWidth = 0.16f;
    [Min(0f)] [SerializeField] private float laserOriginOffset = 0.65f;
    [Min(0.01f)] [SerializeField] private float playerHitRadius = 0.35f;

    [Header("Ground Laser")]
    [Min(0.01f)] [SerializeField] private float groundLaserRadius = 0.75f;
    [Min(0f)] [SerializeField] private float groundHoverHeight = 5f;
    [Min(0.01f)] [SerializeField] private float hoverSpeed = 5.5f;
    [Min(0.01f)] [SerializeField] private float hoverAcceleration = 14f;
    [Min(0f)] [SerializeField] private float hoverBobAmount = 0.08f;
    [Min(0f)] [SerializeField] private float hoverBobSpeed = 18f;

    [Header("Laser Colors")]
    [SerializeField] private Color telegraphColor = new(1f, 0.15f, 0.1f, 0.35f);
    [SerializeField] private Color laserColor = new(1f, 0.05f, 0.02f, 1f);

    [Header("Sky Laser Visual")]
    [Range(0f, 1f)] [SerializeField] private float skyLaserAlphaMultiplier = 0.35f;

    private EyeCubeVisual3D visual3D;
    private BoxCollider2D hitbox;
    private LineRenderer laserLine;
    private Transform target;
    private BossState state;
    private Vector2 lockedLaserDirection = Vector2.down;
    private Vector2 lockedLaserOriginDirection = Vector2.up;
    private LaserMode laserMode;
    private float hoverBaseHeight;
    private float hoverVelocity;
    private bool isAirLanding;
    private Quaternion airLandingStartRotation;
    private Quaternion airLandingEndRotation;
    private float stateElapsed;
    private bool laserDamageApplied;
    private int completedRolls;
    private bool isRolling;
    private Vector3 rollStartPosition;
    private Vector3 rollEndPosition;
    private Quaternion rollStartRotation;
    private Quaternion rollEndRotation;

    public bool IsVulnerable =>
        state == BossState.Aiming ||
        state == BossState.Telegraph ||
        state == BossState.Firing;
    public bool IsDead => state == BossState.Dead;

    private void Awake()
    {
        SpriteRenderer oldSprite = GetComponent<SpriteRenderer>();
        if (oldSprite != null) oldSprite.enabled = false;
        visual3D = GetComponent<EyeCubeVisual3D>();
        if (visual3D == null) visual3D = gameObject.AddComponent<EyeCubeVisual3D>();
        hitbox = GetComponent<BoxCollider2D>();
        CreateLaserLine();
        FindPlayer();
        EnterState(BossState.Closed);
    }

    private void Update()
    {
        if (state == BossState.Dead)
        {
            return;
        }

        if (target == null)
        {
            FindPlayer();
        }

        stateElapsed += Time.deltaTime;
        UpdateVisualHeight();
        switch (state)
        {
            case BossState.Closed:
                if (stateElapsed >= closedDuration) EnterState(BossState.Moving);
                break;
            case BossState.Moving:
                UpdateRollingMovement();
                break;
            case BossState.Aiming:
                if (stateElapsed >= aimingDuration) EnterState(BossState.Telegraph);
                break;
            case BossState.Telegraph:
                DrawLaser(telegraphColor, laserWidth * 0.35f);
                if (stateElapsed >= telegraphDuration) EnterState(BossState.Firing);
                break;
            case BossState.Firing:
                DrawLaser(laserColor, laserWidth);
                TryDamagePlayer();
                if (stateElapsed >= laserDuration) EnterState(BossState.Closed);
                break;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || !IsVulnerable || state == BossState.Dead)
        {
            return;
        }

        EnterState(BossState.Dead);
        hitbox.enabled = false;
        visual3D.SetDefeated(true);
        Debug.Log("EyeCube defeated.", this);
    }

    private void EnterState(BossState nextState)
    {
        bool beginAirLanding = nextState == BossState.Closed &&
            state == BossState.Firing && laserMode == LaserMode.Ground;
        state = nextState;
        stateElapsed = 0f;
        laserDamageApplied = false;
        laserLine.enabled = false;

        if (nextState == BossState.Dead)
        {
            hoverBaseHeight = 0f;
            hoverVelocity = 0f;
            visual3D.VisualHeight = 0f;
        }

        if (beginAirLanding)
        {
            BeginRandomAirLanding();
        }

        if (nextState == BossState.Moving)
        {
            completedRolls = 0;
            isRolling = false;
        }
        else if (nextState == BossState.Aiming)
        {
            LockLaserMode();
        }
    }

    private void LockLaserMode()
    {
        Vector3 normal = visual3D.TopFaceNormal;
        lockedLaserOriginDirection = ResolveCardinalDirection(
            visual3D.TopFaceDirection);

        if (Mathf.Abs(normal.y) > 0.5f)
        {
            laserMode = normal.y > 0f ? LaserMode.Sky : LaserMode.Ground;
            return;
        }

        laserMode = LaserMode.Side;
        lockedLaserDirection = lockedLaserOriginDirection;
    }

    private void UpdateVisualHeight()
    {
        bool groundAttack = laserMode == LaserMode.Ground &&
            (state == BossState.Telegraph ||
             state == BossState.Firing);
        float targetHeight = groundAttack ? groundHoverHeight : 0f;
        float targetVelocity = targetHeight > hoverBaseHeight
            ? hoverSpeed
            : -hoverSpeed;
        hoverVelocity = Mathf.MoveTowards(
            hoverVelocity, targetVelocity, hoverAcceleration * Time.deltaTime);
        hoverBaseHeight += hoverVelocity * Time.deltaTime;

        if ((hoverVelocity > 0f && hoverBaseHeight >= targetHeight) ||
            (hoverVelocity < 0f && hoverBaseHeight <= targetHeight))
        {
            hoverBaseHeight = targetHeight;
            hoverVelocity = 0f;
        }

        float bob = groundAttack
            ? Mathf.Sin(Time.time * hoverBobSpeed) * hoverBobAmount
            : 0f;
        visual3D.VisualHeight = Mathf.Max(0f, hoverBaseHeight + bob);

        if (isAirLanding)
        {
            float landingProgress = groundHoverHeight > 0f
                ? 1f - Mathf.Clamp01(hoverBaseHeight / groundHoverHeight)
                : 1f;
            visual3D.CubeRotation = Quaternion.Slerp(
                airLandingStartRotation, airLandingEndRotation, landingProgress);

            if (landingProgress >= 1f)
            {
                isAirLanding = false;
            }
        }
    }

    private void BeginRandomAirLanding()
    {
        Vector2[] directions =
        {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right
        };
        Vector2 direction = directions[Random.Range(0, directions.Length)];
        airLandingStartRotation = visual3D.CubeRotation;

        Vector3 rotationAxis = Mathf.Abs(direction.x) > 0f
            ? new Vector3(0f, 0f, -direction.x)
            : new Vector3(direction.y, 0f, 0f);
        airLandingEndRotation = Quaternion.AngleAxis(90f, rotationAxis) *
            airLandingStartRotation;
        isAirLanding = true;
    }

    private void UpdateRollingMovement()
    {
        if (target == null)
        {
            return;
        }

        if (!isRolling)
        {
            if (completedRolls >= rollsPerAttack)
            {
                EnterState(BossState.Aiming);
                return;
            }

            if (stateElapsed < pauseBetweenRolls && completedRolls > 0)
            {
                return;
            }

            BeginRoll(ResolveCardinalDirection(target.position - transform.position));
        }

        float progress = Mathf.Clamp01(stateElapsed / rollDuration);
        float angle = progress * Mathf.PI * 0.5f;
        float edgePivotProgress =
            (1f - Mathf.Cos(angle) + Mathf.Sin(angle)) * 0.5f;
        transform.position = Vector3.Lerp(
            rollStartPosition, rollEndPosition, edgePivotProgress);
        visual3D.CubeRotation = Quaternion.Slerp(
            rollStartRotation, rollEndRotation, progress);

        if (progress < 1f)
        {
            return;
        }

        transform.position = rollEndPosition;
        visual3D.CubeRotation = rollEndRotation;
        completedRolls++;
        isRolling = false;
        stateElapsed = 0f;
    }

    private void BeginRoll(Vector2 direction)
    {
        isRolling = true;
        stateElapsed = 0f;
        rollStartPosition = transform.position;
        rollEndPosition = rollStartPosition + (Vector3)(direction * rollDistance);
        rollStartRotation = visual3D.CubeRotation;

        Vector3 rotationAxis;
        if (Mathf.Abs(direction.x) > 0f)
        {
            // Horizontal movement rolls the top face onto a side face.
            rotationAxis = new Vector3(0f, 0f, -direction.x);
        }
        else
        {
            // Vertical movement keeps the previously selected reversed rotation.
            rotationAxis = new Vector3(direction.y, 0f, 0f);
        }

        rollEndRotation =
            Quaternion.AngleAxis(90f, rotationAxis) * rollStartRotation;
    }

    private void FindPlayer()
    {
        PlayerMove player = FindFirstObjectByType<PlayerMove>();
        target = player == null ? null : player.transform;
    }

    private static Vector2 ResolveCardinalDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            return direction.x >= 0f ? Vector2.right : Vector2.left;
        }

        return direction.y >= 0f ? Vector2.up : Vector2.down;
    }

    private void DrawLaser(Color color, float width)
    {
        if (laserMode == LaserMode.Sky)
        {
            Vector3 skyOrigin = transform.position +
                (Vector3)(lockedLaserOriginDirection * laserOriginOffset);
            Color skyColor = new(
                color.r, color.g, color.b,
                color.a * skyLaserAlphaMultiplier);
            laserLine.enabled = true;
            laserLine.loop = false;
            laserLine.positionCount = 2;
            laserLine.startColor = skyColor;
            laserLine.endColor = skyColor;
            laserLine.startWidth = width;
            laserLine.endWidth = width;
            laserLine.SetPosition(0, skyOrigin);
            laserLine.SetPosition(1, skyOrigin +
                (Vector3)(lockedLaserOriginDirection * skyLaserVisualRange));
            return;
        }

        laserLine.loop = false;
        laserLine.positionCount = 2;

        if (laserMode == LaserMode.Ground)
        {
            Vector3 groundPoint = transform.position;
            Vector3 faceCenter = groundPoint +
                Vector3.up * visual3D.VisualHeight +
                (Vector3)(lockedLaserOriginDirection * laserOriginOffset);
            laserLine.enabled = true;
            laserLine.startColor = color;
            laserLine.endColor = color;
            laserLine.startWidth = width;
            laserLine.endWidth = width;
            laserLine.SetPosition(0, faceCenter);
            laserLine.SetPosition(1, groundPoint);
            return;
        }

        Vector3 origin = transform.position +
            (Vector3)(lockedLaserOriginDirection * laserOriginOffset);
        laserLine.enabled = true;
        laserLine.startColor = color;
        laserLine.endColor = color;
        laserLine.startWidth = width;
        laserLine.endWidth = width;
        laserLine.SetPosition(0, origin);
        laserLine.SetPosition(1, origin +
            (Vector3)(lockedLaserDirection * laserRange));
    }

    private void TryDamagePlayer()
    {
        if (laserDamageApplied || target == null)
        {
            return;
        }

        if (laserMode == LaserMode.Sky)
        {
            return;
        }

        if (laserMode == LaserMode.Ground)
        {
            float hitRadius = groundLaserRadius + playerHitRadius;
            if (((Vector2)target.position - (Vector2)transform.position)
                .sqrMagnitude <= hitRadius * hitRadius)
            {
                ApplyLaserDamage();
            }
            return;
        }

        Vector2 origin = (Vector2)transform.position +
            lockedLaserOriginDirection * laserOriginOffset;
        Vector2 toPlayer = (Vector2)target.position - origin;
        float distanceAlongBeam = Vector2.Dot(toPlayer, lockedLaserDirection);
        if (distanceAlongBeam < 0f || distanceAlongBeam > laserRange)
        {
            return;
        }

        Vector2 closestPoint = origin +
            lockedLaserDirection * distanceAlongBeam;
        if (((Vector2)target.position - closestPoint).sqrMagnitude >
            playerHitRadius * playerHitRadius)
        {
            return;
        }

        ApplyLaserDamage();
    }

    private void ApplyLaserDamage()
    {
        laserDamageApplied = true;
        PlayerHealth health = target.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeLaserDamage();
        }
    }

    private void CreateLaserLine()
    {
        GameObject lineObject = new("EyeCube Laser");
        lineObject.transform.SetParent(transform, false);
        laserLine = lineObject.AddComponent<LineRenderer>();
        laserLine.useWorldSpace = true;
        laserLine.positionCount = 2;
        laserLine.numCapVertices = 4;
        laserLine.material = new Material(Shader.Find("Sprites/Default"));
        laserLine.sortingOrder = 2;
        laserLine.enabled = false;
    }

    private void OnDestroy()
    {
        if (laserLine != null && laserLine.material != null)
        {
            Destroy(laserLine.material);
        }
    }
}
