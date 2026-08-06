using UnityEngine;
using System.Collections.Generic;

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

    [Header("Boss Framing")]
    [SerializeField] private LayerMask bossLayerMask = 1 << 9;
    [Min(0.1f)] [SerializeField] private float bossRefreshInterval = 0.5f;

    private Vector3 velocity;
    private float cameraZ;
    private PlayerAttack playerAttack;
    private Camera cameraComponent;
    private float defaultOrthographicSize;
    private float zoomVelocity;
    private readonly List<Transform> bosses = new();
    private float nextBossRefreshTime;

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
        RefreshBosses();
        if (target != null)
            transform.position = TargetPosition;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (Time.unscaledTime >= nextBossRefreshTime)
            RefreshBosses();

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
            if (TryGetFramingBounds(out Vector2 minimum, out Vector2 maximum))
            {
                Vector2 center = (minimum + maximum) * 0.5f;
                return new Vector3(center.x, center.y, cameraZ);
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
        if (TryGetFramingBounds(out Vector2 minimum, out Vector2 maximum))
        {
            Vector2 size = maximum - minimum;
            float verticalSize = size.y * 0.5f + framingPadding;
            float horizontalSize =
                size.x * 0.5f /
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

    private bool TryGetFramingBounds(out Vector2 minimum, out Vector2 maximum)
    {
        minimum = target.position;
        maximum = target.position;
        bool hasAdditionalTarget = false;

        Transform projectile = playerAttack != null
            ? playerAttack.ActiveProjectileTransform
            : null;
        if (projectile != null)
        {
            Encapsulate(projectile.position, ref minimum, ref maximum);
            hasAdditionalTarget = true;
        }

        for (int i = bosses.Count - 1; i >= 0; i--)
        {
            Transform boss = bosses[i];
            if (boss == null || !boss.gameObject.activeInHierarchy)
            {
                bosses.RemoveAt(i);
                continue;
            }

            EyeCubeBoss eyeCube = boss.GetComponent<EyeCubeBoss>();
            if (eyeCube != null && eyeCube.IsDead)
                continue;

            Encapsulate(boss.position, ref minimum, ref maximum);
            hasAdditionalTarget = true;
        }

        return hasAdditionalTarget;
    }

    private void RefreshBosses()
    {
        bosses.Clear();
        Transform[] sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (Transform candidate in sceneTransforms)
        {
            if ((bossLayerMask.value & (1 << candidate.gameObject.layer)) == 0)
                continue;

            Transform parent = candidate.parent;
            if (parent != null &&
                (bossLayerMask.value & (1 << parent.gameObject.layer)) != 0)
                continue;

            bosses.Add(candidate);
        }

        nextBossRefreshTime = Time.unscaledTime + bossRefreshInterval;
    }

    private static void Encapsulate(
        Vector2 point,
        ref Vector2 minimum,
        ref Vector2 maximum)
    {
        minimum = Vector2.Min(minimum, point);
        maximum = Vector2.Max(maximum, point);
    }
}
