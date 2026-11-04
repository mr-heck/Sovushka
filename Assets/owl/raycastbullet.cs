using System;
using UnityEngine;

/// <summary>
/// Purely visual projectile for Sovushka's raycast shots. SovushkaShooter has already worked out
/// (with a raycast) where the shot ends and what it hits; this just flies the bullet sprite there
/// and calls onArrive so the hit lands exactly when the bullet does.
/// </summary>
[DisallowMultipleComponent]
public class RaycastBullet : MonoBehaviour
{
    [Tooltip("Safety net: destroy the bullet after this many seconds even if it never arrives.")]
    public float maxLifetime = 3f;

    Vector3 _end;
    float _speed = 18f;
    Action _onArrive;
    bool _launched;
    float _age;

    public void Launch(Vector3 endPoint, float speed, Action onArrive)
    {
        _end = new Vector3(endPoint.x, endPoint.y, transform.position.z);
        _speed = Mathf.Max(0.1f, speed);
        _onArrive = onArrive;
        _launched = true;
    }

    void Update()
    {
        if (!_launched) return;

        _age += Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, _end, _speed * Time.deltaTime);

        if ((transform.position - _end).sqrMagnitude <= 0.0004f || _age >= maxLifetime)
        {
            _launched = false;
            var cb = _onArrive;
            _onArrive = null;
            Destroy(gameObject);
            cb?.Invoke();
        }
    }
}
