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
    [SerializeField] private float attackCooldown = 1f;

    [Header("Vida")]
    [SerializeField, Min(1)] private int maxHealth = 3;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private PlayerController2D target;
    private float nextAttackAt;
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

        float horizontalSpeed = 0f;
        if (target != null && !target.IsDead)
        {
            Vector2 offset = (Vector2)target.transform.position - rb.position;
            float distance = offset.magnitude;
            if (distance <= detectionRange && !Physics2D.Linecast(rb.position, target.transform.position, terrainLayer))
            {
                if (spriteRenderer != null && Mathf.Abs(offset.x) > 0.01f)
                    spriteRenderer.flipX = offset.x < 0f;

                if (distance <= attackRange)
                {
                    if (Time.fixedTime >= nextAttackAt)
                    {
                        target.TakeDamage(attackDamage);
                        nextAttackAt = Time.fixedTime + attackCooldown;
                    }
                }
                else if (Mathf.Abs(offset.x) > 0.05f)
                {
                    horizontalSpeed = Mathf.Sign(offset.x) * moveSpeed;
                }
            }
        }

        rb.velocity = new Vector2(horizontalSpeed, rb.velocity.y);

        if (spriteRenderer != null && Time.fixedTime >= hitFlashUntil)
            spriteRenderer.color = originalColor;
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
