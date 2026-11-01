using System.Collections.Generic;
using UnityEngine;

namespace Woken.Gameplay
{
    public sealed class RouteOption
    {
        public readonly List<Vector2> Points;
        public readonly float Distance;
        public readonly int Turns;
        public readonly int EstimatedSeconds;

        public RouteOption(List<Vector2> points, float moveSpeed)
        {
            Points = points;
            Distance = Measure(points);
            Turns = CountTurns(points);

            EstimatedSeconds = Mathf.Max(
                1,
                Mathf.RoundToInt(Distance / Mathf.Max(moveSpeed, 0.01f) + Turns * 0.75f)
            );
        }

        private static float Measure(List<Vector2> points)
        {
            float distance = 0f;

            for (int i = 1; i < points.Count; i++)
            {
                distance += Vector2.Distance(points[i - 1], points[i]);
            }

            return distance;
        }

        private static int CountTurns(List<Vector2> points)
        {
            int turns = 0;

            for (int i = 2; i < points.Count; i++)
            {
                Vector2 before = points[i - 1] - points[i - 2];
                Vector2 after = points[i] - points[i - 1];

                if (Mathf.Abs(
                    Vector2.Dot(before.normalized, after.normalized)
                ) < 0.5f)
                {
                    turns++;
                }
            }

            return turns;
        }
    }

    /// <summary>
    /// Finds routes across the current 2D level.
    /// </summary>
    public static class GridRoutePlanner
    {
        // Daha hassas rota hesaplaması için hücre boyutunu küçülttük.
        private const float CellSize = 0.25f;

        private const float MinX = -13.75f;
        private const float MinY = -7.75f;

        private const int Columns = 112;
        private const int Rows = 64;

        // Hücre merkezinden engel kontrolü yapılırken
        // karakterin yaklaşık çarpışma yarıçapını hesaba kat.
        private const float ObstacleCheckRadius = 0.27f;

        private static readonly Vector2Int[] Neighbours =
        {
            Vector2Int.right,
            Vector2Int.left,
            Vector2Int.up,
            Vector2Int.down
        };

        public static List<RouteOption> FindTwoRoutes(
            Vector2 start,
            Vector2 goal,
            float moveSpeed)
        {
            bool[,] blocked = SampleObstacles();

            Vector2Int startCell = CellOf(start);
            Vector2Int goalCell = CellOf(goal);

            blocked[startCell.x, startCell.y] = false;
            blocked[goalCell.x, goalCell.y] = false;

            List<Vector2> shortest = Search(
                blocked,
                startCell,
                goalCell,
                null
            );

            var result = new List<RouteOption>(2);

            if (shortest == null)
                return result;

            result.Add(new RouteOption(shortest, moveSpeed));

            List<Vector2> alternative = Search(
                blocked,
                startCell,
                goalCell,
                shortest
            );

            if (alternative != null &&
                !SamePath(shortest, alternative))
            {
                var option = new RouteOption(alternative, moveSpeed);

                if (option.Distance < result[0].Distance)
                {
                    result.Insert(0, option);
                    result.Add(new RouteOption(shortest, moveSpeed));
                }
                else
                {
                    result.Add(option);
                }
            }

            return result;
        }

        public static List<Vector2> FindPath(
            Vector2 start,
            Vector2 goal)
        {
            bool[,] blocked = SampleObstacles();

            Vector2Int startCell = CellOf(start);
            Vector2Int goalCell = CellOf(goal);

            blocked[startCell.x, startCell.y] = false;
            blocked[goalCell.x, goalCell.y] = false;

            return Search(
                blocked,
                startCell,
                goalCell,
                null
            );
        }

        private static bool[,] SampleObstacles()
        {
            var blocked = new bool[Columns, Rows];

            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    Vector2 point = WorldPoint(new Vector2Int(x, y));

                    Collider2D[] colliders =
                        Physics2D.OverlapCircleAll(
                            point,
                            ObstacleCheckRadius
                        );

                    for (int i = 0; i < colliders.Length; i++)
                    {
                        Collider2D hit = colliders[i];

                        if (hit == null || !hit.enabled || hit.isTrigger)
                            continue;

                        // Oyuncunun ve robotun kendi çarpışma alanlarını
                        // engel olarak değerlendirme.
                        if (IsCharacterCollider(hit))
                            continue;

                        blocked[x, y] = true;
                        break;
                    }
                }
            }

