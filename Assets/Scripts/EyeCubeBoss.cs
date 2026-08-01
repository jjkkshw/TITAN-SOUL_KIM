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
    [Min(0.01f)] [SerializeField] private float laserWidth = 0.16f;
    [Min(0f)] [SerializeField] private float laserOriginOffset = 0.65f;
    [Min(0.01f)] [SerializeField] private float playerHitRadius = 0.35f;

    [Header("Laser Colors")]
    [SerializeField] private Color telegraphColor = new(1f, 0.15f, 0.1f, 0.35f);
    [SerializeField] private Color laserColor = new(1f, 0.05f, 0.02f, 1f);

    private EyeCubeVisual3D visual3D;
    private BoxCollider2D hitbox;
    private LineRenderer laserLine;
    private Transform target;
    private BossState state;
    private Vector2 lockedLaserDirection = Vector2.down;
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
        switch (state)
        {
            case BossState.Closed:
                if (stateElapsed >= closedDuration) EnterState(BossState.Moving);
                break;
            case BossState.Moving:
                UpdateRollingMovement();
                break;
            case BossState.Aiming:
                TrackPlayer();
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
        state = nextState;
        stateElapsed = 0f;
        laserDamageApplied = false;
        laserLine.enabled = false;

        if (nextState == BossState.Moving)
        {
            completedRolls = 0;
            isRolling = false;
        }
        else if (nextState == BossState.Telegraph)
        {
            TrackPlayer();
        }
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

    private void TrackPlayer()
    {
        if (target == null)
        {
            return;
        }

        Vector2 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            lockedLaserDirection = ResolveCardinalDirection(toTarget);
        }
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
        Vector3 origin = transform.position +
            (Vector3)(lockedLaserDirection * laserOriginOffset);
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

        Vector2 origin = (Vector2)transform.position +
            lockedLaserDirection * laserOriginOffset;
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
