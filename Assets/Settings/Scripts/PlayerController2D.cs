using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerController2D : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float jumpForce = 9.5f;
    [SerializeField] private LayerMask groundLayer = 1 << 3;

    [Header("Parede")]
    [SerializeField, Min(0f)] private float wallSlideSpeed = 2.4f;
    [SerializeField] private Vector2 wallJumpVelocity = new Vector2(6.5f, 9.5f);
    [SerializeField, Min(0f)] private float wallJumpLock = 0.18f;

    [Header("Ataque")]
    [SerializeField] private Vector2 attackOffset = new Vector2(0.55f, -0.2f);
    [SerializeField] private float attackRadius = 0.65f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private LayerMask enemyLayer = 1 << 6;

    [Header("Vida")]
    [SerializeField, Min(1)] private int maxHealth = 5;
    [SerializeField, Min(0f)] private float damageCooldown = 0.8f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.16f;
    [SerializeField] private float dashCooldown = 0.7f;

    private const float JumpBufferTime = 0.12f;

    private enum Locomotion
    {
        Idle, Run, Jump, Fall, WallSlide
    }

    private static readonly int LocomotionId = Animator.StringToHash("Locomotion");
    private static readonly int AttackId = Animator.StringToHash("Attack");
    private static readonly int HurtId = Animator.StringToHash("Hurt");
    private static readonly int DeadId = Animator.StringToHash("IsDead");

    private readonly ContactPoint2D[] terrainContacts = new ContactPoint2D[16];
    private readonly HashSet<EnemyController2D> attackedEnemies = new HashSet<EnemyController2D>();

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private ContactFilter2D terrainFilter;
    private float originalGravityScale;
    private float horizontalInput;
    private float jumpUntil = float.NegativeInfinity;
    private float wallJumpLockedUntil = float.NegativeInfinity;
    private float nextAttackAt = float.NegativeInfinity;
    private float nextDashAt = float.NegativeInfinity;
    private float nextDamageAt = float.NegativeInfinity;
    private float dashEndsAt;
    private int facingDirection = 1;
    private bool grounded;
    private bool wallSliding;
    private bool dashing;
    private bool dashRequested;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public bool IsDead => CurrentHealth == 0;
    public event Action HealthChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        terrainFilter.SetLayerMask(groundLayer);
        terrainFilter.useTriggers = false;
        originalGravityScale = rb.gravityScale;
        CurrentHealth = MaxHealth;
    }

    private void Update()
    {
        if (IsDead)
            return;

        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (!dashing && Time.time >= wallJumpLockedUntil && horizontalInput != 0f)
            Face(horizontalInput > 0f ? 1 : -1);

        if (Input.GetKeyDown(KeyCode.Space))
            jumpUntil = Time.time + JumpBufferTime;

        if (Input.GetKeyDown(KeyCode.LeftShift))
            dashRequested = true;

        if ((Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z)) && Time.time >= nextAttackAt)
            Attack();
    }

    private void FixedUpdate()
    {
        if (IsDead)
            return;

        float now = Time.fixedTime;
        if (dashing && now >= dashEndsAt)
            EndDash();

        if (dashRequested)
        {
            dashRequested = false;
            if (!dashing && now >= nextDashAt)
                BeginDash(now);
        }

        if (dashing)
            return;

        Vector2 velocity = rb.velocity;
        int wallNormal = ReadTerrainContacts(velocity.y);
        bool wallJumpLocked = !grounded && now < wallJumpLockedUntil;

        if (grounded)
            wallJumpLockedUntil = float.NegativeInfinity;

        if (!wallJumpLocked)
            velocity.x = horizontalInput * moveSpeed;

        wallSliding = !grounded && !wallJumpLocked && wallNormal * horizontalInput < 0f && velocity.y <= 0f;
        if (now <= jumpUntil && (grounded || (wallNormal != 0 && !wallJumpLocked)))
        {
            if (grounded)
            {
                velocity.y = jumpForce;
            }
            else
            {
                velocity = new Vector2(wallNormal * wallJumpVelocity.x, wallJumpVelocity.y);
                wallJumpLockedUntil = now + wallJumpLock;
                Face(wallNormal);
            }

            jumpUntil = float.NegativeInfinity;
            grounded = false;
            wallSliding = false;
        }

        rb.gravityScale = wallSliding ? 0f : originalGravityScale;
        if (wallSliding)
            velocity.y = -wallSlideSpeed;

        rb.velocity = velocity;
        AnimateMovement(velocity);
    }

    private int ReadTerrainContacts(float verticalSpeed)
    {
        grounded = false;
        int wallNormal = 0;
        int count = rb.GetContacts(terrainFilter, terrainContacts);

        for (int i = 0; i < count; i++)
        {
            Vector2 normal = terrainContacts[i].normal;
            if (normal.y > 0.65f && verticalSpeed <= 0.05f)
                grounded = true;

            if (Mathf.Abs(normal.x) > 0.85f && Mathf.Abs(normal.y) < 0.25f)
            {
                int direction = normal.x > 0f ? 1 : -1;
                if (wallNormal == 0 || direction * horizontalInput < 0f)
                    wallNormal = direction;
            }
        }

        return wallNormal;
    }

    private void Face(int direction)
    {
        facingDirection = direction;
        if (spriteRenderer != null)
            spriteRenderer.flipX = facingDirection < 0;
    }

    private void AnimateMovement(Vector2 velocity)
    {
        if (animator == null)
            return;

        Locomotion state;
        if (grounded)
            state = Mathf.Abs(velocity.x) > 0.05f ? Locomotion.Run : Locomotion.Idle;
        else if (wallSliding)
            state = Locomotion.WallSlide;
        else
            state = velocity.y > 0.05f ? Locomotion.Jump : Locomotion.Fall;

        animator.SetInteger(LocomotionId, (int)state);
    }

    private Vector2 AttackCenter => transform.TransformPoint(attackOffset.x * facingDirection, attackOffset.y, 0f);

    private void Attack()
    {
        nextAttackAt = Time.time + attackCooldown;
        if (animator != null)
            animator.SetTrigger(AttackId);

        attackedEnemies.Clear();
        Vector2 origin = transform.TransformPoint(0f, attackOffset.y, 0f);
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(AttackCenter, attackRadius, enemyLayer))
        {
            EnemyController2D enemy = hit.GetComponentInParent<EnemyController2D>();
            if (enemy != null && !Physics2D.Linecast(origin, hit.bounds.center, groundLayer) && attackedEnemies.Add(enemy))
                enemy.TakeDamage(attackDamage);
        }
    }

    private void BeginDash(float now)
    {
        dashing = true;
        dashEndsAt = now + dashDuration;
        nextDashAt = now + dashCooldown;
        wallJumpLockedUntil = float.NegativeInfinity;
        wallSliding = false;
        rb.gravityScale = 0f;
        rb.velocity = new Vector2(facingDirection * dashSpeed, 0f);
        AnimateMovement(rb.velocity);
    }

    private void EndDash()
    {
        dashing = false;
        rb.gravityScale = originalGravityScale;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0 || Time.time < nextDamageAt)
            return;

        if (damage >= CurrentHealth)
        {
            Die();
            return;
        }

        CurrentHealth -= damage;
        nextDamageAt = Time.time + damageCooldown;
        if (animator != null)
        {
            animator.ResetTrigger(AttackId);
            animator.SetTrigger(HurtId);
        }

        HealthChanged?.Invoke();
    }

    public void Die()
    {
        if (IsDead)
            return;

        CurrentHealth = 0;
        EndDash();
        rb.velocity = Vector2.zero;
        rb.simulated = false;
        if (animator != null)
        {
            animator.ResetTrigger(AttackId);
            animator.ResetTrigger(HurtId);
            animator.SetBool(DeadId, true);
        }

        HealthChanged?.Invoke();
    }

    public void Respawn(Vector2 position)
    {
        EndDash();
        rb.position = position;
        rb.velocity = Vector2.zero;
        rb.simulated = true;
        horizontalInput = 0f;
        grounded = false;
        wallSliding = false;
        dashRequested = false;
        jumpUntil = float.NegativeInfinity;
        wallJumpLockedUntil = float.NegativeInfinity;
        nextAttackAt = float.NegativeInfinity;
        nextDashAt = float.NegativeInfinity;
        nextDamageAt = Time.time + damageCooldown;
        CurrentHealth = MaxHealth;
        Face(1);

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        HealthChanged?.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackCenter, attackRadius);
    }
}
