using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Min(0.01f)]
    [SerializeField] private float smoothTime = 0.3f;
    [Min(0f)]
    [SerializeField] private float aimOffsetDistance = 2f;

    [Header("Projectile Framing")]
    [Min(0f)] [SerializeField] private float framingPadding = 1.5f;
    [Min(0.01f)] [SerializeField] private float zoomSmoothTime = 0.3f;
    [Min(0.01f)] [SerializeField] private float maximumOrthographicSize = 10f;

    private Vector3 velocity;
    private float cameraZ;
    private PlayerAttack playerAttack;
    private Camera cameraComponent;
    private float defaultOrthographicSize;
    private float zoomVelocity;

    private void Awake()
    {
        cameraZ = transform.position.z;
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
            defaultOrthographicSize = cameraComponent.orthographicSize;

        if (target == null)
        {
            PlayerMove player = FindFirstObjectByType<PlayerMove>();
            if (player != null)
                target = player.transform;
        }

        if (target != null)
            playerAttack = target.GetComponent<PlayerAttack>();
    }

    private void Start()
    {
        if (target != null)
            transform.position = TargetPosition;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            TargetPosition,
            ref velocity,
            smoothTime);

        UpdateZoom();
    }

    private Vector3 TargetPosition
    {
        get
        {
            Transform projectile = playerAttack != null
                ? playerAttack.ActiveProjectileTransform
                : null;
            if (projectile != null)
            {
                Vector3 midpoint = (target.position + projectile.position) * 0.5f;
                return new Vector3(midpoint.x, midpoint.y, cameraZ);
            }

            Vector2 aimOffset = Vector2.zero;
            if (playerAttack != null && playerAttack.IsCharging)
                aimOffset = playerAttack.AimDirection * aimOffsetDistance;

            return new Vector3(
                target.position.x + aimOffset.x,
                target.position.y + aimOffset.y,
                cameraZ);
        }
    }

    private void UpdateZoom()
    {
        if (cameraComponent == null || !cameraComponent.orthographic)
            return;

        float targetSize = defaultOrthographicSize;
        Transform projectile = playerAttack != null
            ? playerAttack.ActiveProjectileTransform
            : null;
        if (projectile != null)
        {
            Vector3 separation = projectile.position - target.position;
            float verticalSize = Mathf.Abs(separation.y) * 0.5f + framingPadding;
            float horizontalSize =
                Mathf.Abs(separation.x) * 0.5f /
                Mathf.Max(cameraComponent.aspect, 0.01f) + framingPadding;
            targetSize = Mathf.Max(
                defaultOrthographicSize,
                verticalSize,
                horizontalSize);
            targetSize = Mathf.Min(targetSize, maximumOrthographicSize);
        }

        cameraComponent.orthographicSize = Mathf.SmoothDamp(
            cameraComponent.orthographicSize,
            targetSize,
            ref zoomVelocity,
            zoomSmoothTime);
    }
}