            return blocked;
        }

        private static bool IsCharacterCollider(Collider2D hit)
        {
            Transform current = hit.transform;

            while (current != null)
            {
                if (current.name == "Player" ||
                    current.name == "Companion Robot")
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static List<Vector2> Search(
            bool[,] blocked,
            Vector2Int start,
            Vector2Int goal,
            List<Vector2> avoid)
        {
            bool[,] closed = new bool[Columns, Rows];

            var cameFrom = new Vector2Int[Columns, Rows];
            var costs = new float[Columns, Rows];
            var open = new List<Vector2Int> { start };

            bool[,] detourPenalty = avoid == null
                ? null
                : BuildDetourPenalty(avoid);

            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    costs[x, y] = float.PositiveInfinity;
                    cameFrom[x, y] = new Vector2Int(-1, -1);
                }
            }

            costs[start.x, start.y] = 0f;

            while (open.Count > 0)
            {
                int bestIndex = 0;
                float bestEstimate = float.PositiveInfinity;

                for (int i = 0; i < open.Count; i++)
                {
                    Vector2Int candidate = open[i];

                    float heuristic =
                        Mathf.Abs(candidate.x - goal.x) +
                        Mathf.Abs(candidate.y - goal.y);

                    float estimate =
                        costs[candidate.x, candidate.y] + heuristic;

                    if (estimate < bestEstimate)
                    {
                        bestEstimate = estimate;
                        bestIndex = i;
                    }
                }

                Vector2Int current = open[bestIndex];
                open.RemoveAt(bestIndex);

                if (current == goal)
                {
                    return Reconstruct(cameFrom, start, goal);
                }

                closed[current.x, current.y] = true;

                for (int i = 0; i < Neighbours.Length; i++)
                {
                    Vector2Int next = current + Neighbours[i];

                    if (next.x < 0 || next.x >= Columns ||
                        next.y < 0 || next.y >= Rows)
                    {
                        continue;
                    }

                    if (blocked[next.x, next.y] ||
                        closed[next.x, next.y])
                    {
                        continue;
                    }

                    float stepCost =
                        detourPenalty != null &&
                        detourPenalty[next.x, next.y]
                            ? 4f
                            : 1f;

                    float newCost =
                        costs[current.x, current.y] + stepCost;

                    if (newCost >= costs[next.x, next.y])
                        continue;

                    cameFrom[next.x, next.y] = current;
                    costs[next.x, next.y] = newCost;

                    if (!open.Contains(next))
                        open.Add(next);
                }
            }

            return null;
        }

        private static bool[,] BuildDetourPenalty(List<Vector2> path)
        {
            var penalty = new bool[Columns, Rows];

            for (int x = 0; x < Columns; x++)
            {
                for (int y = 0; y < Rows; y++)
                {
                    Vector2 point = WorldPoint(new Vector2Int(x, y));

                    for (int i = 1; i < path.Count - 1; i++)
                    {
                        if ((point - path[i]).sqrMagnitude < 0.64f)
                        {
                            penalty[x, y] = true;
                            break;
                        }
                    }
                }
            }

            return penalty;
        }

        private static List<Vector2> Reconstruct(
            Vector2Int[,] cameFrom,
            Vector2Int start,
            Vector2Int goal)
        {
            var cells = new List<Vector2Int> { goal };
            Vector2Int current = goal;

            while (current != start)
            {
                current = cameFrom[current.x, current.y];

                if (current.x < 0)
                    return null;

                cells.Add(current);
            }

            cells.Reverse();

            var points = new List<Vector2>(cells.Count);

            for (int i = 0; i < cells.Count; i++)
            {
                points.Add(WorldPoint(cells[i]));
            }

            return points;
        }

        private static bool SamePath(
            List<Vector2> a,
            List<Vector2> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if ((a[i] - b[i]).sqrMagnitude > 0.001f)
                    return false;
            }

            return true;
        }

        private static Vector2Int CellOf(Vector2 point)
        {
            return new Vector2Int(
                Mathf.Clamp(
                    Mathf.RoundToInt((point.x - MinX) / CellSize),
                    0,
                    Columns - 1
                ),
                Mathf.Clamp(
                    Mathf.RoundToInt((point.y - MinY) / CellSize),
                    0,
                    Rows - 1
                )
            );
        }

        private static Vector2 WorldPoint(Vector2Int cell)
        {
            return new Vector2(
                MinX + cell.x * CellSize,
                MinY + cell.y * CellSize
            );
        }
    }
}
