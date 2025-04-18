using UnityEngine;
using UnityEngine.SceneManagement;

public class WheelSpike : MonoBehaviour
{
    [Header("Rotation & Movement")]
    [Tooltip("Degrees per second to spin the wheel")]
    public float rotationSpeed = 180f;
    [Tooltip("Units per second to move horizontally")]
    public float moveSpeed     = 2f;
    [Tooltip("How far (in world units) to move left and right from the start")]
    public float patrolDistance = 3f;

    private float _leftX;
    private float _rightX;
    private Vector2 _target;

    void Start()
    {
        // Record initial X, set left/right bounds
        float startX = transform.position.x;
        _leftX  = startX - patrolDistance;
        _rightX = startX + patrolDistance;

        // Start heading right
        _target = new Vector2(_rightX, transform.position.y);
    }

    void Update()
    {
        // 1) Rotate around Z axis
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // 2) Move toward the current horizontal target
        Vector2 pos = transform.position;
        pos = Vector2.MoveTowards(pos, _target, moveSpeed * Time.deltaTime);
        transform.position = pos;

        // 3) If we’ve reached (or passed) the target X, flip direction
        if (Mathf.Abs(pos.x - _target.x) < 0.05f)
        {
            if (_target.x == _rightX)
                _target.x = _leftX;
            else
                _target.x = _rightX;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If the thing we hit is tagged "Player" (the robot) or "Package"
        if (other.CompareTag("Package"))
        {
            
            // Destroy that GameObject
            Destroy(other.gameObject);
        }else if(other.CompareTag("Player")){
            RobotController player = other.GetComponent<RobotController>();
            if (player != null)
            {
                player.Die();
            }
        }
        
    }
}
