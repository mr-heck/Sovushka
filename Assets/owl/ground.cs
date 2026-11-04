using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TileRestrictedMovement : MonoBehaviour
{
    public Tilemap tilemap;                      // Assign your Tilemap in Inspector
    public List<TileBase> allowedTiles;          // Drag multiple allowed tiles here
    public float moveSpeed = 5f;

    private Vector3 targetPosition;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W)) TryMove(Vector3Int.up);
        if (Input.GetKeyDown(KeyCode.S)) TryMove(Vector3Int.down);
        if (Input.GetKeyDown(KeyCode.A)) TryMove(Vector3Int.left);
        if (Input.GetKeyDown(KeyCode.D)) TryMove(Vector3Int.right);

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    }

    void TryMove(Vector3Int direction)
    {
        Vector3Int newGridPos = tilemap.WorldToCell(transform.position) + direction;
        TileBase tileAtNewPos = tilemap.GetTile(newGridPos);

        if (allowedTiles.Contains(tileAtNewPos))   // ✅ Check against multiple allowed tiles
        {
            targetPosition = tilemap.GetCellCenterWorld(newGridPos);
        }
        else
        {
            Debug.Log("Blocked! Cannot move on this tile.");
        }
    }
}
