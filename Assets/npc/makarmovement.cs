using UnityEngine;

public class MakarNPC : MonoBehaviour
{
    // Assign these in the Inspector
    public GameObject makarUpR;
    public GameObject makarUpL;
    public GameObject makarDownR;
    public GameObject makarDownL;

    private Rigidbody2D rb;
    public float moveSpeed = 3f;
    public float changeDirectionTime = 2f;

    private float timer;
    private Vector2 moveDir;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        PickRandomDirection();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PickRandomDirection();
        }

        rb.linearVelocity = moveDir * moveSpeed;
    }

    void PickRandomDirection()
    {
        timer = changeDirectionTime;

        int rand = Random.Range(0, 4);

        // Disable all first
        makarUpR.SetActive(false);
        makarUpL.SetActive(false);
        makarDownR.SetActive(false);
        makarDownL.SetActive(false);

        switch (rand)
        {
            case 0: // Up Right (your "makar up R" = leftwards upward)
                moveDir = new Vector2(-1, 1).normalized;
                makarUpR.SetActive(true);
                break;

            case 1: // Up Left
                moveDir = new Vector2(1, 1).normalized;
                makarUpL.SetActive(true);
                break;

            case 2: // Down Right
                moveDir = new Vector2(-1, -1).normalized;
                makarDownR.SetActive(true);
                break;

            case 3: // Down Left
                moveDir = new Vector2(1, -1).normalized;
                makarDownL.SetActive(true);
                break;
        }
    }
}
