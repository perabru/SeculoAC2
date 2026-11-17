using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyController2D : MonoBehaviour
{
    [Header("Alvo")]
    [SerializeField] private Transform player;
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private LayerMask terrainLayer = 1 << 3;

    [Header("Movimento e ataque")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float attackRange = 0.9f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField, Min(0.02f)] private float attackWindup = 0.3f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Vida")]
    [SerializeField, Min(1)] private int maxHealth = 3;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private PlayerController2D target;
    private float nextAttackAt;
    private float attackHitsAt;
    private bool preparingAttack;
    private float hitFlashUntil;
    private Color originalColor;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public bool IsDead => CurrentHealth == 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        CurrentHealth = MaxHealth;
    }

    private void Start()
    {
        target = player != null ? player.GetComponent<PlayerController2D>() : FindObjectOfType<PlayerController2D>();
    }

    private void FixedUpdate()
    {
        if (IsDead)
            return;

        float now = Time.fixedTime;
        rb.velocity = new Vector2(UpdateCombat(now), rb.velocity.y);

        if (spriteRenderer != null && now >= hitFlashUntil)
            spriteRenderer.color = preparingAttack ? new Color(1f, 0.8f, 0.4f) : originalColor;
    }

    private float UpdateCombat(float now)
    {
        if (target == null || target.IsDead)
        {
            preparingAttack = false;
            return 0f;
        }

        Vector2 offset = (Vector2)target.transform.position - rb.position;
        float distance = offset.magnitude;
        bool canSeeTarget = distance <= detectionRange &&
            !Physics2D.Linecast(rb.position, target.transform.position, terrainLayer);

        if (preparingAttack)
        {
            if (now < attackHitsAt)
                return 0f;

            preparingAttack = false;
            nextAttackAt = now + attackCooldown;
            if (canSeeTarget && distance <= attackRange)
                target.TakeDamage(attackDamage);
            return 0f;
        }

        if (!canSeeTarget)
            return 0f;

        if (spriteRenderer != null && Mathf.Abs(offset.x) > 0.01f)
            spriteRenderer.flipX = offset.x < 0f;

        if (distance > attackRange)
            return Mathf.Abs(offset.x) > 0.05f ? Mathf.Sign(offset.x) * moveSpeed : 0f;

        if (now >= nextAttackAt)
        {
            preparingAttack = true;
            attackHitsAt = now + attackWindup;
        }

        return 0f;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        if (IsDead)
        {
            rb.simulated = false;
            Destroy(gameObject);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 0.5f, 0.5f);
            hitFlashUntil = Time.time + 0.12f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
