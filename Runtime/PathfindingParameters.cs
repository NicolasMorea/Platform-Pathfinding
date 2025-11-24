using Entity.Enemy;
using UnityEngine;

namespace Entity.Pathfinding
{
    [System.Serializable]
    public class PathfindingParameters
    {
        public EnemyData enemyData;
        [Min(3)] public int JumpConnectionMaxRange;
        [Min(2)] public int NumberOfTestedTrajectories;
        [Min(1)] public int NumberOfVelocitiesTested;
        [Min(0)] public float trajectoriesCheckInterval;
        [Min(0)] public int SimilarEdgesRemovalRange;
        [Min(1f)] public float jumpCostMultiplier;
        [Min(0f)] public float fallCostMultiplier;
        [Min(0f)] public float colliderSizeBuffer;
        public Vector2Int TrajectoriesBoxCheckSize;
        public PathfindingParameters Clone()
        {
            PathfindingParameters clone = new PathfindingParameters();
            clone.enemyData = enemyData;
            clone.JumpConnectionMaxRange = JumpConnectionMaxRange;
            clone.NumberOfTestedTrajectories = NumberOfTestedTrajectories;
            clone.SimilarEdgesRemovalRange = SimilarEdgesRemovalRange;
            clone.jumpCostMultiplier = jumpCostMultiplier;
            clone.fallCostMultiplier = fallCostMultiplier;
            clone.colliderSizeBuffer = colliderSizeBuffer;
            clone.TrajectoriesBoxCheckSize = TrajectoriesBoxCheckSize;
            clone.trajectoriesCheckInterval = trajectoriesCheckInterval;
            clone.NumberOfVelocitiesTested = NumberOfVelocitiesTested;
            return clone;
        }
    }
}