using UnityEngine;

public interface IProjectileDamageReceiver
{
    void TakeDamage(int damage);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerProjectile : MonoBehaviour
{
    private const float PickupDistance = 0.7f;

    private Vector2 direction;
    private float speed;
    private float deceleration;
    private float lifetime;
    private float returnAcceleration;
    private float returnDeceleration;
    private float maximumReturnSpeed;
    private float returnSpeed;
    private int damage;
    private GameObject owner;
    private PlayerAttack ownerAttack;
    private Rigidbody2D body;
    private BoxCollider2D hitbox;
    private bool hasLanded;
    private bool isReturning;
    private bool isReturnDecelerating;

    public void Initialize(
        Vector2 travelDirection,
        float travelSpeed,
        float travelDeceleration,
        float maxLifetime,
        float recallAcceleration,
        float recallDeceleration,
        float maxRecallSpeed,
        int projectileDamage,
        GameObject projectileOwner,
        PlayerAttack attackController)
    {
        direction = travelDirection.normalized;
        speed = travelSpeed;
        deceleration = travelDeceleration;
        lifetime = maxLifetime;
        returnAcceleration = recallAcceleration;
        returnDeceleration = recallDeceleration;
        maximumReturnSpeed = maxRecallSpeed;
        damage = projectileDamage;
        owner = projectileOwner;
        ownerAttack = attackController;

        // 원본 화살 스프라이트가 왼쪽을 바라보므로 180도 보정한다.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);

        body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        hitbox = gameObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = new Vector2(0.35f, 0.18f);
    }

    private void Update()
    {
        if (isReturning)
        {
            ReturnStep();
            return;
        }

        if (isReturnDecelerating)
        {
            ReturnDecelerationStep();
            return;
        }

        if (hasLanded)
        {
            TryRecover();
            return;
        }

        transform.position +=
            (Vector3)(direction * speed * Time.deltaTime);
        speed = Mathf.Max(0f, speed - deceleration * Time.deltaTime);

        lifetime -= Time.deltaTime;
        if (speed <= 0f || lifetime <= 0f)
        {
            Land();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isReturning)
        {
            return;
        }

        if (other.gameObject == owner ||
            other.transform.IsChildOf(owner.transform))
        {
            return;
        }

        IProjectileDamageReceiver receiver =
            other.GetComponentInParent<IProjectileDamageReceiver>();
        receiver?.TakeDamage(damage);
        Land();
    }

    private void Land()
    {
        if (hasLanded)
        {
            return;
        }

        hasLanded = true;
        isReturning = false;
        isReturnDecelerating = false;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
        }

        if (hitbox != null)
        {
            hitbox.enabled = true;
        }
    }

    private void TryRecover()
    {
        if (ownerAttack == null || owner == null)
        {
            return;
        }

        float pickupDistanceSquared = PickupDistance * PickupDistance;
        if (((Vector2)(owner.transform.position - transform.position)).sqrMagnitude
            <= pickupDistanceSquared)
        {
            ownerAttack.RecoverArrow(this);
        }
    }

    public void SetReturning(bool shouldReturn)
    {
        if (ownerAttack == null || owner == null ||
            shouldReturn == isReturning)
        {
            return;
        }

        bool wasDecelerating = isReturnDecelerating;
        isReturning = shouldReturn;
        isReturnDecelerating = !shouldReturn;
        hasLanded = false;

        if (shouldReturn && !wasDecelerating)
        {
            returnSpeed = 0f;
        }

        if (hitbox != null)
        {
            hitbox.enabled = false;
        }
    }

    private void ReturnStep()
    {
        if (ownerAttack == null || owner == null)
        {
            return;
        }

        Vector2 toOwner = owner.transform.position - transform.position;
        float pickupDistanceSquared = PickupDistance * PickupDistance;
        if (toOwner.sqrMagnitude <= pickupDistanceSquared)
        {
            ownerAttack.RecoverArrow(this);
            return;
        }

        direction = toOwner.normalized;
        returnSpeed = Mathf.MoveTowards(
            returnSpeed,
            maximumReturnSpeed,
            returnAcceleration * Time.deltaTime);
        transform.position +=
            (Vector3)(direction * returnSpeed * Time.deltaTime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);
    }

    private void ReturnDecelerationStep()
    {
        if (IsWithinPickupDistance())
        {
            ownerAttack.RecoverArrow(this);
            return;
        }

        returnSpeed = Mathf.Max(
            0f,
            returnSpeed - returnDeceleration * Time.deltaTime);
        transform.position +=
            (Vector3)(direction * returnSpeed * Time.deltaTime);

        if (IsWithinPickupDistance())
        {
            ownerAttack.RecoverArrow(this);
        }
        else if (returnSpeed <= 0f)
        {
            Land();
        }
    }

    private bool IsWithinPickupDistance()
    {
        if (ownerAttack == null || owner == null)
        {
            return false;
        }

        float pickupDistanceSquared = PickupDistance * PickupDistance;
        return ((Vector2)(owner.transform.position - transform.position))
            .sqrMagnitude <= pickupDistanceSquared;
    }
}
