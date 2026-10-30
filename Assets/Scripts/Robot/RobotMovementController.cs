using System;
using System.Collections.Generic;
using UnityEngine;
using Woken.Gameplay;

namespace Woken.Characters
{
    public enum RobotTaskResult
    {
        Completed,
        Cancelled,
        NoRoute
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(CharacterAnimator))]
    public sealed class RobotMovementController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private float moveSpeed = 3.4f;
        [SerializeField] private float stopTolerance = 0.1f;

        [Header("Task Settings")]
        [SerializeField] private float taskArrivalTolerance = 0.3f;

        [Header("Route Settings")]
        [SerializeField] private float routeUpdateInterval = 0.3f;
        [SerializeField] private float waypointTolerance = 0.15f;
        [SerializeField] private float minimumTargetMovement = 0.4f;

        private enum MovementMode
        {
            FollowPlayer,
            MoveToTaskTarget
        }

        private Rigidbody2D body;
        private Transform player;
        private CharacterAnimator animator;
        private MovementMode mode = MovementMode.FollowPlayer;
        private Vector2 taskTarget;

        private List<Vector2> currentPath = new List<Vector2>();
        private int currentWaypoint;
        private float routeUpdateTimer;
        private Vector2 lastRouteTarget;
        private bool hasRoute;

        /// <summary>Raised when a move task completes, is cancelled, or has no route.</summary>
        public event Action<RobotTaskResult> TaskFinished;

        public bool IsExecutingTask => mode == MovementMode.MoveToTaskTarget;
        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<CharacterAnimator>();

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
        }

        public void SetPlayer(Transform playerTransform)
        {
            player = playerTransform;
            ResetRoute();
        }

        /// <summary>
        /// Sends the robot to a world position. Returns false and keeps follow mode
        /// if the route planner cannot find a route.
        /// </summary>
        public bool StartMoveTask(Vector2 worldTarget)
        {
            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (body == null)
                return false;

            List<Vector2> path = GridRoutePlanner.FindPath(body.position, worldTarget);
            if (path == null || path.Count == 0)
            {
                StopMovement();
                TaskFinished?.Invoke(RobotTaskResult.NoRoute);
                return false;
            }

            taskTarget = worldTarget;
            mode = MovementMode.MoveToTaskTarget;
            SetRoute(path, worldTarget);
            return true;
        }

        /// <summary>Cancels the current task and resumes following the player.</summary>
        public void CancelTask()
        {
            if (mode != MovementMode.MoveToTaskTarget)
                return;

            mode = MovementMode.FollowPlayer;
            ResetRoute();
            StopMovement();
            TaskFinished?.Invoke(RobotTaskResult.Cancelled);
        }

private void FixedUpdate()
        {
            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (animator == null)
                animator = GetComponent<CharacterAnimator>();

            if (body == null || animator == null)
            {
                StopMovement();
                return;
            }

            if (mode == MovementMode.MoveToTaskTarget)
            {
                MoveToTaskTarget();
                return;
            }

            FollowPlayer();
        }

        private void MoveToTaskTarget()
        {
            if (currentPath == null || currentPath.Count == 0)
            {
                FinishTask(RobotTaskResult.NoRoute);
                return;
            }

            Vector2 finalPathPoint = currentPath[currentPath.Count - 1];
            if (Vector2.Distance(body.position, finalPathPoint) <= taskArrivalTolerance)
            {
                FinishTask(RobotTaskResult.Completed);
                return;
            }

            routeUpdateTimer -= Time.fixedDeltaTime;
            if (routeUpdateTimer <= 0f)
            {
                List<Vector2> newPath = GridRoutePlanner.FindPath(body.position, taskTarget);
                if (newPath == null || newPath.Count == 0)
                {
                    FinishTask(RobotTaskResult.NoRoute);
                    return;
                }

                SetRoute(newPath, taskTarget);
            }

            FollowRoute();
        }

        private void FollowPlayer()
        {
            if (player == null)
            {
                StopMovement();
                return;
            }

            Vector2 robotPosition = body.position;
            Vector2 playerPosition = player.position;
            float distance = Vector2.Distance(robotPosition, playerPosition);
            float lowerDistance = followDistance - stopTolerance;
            float upperDistance = followDistance + stopTolerance;

            if (distance > upperDistance)
            {
                UpdateAndFollowRoute(robotPosition, playerPosition);
                return;
            }

            if (distance < lowerDistance)
            {
                Vector2 awayDirection = robotPosition - playerPosition;
                if (awayDirection.sqrMagnitude < 0.001f)
                    awayDirection = Vector2.right;

                Vector2 retreatTarget = playerPosition + awayDirection.normalized * followDistance;
                UpdateAndFollowRoute(robotPosition, retreatTarget);
                return;
            }

            StopMovement();
            ResetRoute();
        }

        private void UpdateAndFollowRoute(Vector2 robotPosition, Vector2 targetPosition)
        {
            routeUpdateTimer -= Time.fixedDeltaTime;

            bool targetMoved = Vector2.Distance(targetPosition, lastRouteTarget) >= minimumTargetMovement;
            if (!hasRoute || routeUpdateTimer <= 0f || targetMoved)
            {
                List<Vector2> newPath = GridRoutePlanner.FindPath(robotPosition, targetPosition);
                if (newPath == null || newPath.Count == 0)
                {
                    currentPath.Clear();
                    hasRoute = false;
                    StopMovement();
                    return;
                }

                SetRoute(newPath, targetPosition);
            }

            FollowRoute();
        }

        private void SetRoute(List<Vector2> path, Vector2 target)
        {
            currentPath = path;
            currentWaypoint = currentPath.Count > 1 ? 1 : 0;
            lastRouteTarget = target;
            routeUpdateTimer = routeUpdateInterval;
            hasRoute = currentPath.Count > 0;
        }

        private void FollowRoute()
        {
            if (body == null || animator == null || !hasRoute || currentPath == null || currentPath.Count == 0)
            {
                StopMovement();
                return;
            }

            Vector2 currentPosition = body.position;
            while (currentWaypoint < currentPath.Count - 1 &&
                   Vector2.Distance(currentPosition, currentPath[currentWaypoint]) <= waypointTolerance)
            {
                currentWaypoint++;
            }

            Vector2 direction = currentPath[currentWaypoint] - currentPosition;
            if (direction.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                StopMovement();
                return;
            }

            Vector2 movement = direction.normalized;
            body.MovePosition(currentPosition + movement * moveSpeed * Time.fixedDeltaTime);
            animator.SetMotion(movement);
        }

        private void FinishTask(RobotTaskResult result)
        {
            mode = MovementMode.FollowPlayer;
            ResetRoute();
            StopMovement();
            TaskFinished?.Invoke(result);
        }

        private void ResetRoute()
        {
            currentPath.Clear();
            currentWaypoint = 0;
            routeUpdateTimer = 0f;
            hasRoute = false;
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
