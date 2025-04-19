using UnityEngine;
using UnityEngine.SceneManagement;

public class WheelSpike : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Degrees per second to spin the wheel")]
    public float rotationSpeed = 180f;

    [Header("Movement Settings")]
    [Tooltip("Movement type: Horizontal, Vertical, or Both")]
    public MovementType movementType = MovementType.Horizontal;

    [Tooltip("Units per second to move")]
    public float moveSpeed = 2f;

    [Tooltip("How far (in world units) to move from the starting position")]
    public float patrolDistance = 3f;

    [Tooltip("Pause at endpoints (seconds)")]
    public float endpointPause = 0f;

    public enum MovementType
    {
        Horizontal,
        Vertical,
        Both
    }

    // Movement boundaries
    private float _leftX;
    private float _rightX;
    private float _bottomY;
    private float _topY;

    // Target position
    private Vector2 _target;

    // Sound Effects
    private ObjectSoundEffects sawSounds;

    // Starting position
    private Vector2 _startPosition;

    // Current movement direction
    private bool _movingPositive = true; // true = right/up, false = left/down

    // Timer for pausing at endpoints
    private float _pauseTimer = 0f;

    void Start()
    {
        // Init sound
        sawSounds = GetComponent<ObjectSoundEffects>();

        // Record initial position
        _startPosition = transform.position;

        // Set movement boundaries
        _leftX = _startPosition.x - patrolDistance;
        _rightX = _startPosition.x + patrolDistance;
        _bottomY = _startPosition.y - patrolDistance;
        _topY = _startPosition.y + patrolDistance;

        // Set initial target based on movement type
        SetInitialTarget();
    }

    void SetInitialTarget()
    {
        switch (movementType)
        {
            case MovementType.Horizontal:
                _target = new Vector2(_rightX, _startPosition.y);
                break;

            case MovementType.Vertical:
                _target = new Vector2(_startPosition.x, _topY);
                break;

            case MovementType.Both:
                // Start moving both right and up
                _target = new Vector2(_rightX, _topY);
                break;
        }
    }

    void Update()
    {
        // 1) Always rotate around Z axis
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // Check if we're paused at an endpoint
        if (_pauseTimer > 0)
        {
            _pauseTimer -= Time.deltaTime;
            return; // Skip movement while paused
        }

        // 2) Move toward the current target
        Vector2 pos = transform.position;
        pos = Vector2.MoveTowards(pos, _target, moveSpeed * Time.deltaTime);
        transform.position = pos;

        // 3) Check if we've reached the target
        if (Vector2.Distance(pos, _target) < 0.05f)
        {
            // Start pause timer if needed
            if (endpointPause > 0)
            {
                _pauseTimer = endpointPause;
            }

            // Set new target based on movement type
            UpdateTargetPosition();
        }

        // 4) Play ambient saw audio
        sawSounds.PlayAudio();
    }

    void UpdateTargetPosition()
    {
        switch (movementType)
        {
            case MovementType.Horizontal:
                // Toggle between left and right
                if (_target.x >= _rightX - 0.1f)
                {
                    _target.x = _leftX;
                }
                else
                {
                    _target.x = _rightX;
                }
                break;

            case MovementType.Vertical:
                // Toggle between top and bottom
                if (_target.y >= _topY - 0.1f)
                {
                    _target.y = _bottomY;
                }
                else
                {
                    _target.y = _topY;
                }
                break;

            case MovementType.Both:
                // Handle diagonal movement by toggling direction
                _movingPositive = !_movingPositive;

                // Set new target based on current direction
                if (_movingPositive)
                {
                    _target = new Vector2(_rightX, _topY);
                }
                else
                {
                    _target = new Vector2(_leftX, _bottomY);
                }
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // If the thing we hit is tagged "Player" (the robot) or "Package"
        if (other.CompareTag("Package"))
        {
            // Destroy that GameObject
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Player"))
        {
            RobotController player = other.GetComponent<RobotController>();
            if (player != null)
            {
                player.Die();
            }
        }
    }

    // Draw gizmos to show patrol path
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            // Draw in editor based on current transform
            Vector2 startPos = transform.position;

            Gizmos.color = Color.red;

            // Draw based on movement type
            switch (movementType)
            {
                case MovementType.Horizontal:
                    // Draw horizontal line
                    Gizmos.DrawLine(
                        new Vector3(startPos.x - patrolDistance, startPos.y, 0),
                        new Vector3(startPos.x + patrolDistance, startPos.y, 0)
                    );
                    break;

                case MovementType.Vertical:
                    // Draw vertical line
                    Gizmos.DrawLine(
                        new Vector3(startPos.x, startPos.y - patrolDistance, 0),
                        new Vector3(startPos.x, startPos.y + patrolDistance, 0)
                    );
                    break;

                case MovementType.Both:
                    // Draw rectangle for diagonal movement
                    Gizmos.DrawLine(
                        new Vector3(startPos.x - patrolDistance, startPos.y - patrolDistance, 0),
                        new Vector3(startPos.x + patrolDistance, startPos.y - patrolDistance, 0)
                    );
                    Gizmos.DrawLine(
                        new Vector3(startPos.x + patrolDistance, startPos.y - patrolDistance, 0),
                        new Vector3(startPos.x + patrolDistance, startPos.y + patrolDistance, 0)
                    );
                    Gizmos.DrawLine(
                        new Vector3(startPos.x + patrolDistance, startPos.y + patrolDistance, 0),
                        new Vector3(startPos.x - patrolDistance, startPos.y + patrolDistance, 0)
                    );
                    Gizmos.DrawLine(
                        new Vector3(startPos.x - patrolDistance, startPos.y + patrolDistance, 0),
                        new Vector3(startPos.x - patrolDistance, startPos.y - patrolDistance, 0)
                    );

                    // Draw diagonal lines
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(
                        new Vector3(startPos.x - patrolDistance, startPos.y - patrolDistance, 0),
                        new Vector3(startPos.x + patrolDistance, startPos.y + patrolDistance, 0)
                    );
                    Gizmos.DrawLine(
                        new Vector3(startPos.x - patrolDistance, startPos.y + patrolDistance, 0),
                        new Vector3(startPos.x + patrolDistance, startPos.y - patrolDistance, 0)
                    );
                    break;
            }
        }
        else
        {
            // Draw during runtime with actual patrol points
            Gizmos.color = Color.red;

            switch (movementType)
            {
                case MovementType.Horizontal:
                    Gizmos.DrawLine(new Vector3(_leftX, _startPosition.y, 0), new Vector3(_rightX, _startPosition.y, 0));
                    break;

                case MovementType.Vertical:
                    Gizmos.DrawLine(new Vector3(_startPosition.x, _bottomY, 0), new Vector3(_startPosition.x, _topY, 0));
                    break;

                case MovementType.Both:
                    // Draw full rectangle
                    Gizmos.DrawLine(new Vector3(_leftX, _bottomY, 0), new Vector3(_rightX, _bottomY, 0));
                    Gizmos.DrawLine(new Vector3(_rightX, _bottomY, 0), new Vector3(_rightX, _topY, 0));
                    Gizmos.DrawLine(new Vector3(_rightX, _topY, 0), new Vector3(_leftX, _topY, 0));
                    Gizmos.DrawLine(new Vector3(_leftX, _topY, 0), new Vector3(_leftX, _bottomY, 0));

                    // Draw diagonals
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(new Vector3(_leftX, _bottomY, 0), new Vector3(_rightX, _topY, 0));
                    Gizmos.DrawLine(new Vector3(_leftX, _topY, 0), new Vector3(_rightX, _bottomY, 0));
                    break;
            }
        }

        // Draw the current target position
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(new Vector3(_target.x, _target.y, 0), 0.2f);
        }
    }
}