using System;
using UnityEngine;
using UnityEngine.AI;
using Woken.Gameplay;

namespace Woken.Characters
{
    public enum RobotTaskResult
    {
        Completed,
        Cancelled,
        NoRoute
    }

    public interface IRobotMovementTasks
    {
        event Action<RobotTaskResult> TaskFinished;
        bool IsExecutingTask { get; }
        bool StartMoveTask(Vector2 worldTarget, bool resumeFollowing = true);
        void HoldPosition();
        void ResumeFollowingPlayer();
        void CancelTask();
        void SetMovementEnabled(bool isEnabled);
    }
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(CharacterAnimator))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RobotMovementController : MonoBehaviour, IRobotMovementTasks
    {
        [Header("Follow Settings")]
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private float moveSpeed = 4f;
        [SerializeField] private float stopTolerance = 0.1f;

        [Header("Task Settings")]
        [SerializeField] private float taskArrivalTolerance = 0.3f;
        [SerializeField] private float navMeshSampleDistance = 2f;
        [SerializeField] private float navMeshAttachDistance = 0.45f;

        [Header("Navigation Settings")]
        [SerializeField] private float routeUpdateInterval = 0.3f;
        [SerializeField] private float minimumTargetMovement = 0.4f;

        [Header("Obstacle Sensor Settings")]
        [SerializeField] private float obstacleSensorDistance = 0.9f;
        [SerializeField] private float obstacleSensorOriginOffset = 0.3f;
        [SerializeField, Range(0f, 45f)] private float obstacleSensorSpreadAngle = 25f;
        [SerializeField] private float obstacleSensorRepathDistance = 0.5f;
        [SerializeField] private float obstacleSensorRepathCooldown = 0.75f;
        [SerializeField] private LayerMask obstacleSensorLayers = Physics2D.DefaultRaycastLayers;
        [SerializeField] private bool drawObstacleSensorRays = true;

        private enum MovementMode
        {
            FollowPlayer,
            MoveToTaskTarget,
            StayAtDestination
        }

        private Rigidbody2D body;
        private Transform player;
        private CharacterAnimator animator;
        private NavMeshAgent navigationAgent;
        private const int MaxObstacleSensorHits = 16;
        private readonly RaycastHit2D[] obstacleSensorHits = new RaycastHit2D[MaxObstacleSensorHits];
        private ContactFilter2D obstacleSensorFilter;
        private float nextObstacleRepathTime;
        private MovementMode mode = MovementMode.FollowPlayer;
        private Vector3 taskTarget;
        private Vector3 currentDestination;
        private float routeUpdateTimer;
        private bool hasDestination;
        private bool resumeFollowingAfterTask = true;

        /// <summary>Raised when a move task completes, is cancelled, or has no route.</summary>
        public event Action<RobotTaskResult> TaskFinished;

        public bool IsExecutingTask => mode == MovementMode.MoveToTaskTarget;
        public float MoveSpeed => moveSpeed;
        public bool HasObstacleAhead { get; private set; }
        public float FrontObstacleDistance { get; private set; } = float.PositiveInfinity;
        public float NearestObstacleDistance { get; private set; } = float.PositiveInfinity;
        public float NearestObstacleAngle { get; private set; }
        public Collider2D NearestObstacleCollider { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<CharacterAnimator>();
            navigationAgent = GetComponent<NavMeshAgent>();
            obstacleSensorFilter = new ContactFilter2D();
            obstacleSensorFilter.useTriggers = false;
            obstacleSensorFilter.SetLayerMask(obstacleSensorLayers);

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (navigationAgent != null)
            {
                navigationAgent.updatePosition = false;
                navigationAgent.updateRotation = false;
                navigationAgent.updateUpAxis = false;
                navigationAgent.speed = moveSpeed;
                navigationAgent.acceleration = moveSpeed * 8f;
                navigationAgent.angularSpeed = 0f;
                navigationAgent.radius = 0.21f;
                navigationAgent.height = 1f;
                navigationAgent.stoppingDistance = 0.05f;
                int notWalkableArea = NavMesh.GetAreaFromName("Not Walkable");
                if (notWalkableArea >= 0)
                    navigationAgent.areaMask = NavMesh.AllAreas & ~(1 << notWalkableArea);
            }
        }

        public void SetMovementEnabled(bool isEnabled) => enabled = isEnabled;

        public void SetPlayer(Transform playerTransform)
        {
            player = playerTransform;
            ClearDestination();

            // NavMesh örneklemesi spawn noktasını birkaç santim kaydırabilir.
            // Takip sistemi ilk karede robotu başka bir rotaya çekmesin; başlangıç
            // aralığını gerçek spawn konumuna göre ayarla.
            if (player != null && body != null)
                followDistance = Vector2.Distance(body.position, player.position);
        }

        /// <summary>
        /// Sends the robot to a world position using the baked 2D NavMesh.
        /// Returns false and keeps follow mode if no complete route exists.
        /// </summary>
        public bool StartMoveTask(Vector2 worldTarget, bool resumeFollowing = true)
        {
            resumeFollowingAfterTask = resumeFollowing;
            if (body == null || navigationAgent == null || !TryPlaceAgentOnNavMesh())
                return FailToStartTask();

            if (!TrySetDestination(worldTarget))
                return FailToStartTask();

            mode = MovementMode.MoveToTaskTarget;
            taskTarget = currentDestination;
            routeUpdateTimer = 0f;
            return true;
        }

        /// <summary>Stops the robot where it is and keeps it there until another task starts.</summary>
        public void HoldPosition()
        {
            mode = MovementMode.StayAtDestination;
            resumeFollowingAfterTask = false;
            StopNavigation();
            StopMovement();
        }

        /// <summary>Resumes following the player after a route arrival pause.</summary>
        public void ResumeFollowingPlayer()
        {
            resumeFollowingAfterTask = true;
            mode = MovementMode.FollowPlayer;
            ClearDestination();
            StopMovement();
        }

        /// <summary>Cancels the current task and resumes following the player.</summary>
        public void CancelTask()
        {
            if (mode != MovementMode.MoveToTaskTarget)
                return;

            mode = MovementMode.FollowPlayer;
            StopNavigation();
            TaskFinished?.Invoke(RobotTaskResult.Cancelled);
        }

        private void FixedUpdate()
        {
            if (body == null || animator == null || navigationAgent == null)
            {
                StopMovement();
                return;
            }

            if (!TryPlaceAgentOnNavMesh())
            {
                StopMovement();
                if (mode == MovementMode.MoveToTaskTarget)
                    FinishTask(RobotTaskResult.NoRoute);
                return;
            }

            if (mode == MovementMode.MoveToTaskTarget)
            {
                UpdateTask();
                if (mode != MovementMode.MoveToTaskTarget)
                {
                    if (mode == MovementMode.StayAtDestination)
                        StopMovement();
                    return;
                }
            }
            else if (mode == MovementMode.FollowPlayer)
            {
                UpdateFollow();
            }
            else
            {
                StopMovement();
                return;
            }

            MoveBodyWithAgent();
        }

        private void UpdateFollow()
        {
            if (player == null)
            {
                StopNavigation();
                StopMovement();
                return;
            }

            Vector2 robotPosition = body.position;
            Vector2 playerPosition = player.position;
            float distance = Vector2.Distance(robotPosition, playerPosition);
            float lowerDistance = followDistance - stopTolerance;
            float upperDistance = followDistance + stopTolerance;

            if (distance >= lowerDistance && distance <= upperDistance)
            {
                StopNavigation();
                StopMovement();
                return;
            }

            Vector2 awayDirection = robotPosition - playerPosition;
            if (awayDirection.sqrMagnitude < 0.001f)
                awayDirection = Vector2.right;

            Vector2 desiredPosition = playerPosition + awayDirection.normalized * followDistance;
            routeUpdateTimer -= Time.fixedDeltaTime;

            bool targetMoved = !hasDestination ||
                Vector2.Distance(desiredPosition, currentDestination) >= minimumTargetMovement;

            if (targetMoved || routeUpdateTimer <= 0f)
            {
                routeUpdateTimer = routeUpdateInterval;
                if (!TrySetDestination(desiredPosition))
                {
                    hasDestination = false;
                    StopNavigation();
                    StopMovement();
                    return;
                }
            }
        }

        private void UpdateTask()
        {
            if (navigationAgent.pathPending)
                return;

            float distanceToTarget = Vector2.Distance(
                body.position,
                new Vector2(taskTarget.x, taskTarget.y)
            );

            // The agent may clear its path as soon as it reaches the destination.
            // Check arrival before treating an empty path as a routing failure.
            if (distanceToTarget <= taskArrivalTolerance &&
                (!navigationAgent.hasPath || navigationAgent.velocity.sqrMagnitude <= 0.04f))
            {
                FinishTask(RobotTaskResult.Completed);
                return;
            }

            if (!navigationAgent.hasPath ||
                navigationAgent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                FinishTask(RobotTaskResult.NoRoute);
            }
        }
        private bool TrySetDestination(Vector2 requestedPosition)
        {
            if (!TryPlaceAgentOnNavMesh())
                return false;

            Vector3 samplePoint = new Vector3(
                requestedPosition.x,
                requestedPosition.y,
                transform.position.z
            );

            if (!NavMesh.SamplePosition(
                    samplePoint,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    navigationAgent.areaMask))
            {
                return false;
            }

            var path = new NavMeshPath();
            if (!navigationAgent.CalculatePath(hit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete ||
                !navigationAgent.SetPath(path))
            {
                return false;
            }

            navigationAgent.isStopped = false;
            currentDestination = hit.position;
            hasDestination = true;
            return true;
        }

        private bool TryPlaceAgentOnNavMesh()
        {
            if (navigationAgent == null || !navigationAgent.enabled)
                return false;

            if (navigationAgent.isOnNavMesh)
                return true;

            Vector3 position = body != null ? body.position : transform.position;
            if (!NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    navMeshAttachDistance,
                    navigationAgent.areaMask))
            {
                return false;
            }

            if (body != null)
                body.position = hit.position;

            transform.position = hit.position;
            return navigationAgent.Warp(hit.position);
        }

        private void MoveBodyWithAgent()
        {
            if (navigationAgent.isStopped || !hasDestination)
            {
                StopMovement();
                return;
            }

            Vector3 nextPosition = navigationAgent.nextPosition;
            Vector2 next2D = new Vector2(nextPosition.x, nextPosition.y);
            Vector2 movement = next2D - body.position;

            if (movement.sqrMagnitude <= 0.0001f)
            {
                StopMovement();
                return;
            }

            Vector2 movementDirection = movement.normalized;
            ObserveObstacles(movementDirection);
            RefreshPathForCloseObstacle();

            body.MovePosition(next2D);
            animator.SetMotion(movementDirection);
        }

        private void ObserveObstacles(Vector2 movementDirection)
        {
            HasObstacleAhead = false;
            FrontObstacleDistance = float.PositiveInfinity;
            NearestObstacleDistance = float.PositiveInfinity;
            NearestObstacleAngle = 0f;
            NearestObstacleCollider = null;

            if (movementDirection.sqrMagnitude <= 0.0001f)
                return;

            movementDirection.Normalize();
            Vector2 origin = body.position + movementDirection * obstacleSensorOriginOffset;
            float halfAngle = obstacleSensorSpreadAngle * 0.5f;

            CastObstacleRay(origin, movementDirection, movementDirection, true);
            CastObstacleRay(origin, RotateDirection(movementDirection, -halfAngle), movementDirection, false);
            CastObstacleRay(origin, RotateDirection(movementDirection, halfAngle), movementDirection, false);
        }

        private void CastObstacleRay(
            Vector2 origin,
            Vector2 rayDirection,
            Vector2 movementDirection,
            bool isFrontRay)
        {
            int hitCount = Physics2D.Raycast(
                origin,
                rayDirection,
                obstacleSensorFilter,
                obstacleSensorHits,
                obstacleSensorDistance);

            bool rayHitObstacle = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = obstacleSensorHits[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.isTrigger ||
                    hitCollider.attachedRigidbody == body)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;
                if (player != null &&
                    (hitTransform == player || hitTransform.IsChildOf(player)))
                {
                    continue;
                }

                rayHitObstacle = true;
                float distanceFromRobot = obstacleSensorOriginOffset + hit.distance;
                if (isFrontRay && distanceFromRobot < FrontObstacleDistance)
                    FrontObstacleDistance = distanceFromRobot;

                if (distanceFromRobot < NearestObstacleDistance)
                {
                    NearestObstacleDistance = distanceFromRobot;
                    NearestObstacleCollider = hitCollider;
                    Vector2 toObstacle = hit.point - body.position;
                    NearestObstacleAngle = toObstacle.sqrMagnitude > 0.0001f
                        ? Vector2.SignedAngle(movementDirection, toObstacle.normalized)
                        : Vector2.SignedAngle(movementDirection, rayDirection);
                }
            }

            if (isFrontRay)
                HasObstacleAhead = rayHitObstacle;

            if (drawObstacleSensorRays)
            {
                Color rayColor = rayHitObstacle
                    ? new Color(1f, 0.25f, 0.2f, 1f)
                    : new Color(0.2f, 0.85f, 0.45f, 1f);
                Debug.DrawRay(origin, rayDirection * obstacleSensorDistance, rayColor, Time.fixedDeltaTime);
            }
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

        private void RefreshPathForCloseObstacle()
        {
            if (!HasObstacleAhead ||
                FrontObstacleDistance > obstacleSensorRepathDistance ||
                !hasDestination ||
                Time.fixedTime < nextObstacleRepathTime)
            {
                return;
            }

            // NavMesh remains the route planner. The sensor only refreshes
            // the existing goal when a close obstacle is directly ahead.
            Vector2 destination = mode == MovementMode.MoveToTaskTarget
                ? new Vector2(taskTarget.x, taskTarget.y)
                : new Vector2(currentDestination.x, currentDestination.y);

            TrySetDestination(destination);
            nextObstacleRepathTime = Time.fixedTime + obstacleSensorRepathCooldown;
        }
        private bool FailToStartTask()
        {
            mode = resumeFollowingAfterTask ? MovementMode.FollowPlayer : MovementMode.StayAtDestination;
            StopNavigation();
            TaskFinished?.Invoke(RobotTaskResult.NoRoute);
            return false;
        }

        private void FinishTask(RobotTaskResult result)
        {
            mode = resumeFollowingAfterTask ? MovementMode.FollowPlayer : MovementMode.StayAtDestination;
            resumeFollowingAfterTask = true;
            StopNavigation();
            StopMovement();
            TaskFinished?.Invoke(result);
        }
        private void StopNavigation()
        {
            if (navigationAgent != null && navigationAgent.enabled && navigationAgent.isOnNavMesh)
            {
                navigationAgent.isStopped = true;
                navigationAgent.ResetPath();
            }

            hasDestination = false;
            routeUpdateTimer = 0f;
        }

        private void ClearDestination()
        {
            StopNavigation();
        }

        private void StopMovement()
        {
            if (body != null)
                body.linearVelocity = Vector2.zero;

            if (animator != null)
                animator.SetMotion(Vector2.zero);
        }
    }
}
