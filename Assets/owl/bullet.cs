using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class Bullet : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 8f;
    public Vector2 initialDirection = Vector2.right;
    public bool setRotationToDirection = true;

    [Header("Lifetime")]
    public float lifeTime = 5f;

    [Header("Collision Filtering")]
    public LayerMask hitLayers = ~0;
    public string hitTag = "";

    [Header("Impact")]
    public GameObject impactEffectPrefab;

    Rigidbody2D _rb;
    float _spawnTime;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spawnTime = Time.time;
    }

    void Start()
    {
        ApplyVelocity(initialDirection.normalized * speed);

        if (setRotationToDirection && initialDirection.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void Update()
    {
        if (lifeTime > 0f && Time.time - _spawnTime >= lifeTime)
            Destroy(gameObject);
    }

    /// <summary>
    /// Call immediately after Instantiate to set direction and optional speed override.
    /// </summary>
    public void Initialize(Vector2 direction, float speedOverride = -1f)
    {
        Vector2 dir = direction.normalized;
        if (speedOverride > 0f) speed = speedOverride;
        initialDirection = dir;
        ApplyVelocity(dir * speed);

        if (setRotationToDirection && dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void ApplyVelocity(Vector2 vel)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) return;

        var linearProp = typeof(Rigidbody2D).GetProperty("linearVelocity");
        if (linearProp != null)
        {
            linearProp.SetValue(_rb, vel, null);
        }
        else
        {
#pragma warning disable CS0618
            _rb.velocity = vel;
#pragma warning restore CS0618
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!ShouldHit(other.gameObject)) return;
        HandleHit(other.gameObject, other.transform.position);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!ShouldHit(collision.gameObject)) return;
        Vector2 contactPoint = collision.contacts != null && collision.contacts.Length > 0
            ? collision.contacts[0].point
            : (Vector2)transform.position;
        HandleHit(collision.gameObject, contactPoint);
    }

    bool ShouldHit(GameObject other)
    {
        if (other == null) return false;
        if (other == gameObject) return false;
        if (((1 << other.layer) & hitLayers) == 0) return false;
        if (!string.IsNullOrEmpty(hitTag) && !other.CompareTag(hitTag)) return false;
        return true;
    }

    void HandleHit(GameObject other, Vector2 hitPoint)
    {
        if (impactEffectPrefab != null)
            Instantiate(impactEffectPrefab, hitPoint, Quaternion.identity);

        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
            damageable.TakeDamage(1);

        Destroy(gameObject);
    }
}

public interface IDamageable
{
    void TakeDamage(int amount);
}
