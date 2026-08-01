using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    private bool isDead;

    public bool IsDead => isDead;

    public void TakeLaserDamage()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        PlayerMove movement = GetComponent<PlayerMove>();
        PlayerAttack attack = GetComponent<PlayerAttack>();
        Animator animator = GetComponent<Animator>();
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        if (movement != null) movement.enabled = false;
        if (attack != null) attack.enabled = false;
        if (animator != null) animator.enabled = false;
        if (spriteRenderer != null) spriteRenderer.color = Color.gray;

        Debug.Log("Player was hit by the EyeCube laser.", this);
    }
}
