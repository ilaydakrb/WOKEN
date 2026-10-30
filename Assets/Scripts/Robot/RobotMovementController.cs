using System.Collections.Generic;
using UnityEngine;
using Woken.Gameplay;

namespace Woken.Characters
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(CharacterAnimator))]
    public sealed class RobotMovementController : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private float moveSpeed = 3.4f;
        [SerializeField] private float stopTolerance = 0.1f;

        [Header("Route Settings")]
        [SerializeField] private float routeUpdateInterval = 0.3f;
        [SerializeField] private float waypointTolerance = 0.15f;
        [SerializeField] private float minimumTargetMovement = 0.4f;

        private Rigidbody2D body;
        private Transform player;
        private CharacterAnimator animator;

        private List<Vector2> currentPath = new List<Vector2>();
        private int currentWaypoint;
        private float routeUpdateTimer;
        private Vector2 lastRouteTarget;
        private bool hasRoute;

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
                body.collisionDetectionMode =
                    CollisionDetectionMode2D.Continuous;
            }
        }

        public void SetPlayer(Transform playerTransform)
        {
            player = playerTransform;

            currentPath = new List<Vector2>();
            currentWaypoint = 0;
            routeUpdateTimer = 0f;
            hasRoute = false;
        }

        private void FixedUpdate()
        {
            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (animator == null)
                animator = GetComponent<CharacterAnimator>();

            if (body == null || animator == null || player == null)
            {
                StopMovement();
                return;
            }

            Vector2 robotPosition = body.position;
            Vector2 playerPosition = player.position;

            float distance = Vector2.Distance(
                robotPosition,
                playerPosition
            );

            float lowerDistance = followDistance - stopTolerance;
            float upperDistance = followDistance + stopTolerance;

            // Oyuncu uzaktaysa ona doğru rota oluştur.
            if (distance > upperDistance)
            {
                UpdateAndFollowRoute(robotPosition, playerPosition);
                return;
            }

            // Oyuncu fazla yaklaşırsa geri çekil.
            if (distance < lowerDistance)
            {
                Vector2 awayDirection = robotPosition - playerPosition;

                if (awayDirection.sqrMagnitude < 0.001f)
                    awayDirection = Vector2.right;

                awayDirection.Normalize();

                Vector2 retreatTarget =
                    playerPosition + awayDirection * followDistance;

                UpdateAndFollowRoute(robotPosition, retreatTarget);
                return;
            }

            // İstenen mesafedeyse dur.
            StopMovement();
            hasRoute = false;

            if (currentPath == null)
                currentPath = new List<Vector2>();
            else
                currentPath.Clear();
        }

        private void UpdateAndFollowRoute(
            Vector2 robotPosition,
            Vector2 targetPosition)
        {
            routeUpdateTimer -= Time.fixedDeltaTime;

            bool targetMoved =
                Vector2.Distance(targetPosition, lastRouteTarget)
                >= minimumTargetMovement;

            if (!hasRoute || routeUpdateTimer <= 0f || targetMoved)
            {
                List<Vector2> newPath = GridRoutePlanner.FindPath(
                    robotPosition,
                    targetPosition
                );

                currentPath = newPath ?? new List<Vector2>();

                currentWaypoint =
                    currentPath.Count > 1 ? 1 : 0;

                lastRouteTarget = targetPosition;
                routeUpdateTimer = routeUpdateInterval;

                hasRoute = currentPath.Count > 0;

                if (!hasRoute)
                {
                    // Rota bulunamazsa robot durur.
                    StopMovement();
                }
            }

            FollowRoute();
        }

        private void FollowRoute()
        {
            if (body == null ||
                animator == null ||
                !hasRoute ||
                currentPath == null ||
                currentPath.Count == 0)
            {
                StopMovement();
                return;
            }

            Vector2 currentPosition = body.position;

            // Ulaşılan rota noktalarını geç.
            while (currentWaypoint < currentPath.Count - 1 &&
                   Vector2.Distance(
                       currentPosition,
                       currentPath[currentWaypoint]
                   ) <= waypointTolerance)
            {
                currentWaypoint++;
            }

            Vector2 waypoint = currentPath[currentWaypoint];
            Vector2 direction = waypoint - currentPosition;

            if (direction.sqrMagnitude <=
                waypointTolerance * waypointTolerance)
            {
                StopMovement();
                return;
            }

            Vector2 movement = direction.normalized;

            body.MovePosition(
                currentPosition +
                movement * moveSpeed * Time.fixedDeltaTime
            );

            animator.SetMotion(movement);
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