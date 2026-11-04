using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MrHeckBehaviour : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float diagonalSpeedMultiplier = 0.7071f;
    public float detectionRadius = 5f;

    [Header("Patrol Area (rectangle)")]
    public Transform patrolAreaCenter;
    public Vector2 patrolAreaHalfExtents = new Vector2(6f, 6f);
    public float patrolPointTolerance = 0.12f;
    public float patrolWaitTime = 0f;
    public float minPatrolDistance = 0.5f;

    [Header("Protagonist and Bullet Detection")]
    public Transform protagonist;                       // assign Sovushka transform
    public Transform protagonistBulletReference = null; // optional direct bullet reference
    public string protagonistBulletChildName = "bullet";// fallback child name under protagonist
    public LayerMask bulletLayerMask;                   // assign Bullet layer(s)
    public float bulletDetectRadius = 1f;               // 1 tile radius
    public bool destroyBulletOnHit = true;

    [Header("Respawn / Ship")]
    public Transform protagonistRespawnPoint;           // assign ship spawn Transform
    public float respawnAvoidRadius = 5f;
    public float respawnSafeDistance = 1.5f;

    [Header("Eats / Spawn Animations (assign child GameObjects)")]
    public GameObject heckSpawning;
    public GameObject heckEats;
    public GameObject heckEatsL;

    [Header("Movement Animations (assign child GameObjects)")]
    public GameObject heckFrontR;
    public GameObject heckFrontL;
    public GameObject heckBackR;
    public GameObject heckBackL;

    [Header("Shot Animations (assign child GameObjects)")]
    public GameObject heckShotL;
    public GameObject heckShotR;

    [Header("Animation / timing fallback")]
    public float defaultClipLength = 0.9f;

    [Header("Respawn / Vanish")]
    public float vanishToSpawnDelay = 15f;

    [Header("Eat effect")]
    public float protagonistDisappearDuration = 15f;

    // internal
    private GameObject currentAnim = null;
    private Vector3 currentPatrolTarget;
    private Coroutine patrolCoroutine = null;
    private Coroutine hitCoroutine = null;
    private Coroutine eatCoroutine = null;
    private Coroutine respawnCoroutine = null;
    private bool isChasing = false;
    private bool isStunned = false;
    private bool isVanished = false;

    private Rigidbody2D mrRb;

    // False while Sovushka is hidden (waiting for Yanam to spawn her, or already being eaten):
    // she must not be chased or "eaten" then, otherwise the eat-then-respawn logic reveals her early.
    private SovushkaRespawner _protRespawner;
    private bool ProtagonistActive
    {
        get
        {
            if (protagonist == null || !protagonist.gameObject.activeInHierarchy) return false;
            if (_protRespawner == null) _protRespawner = protagonist.GetComponent<SovushkaRespawner>();
            return _protRespawner == null || !_protRespawner.IsHidden;
        }
    }

    void Awake()
    {
        if (patrolAreaCenter == null) patrolAreaCenter = transform;
        mrRb = GetComponent<Rigidbody2D>();
        if (mrRb != null)
        {
            // Use Dynamic so Unity generates collision callbacks reliably.
            // Keep gravityScale = 0 and freezeRotation so movement remains scripted.
            mrRb.bodyType = RigidbodyType2D.Dynamic;
            mrRb.simulated = true;
            mrRb.linearVelocity = Vector2.zero;
            mrRb.angularVelocity = 0f;
            mrRb.freezeRotation = true;
            mrRb.gravityScale = 0f;
        }
    }

    void Start()
    {
        DeactivateAllChildren();
        ChooseNewPatrolTarget();
        patrolCoroutine = StartCoroutine(ContinuousRandomPatrol());
    }

    void Update()
    {
        if (isStunned || isVanished)
        {
            if (mrRb != null) mrRb.linearVelocity = Vector2.zero;
            return;
        }

        // Bullet detection
        Transform bullet = FindNearestBulletWithinRadius();
        if (bullet != null)
        {
            if (hitCoroutine == null)
                hitCoroutine = StartCoroutine(HandleShotThenVanish(bullet));
            return;
        }

        // Eat detection (proximity)
        if (ProtagonistActive && Vector3.Distance(transform.position, protagonist.position) <= bulletDetectRadius)
        {
            if (eatCoroutine == null)
                eatCoroutine = StartCoroutine(HandleEatReaction());
            return;
        }

        // Chase vs patrol
        if (ProtagonistActive && Vector3.Distance(transform.position, protagonist.position) <= detectionRadius)
        {
            if (!isChasing)
            {
                isChasing = true;
                if (patrolCoroutine != null) { StopCoroutine(patrolCoroutine); patrolCoroutine = null; }
            }
            ChaseProtagonist();
        }
        else
        {
            if (isChasing)
            {
                isChasing = false;
                if (patrolCoroutine == null && !isVanished) patrolCoroutine = StartCoroutine(ContinuousRandomPatrol());
            }
        }

        // Update animation from current linear velocity
        UpdateAnimationFromVelocity();
    }

    // Collision handlers - reflect/change direction on hit
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isVanished) return;

        Vector2 normal = Vector2.zero;
        if (collision.contacts != null && collision.contacts.Length > 0)
            normal = collision.contacts[0].normal;

        HandleCollisionDirectionChange(normal, collision.collider);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (isVanished) return;
        if (IsBulletCollider(other)) return;

        Vector2 normal = (transform.position - other.transform.position).normalized;
        HandleCollisionDirectionChange(normal, other);
    }

    private void HandleCollisionDirectionChange(Vector2 collisionNormal, Collider2D other)
    {
        if (mrRb == null) return;

        Vector2 currentVel = mrRb.linearVelocity;
        float speed = currentVel.magnitude;

        if (speed < 0.01f)
        {
            // If nearly stationary, pick a direction away from the collider
            Vector2 away = (transform.position - other.transform.position).normalized;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle.normalized;
            mrRb.linearVelocity = away * moveSpeed;
        }
        else
        {
            Vector2 normal = collisionNormal;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = (transform.position - other.transform.position).normalized;
                if (normal.sqrMagnitude < 0.0001f) normal = Random.insideUnitCircle.normalized;
            }

            // Reflect current velocity across the collision normal
            Vector2 newDir = Vector2.Reflect(currentVel.normalized, normal).normalized;

            // Add slight jitter to avoid perfect oscillation
            newDir += Random.insideUnitCircle * 0.05f;
            newDir.Normalize();

            mrRb.linearVelocity = newDir * Mathf.Max(speed, moveSpeed);
        }

        // Move patrol target away from collision to reduce immediate re-collision
        currentPatrolTarget = ClampPointToPatrolArea(transform.position + (Vector3)(mrRb.linearVelocity.normalized * (minPatrolDistance + 1f)));
        if (patrolCoroutine != null) { StopCoroutine(patrolCoroutine); patrolCoroutine = null; }
        patrolCoroutine = StartCoroutine(ContinuousRandomPatrol());
    }

    private void UpdateAnimationFromVelocity()
    {
        if (mrRb == null) return;
        Vector2 vel = mrRb.linearVelocity;
        if (vel.sqrMagnitude < 0.0001f) return;
        Vector3 moveDir = new Vector3(vel.x, vel.y, 0f).normalized;
        ChooseDiagonalAnimation(moveDir);
    }

    private void HandleImmediateAvoidance(Vector2 collisionNormal)
    {
        Vector3 center = patrolAreaCenter != null ? patrolAreaCenter.position : transform.position;
        Vector3 newTarget;
        int attempts = 0;
        do
        {
            float rx = Random.Range(-patrolAreaHalfExtents.x, patrolAreaHalfExtents.x);
            float ry = Random.Range(-patrolAreaHalfExtents.y, patrolAreaHalfExtents.y);
            newTarget = new Vector3(center.x + rx, center.y + ry, transform.position.z);
            attempts++;
        } while ((Vector3.Distance(transform.position, newTarget) < minPatrolDistance) && attempts < 12);

        currentPatrolTarget = ClampPointToPatrolArea(newTarget);
        if (patrolCoroutine != null) { StopCoroutine(patrolCoroutine); patrolCoroutine = null; }
        StartCoroutine(MoveToImmediateTarget(currentPatrolTarget));
    }

    private IEnumerator MoveToImmediateTarget(Vector3 target)
    {
        while (!isVanished && Vector3.Distance(transform.position, target) > patrolPointTolerance)
        {
            if (ProtagonistActive && Vector3.Distance(transform.position, protagonist.position) <= detectionRadius) yield break;
            Vector3 dir = (target - transform.position); dir.z = 0f;
            Vector3 moveDir = dir.normalized;
            float speed = moveSpeed;
            if (Mathf.Abs(moveDir.x) > 0.01f && Mathf.Abs(moveDir.y) > 0.01f) speed *= diagonalSpeedMultiplier;

            if (mrRb != null) mrRb.linearVelocity = (Vector2)(moveDir * speed);
            ChooseDiagonalAnimation(moveDir);
            yield return null;
        }

        if (mrRb != null) mrRb.linearVelocity = Vector2.zero;

        if (!isChasing && !isVanished && patrolCoroutine == null) patrolCoroutine = StartCoroutine(ContinuousRandomPatrol());
    }

    // Patrol
    private IEnumerator ContinuousRandomPatrol()
    {
        while (!isVanished)
        {
            Vector3 center = patrolAreaCenter != null ? patrolAreaCenter.position : transform.position;
            Vector3 candidate;
            int attempts = 0;
            do
            {
                float rx = Random.Range(-patrolAreaHalfExtents.x, patrolAreaHalfExtents.x);
                float ry = Random.Range(-patrolAreaHalfExtents.y, patrolAreaHalfExtents.y);
                candidate = new Vector3(center.x + rx, center.y + ry, transform.position.z);
                attempts++;
            } while (Vector3.Distance(transform.position, candidate) < minPatrolDistance && attempts < 8);

            currentPatrolTarget = ClampPointToPatrolArea(candidate);

            while (!isVanished && Vector3.Distance(transform.position, currentPatrolTarget) > patrolPointTolerance)
            {
                if (ProtagonistActive && Vector3.Distance(transform.position, protagonist.position) <= detectionRadius) yield break;
                Vector3 dir = (currentPatrolTarget - transform.position); dir.z = 0f;
                Vector3 moveDir = dir.normalized;
                float speed = moveSpeed;
                if (Mathf.Abs(moveDir.x) > 0.01f && Mathf.Abs(moveDir.y) > 0.01f) speed *= diagonalSpeedMultiplier;

                if (mrRb != null) mrRb.linearVelocity = (Vector2)(moveDir * speed);
                ChooseDiagonalAnimation(moveDir);
                yield return null;
            }

            if (mrRb != null) mrRb.linearVelocity = Vector2.zero;

            if (patrolWaitTime > 0f) yield return new WaitForSeconds(patrolWaitTime);
            else yield return null;
        }
    }

    private Vector3 ClampPointToPatrolArea(Vector3 point)
    {
        Vector3 center = patrolAreaCenter != null ? patrolAreaCenter.position : transform.position;
        float minX = center.x - patrolAreaHalfExtents.x;
        float maxX = center.x + patrolAreaHalfExtents.x;
        float minY = center.y - patrolAreaHalfExtents.y;
        float maxY = center.y + patrolAreaHalfExtents.y;
        point.x = Mathf.Clamp(point.x, minX, maxX);
        point.y = Mathf.Clamp(point.y, minY, maxY);
        point.z = transform.position.z;
        return point;
    }

    // Chase
    private void ChaseProtagonist()
    {
        if (protagonist == null || isStunned || isVanished) return;
        Vector3 dir = protagonist.position - transform.position; dir.z = 0f;
        Vector3 moveDir = dir.normalized;
        float speed = moveSpeed;
        if (Mathf.Abs(moveDir.x) > 0.01f && Mathf.Abs(moveDir.y) > 0.01f) speed *= diagonalSpeedMultiplier;

        if (mrRb != null) mrRb.linearVelocity = (Vector2)(moveDir * speed);
        ChooseDiagonalAnimation(moveDir);
    }

    private void ChooseDiagonalAnimation(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return;
        bool up = moveDir.y > 0f;
        bool right = moveDir.x > 0f;
        if (up && right) PlayExclusive(heckBackR);
        else if (up && !right) PlayExclusive(heckBackL);
        else if (!up && right) PlayExclusive(heckFrontR);
        else PlayExclusive(heckFrontL);
    }

    // Bullet detection
    private Transform FindNearestBulletWithinRadius()
    {
        Transform nearest = null;
        float best = float.MaxValue;

        if (protagonistBulletReference != null)
        {
            float d = Vector3.Distance(transform.position, protagonistBulletReference.position);
            if (d <= bulletDetectRadius) return protagonistBulletReference;
        }

        if (protagonist != null)
        {
            Transform child = protagonist.Find(protagonistBulletChildName);
            if (child != null)
            {
                float d = Vector3.Distance(transform.position, child.position);
                if (d <= bulletDetectRadius) return child;
            }

            for (int i = 0; i < protagonist.childCount; i++)
            {
                Transform c = protagonist.GetChild(i);
                if (c == null) continue;
                string lname = c.name.ToLower();
                if (lname.Contains("bullet") || lname.Contains("shot"))
                {
                    float d = Vector3.Distance(transform.position, c.position);
                    if (d <= bulletDetectRadius && d < best)
                    {
                        best = d;
                        nearest = c;
                    }
                }
            }
            if (nearest != null) return nearest;
        }

        if (bulletLayerMask != 0)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, bulletDetectRadius, bulletLayerMask);
            if (hits != null && hits.Length > 0)
            {
                Collider2D nearestCol = null;
                float bestD = float.MaxValue;
                foreach (var h in hits)
                {
                    if (h == null) continue;
                    float d = Vector2.Distance(transform.position, h.transform.position);
                    if (d < bestD)
                    {
                        bestD = d;
                        nearestCol = h;
                    }
                }
                if (nearestCol != null) return nearestCol.transform;
            }
        }

        return null;
    }

    private bool IsBulletCollider(Collider2D col)
    {
        if (col == null) return false;
        if (protagonistBulletReference != null && col.transform == protagonistBulletReference) return true;
        if (protagonist != null && col.transform.IsChildOf(protagonist)) return col.name.ToLower().Contains("bullet") || col.name.ToLower().Contains("shot");
        if (bulletLayerMask != 0) return ((1 << col.gameObject.layer) & bulletLayerMask) != 0;
        return false;
    }

    // Shot reaction -> vanish -> respawn
    private IEnumerator HandleShotThenVanish(Transform bullet)
    {
        if (bullet == null) { hitCoroutine = null; yield break; }

        isStunned = true;
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.mrHeckShot : null);
        bool shotFromLeft = bullet.position.x < transform.position.x;
        GameObject shotAnim = shotFromLeft ? heckShotL : heckShotR;

        if (shotAnim != null) yield return StartCoroutine(PlayAnimationAndHold(shotAnim));
        else yield return new WaitForSeconds(0.12f);

        if (destroyBulletOnHit && bullet != null) { try { Destroy(bullet.gameObject); } catch { } }

        VanishAndScheduleRespawn();
        hitCoroutine = null;
    }

    private void VanishAndScheduleRespawn()
    {
        isVanished = true;
        isStunned = false;
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.mrHeckVanish : null);
        if (patrolCoroutine != null) { StopCoroutine(patrolCoroutine); patrolCoroutine = null; }
        DeactivateAllChildren();
        if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
        respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, vanishToSpawnDelay));
        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.mrHeckRespawn : null);
        if (heckSpawning != null) { yield return StartCoroutine(PlayAnimationAndHold(heckSpawning)); SafeSetActive(heckSpawning, false); }
        isVanished = false;
        isStunned = false;
        if (patrolCoroutine == null) patrolCoroutine = StartCoroutine(ContinuousRandomPatrol());
        respawnCoroutine = null;
    }

    // Eat reaction
    private IEnumerator HandleEatReaction()
    {
        if (protagonist == null) { eatCoroutine = null; yield break; }

        SovushkaRespawner respawner = protagonist.GetComponent<SovushkaRespawner>();
        GameObject protGO = protagonist.gameObject;

        if (respawner != null)
        {
            Debug.Log("[MrHeck] Hiding Sovushka via SovushkaRespawner (hide-only)");
            respawner.HideVisualsAndControlsPublic();
        }
        else
        {
            Debug.Log("[MrHeck] Hiding Sovushka (fallback)");
            var rends = protGO.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends) r.enabled = false;
            var cols = protGO.GetComponentsInChildren<Collider2D>(true);
            foreach (var c in cols) c.enabled = false;
            var behaviours = protGO.GetComponentsInChildren<Behaviour>();
            foreach (var b in behaviours) { if (b == null) continue; if (b != this) b.enabled = false; }
            Rigidbody2D protRb = protGO.GetComponent<Rigidbody2D>();
            if (protRb != null) { protRb.linearVelocity = Vector2.zero; protRb.bodyType = RigidbodyType2D.Kinematic; }
            StartCoroutine(RespawnProtagonistCoroutine(protGO, protagonistDisappearDuration));
        }

        Vector3 dir = protagonist.position - transform.position; dir.z = 0f;
        GameObject eatAnim = ChooseEatAnimation(dir);

        PlayOneShotSound(GameSoundLibrary.Instance != null ? GameSoundLibrary.Instance.mrHeckEat : null);
        if (eatAnim != null)
        {
            Debug.Log("[MrHeck] Playing eat animation");
            isStunned = true;
            if (mrRb != null) mrRb.linearVelocity = Vector2.zero;
            yield return StartCoroutine(PlayAnimationAndHold(eatAnim));
            isStunned = false;
            Debug.Log("[MrHeck] Eat animation finished, resuming");
        }
        else
        {
            yield return new WaitForSeconds(0.12f);
        }

        if (respawner != null)
        {
            respawner.SpawnNow(respawner.respawnPoint != null ? (Vector3?)respawner.respawnPoint.position : ComputeRespawnPositionForProtagonist());
        }

        eatCoroutine = null;
    }

    private IEnumerator RespawnProtagonistCoroutine(GameObject protGO, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        if (protGO == null) yield break;
        Vector3 respawnPos = ComputeRespawnPositionForProtagonist();
        var rends = protGO.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) r.enabled = true;
        var cols = protGO.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols) c.enabled = true;
        var behaviours = protGO.GetComponentsInChildren<Behaviour>();
        foreach (var b in behaviours) { if (b == null) continue; b.enabled = true; }
        Rigidbody2D protRb = protGO.GetComponent<Rigidbody2D>();
        if (protRb != null) { protRb.bodyType = RigidbodyType2D.Dynamic; protRb.linearVelocity = Vector2.zero; protRb.angularVelocity = 0f; }
        protGO.transform.position = respawnPos;
        Debug.Log("[MrHeck] Sovushka respawned at " + respawnPos);
    }

    private Vector3 ComputeRespawnPositionForProtagonist()
    {
        Vector3 respawnPos;
        if (protagonistRespawnPoint != null)
        {
            respawnPos = protagonistRespawnPoint.position;
            float distToShip = Vector3.Distance(transform.position, respawnPos);
            if (distToShip <= respawnAvoidRadius)
            {
                Vector3 away = (respawnPos - transform.position);
                if (away.sqrMagnitude < 0.0001f) away = Vector3.right;
                away = away.normalized;
                respawnPos = transform.position + away * (respawnAvoidRadius + 0.25f);
            }
            respawnPos = ClampPointToPatrolArea(respawnPos);
        }
        else respawnPos = ComputeSafeRespawnPosition();
        return respawnPos;
    }

    private Vector3 ComputeSafeRespawnPosition()
    {
        Vector3 origin = transform.position;
        float d = Mathf.Max(0.01f, respawnSafeDistance);
        Vector3[] offsets = new Vector3[] {
            new Vector3(d,0,0), new Vector3(d,d,0), new Vector3(0,d,0), new Vector3(-d,d,0),
            new Vector3(-d,0,0), new Vector3(-d,-d,0), new Vector3(0,-d,0), new Vector3(d,-d,0)
        };
        Vector3 center = patrolAreaCenter != null ? patrolAreaCenter.position : transform.position;
        float minX = center.x - patrolAreaHalfExtents.x;
        float maxX = center.x + patrolAreaHalfExtents.x;
        float minY = center.y - patrolAreaHalfExtents.y;
        float maxY = center.y + patrolAreaHalfExtents.y;
        float safeRadius = Mathf.Max(bulletDetectRadius, respawnSafeDistance);
        foreach (var off in offsets)
        {
            Vector3 cand = origin + off;
            cand.x = Mathf.Clamp(cand.x, minX, maxX);
            cand.y = Mathf.Clamp(cand.y, minY, maxY);
            cand.z = protagonist != null ? protagonist.position.z : transform.position.z;
            if (Vector3.Distance(cand, transform.position) >= safeRadius + 0.01f) return cand;
        }
        Vector3 fallback = origin + new Vector3(d, 0f, 0f);
        fallback.x = Mathf.Clamp(fallback.x, minX, maxX);
        fallback.y = Mathf.Clamp(fallback.y, minY, maxY);
        fallback.z = protagonist != null ? protagonist.position.z : transform.position.z;
        return fallback;
    }

    // Animation helpers
    private void PlayExclusive(GameObject anim)
    {
        if (currentAnim == anim) return;
        SafeSetActive(heckFrontR, false);
        SafeSetActive(heckFrontL, false);
        SafeSetActive(heckBackR, false);
        SafeSetActive(heckBackL, false);
        SafeSetActive(heckShotL, false);
        SafeSetActive(heckShotR, false);
        SafeSetActive(heckEats, false);
        SafeSetActive(heckEatsL, false);
        SafeSetActive(heckSpawning, false);
        ResetAnimatorSpeed(currentAnim);
        currentAnim = anim;
        if (currentAnim != null) currentAnim.SetActive(true);
    }

    private void DeactivateAllChildren()
    {
        SafeSetActive(heckFrontR, false);
        SafeSetActive(heckFrontL, false);
        SafeSetActive(heckBackR, false);
        SafeSetActive(heckBackL, false);
        SafeSetActive(heckShotL, false);
        SafeSetActive(heckShotR, false);
        SafeSetActive(heckEats, false);
        SafeSetActive(heckEatsL, false);
        SafeSetActive(heckSpawning, false);
        currentAnim = null;
    }

    private void SafeSetActive(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf == active) return;
        go.SetActive(active);
    }

    private void ResetAnimatorSpeed(GameObject go)
    {
        if (go == null) return;
        Animator a = go.GetComponent<Animator>();
        if (a != null) a.speed = 1f;
    }

    private void PlayOneShotSound(AudioClip clip)
    {
        if (clip == null) return;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(clip);
    }

    private IEnumerator PlayAnimationAndHold(GameObject anim)
    {
        if (anim == null) yield break;
        PlayExclusive(anim);
        Animator a = anim.GetComponent<Animator>();
        float clipLength = defaultClipLength;
        if (a != null && a.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = a.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                clipLength = clips[0].length;
                string clipName = clips[0].name;
                a.Rebind(); // restart the controller's default state (state names can differ from clip names, e.g. "mr_ heck spawn" vs "mr. heck spawn")
                a.Update(0f);
                a.speed = 1f;
            }
        }
        yield return new WaitForSeconds(Mathf.Max(0.01f, clipLength));
        DeactivateAllChildren();
        yield break;
    }

    private GameObject ChooseEatAnimation(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return heckEats;
        if (dir.x >= 0f) return heckEats;
        return heckEatsL;
    }

    // Utility: choose a new random patrol target inside the patrol area
    private void ChooseNewPatrolTarget()
    {
        Vector3 center = patrolAreaCenter != null ? patrolAreaCenter.position : transform.position;
        float rx = Random.Range(-patrolAreaHalfExtents.x, patrolAreaHalfExtents.x);
        float ry = Random.Range(-patrolAreaHalfExtents.y, patrolAreaHalfExtents.y);
        Vector3 candidate = new Vector3(center.x + rx, center.y + ry, transform.position.z);
        currentPatrolTarget = ClampPointToPatrolArea(candidate);
    }

    // Debug gizmos
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bulletDetectRadius);
        if (patrolAreaCenter != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 c = patrolAreaCenter.position;
            Vector3 size = new Vector3(patrolAreaHalfExtents.x * 2f, patrolAreaHalfExtents.y * 2f, 0.01f);
            Gizmos.DrawWireCube(c, size);
        }
        if (protagonistRespawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(protagonistRespawnPoint.position, respawnAvoidRadius);
        }
    }
}
