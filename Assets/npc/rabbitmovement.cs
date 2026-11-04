using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class RabbitWander : MonoBehaviour
{
    [Header("Wander Settings")]
    public float moveSpeed = 1f;
    public float wanderRadius = 3f;

    [Header("Isometric Settings")]
    public float isometricAngle = 0.5f;

    [Header("Animation Objects")]
    public GameObject rabbitRight;
    public GameObject rabbitLeft;
    public GameObject rabbitBack;

    [Header("Tilemap Restriction")]
    public Tilemap tilemap;                 // ✅ Assign your Tilemap in Inspector
    public List<TileBase> allowedTiles;     // ✅ Drag allowed tiles here

    private Rigidbody2D rb;
    private Vector2 startPosition;
    private Vector2 moveDirection;
    private GameObject currentMoveAnim = null;

    private enum NPCState { Moving, Idle }
    private NPCState currentState;

    private float stateTimer = 0f;

    private const float MOVE_DURATION = 1f;
    private const float IDLE_DURATION = 2f;

    // Stores last direction so idle animation keeps playing correct direction
    private GameObject lastDirectionAnim = null;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        startPosition = transform.position;

        HideAllAnimations();
        EnterMoving();
    }

    void Update()
    {
        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case NPCState.Moving:
                HandleMovementAnimation();
                if (stateTimer >= MOVE_DURATION)
                    EnterIdle();
                break;

            case NPCState.Idle:
                // ✅ Keep showing last direction animation while stopped
                if (lastDirectionAnim != null)
                    lastDirectionAnim.SetActive(true);

                if (stateTimer >= IDLE_DURATION)
                    EnterMoving();
                break;
        }
    }

    void FixedUpdate()
    {
        if (currentState == NPCState.Moving)
        {
            Vector2 nextPos = rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime;

            // ✅ Restrict wandering to allowed tiles
            Vector3Int gridPos = tilemap.WorldToCell(nextPos);
            TileBase tileAtNext = tilemap.GetTile(gridPos);

            if (tileAtNext != null && allowedTiles.Contains(tileAtNext))
            {
                // ✅ Also keep rabbit inside wander radius
                if (Vector2.Distance(nextPos, startPosition) > wanderRadius)
                    moveDirection = (startPosition - rb.position).normalized;

                Vector2 isoMove = new Vector2(
                    moveDirection.x,
                    moveDirection.y * isometricAngle
                );
                rb.linearVelocity = isoMove.normalized * moveSpeed;
            }
            else
            {
                // ❌ Block movement outside allowed tiles
                rb.linearVelocity = Vector2.zero;
                EnterIdle();
            }
        }
        else
        {
            // ✅ Stop movement but NOT animation
            rb.linearVelocity = Vector2.zero;
        }
    }

    void EnterMoving()
    {
        currentState = NPCState.Moving;
        stateTimer = 0f;
        currentMoveAnim = null;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        moveDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;

        HideAllAnimations();
    }

    void EnterIdle()
    {
        currentState = NPCState.Idle;
        stateTimer = 0f;
        moveDirection = Vector2.zero;
        // ✅ Don't hide animations here — keep last one playing
    }

    void HandleMovementAnimation()
    {
        GameObject targetAnim = null;

        if (moveDirection.y > 0.3f)
            targetAnim = rabbitBack;
        else if (moveDirection.x < 0)
            targetAnim = rabbitLeft;
        else if (moveDirection.x > 0)
            targetAnim = rabbitRight;
        else
            targetAnim = rabbitBack;

        if (targetAnim != currentMoveAnim)
        {
            HideAllAnimations();
            currentMoveAnim = targetAnim;
            lastDirectionAnim = targetAnim; // ✅ Remember last direction
            if (currentMoveAnim != null)
                currentMoveAnim.SetActive(true);
        }
    }

    void HideAllAnimations()
    {
        if (rabbitRight) rabbitRight.SetActive(false);
        if (rabbitLeft)  rabbitLeft.SetActive(false);
        if (rabbitBack)  rabbitBack.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            Application.isPlaying ? (Vector3)startPosition : transform.position,
            wanderRadius
        );
    }
}
