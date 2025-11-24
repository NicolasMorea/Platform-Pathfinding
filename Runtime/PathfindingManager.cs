/// <summary>
///* The class used to generate the pathfinding graph
///TODO Needs a lot of refactoring, it runs only in the editor so it does not need to be optimized for performance, but the code needs to be cleaned up for readability
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Gravity;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Entity.Pathfinding
{
    public class PathfindingManager : MonoBehaviour
    {
#if UNITY_EDITOR
        #region Variables
        [Header("Needed components")]
        [SerializeField] private Tilemap wallsTileMap;
        [SerializeField] private Tilemap gravityTileMap;
        [SerializeField] private PathfindingGraph graph;

        [Header("Pathfinding parameters")]
        public PathfindingParameters parameters;

        [Header("Main Debug")]
        [SerializeField] private bool DrawGizmos = false;
        [SerializeField] private bool DrawEdges = false;
        [Tooltip("gizmos trajectory draw details amount"), SerializeField, Min(1f)] private float trajectorySimplifications = 10f;

        [Header("Fails Debug")]
        [SerializeField] private bool drawFailJumpTrajectory = false;
        [SerializeField, Range(0f, 1f)] private float ShowedTrajectoryHeight = 0f;
        [SerializeField] private bool drawFailFallTrajectories = false;
        [SerializeField] private bool drawFailMultiGravTrajectories = false;
        [SerializeField] private bool drawRemovedTrajectories = false;

        [Header("Entity Debug")]
        [SerializeField] private bool drawEnemyBoxes = false;
        [SerializeField] private Vector2Int EnemyDrawPosition;
        [HideInInspector] public bool buttonVerif = false;
        private bool hasBeenLoaded = false;
        private BoundsInt bounds;
        private Pathfinding.Tile[,] grid;
        #endregion

        #region debugForGizmos
        private List<Pathfinding.Tile> agentSizeTileDebug;
        private List<Pathfinding.Tile> agentGroundTilesDebug;
        private List<List<Vector3>> collidingPointsListDebug = new List<List<Vector3>>();
        private List<Vector3> cornerDebug = new List<Vector3>();
        private List<Pathfinding.Tile> trnsitionDebug = new List<Pathfinding.Tile>();
        private List<List<Waypoint>> JumpTrajectoriesDebug = new List<List<Waypoint>>();
        private List<Waypoint> FallTrajectoriesDebug = new List<Waypoint>();
        private List<Waypoint> MultiGravTrajectoriesDebug = new List<Waypoint>();
        private List<Waypoint> MultiGravTrajectoriesValidDebug = new List<Waypoint>();
        private List<Waypoint> RemovedTrajectoriesDebug = new List<Waypoint>();
        private List<Vector3> debugCollisionPoints = new List<Vector3>();
        private List<Vector3> fitsPointsDebug = new List<Vector3>();

        #endregion

        #region GeneralFunctions
        public void TryInitGraph()
        {
            if (CheckParameters())
            {
                Clear();
                hasBeenLoaded = false;
                CreateGraph();
            }
            else
            {
                Debug.Log("Can't create graph, parameters are not valid");
            }
        }

        public bool GraphExists()
        {
            if (graph != null && graph.tiles != null && graph.tiles.Count > 0)
            {
                return true;
            }
            return false;
        }

        public void Clear()
        {
            grid = null;
            graph.tiles = new List<Pathfinding.Tile>();
            graph.numberOfEdges = 0;
            JumpTrajectoriesDebug = new List<List<Waypoint>>();
            for (int i = 0; i < parameters.NumberOfTestedTrajectories; i++)
            {
                JumpTrajectoriesDebug.Add(new List<Waypoint>());
            }
            collidingPointsListDebug = new List<List<Vector3>>();
            for (int i = 0; i < parameters.NumberOfTestedTrajectories; i++)
            {
                collidingPointsListDebug.Add(new List<Vector3>());
            }
            cornerDebug = new List<Vector3>();
            trnsitionDebug = new List<Pathfinding.Tile>();

            FallTrajectoriesDebug = new List<Waypoint>();
            MultiGravTrajectoriesDebug = new List<Waypoint>();
            MultiGravTrajectoriesValidDebug = new List<Waypoint>();
            RemovedTrajectoriesDebug = new List<Waypoint>();
            debugCollisionPoints = new List<Vector3>();
            fitsPointsDebug = new List<Vector3>();
        }

        public void SetDefault()
        {
            parameters.JumpConnectionMaxRange = 8;
            parameters.NumberOfTestedTrajectories = 3;
            parameters.SimilarEdgesRemovalRange = 3;
            parameters.jumpCostMultiplier = 20f;
            parameters.fallCostMultiplier = 4f;
            parameters.colliderSizeBuffer = 0.2f;
            parameters.TrajectoriesBoxCheckSize = new Vector2Int(7, 20);
            parameters.trajectoriesCheckInterval = 0.002f;
            trajectorySimplifications = 10f;
            parameters.NumberOfVelocitiesTested = 5;
        }

        void OnValidate()
        {
            if (gravityTileMap != null)
            {
                var bounds = gravityTileMap.cellBounds;
                if (EnemyDrawPosition.x < 0)
                {
                    EnemyDrawPosition.x = 0;
                }
                if (EnemyDrawPosition.x > bounds.max.x - bounds.min.x - 1)
                {
                    EnemyDrawPosition.x = bounds.max.x - bounds.min.x - 1;
                }
                if (EnemyDrawPosition.y < 0)
                {
                    EnemyDrawPosition.y = 0;
                }
                if (EnemyDrawPosition.y > bounds.max.y - bounds.min.y - 1)
                {
                    EnemyDrawPosition.y = bounds.max.y - bounds.min.y - 1;
                }

                bounds = gravityTileMap.cellBounds;
            }
        }

        public void CreateGraph()
        {
            bounds = gravityTileMap.cellBounds;

            InitGrid();
            Debug.Log("grid initialized");

            FillWalkableTiles();
            Debug.Log("walkable tiles created");

            CreateFallingConnections();
            Debug.Log("falling connections created");

            CreateJumpingConnections();
            Debug.Log("jumping connections created");

            CreateInterGravityConnections();
            Debug.Log("inter-gravity connections created");

            int removed = 0;
            removed += RemoveUnnecessaryConnections();

            MoveToList();

            SetScriptableObjectParameters();

            SaveChanges();

            Debug.Log("graph created ( " + (graph.numberOfEdges + removed) + " edges created, " + removed + " removed, " + graph.numberOfEdges + " remaining )");

            if (grid[EnemyDrawPosition.x, EnemyDrawPosition.y].gravityDirection != Vector2Int.zero)
            {
                agentSizeTileDebug = GetTileSelection(EnemyDrawPosition.x, EnemyDrawPosition.y);
                agentGroundTilesDebug = GetTileGroundSelection(EnemyDrawPosition.x, EnemyDrawPosition.y);
            }
            else if (drawEnemyBoxes) Debug.Log("EnemyDrawPosition is not on a gravity tile");
        }

        #endregion

        #region StepsFunctions

        private bool CheckParameters()
        {
            if (parameters == null)
            {
                Debug.LogError("parameters is null");
                return false;
            }
            if (parameters.trajectoriesCheckInterval <= 0)
            {
                Debug.LogError("trajectoriesCheckInterval in parameters is not correct");
                return false;
            }
            if (parameters.enemyData == null)
            {
                Debug.LogError("parameters.enemyData is null");
                return false;
            }
            if (parameters.enemyData.maxJumpVelocity <= 0)
            {
                Debug.LogError("maxJumpVelocity in EnemyData is not correct");
                return false;
            }
            if (parameters.enemyData.gravityMult <= 0)
            {
                Debug.LogError("gravityMult in EnemyData is not correct");
                return false;
            }
            if (parameters.enemyData.entitySize.x < 1 || parameters.enemyData.entitySize.y < 1)
            {
                Debug.LogError("entity size in EnemyData uncorrect");
                return false;
            }
            if (parameters.enemyData.colliderSize == Vector2.zero)
            {
                Debug.LogWarning("collider size in EnemyData zero");
            }
            if (gravityTileMap == null)
            {
                Debug.LogError("gravityTileMap is null");
                return false;
            }
            if (wallsTileMap == null)
            {
                Debug.LogError("wallsTileMap is null");
                return false;
            }
            if (graph == null)
            {
                Debug.LogError("graph is null, your must assign a graph to the pathfindingManager");
                return false;
            }
            return true;
        }

        private void InitGrid()
        {
            var wallsBounds = wallsTileMap.cellBounds;
            grid = new Pathfinding.Tile[bounds.max.x - bounds.min.x, bounds.max.y - bounds.min.y];
            for (int x = bounds.min.x; x < bounds.max.x; x++)
            {
                for (int y = bounds.min.y; y < bounds.max.y; y++)
                {
                    int indexX = x - bounds.min.x;
                    int indexY = y - bounds.min.y;
                    Vector3Int gravCellPosition = new Vector3Int(x, y, 0);
                    TileBase gravTile = gravityTileMap.GetTile(gravCellPosition);

                    if (gravTile != null)
                    {
                        Vector3 WorldPos = gravityTileMap.GetCellCenterWorld(gravCellPosition);
                        Vector3Int worldCellPos = gravityTileMap.WorldToCell(WorldPos);
                        TileBase wallTile = wallsTileMap.GetTile(worldCellPos);

                        if (!GravityManager.tileGravity.ContainsKey(gravTile))
                        {
                            Debug.LogError(gravTile.name + " gravTile not in tileGravity");
                            return;
                        }

                        Vector2Int gravDirection = Vector2Int.RoundToInt(GravityManager.tileGravity[gravTile]);
                        if (gravDirection == null)
                        {
                            Debug.LogError("gravDirection null, this should not happend");
                            return;
                        }
                        grid[indexX, indexY] = new Pathfinding.Tile((Vector2Int)gravCellPosition, gravDirection);
                        if (wallTile == null)
                        {
                            grid[indexX, indexY].type = TileType.Void;
                        }
                        else
                        {
                            grid[indexX, indexY].type = TileType.Obstacle;
                        }
                    }
                    else
                    {
                        grid[indexX, indexY] = new Pathfinding.Tile((Vector2Int)gravCellPosition, Vector2Int.zero);
                        grid[indexX, indexY].type = TileType.Empty;
                    }
                }
            }
        }

        private void FillEmptyTiles()
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y] == null)
                    {
                        Vector2Int gravCellPosition = new Vector2Int(x, y);
                        grid[x, y] = new Pathfinding.Tile(gravCellPosition, Vector2Int.zero);
                    }
                }
            }
        }

        private void FillWalkableTiles()
        {
            int platformIndex = 0;
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y].type == TileType.Void)
                    {
                        Vector2Int gravDirection = grid[x, y].gravityDirection;
                        // Vector2Int gravPosition = grid[x, y].position;

                        if (Mathf.Abs(gravDirection.x) + Mathf.Abs(gravDirection.y) != 1)
                        {
                            Debug.LogError("gravDirection not correct");
                            return;
                        }
                        if (GetTileSelection(x, y, true).Count == parameters.enemyData.entitySize.x * parameters.enemyData.entitySize.y)
                        {
                            if (GetTileGroundSelection(x, y, true).Count > 0)
                            {
                                grid[x, y].type = TileType.Walkable;
                                if (grid[x + gravDirection.y, y + gravDirection.x].type == TileType.Walkable)
                                {
                                    grid[x + gravDirection.y, y + gravDirection.x].AddEdge(grid[x, y].position, grid[x + gravDirection.y, y + gravDirection.x].position, 1f, EdgeType.Walk);
                                    grid[x, y].AddEdge(grid[x + gravDirection.y, y + gravDirection.x].position, grid[x, y].position, 1f, EdgeType.Walk);
                                    grid[x, y].platformIndex = grid[x + gravDirection.y, y + gravDirection.x].platformIndex;
                                }
                                else if (grid[x - gravDirection.y, y - gravDirection.x].type == TileType.Walkable)
                                {
                                    grid[x - gravDirection.y, y - gravDirection.x].AddEdge(grid[x, y].position, grid[x + gravDirection.y, y + gravDirection.x].position, 1f, EdgeType.Walk);
                                    grid[x, y].AddEdge(grid[x - gravDirection.y, y - gravDirection.x].position, grid[x, y].position, 1f, EdgeType.Walk);
                                    grid[x, y].platformIndex = grid[x - gravDirection.y, y - gravDirection.x].platformIndex;
                                }
                                else
                                {
                                    grid[x, y].platformIndex = platformIndex;
                                    platformIndex++;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CreateFallingConnections()
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y].type == TileType.Walkable)
                    {
                        Vector2Int gravDirection = grid[x, y].gravityDirection;
                        for (int i = 0; i < 2; i++)
                        {
                            int side = i == 0 ? -1 : 1;
                            var Pos = new Vector2Int(x + gravDirection.y * side, y + gravDirection.x * side);
                            if (grid[Pos.x, Pos.y].type == TileType.Void && GetTileSelection(Pos.x, Pos.y, true).Count == parameters.enemyData.entitySize.x * parameters.enemyData.entitySize.y)
                            {
                                if (grid[Pos.x, Pos.y].gravityDirection == gravDirection)
                                {

                                    Vector3 launch = GetFallPosition(Pos.x, Pos.y, side, gravDirection);

                                    Vector2 initialSpeed = new Vector2(gravDirection.y * side * parameters.enemyData.awareSpeed / 1.5f, gravDirection.x * side * parameters.enemyData.awareSpeed / 1.5f);
                                    var firstCorner = new Vector2Int(Pos.x + gravDirection.x + parameters.TrajectoriesBoxCheckSize.x * gravDirection.y, Pos.y + gravDirection.y + parameters.TrajectoriesBoxCheckSize.x * gravDirection.x);
                                    var secondCorner = new Vector2Int(Pos.x + gravDirection.x - parameters.TrajectoriesBoxCheckSize.x * gravDirection.y + parameters.TrajectoriesBoxCheckSize.y * gravDirection.x, Pos.y + gravDirection.y - parameters.TrajectoriesBoxCheckSize.x * gravDirection.x + parameters.TrajectoriesBoxCheckSize.y * gravDirection.y);
                                    List<Pathfinding.Tile> potentialTarget = GetTilesInBox(firstCorner, secondCorner, TileType.Walkable, gravDirection, grid[Pos.x, Pos.y].platformIndex);
                                    // Debug.Log(potentialTarget.Count);
                                    foreach (var target in potentialTarget)
                                    {
                                        if (target.gravityDirection == gravDirection)
                                        {
                                            bool found = false;
                                            for (int j = 0; j < parameters.NumberOfVelocitiesTested && found == false; j++)
                                            {
                                                Vector2 currentVelocity = Vector2.Lerp(initialSpeed, Vector2.zero, ((float)j) / parameters.NumberOfVelocitiesTested);
                                                var targetPos = GetCenterPosition(gravityTileMap.GetCellCenterWorld(new Vector3Int(target.position.x, target.position.y, 0)));
                                                float time = ComputeFallTrajectoryTime(launch, targetPos, gravDirection, currentVelocity);
                                                Vector2 acc = GetAccelerationFromFall(launch, targetPos, gravDirection, currentVelocity, time);
                                                if (time > 0 && acc.magnitude < parameters.enemyData.maxAcceleration)
                                                {
                                                    // Debug.Log(acc.magnitude + " " + time);
                                                    if (CheckTrajectory(launch, gravDirection, acc, currentVelocity, time, false, true))
                                                    {
                                                        // Debug.Log("falling from " + grid[x, y].position + " to " + target.position + " in " + time + " with " + acc.magnitude + " acceleration");
                                                        Edge edge = grid[x, y].AddEdge(target.position, grid[x, y].position, (time + acc.magnitude) * parameters.fallCostMultiplier, EdgeType.Fall);
                                                        edge.waypoints.Add(new Waypoint(launch, currentVelocity, acc, time, gravDirection));
                                                        found = true;
                                                    }
                                                    else
                                                    {
                                                        FallTrajectoriesDebug.Add(new Waypoint(launch, currentVelocity, acc, time, gravDirection));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CreateJumpingConnections()
        {
            int count = 0;
            Vector2 collider = new Vector2(parameters.enemyData.colliderSize.x + parameters.colliderSizeBuffer, parameters.enemyData.colliderSize.y + parameters.colliderSizeBuffer / 2); ;
            Vector2Int entitySize = parameters.enemyData.entitySize;
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y].type == TileType.Walkable)
                    {
                        Vector2Int gravDirection = grid[x, y].gravityDirection;
                        Vector2Int corner1 = new Vector2Int(x - parameters.JumpConnectionMaxRange, y + parameters.JumpConnectionMaxRange);
                        Vector2Int corner2 = new Vector2Int(x + parameters.JumpConnectionMaxRange, y - parameters.JumpConnectionMaxRange);
                        List<Pathfinding.Tile> potentialTarget = GetTilesInBox(corner1, corner2, TileType.Walkable, gravDirection, grid[x, y].platformIndex);
                        foreach (Pathfinding.Tile target in potentialTarget)
                        {
                            if (target.position != grid[x, y].position && target.gravityDirection == gravDirection)
                            {
                                Vector3 launch = gravityTileMap.GetCellCenterWorld((Vector3Int)grid[x, y].position);
                                launch = GetCenterPosition(launch);
                                Vector3 targetPos = GetCenterPosition(gravityTileMap.GetCellCenterWorld((Vector3Int)target.position));
                                Vector3 a = new Vector3(gravDirection.x, gravDirection.y, 0f) * parameters.enemyData.gravityMult * 10f;
                                List<float> times = ComputeJumpTrajectoryTimes(launch, targetPos, a);

                                bool foundOne = false;
                                int i = 0;
                                while (foundOne == false && i < parameters.NumberOfTestedTrajectories)
                                {
                                    float time = times[1] + (times[2] - times[1]) * i / (parameters.NumberOfTestedTrajectories - 1);
                                    if (time >= 0)
                                    {
                                        Vector3 velocity = (targetPos - launch) / time - a * time / 2f;

                                        if (CheckTrajectory(launch, gravDirection, Vector2.zero, velocity, time) && velocity.magnitude < parameters.enemyData.maxJumpVelocity)
                                        {
                                            Edge edge = grid[x, y].AddEdge(target.position, grid[x, y].position, time * parameters.jumpCostMultiplier, EdgeType.Jump);
                                            edge.waypoints.Add(new Waypoint(launch, velocity, Vector2.zero, time, gravDirection));
                                            count++;
                                            foundOne = true;
                                        }
                                        else
                                        {
                                            JumpTrajectoriesDebug[i].Add(new Waypoint(launch, velocity, Vector2.zero, time, gravDirection));
                                        }
                                    }
                                    i++;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CreateInterGravityConnections()
        {
            int count = 0;
            Vector2 collider = new Vector2(parameters.enemyData.colliderSize.x + parameters.colliderSizeBuffer, parameters.enemyData.colliderSize.y + parameters.colliderSizeBuffer / 2); ;
            Vector2Int entitySize = parameters.enemyData.entitySize;
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y].type == TileType.Walkable)
                    {
                        Vector2Int gravDirection = grid[x, y].gravityDirection;
                        Vector2Int corner1 = new Vector2Int(x - parameters.JumpConnectionMaxRange, y + parameters.JumpConnectionMaxRange);
                        Vector2Int corner2 = new Vector2Int(x + parameters.JumpConnectionMaxRange, y - parameters.JumpConnectionMaxRange);


                        Vector2Int[] tempCorners = GenerateBounds(corner1, corner2, gravDirection);
                        corner1 = tempCorners[0];
                        corner2 = tempCorners[1];
                        List<Vector2Int> exploredCorners = new List<Vector2Int>();

                        List<Edge> currentEdges = new List<Edge>();

                        //from a jump
                        List<List<Pathfinding.Tile>> InitTransitArea = GetTransitionTilesInBox(corner1, corner2, gravDirection, exploredCorners);
                        foreach (List<Pathfinding.Tile> transitArea in InitTransitArea)
                        {
                            currentEdges.Clear();
                            currentEdges.AddRange(FindBestJumpEdge(grid[x, y], transitArea, gravDirection));
                        }
                        //from a fall
                        for (int i = 0; i < 2; i++)
                        {
                            int side = i == 0 ? -1 : 1;
                            var Pos = new Vector2Int(x + gravDirection.y * side, y + gravDirection.x * side);
                            if (grid[Pos.x, Pos.y].type == TileType.Void && GetTileSelection(Pos.x, Pos.y, true).Count == parameters.enemyData.entitySize.x * parameters.enemyData.entitySize.y)
                            {
                                if (grid[Pos.x, Pos.y].gravityDirection == gravDirection)
                                {
                                    Vector2Int[] fallCorners = GenerateBounds(Pos, Pos, gravDirection);
                                    InitTransitArea = GetTransitionTilesInBox(fallCorners[0], fallCorners[1], gravDirection, exploredCorners);
                                    Vector3 launch = GetFallPosition(Pos.x, Pos.y, side, gravDirection);
                                    Vector2 initialSpeed = new Vector2(gravDirection.y * side * parameters.enemyData.awareSpeed / 1.5f, gravDirection.x * side * parameters.enemyData.awareSpeed / 1.5f);
                                    foreach (List<Pathfinding.Tile> transitArea in InitTransitArea)
                                    {
                                        currentEdges.AddRange(FindBestFallEdge(launch, transitArea, gravDirection, initialSpeed, grid[x, y].position));
                                    }
                                }
                            }
                        }
                        exploredCorners.Add(corner1);
                        exploredCorners.Add(corner2);


                        while (currentEdges.Count > 0)
                        {
                            if (currentEdges[0].waypoints.Count > 5)
                            {
                                currentEdges.RemoveAt(0);
                                Debug.Log("reached waypoints count limit");
                                continue;
                            }
                            Vector3 launch = currentEdges[0].temp;
                            if (launch == Vector3.zero)
                            {
                                currentEdges.RemoveAt(0);
                                Debug.Log(currentEdges[0].targetPos + " is not a valid as a target");
                                continue;
                            }
                            Pathfinding.Tile newOrigin = GetTileAtWorldPos(launch);

                            Vector2Int originCell = currentEdges[0].targetPos;
                            originCell -= new Vector2Int(bounds.min.x, bounds.min.y);
                            if (newOrigin.gravityDirection != grid[originCell.x, originCell.y].gravityDirection)
                            {
                                Debug.Log("oulaCestlaMerde cestpasnormal aled");
                            }

                            Vector2Int[] newCorners = GenerateBounds(new List<Pathfinding.Tile>() { newOrigin }, newOrigin.gravityDirection);
                            Vector2 initialVelocity = ComputeInitialVelocity(currentEdges[0].waypoints[currentEdges[0].waypoints.Count - 1]);

                            List<Pathfinding.Tile> potentialTarget = GetTilesInBox(newCorners[0], newCorners[1], TileType.Walkable, newOrigin.gravityDirection, grid[x, y].platformIndex);
                            foreach (Pathfinding.Tile target in potentialTarget)
                            {
                                var targetPos = GetCenterPosition(gravityTileMap.GetCellCenterWorld(new Vector3Int(target.position.x, target.position.y, 0)));
                                bool found = false;
                                for (int i = 0; i < parameters.NumberOfVelocitiesTested && found == false; i++)
                                {

                                    Vector2 currentVelocity = Vector2.Lerp(initialVelocity, Vector2.zero, ((float)i) / parameters.NumberOfVelocitiesTested);
                                    float time = ComputeFallTrajectoryTime(launch, targetPos, newOrigin.gravityDirection, currentVelocity);
                                    Vector2 acc = GetAccelerationFromFall(launch, targetPos, newOrigin.gravityDirection, currentVelocity, time);
                                    if (time > 0 && acc.magnitude < parameters.enemyData.maxAcceleration)
                                    {
                                        if (CheckTrajectory(launch, newOrigin.gravityDirection, acc, currentVelocity, time))
                                        {
                                            Edge edge = grid[x, y].AddPremadeEdge(currentEdges[0]);
                                            edge.waypoints.Add(new Waypoint(launch, currentVelocity, acc, time, newOrigin.gravityDirection));
                                            edge.targetPos = target.position;
                                            edge.cost += (time + acc.magnitude) * parameters.fallCostMultiplier;
                                            found = true;
                                            count++;
                                        }
                                        else
                                        {
                                            foreach (var waypoint in currentEdges[0].waypoints)
                                            {
                                                MultiGravTrajectoriesValidDebug.Add(waypoint);
                                            }
                                            MultiGravTrajectoriesDebug.Add(new Waypoint(launch, currentVelocity, acc, time, newOrigin.gravityDirection));
                                        }
                                    }
                                }
                            }

                            List<List<Pathfinding.Tile>> secondTransitArea = GetTransitionTilesInBox(newCorners[0], newCorners[1], newOrigin.gravityDirection, exploredCorners);
                            exploredCorners.Add(newCorners[0]);
                            exploredCorners.Add(newCorners[1]);
                            foreach (List<Pathfinding.Tile> transitArea2 in secondTransitArea)
                            {
                                List<Edge> potentialEdges = FindBestFallEdge(launch, transitArea2, newOrigin.gravityDirection, initialVelocity, newOrigin.position);
                                foreach (Edge potentialEdge in potentialEdges)
                                {
                                    Edge edgeToAdd = currentEdges[0].Clone();
                                    edgeToAdd.waypoints.Add(potentialEdge.waypoints[0]);
                                    edgeToAdd.cost += potentialEdge.cost;
                                    edgeToAdd.targetPos = potentialEdge.targetPos;
                                    edgeToAdd.temp = potentialEdge.temp;
                                    currentEdges.Add(edgeToAdd);
                                }
                            }
                            foreach (var waypoint in currentEdges[0].waypoints)
                            {
                                MultiGravTrajectoriesValidDebug.Add(waypoint);
                            }
                            currentEdges.RemoveAt(0);
                        }
                    }
                }
            }
            Debug.Log("created " + count + " interGravity edges");
        }

        private int RemoveUnnecessaryConnections()
        {
            int count = 0;
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y].type == TileType.Walkable)
                    {
                        for (int i = 0; i < grid[x, y].edges.Count; i++)
                        {
                            //remove the other edges that go to the same platform if they are more expensive
                            Edge edge = grid[x, y].edges[0];
                            if (edge.type == EdgeType.Walk)
                            {
                                continue;
                            }
                            grid[x, y].edges.Remove(edge);
                            for (int j = 0; j < grid[x, y].edges.Count; j++)
                            {
                                Edge edge2 = grid[x, y].edges[j];
                                if (edge2.type == edge.type && grid[edge2.targetPos.x - bounds.min.x, edge2.targetPos.y - bounds.min.y].platformIndex == grid[edge.targetPos.x - bounds.min.x, edge.targetPos.y - bounds.min.y].platformIndex)
                                {
                                    if (edge.cost > edge2.cost)
                                    {
                                        edge = edge2;
                                    }
                                    foreach (Waypoint waypoint in edge2.waypoints)
                                    {
                                        Debug.Log("removed trajectory");
                                        RemovedTrajectoriesDebug.Add(new Waypoint(waypoint.position, waypoint.initialSpeed, waypoint.acceleration, waypoint.time, waypoint.gravityDirection));
                                    }
                                    grid[x, y].edges.Remove(edge2);
                                    j--;
                                    count++;
                                }
                            }
                            grid[x, y].edges.Add(edge);

                        }

                        Vector2Int gravityDirection = grid[x, y].gravityDirection;
                        for (int i = 1; i < parameters.SimilarEdgesRemovalRange + 1; i++)
                        {
                            for (int j = 0; j < 2; j++)
                            {
                                int side = j == 0 ? -1 : 1;

                                //for each edge in sortedEdges, we check if there is an edge with the same platform index on the target
                                Vector2Int targetPos = new Vector2Int(x + gravityDirection.y, y + gravityDirection.x) * i * side;

                                if (CheckGrid(targetPos.x, targetPos.y))
                                {
                                    if (grid[targetPos.x, targetPos.y].type == TileType.Walkable)
                                    {
                                        for (int a = 0; a < grid[x, y].edges.Count; a++)
                                        {
                                            Edge edge = grid[x, y].edges[a];
                                            for (int b = 0; b < grid[targetPos.x, targetPos.y].edges.Count; b++)
                                            {
                                                Edge edge2 = grid[targetPos.x, targetPos.y].edges[b];
                                                if (edge.type == EdgeType.Jump && edge2.type == EdgeType.Jump)
                                                {
                                                    if (grid[edge.targetPos.x - bounds.min.x, edge.targetPos.y - bounds.min.y].platformIndex == grid[edge2.targetPos.x - bounds.min.x, edge2.targetPos.y - bounds.min.y].platformIndex)
                                                    {
                                                        if (edge.cost < edge2.cost)
                                                        {
                                                            foreach (Waypoint waypoint in edge2.waypoints)
                                                            {
                                                                Debug.Log("removed trajectory");
                                                                RemovedTrajectoriesDebug.Add(new Waypoint(waypoint.position, waypoint.initialSpeed, waypoint.acceleration, waypoint.time, waypoint.gravityDirection));
                                                            }
                                                            grid[targetPos.x, targetPos.y].edges.Remove(edge2);
                                                        }
                                                        else
                                                        {
                                                            foreach (Waypoint waypoint in edge.waypoints)
                                                            {
                                                                Debug.Log("removed trajectory");
                                                                RemovedTrajectoriesDebug.Add(new Waypoint(waypoint.position, waypoint.initialSpeed, waypoint.acceleration, waypoint.time, waypoint.gravityDirection));
                                                            }
                                                            grid[x, y].edges.Remove(edge);
                                                        }
                                                        count++;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return count;
        }

        private void MoveToList()
        {
            for (int x = 0; x < grid.GetLength(0); x++)
            {
                for (int y = 0; y < grid.GetLength(1); y++)
                {
                    if (grid[x, y] != null && grid[x, y].type != TileType.Empty)
                    {
                        graph.numberOfEdges += grid[x, y].edges.Count;
                        graph.tiles.Add(grid[x, y]);
                    }
                }
            }
        }

        private void SetScriptableObjectParameters()
        {
            graph.parameters = parameters.Clone();
            graph.gravityTilemapName = gravityTileMap.gameObject.name;
            graph.wallsTilemapName = wallsTileMap.gameObject.name;
        }

        private void SaveChanges()
        {
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void LoadGraph()
        {
            if (graph != null && graph.tiles.Count > 0)
            {
                hasBeenLoaded = true;
                if (graph.parameters == null)
                {
                    Debug.Log("error : parameters must are null");
                    return;
                }
                parameters = graph.parameters.Clone();
                GenerateGridFromList();
                Debug.Log("graph loaded");
                if (gravityTileMap == null || wallsTileMap == null)
                {
                    Debug.Log("tilemaps must be loaded manually");
                    if (gravityTileMap == null)
                    {
                        Debug.Log("gravityTileMap should be " + graph.gravityTilemapName);
                    }
                    if (wallsTileMap == null)
                    {
                        Debug.Log("wallsTileMap should be " + graph.wallsTilemapName);
                    }
                }
                if (gravityTileMap != null && gravityTileMap.gameObject.name != graph.gravityTilemapName)
                {
                    Debug.LogWarning("gravityTileMap should be " + graph.gravityTilemapName + " but is " + gravityTileMap.gameObject.name);
                }
                if (wallsTileMap != null && wallsTileMap.gameObject.name != graph.wallsTilemapName)
                {
                    Debug.LogWarning("wallsTileMap should be " + graph.wallsTilemapName + " but is " + wallsTileMap.gameObject.name);
                }
            }
            else
            {
                Debug.Log("graph is not loadable");
            }
        }

        #endregion

        #region Helpers

        #region trajectoryFunctions

        private bool fitsFromFeets(Vector3 pos, Vector2Int gravDirection)
        {
            Vector2 collider = new Vector2(parameters.enemyData.colliderSize.x + parameters.colliderSizeBuffer, parameters.enemyData.colliderSize.y + parameters.colliderSizeBuffer / 2); ;
            Vector2Int entitySize = parameters.enemyData.entitySize;
            Vector3 leftBot = pos + new Vector3((collider.x / 2f) * gravDirection.y, (-collider.x / 2f) * gravDirection.x, 0f);
            Vector3 xOffset = new Vector3(-collider.x * gravDirection.y, collider.x * gravDirection.x, 0f);
            Vector3 yOffset = new Vector3(-collider.y * gravDirection.x, -collider.y * gravDirection.y, 0f);
            Vector3 currentPos;
            for (int k = 0; k < entitySize.x + 1; k++)
            {
                for (int l = 0; l < entitySize.y + 1; l++)
                {
                    currentPos = leftBot + xOffset * (k / entitySize.x) + yOffset * (l / entitySize.y);

                    Pathfinding.Tile currentTile = GetTileAtWorldPos(currentPos);
                    if (currentTile == null || currentTile.type == TileType.Obstacle)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool fitsFromCenter(Vector3 pos, Vector2Int gravDirection, bool debug = false)
        {
            Vector2 rawCollider = parameters.enemyData.colliderSize;
            Vector2 collider = new Vector2(parameters.enemyData.colliderSize.x + parameters.colliderSizeBuffer, parameters.enemyData.colliderSize.y + parameters.colliderSizeBuffer / 2); ;
            Vector2Int entitySize = parameters.enemyData.entitySize;
            Vector3 leftBot = pos + new Vector3((collider.x / 2f) * gravDirection.y + (rawCollider.y / 2f) * gravDirection.x, (-collider.x / 2f) * gravDirection.x + (rawCollider.y / 2f) * gravDirection.y, 0f);
            Vector3 xOffset = new Vector3(-collider.x * gravDirection.y, collider.x * gravDirection.x, 0f);
            Vector3 yOffset = new Vector3(-collider.y * gravDirection.x, -collider.y * gravDirection.y, 0f);
            Vector3 currentPos;
            for (int k = 0; k < entitySize.x + 1; k++)
            {
                for (int l = 0; l < entitySize.y + 1; l++)
                {
                    currentPos = leftBot + xOffset * (k / entitySize.x) + yOffset * (l / entitySize.y);
                    if (debug)
                    {
                        fitsPointsDebug.Add(currentPos);
                    }
                    Pathfinding.Tile currentTile = GetTileAtWorldPos(currentPos);
                    if (currentTile == null || currentTile.type == TileType.Obstacle)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        private bool CheckTrajectory(Vector3 launch, Vector2Int gravityDirection, Vector2 additionalAcc, Vector2 initialVelocity, float T, bool interGrav = false, bool debug = false)
        {
            Vector3 pos;
            Vector3 a = new Vector3(gravityDirection.x * 10f + additionalAcc.x, gravityDirection.y * 10f + additionalAcc.y, 0f);
            for (float time = 0f; time < T - 0.01f; time += parameters.trajectoriesCheckInterval)
            {
                pos = launch + (Vector3)initialVelocity * time + 0.5f * a * time * time;
                Vector3Int cellPos = gravityTileMap.WorldToCell(pos);
                if (grid[cellPos.x - bounds.min.x, cellPos.y - bounds.min.y].gravityDirection != gravityDirection)
                {
                    if (!interGrav)
                    {
                        return false;
                    }
                }
                if (!fitsFromCenter(pos, gravityDirection))
                {
                    if (debug)
                    {
                        fitsFromCenter(pos, gravityDirection, true);
                    }
                    return false;
                }
            }
            return true;
        }

        private Vector3 SearchCollisionPoint(Vector3 launch, Vector2Int gravityDirection, Vector2 additionalAcc, Vector2 initialVelocity, float T, out float deltaT)
        {
            // Dichotomy to find the point where the gravity is no longer gravityDirection
            Vector3Int cellPos = gravityTileMap.WorldToCell(launch);
            if (grid[cellPos.x - bounds.min.x, cellPos.y - bounds.min.y].gravityDirection != gravityDirection)
            {
                deltaT = 0f;
                Debug.Log("starting with wrong gravity, shouldn't happen");
                return Vector3.zero;
            }

            float t1 = 0f;
            float t2 = T;
            float time = (t1 + t2) / 2f;
            Vector3 pos;
            Vector3 a = new Vector3(gravityDirection.x * 10f + additionalAcc.x, gravityDirection.y * 10f + additionalAcc.y, 0f);
            while (t2 - t1 > parameters.trajectoriesCheckInterval)
            {
                time = (t1 + t2) / 2f;
                pos = launch + (Vector3)initialVelocity * time + 0.5f * a * time * time;
                cellPos = gravityTileMap.WorldToCell(pos);
                if (grid[cellPos.x - bounds.min.x, cellPos.y - bounds.min.y].gravityDirection != gravityDirection)
                {
                    t2 = time;
                }
                else
                {
                    t1 = time;
                }
            }
            debugCollisionPoints.Add(launch + (Vector3)initialVelocity * t2 + 0.5f * a * t2 * t2);
            deltaT = T - t2;
            return launch + (Vector3)initialVelocity * t2 + 0.5f * a * t2 * t2;
        }

        private List<Edge> FindBestJumpEdge(Pathfinding.Tile origin, List<Pathfinding.Tile> targets, Vector2Int gravDirection)
        {
            List<Edge> currentsEdge = new List<Edge>();
            if (targets.Count == 0)
            {
                Debug.Log("no target");
                return currentsEdge;
            }
            Vector3 launch = gravityTileMap.GetCellCenterWorld((Vector3Int)origin.position);
            launch = GetCenterPosition(launch);
            foreach (Pathfinding.Tile target in targets)
            {
                Vector3 targetPos = gravityTileMap.GetCellCenterWorld((Vector3Int)target.position);
                Vector3 a = new Vector3(gravDirection.x, gravDirection.y, 0f) * parameters.enemyData.gravityMult * 10f;
                List<float> times = ComputeJumpTrajectoryTimes(launch, targetPos, a);
                bool found = false;
                for (int i = 0; i < parameters.NumberOfTestedTrajectories; i++)
                {
                    if (found)
                    {
                        continue;
                    }
                    float time = times[1] + (times[2] - times[1]) * i / (parameters.NumberOfTestedTrajectories - 1);
                    if (time >= 0)
                    {
                        Vector3 velocity = (targetPos - launch) / time - a * time / 2f;
                        if (CheckTrajectory(launch, gravDirection, Vector2.zero, velocity, time, true))
                        {
                            float deltaT;
                            Vector3 collidPoint = SearchCollisionPoint(launch, gravDirection, Vector2.zero, velocity, time, out deltaT);
                            time -= deltaT;
                            if (collidPoint != Vector3.zero && fitsFromCenter(collidPoint, target.gravityDirection) && GetTileAtWorldPos(collidPoint).gravityDirection == target.gravityDirection)
                            {
                                if (currentsEdge.Count == 0)
                                {
                                    currentsEdge.Add(new Edge(target.position, origin.position, time, EdgeType.Jump));
                                }
                                else
                                {
                                    currentsEdge.Add(new Edge(target.position, origin.position, time, EdgeType.Jump));
                                }
                                currentsEdge[currentsEdge.Count - 1].temp = collidPoint;
                                currentsEdge[currentsEdge.Count - 1].waypoints.Add(new Waypoint(launch, velocity, Vector2.zero, time, gravDirection));
                                found = true;
                            }
                        }
                    }
                }
            }
            foreach (Edge edge in currentsEdge)
            {
                edge.cost = edge.cost * parameters.jumpCostMultiplier;
            }
            return currentsEdge;
        }

        private List<Edge> FindBestFallEdge(Vector3 launch, List<Pathfinding.Tile> targets, Vector2Int gravDirection, Vector2 initialSpeed, Vector2Int originPos, bool toCenter = false)
        {
            List<Edge> currentsEdge = new List<Edge>();
            if (targets.Count == 0)
            {
                return currentsEdge;
            }
            foreach (Pathfinding.Tile target in targets)
            {
                //TODO mettre les feet pos si c est la derniere destination
                var targetPos = gravityTileMap.GetCellCenterWorld(new Vector3Int(target.position.x, target.position.y, 0));
                if (toCenter)
                {
                    targetPos = GetCenterPosition(targetPos);
                }
                float time = ComputeFallTrajectoryTime(launch, targetPos, gravDirection, initialSpeed);
                Vector2 acc = GetAccelerationFromFall(launch, targetPos, gravDirection, initialSpeed, time);
                if (time > 0 && acc.magnitude < parameters.enemyData.maxAcceleration)
                {
                    float deltaT;
                    Vector3 collidPoint = SearchCollisionPoint(launch, gravDirection, acc, initialSpeed, time, out deltaT);
                    time -= deltaT;
                    if (CheckTrajectory(launch, gravDirection, acc, initialSpeed, time, true))
                    {
                        if (collidPoint != Vector3.zero && fitsFromCenter(collidPoint, target.gravityDirection) && GetTileAtWorldPos(collidPoint).gravityDirection == target.gravityDirection)
                        {
                            if (currentsEdge.Count == 0)
                            {
                                currentsEdge.Add(new Edge(target.position, originPos, time, EdgeType.Fall));
                            }
                            else
                            {
                                currentsEdge[0] = new Edge(target.position, originPos, time, EdgeType.Fall);
                            }
                            currentsEdge[0].temp = collidPoint;
                            currentsEdge[0].waypoints.Add(new Waypoint(launch, initialSpeed, acc, time, gravDirection));
                        }
                    }
                }
            }
            foreach (Edge edge in currentsEdge)
            {
                edge.cost = edge.cost * parameters.jumpCostMultiplier;
            }
            return currentsEdge;
        }

        private List<float> ComputeJumpTrajectoryTimes(Vector3 launch, Vector3 target, Vector3 a)
        {
            List<float> T = new List<float>();
            Vector3 deltaP = target - launch;
            float b1 = Vector3.Dot(deltaP, a) + parameters.enemyData.maxJumpVelocity * parameters.enemyData.maxJumpVelocity;
            float discriminant = b1 * b1 - Vector3.Dot(a, a) * Vector3.Dot(deltaP, deltaP);
            float T_min = Mathf.Sqrt((b1 - Mathf.Sqrt(discriminant)) * 2 / Vector3.Dot(a, a));
            float T_max = Mathf.Sqrt((b1 + Mathf.Sqrt(discriminant)) * 2 / Vector3.Dot(a, a));
            float T_lowEnergy = Mathf.Sqrt(Mathf.Sqrt(4.0f * Vector3.Dot(deltaP, deltaP) / Vector3.Dot(a, a)));
            T.Add(T_min);
            T.Add(T_lowEnergy);
            T.Add(T_max);
            return T;
        }

        private float ComputeFallTrajectoryTime(Vector3 launch, Vector3 target, Vector2 gravityDir, Vector2 v0)
        {
            float deltaP = Vector3.Dot(target - launch, gravityDir);
            if (deltaP <= 0)
            {
                return -1;
            }

            float v0y = Vector3.Dot(v0, -gravityDir);
            float discriminant = v0y * v0y + 2 * 10f * deltaP;

            return (v0y + Mathf.Sqrt(discriminant)) / 10f;
        }

        private Vector2 GetAccelerationFromFall(Vector3 launch, Vector3 target, Vector2 gravityDir, Vector2 v0, float time)
        {
            Vector2 rightDir = Vector2.Perpendicular(gravityDir);
            float deltaP = Vector3.Dot(target - launch, rightDir);
            float v0x = Vector3.Dot(v0, rightDir);
            float a = 2 * deltaP / (time * time) - 2 * v0x / time;
            return a * rightDir;
        }

        private Vector2 ComputeInitialVelocity(Waypoint waypoint)
        {
            Vector2 Accel = new Vector2(waypoint.acceleration.x + waypoint.gravityDirection.x * 10f, waypoint.acceleration.y + waypoint.gravityDirection.y * 10f);
            return waypoint.initialSpeed + Accel * waypoint.time;
        }

        Vector3 TrajectoryPosition(float time, Vector3 launchVelocity, Vector3 a, Vector3 lauch)
        {
            return lauch + launchVelocity * time + a * time * time / 2.0f;
        }

        #endregion

        #region TilesGetterFunctions
        private Pathfinding.Tile GetTileAtWorldPos(Vector3 worldPos)
        {
            Vector3Int cellPos = gravityTileMap.WorldToCell(worldPos);

            cellPos = new Vector3Int(cellPos.x - bounds.xMin, cellPos.y - bounds.yMin, 0);
            if (CheckGrid(cellPos.x, cellPos.y))
            {
                return grid[cellPos.x, cellPos.y];
            }
            else
            {
                return null;
            }
        }

        private List<Pathfinding.Tile> GetTilesInBox(Vector2Int corner1, Vector2Int corner2, TileType type, Vector2Int gravityDirection, int platformIndex = -1)
        {
            List<Pathfinding.Tile> tiles = new List<Pathfinding.Tile>();
            int minX = Mathf.Min(corner1.x, corner2.x);
            int maxX = Mathf.Max(corner1.x, corner2.x);
            int minY = Mathf.Min(corner1.y, corner2.y);
            int maxY = Mathf.Max(corner1.y, corner2.y);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (CheckGrid(x, y) && grid[x, y] != null)
                    {
                        if (grid[x, y].type == type && platformIndex != grid[x, y].platformIndex && grid[x, y].gravityDirection == gravityDirection)
                        {
                            tiles.Add(grid[x, y]);
                        }
                    }
                }
            }
            return tiles;
        }

        private List<List<Pathfinding.Tile>> GetTransitionTilesInBox(Vector2Int corner1, Vector2Int corner2, Vector2Int gravityDirection, List<Vector2Int> exploredCorners)
        {
            List<List<Pathfinding.Tile>> areas = new List<List<Pathfinding.Tile>>();
            int minX = Mathf.Min(corner1.x, corner2.x);
            int maxX = Mathf.Max(corner1.x, corner2.x);
            int minY = Mathf.Min(corner1.y, corner2.y);
            int maxY = Mathf.Max(corner1.y, corner2.y);
            List<Pathfinding.Tile> tilesToTest = new List<Pathfinding.Tile>();
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (CheckGrid(x, y) && grid[x, y] != null && !IsAlreadyVisisted(x, y, exploredCorners))
                    {
                        if (grid[x, y].type == TileType.Void && grid[x, y].gravityDirection != gravityDirection)
                        {
                            if (GetNeighboors(grid[x, y].position, TileType.Void, gravityDirection, true).Count > 0)
                            {
                                tilesToTest.Add(grid[x, y]);
                                trnsitionDebug.Add(grid[x, y]);
                            }
                        }
                    }
                }
            }
            while (tilesToTest.Count > 0)
            {
                List<Pathfinding.Tile> area = new List<Pathfinding.Tile>();
                List<Pathfinding.Tile> neighboors = new List<Pathfinding.Tile>();
                neighboors.Add(tilesToTest[0]);
                int i = 0;
                while (neighboors.Count > 0 && i < 100)
                {
                    i += 1;
                    if (tilesToTest.Contains(neighboors[0]))
                    {
                        area.Add(neighboors[0]);
                        List<Pathfinding.Tile> newNeighboors = GetNeighboors(neighboors[0].position, TileType.Void, neighboors[0].gravityDirection);
                        foreach (Pathfinding.Tile newNeighboor in newNeighboors)
                        {
                            if (!neighboors.Contains(newNeighboor) && !area.Contains(newNeighboor) && tilesToTest.Contains(newNeighboor))
                            {
                                neighboors.Add(newNeighboor);
                            }
                        }
                        tilesToTest.Remove(neighboors[0]);
                    }
                    neighboors.RemoveAt(0);
                }
                if (i == 100)
                {
                    Debug.Log("more than 100 tiles for a transition");
                }
                if (area.Count > 0)
                {
                    areas.Add(area);
                }
            }
            return areas;
        }

        private List<Pathfinding.Tile> GetNeighboors(Vector2Int position, TileType type, Vector2Int gravityDirection, bool diagonals = false)
        {
            List<Pathfinding.Tile> neighboors = new List<Pathfinding.Tile>();
            position -= new Vector2Int(bounds.xMin, bounds.yMin);
            if (CheckGrid(position.x + 1, position.y) && grid[position.x + 1, position.y] != null && grid[position.x + 1, position.y].type == type && grid[position.x + 1, position.y].gravityDirection == gravityDirection)
            {
                neighboors.Add(grid[position.x + 1, position.y]);
            }
            if (CheckGrid(position.x - 1, position.y) && grid[position.x - 1, position.y] != null && grid[position.x - 1, position.y].type == type && grid[position.x - 1, position.y].gravityDirection == gravityDirection)
            {
                neighboors.Add(grid[position.x - 1, position.y]);
            }
            if (CheckGrid(position.x, position.y + 1) && grid[position.x, position.y + 1] != null && grid[position.x, position.y + 1].type == type && grid[position.x, position.y + 1].gravityDirection == gravityDirection)
            {
                neighboors.Add(grid[position.x, position.y + 1]);
            }
            if (CheckGrid(position.x, position.y - 1) && grid[position.x, position.y - 1] != null && grid[position.x, position.y - 1].type == type && grid[position.x, position.y - 1].gravityDirection == gravityDirection)
            {
                neighboors.Add(grid[position.x, position.y - 1]);
            }
            if (diagonals)
            {
                if (CheckGrid(position.x + 1, position.y + 1) && grid[position.x + 1, position.y + 1] != null && grid[position.x + 1, position.y + 1].type == type && grid[position.x + 1, position.y + 1].gravityDirection == gravityDirection)
                {
                    neighboors.Add(grid[position.x + 1, position.y + 1]);
                }
                if (CheckGrid(position.x - 1, position.y + 1) && grid[position.x - 1, position.y + 1] != null && grid[position.x - 1, position.y + 1].type == type && grid[position.x - 1, position.y + 1].gravityDirection == gravityDirection)
                {
                    neighboors.Add(grid[position.x - 1, position.y + 1]);
                }
                if (CheckGrid(position.x + 1, position.y - 1) && grid[position.x + 1, position.y - 1] != null && grid[position.x + 1, position.y - 1].type == type && grid[position.x + 1, position.y - 1].gravityDirection == gravityDirection)
                {
                    neighboors.Add(grid[position.x + 1, position.y - 1]);
                }
                if (CheckGrid(position.x - 1, position.y - 1) && grid[position.x - 1, position.y - 1] != null && grid[position.x - 1, position.y - 1].type == type && grid[position.x - 1, position.y - 1].gravityDirection == gravityDirection)
                {
                    neighboors.Add(grid[position.x - 1, position.y - 1]);
                }
            }
            return neighboors;
        }

        private Pathfinding.Tile GetTileAtCorner(int x, int y, int side = 1)
        {
            if (!CheckGrid(x, y))
            {
                Debug.Log("gravDirection not correct");
                return null;
            }
            else
            {
                var gravDirection = grid[x, y].gravityDirection;
                var offsetx = ((parameters.enemyData.entitySize.x - 1) / 2) * side;
                var offsety = Mathf.FloorToInt((parameters.enemyData.colliderSize.y / 2) / gravityTileMap.cellSize.y);
                return grid[x + gravDirection.x * offsety + gravDirection.y * offsetx, y + gravDirection.y * offsety - gravDirection.x * offsetx];
            }
        }

        private List<Pathfinding.Tile> GetTileSelection(int x, int y, bool onlyVoidOrWalkable = false)
        {
            Vector2Int gravDirection = grid[x, y].gravityDirection;
            if (Mathf.Abs(gravDirection.x) + Mathf.Abs(gravDirection.y) != 1)
            {
                Debug.Log("gravDirection not correct");
                return new List<Pathfinding.Tile>(); ;
            }
            List<Pathfinding.Tile> tileSelection = new List<Pathfinding.Tile>();
            // int currentrowx;
            // int currentrowy;
            Pathfinding.Tile currentTile = GetTileAtCorner(x, y);

            int cornerx = currentTile.position.x - bounds.min.x;
            int cornery = currentTile.position.y - bounds.min.y;
            int currentx = cornerx;
            int currenty = cornery;
            // tileSelection.Add(grid[x,y]);
            // tileSelection.Add(grid[x + gravDirection.x,y + gravDirection.y]);

            for (int i = 0; i < parameters.enemyData.entitySize.x; i++)
            {
                for (int j = 0; j < parameters.enemyData.entitySize.y; j++)
                {
                    currentx = cornerx - gravDirection.x * i - gravDirection.y * j;
                    currenty = cornery - gravDirection.y * i + gravDirection.x * j;

                    if (CheckGrid(currentx, currenty))
                    {
                        currentTile = grid[currentx, currenty];
                        if (currentTile != null)
                        {
                            if ((onlyVoidOrWalkable && (currentTile.type == TileType.Void || currentTile.type == TileType.Walkable)) || !onlyVoidOrWalkable)
                            {
                                tileSelection.Add(currentTile);
                            }
                        }
                        else
                        {
                            Debug.Log("tile null");
                            return new List<Pathfinding.Tile>();
                        }
                    }
                }
            }
            return tileSelection;
        }

        private List<Pathfinding.Tile> GetTileGroundSelection(int x, int y, bool onlyObstacles = false)
        {
            Vector2Int gravDirection = grid[x, y].gravityDirection;
            if (Mathf.Abs(gravDirection.x) + Mathf.Abs(gravDirection.y) != 1)
            {
                Debug.Log("gravDirection not correct");
                return new List<Pathfinding.Tile>(); ;
            }
            List<Pathfinding.Tile> tileSelection = new List<Pathfinding.Tile>();
            Pathfinding.Tile currentTile = GetTileAtCorner(x, y);

            int cornerx = currentTile.position.x - bounds.min.x + gravDirection.x;
            int cornery = currentTile.position.y - bounds.min.y + gravDirection.y;
            int currentx;
            int currenty;

            for (int i = 0; i < parameters.enemyData.entitySize.x; i++)
            {
                currentx = cornerx - gravDirection.y * i;
                currenty = cornery + gravDirection.x * i;
                if (CheckGrid(currentx, currenty))
                {
                    if (grid[currentx, currenty].type == TileType.Obstacle)
                    {
                        tileSelection.Add(grid[currentx, currenty]);
                    }
                    else if (!onlyObstacles && grid[currentx, currenty] != null)
                    {
                        tileSelection.Add(grid[currentx, currenty]);
                    }
                }
            }
            return tileSelection;
        }

        #endregion

        #region OtherHelpersFunctions

        private Vector3 GetFeetPosition(Vector3 worldPos)
        {
            Vector3Int cellPos = gravityTileMap.WorldToCell(worldPos);
            cellPos = new Vector3Int(cellPos.x - bounds.xMin, cellPos.y - bounds.yMin, 0);
            if (!CheckGrid(cellPos.x, cellPos.y))
            {
                Debug.Log("cellPos is out of bounds, this should not happen");
                return Vector3.zero;
            }
            Vector2Int gravDir = grid[cellPos.x, cellPos.y].gravityDirection;
            Vector3 feets = GetFeetsFromCenter(GetCenterPosition(worldPos), gravDir);
            return worldPos + (feets - worldPos) * 0.98f;
        }

        private Vector3 GetCenterPosition(Vector3 worldPos)
        {
            Vector3Int cellPos = gravityTileMap.WorldToCell(worldPos);
            cellPos = new Vector3Int(cellPos.x - bounds.xMin, cellPos.y - bounds.yMin, 0);
            if (!CheckGrid(cellPos.x, cellPos.y))
            {
                Debug.Log("cellPos is out of bounds, this should not happen");
                return Vector3.zero;
            }
            Vector2Int gravDir = grid[cellPos.x, cellPos.y].gravityDirection;
            // If parameters.enemyData.entitySize is pair, we need to add half the size of a tile to the world pos to get the center of the tile
            if (parameters.enemyData.entitySize.x % 2 == 0)
            {
                worldPos += new Vector3(0.5f * gravityTileMap.cellSize.x * (-gravDir.y), 0.5f * gravityTileMap.cellSize.y * (gravDir.x), 0);
            }

            // Add to world pos the offset of half the size of a tile in the direction of the gravity
            float offset = (gravityTileMap.cellSize.y / 2 - ((parameters.enemyData.colliderSize.y / 2) % gravityTileMap.cellSize.y) * 1.01f);
            return worldPos + new Vector3(offset * gravDir.x, offset * gravDir.y, 0);
        }

        private Vector3 GetCenterFromFeets(Vector3 feets, Vector2 gravityDirection)
        {
            gravityDirection = gravityDirection == Vector2.zero ? GetTileAtWorldPos(feets).gravityDirection : gravityDirection;
            Vector3 offset = new Vector3(gravityDirection.x, gravityDirection.y, 0) * parameters.enemyData.colliderSize.y / 2;
            return feets - offset * 1.03f;
        }

        private Vector3 GetFeetsFromCenter(Vector3 center, Vector2 gravityDirection)
        {
            gravityDirection = gravityDirection == Vector2.zero ? GetTileAtWorldPos(center).gravityDirection : gravityDirection;
            Vector3 offset = new Vector3(gravityDirection.x, gravityDirection.y, 0) * parameters.enemyData.colliderSize.y / 2;
            return center + offset * 0.99f;
        }

        private Vector3 GetFallPosition(int x, int y, int side, Vector2Int gravDirection)
        {
            var feetPos = GetFeetPosition(gravityTileMap.GetCellCenterWorld(new Vector3Int(x + bounds.min.x, y + bounds.min.y, 0)));
            Vector2Int corner = GetTileAtCorner(x, y, gravDirection.y == 0 ? side : -side).position;
            var cornerPos = gravityTileMap.GetCellCenterWorld(new Vector3Int(corner.x, corner.y, 0));
            cornerPos -= new Vector3(gravDirection.y * side * gravityTileMap.cellSize.x / 2, gravDirection.x * side * gravityTileMap.cellSize.y / 2);
            cornerPos += new Vector3(gravDirection.x * gravityTileMap.cellSize.x / 2, gravDirection.y * gravityTileMap.cellSize.y / 2);
            cornerDebug.Add(cornerPos);
            return GetCenterFromFeets(cornerPos + ((feetPos - cornerPos).normalized * 1.05f) * (parameters.enemyData.colliderSize.x + parameters.colliderSizeBuffer) / 2, gravDirection);
        }

        private bool IsAlreadyVisisted(int x, int y, List<Vector2Int> corners)
        {
            for (int i = 0; i < corners.Count; i += 2)
            {
                Vector2Int corner1 = corners[i];
                Vector2Int corner2 = corners[i + 1];
                int minX = Mathf.Min(corner1.x, corner2.x);
                int maxX = Mathf.Max(corner1.x, corner2.x);
                int minY = Mathf.Min(corner1.y, corner2.y);
                int maxY = Mathf.Max(corner1.y, corner2.y);
                if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckGrid(int x, int y)
        {
            if (x < 0 || x >= grid.GetLength(0) || y < 0 || y >= grid.GetLength(1))
            {
                return false;
            }
            return true;
        }

        private void GenerateGridFromList()
        {
            if (graph != null && graph.tiles != null)
            {
                grid = new Pathfinding.Tile[bounds.max.x - bounds.min.x, bounds.max.y - bounds.min.y];
                foreach (Pathfinding.Tile tile in graph.tiles)
                {
                    grid[tile.position.x - bounds.min.x, tile.position.y - bounds.min.y] = tile;
                }
            }
        }

        private Vector2Int[] GenerateBounds(List<Pathfinding.Tile> tiles, Vector2Int gravityDir)
        {
            if (tiles == null || tiles.Count == 0)
            {
                Debug.Log("tiles null or empty, cannont generate bounds");
                return new Vector2Int[2];
            }
            Vector2Int min = new Vector2Int(tiles[0].position.x, tiles[0].position.y);
            Vector2Int max = new Vector2Int(tiles[0].position.x, tiles[0].position.y);
            foreach (Pathfinding.Tile tile in tiles)
            {
                if (tile.position.x < min.x) min.x = tile.position.x;
                if (tile.position.y < min.y) min.y = tile.position.y;
                if (tile.position.x > max.x) max.x = tile.position.x;
                if (tile.position.y > max.y) max.y = tile.position.y;
            }
            if (gravityDir.y == -1)
            {
                min.y -= parameters.TrajectoriesBoxCheckSize.y;
                if (max.x - min.x < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.x += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.x -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            else if (gravityDir.x == -1)
            {
                min.x -= parameters.TrajectoriesBoxCheckSize.y;
                if (max.y - min.y < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.y += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.y -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            else if (gravityDir.y == 1)
            {
                max.y += parameters.TrajectoriesBoxCheckSize.y;
                if (max.x - min.x < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.x += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.x -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            else if (gravityDir.x == 1)
            {
                max.x += parameters.TrajectoriesBoxCheckSize.y;
                if (max.y - min.y < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.y += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.y -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            min -= new Vector2Int(bounds.min.x, bounds.min.y);
            max -= new Vector2Int(bounds.min.x, bounds.min.y);
            Vector2Int[] generatedBounds = new Vector2Int[2];
            generatedBounds[0] = min;
            generatedBounds[1] = max;
            return generatedBounds;
        }

        private Vector2Int[] GenerateBounds(Vector2Int corner1, Vector2Int corner2, Vector2Int gravityDir)
        {
            Vector2Int min = new Vector2Int(Mathf.Min(corner1.x, corner2.x), Mathf.Min(corner1.y, corner2.y));
            Vector2Int max = new Vector2Int(Mathf.Max(corner1.x, corner2.x), Mathf.Max(corner1.y, corner2.y));

            if (gravityDir.y == -1)
            {
                min.y -= parameters.TrajectoriesBoxCheckSize.y;
                if (max.x - min.x < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.x += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.x -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            else if (gravityDir.x == -1)
            {
                min.x -= parameters.TrajectoriesBoxCheckSize.y;
                if (max.y - min.y < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.y += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.y -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            else if (gravityDir.y == 1)
            {
                max.y += parameters.TrajectoriesBoxCheckSize.y;
                if (max.x - min.x < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.x += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.x -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            else if (gravityDir.x == 1)
            {
                max.x += parameters.TrajectoriesBoxCheckSize.y;
                if (max.y - min.y < parameters.TrajectoriesBoxCheckSize.x)
                {
                    max.y += parameters.TrajectoriesBoxCheckSize.x / 2;
                    min.y -= parameters.TrajectoriesBoxCheckSize.x / 2;
                }
            }
            if (min.x < 0) min = new Vector2Int(0, min.y);
            if (max.x > bounds.max.x - bounds.min.x - 1) max = new Vector2Int(bounds.max.x - bounds.min.x - 1, max.y);
            if (min.y < 0) min = new Vector2Int(min.x, 0);
            if (max.y > bounds.max.y - bounds.min.y - 1) max = new Vector2Int(max.x, bounds.max.y - bounds.min.y - 1);
            // min -= new Vector2Int(bounds.min.x, bounds.min.y);
            // max -= new Vector2Int(bounds.min.x, bounds.min.y);
            Vector2Int[] generatedBounds = new Vector2Int[2];
            generatedBounds[0] = min;
            generatedBounds[1] = max;
            return generatedBounds;
        }

        #endregion

        #region Gizmos
        private void DrawTrajectory(Vector3 launch, Vector2Int gravityDirection, Vector2 additionalAcc, Vector2 initialVelocity, float T)
        {
            Vector3 pos = launch;
            Vector3 lastPos;
            Vector3 a = new Vector3(gravityDirection.x * 10f + additionalAcc.x, gravityDirection.y * 10f + additionalAcc.y, 0f);
            float pas = parameters.trajectoriesCheckInterval * trajectorySimplifications;
            for (float time = 0f; time < T; time += pas)
            {
                lastPos = pos;
                pos = launch + (Vector3)initialVelocity * time + 0.5f * a * time * time;
                Gizmos.DrawLine(lastPos, pos);
            }
            Vector3 finalPos = launch + (Vector3)initialVelocity * T + 0.5f * a * T * T;
            Gizmos.DrawLine(pos, finalPos);
        }

        private Color GenerateRandomColor(float cost)
        {
            float a = Mathf.Sin(2000 * cost);
            float b = Mathf.Sin(1400 * cost);
            float c = Mathf.Sin(1900 * cost);
            float max = Mathf.Max(a, b, c);
            return new Color(a / max, b / max, c / max, 1f);

        }

        void OnDrawGizmos()
        {
            if ((drawFailJumpTrajectory || drawFailFallTrajectories || drawFailMultiGravTrajectories || DrawEdges) && parameters.trajectoriesCheckInterval == 0f)
            {
                Debug.LogError("Trajectories check interval is 0, can't draw trajectories");
                return;
            }
            if ((drawFailJumpTrajectory || drawFailFallTrajectories || drawFailMultiGravTrajectories) && hasBeenLoaded)
            {
                Debug.Log("Can only show trajectories calcul when the graph is created, not when it is loaded");
                drawFailJumpTrajectory = false;
                drawFailFallTrajectories = false;
                drawFailMultiGravTrajectories = false;
            }
            else
            {
                int trajIndex = (int)(ShowedTrajectoryHeight * (parameters.NumberOfTestedTrajectories - 1));

                if (collidingPointsListDebug != null && drawFailJumpTrajectory && collidingPointsListDebug.Count >= trajIndex - 1)
                {
                    Gizmos.color = Color.blue;
                    foreach (var point in collidingPointsListDebug[trajIndex])
                    {
                        Gizmos.DrawWireSphere(point, 0.1f);
                    }
                }
                if (JumpTrajectoriesDebug != null && drawFailJumpTrajectory && JumpTrajectoriesDebug.Count >= trajIndex - 1)
                {
                    Gizmos.color = Color.red;
                    foreach (var waypoint in JumpTrajectoriesDebug[trajIndex])
                    {
                        DrawTrajectory(waypoint.position, waypoint.gravityDirection, waypoint.acceleration, waypoint.initialSpeed, waypoint.time);
                    }
                }
                if (FallTrajectoriesDebug != null && drawFailFallTrajectories)
                {
                    Gizmos.color = Color.red;
                    foreach (var waypoint in FallTrajectoriesDebug)
                    {
                        DrawTrajectory(waypoint.position, waypoint.gravityDirection, waypoint.acceleration, waypoint.initialSpeed, waypoint.time);
                    }
                }
                if (RemovedTrajectoriesDebug != null && drawRemovedTrajectories)
                {
                    Gizmos.color = Color.red;
                    foreach (var waypoint in RemovedTrajectoriesDebug)
                    {
                        DrawTrajectory(waypoint.position, waypoint.gravityDirection, waypoint.acceleration, waypoint.initialSpeed, waypoint.time);
                    }
                }
                if (fitsPointsDebug != null && drawFailFallTrajectories)
                {
                    Gizmos.color = Color.green;
                    foreach (var point in fitsPointsDebug)
                    {
                        Gizmos.DrawSphere(point, 0.03f);
                    }
                }
                if (MultiGravTrajectoriesDebug != null && drawFailMultiGravTrajectories)
                {
                    Gizmos.color = Color.red;
                    foreach (var waypoint in MultiGravTrajectoriesDebug)
                    {
                        DrawTrajectory(waypoint.position, waypoint.gravityDirection, waypoint.acceleration, waypoint.initialSpeed, waypoint.time);
                    }
                }
                if (MultiGravTrajectoriesValidDebug != null && drawFailMultiGravTrajectories)
                {
                    Gizmos.color = new Color(1f, 0.8f, 0f, 1f);
                    foreach (var waypoint in MultiGravTrajectoriesValidDebug)
                    {
                        DrawTrajectory(waypoint.position, waypoint.gravityDirection, waypoint.acceleration, waypoint.initialSpeed, waypoint.time);
                    }
                }
                if (debugCollisionPoints != null && drawFailMultiGravTrajectories)
                {
                    Gizmos.color = Color.red;
                    foreach (var point in debugCollisionPoints)
                    {
                        Gizmos.DrawSphere(point, 0.01f);
                    }
                }
            }

            if (cornerDebug != null && DrawGizmos)
            {
                Gizmos.color = Color.green;

                foreach (var point in cornerDebug)
                {
                    Gizmos.DrawSphere(point, 0.3f);
                }
            }
            if (trnsitionDebug != null && DrawGizmos)
            {
                Gizmos.color = Color.blue;
                foreach (var point in trnsitionDebug)
                {
                    Gizmos.DrawSphere(gravityTileMap.GetCellCenterWorld(new Vector3Int(point.position.x, point.position.y, 0)), 0.3f);
                }
            }
            Gizmos.color = Color.green;
            if (graph != null)
            {
                if (graph.tiles != null && gravityTileMap != null)
                {
                    Color voidColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
                    Color obstacleColor = new Color(1f, 0f, 0f, 0.03f);
                    Color walkableColor = new Color(0f, 1f, 0f, 0.3f);
                    Color emptyColor = new Color(1f, 1f, 0f, 0.03f);
                    foreach (var tile in graph.tiles)
                    {
                        if (tile != null)
                        {
                            if (DrawGizmos)
                            {
                                switch (tile.type)
                                {
                                    case TileType.Void:
                                        Gizmos.color = voidColor;
                                        break;
                                    case TileType.Obstacle:
                                        Gizmos.color = obstacleColor;
                                        Gizmos.DrawWireCube(gravityTileMap.GetCellCenterWorld((Vector3Int)tile.position), new Vector3(0.98f, 0.98f, 1));
                                        break;
                                    case TileType.Walkable:
                                        Gizmos.color = walkableColor;
                                        Gizmos.DrawWireCube(gravityTileMap.GetCellCenterWorld((Vector3Int)tile.position), new Vector3(0.98f, 0.98f, 1));
                                        break;
                                    case TileType.Empty:
                                        Gizmos.color = emptyColor;
                                        break;
                                    default:
                                        Gizmos.color = Color.white;
                                        break;

                                }
                                if (tile.platformIndex != -1)
                                {
                                    Handles.Label(gravityTileMap.GetCellCenterWorld((Vector3Int)tile.position), tile.platformIndex.ToString());
                                }
                            }

                            if (tile.type == TileType.Walkable && DrawEdges && grid != null)
                            {
                                if (tile.edges != null)
                                {
                                    foreach (var edge in tile.edges)
                                    {
                                        Gizmos.color = GenerateRandomColor(edge.cost);

                                        if (edge.type == EdgeType.Jump || edge.type == EdgeType.Fall)
                                        {
                                            for (int i = 0; i < edge.waypoints.Count; i++)
                                            {
                                                if (edge.type == EdgeType.Fall)
                                                {
                                                    Gizmos.color = new Color(1f, 0.7f, 0f, 1f);
                                                }

                                                DrawTrajectory(edge.waypoints[i].position, edge.waypoints[i].gravityDirection, edge.waypoints[i].acceleration, edge.waypoints[i].initialSpeed, edge.waypoints[i].time);
                                                if (i > 0)
                                                {
                                                    Gizmos.color = new Color(0f, 0.8f, 0.9f, 1f);
                                                    Gizmos.DrawSphere(edge.waypoints[i].position, 0.05f);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Gizmos.color = new Color(0f, 1f, 0f, 1f);
                                            Vector3 pos1 = GetCenterPosition(gravityTileMap.GetCellCenterWorld((Vector3Int)tile.position));
                                            Vector3 pos2 = GetCenterPosition(gravityTileMap.GetCellCenterWorld((Vector3Int)edge.targetPos));

                                            Gizmos.DrawLine(pos1, pos2);
                                        }

                                    }
                                }
                            }
                        }
                    }
                    if (drawEnemyBoxes && grid != null && grid[EnemyDrawPosition.x, EnemyDrawPosition.y].gravityDirection != Vector2Int.zero)
                    {
                        if (grid[EnemyDrawPosition.x, EnemyDrawPosition.y].gravityDirection != Vector2Int.zero)
                        {
                            if (agentSizeTileDebug != null && agentSizeTileDebug.Count > 0)
                            {
                                Gizmos.color = Color.red;
                                foreach (var tile in agentSizeTileDebug)
                                {
                                    Gizmos.DrawWireCube(gravityTileMap.GetCellCenterWorld((Vector3Int)tile.position), new Vector3(0.98f, 0.98f, 1));
                                }
                                Gizmos.color = Color.green;
                                Gizmos.DrawWireCube(gravityTileMap.GetCellCenterWorld(new Vector3Int(EnemyDrawPosition.x + bounds.min.x, EnemyDrawPosition.y + bounds.min.y, 0)), new Vector3(0.98f, 0.98f, 1));
                            }
                            if (agentGroundTilesDebug != null)
                            {
                                Gizmos.color = Color.blue;
                                foreach (var tile in agentGroundTilesDebug)
                                {
                                    Gizmos.DrawWireCube(gravityTileMap.GetCellCenterWorld((Vector3Int)tile.position), new Vector3(0.98f, 0.98f, 1));
                                }
                            }
                            Vector2 gravDirection = (Vector2)grid[EnemyDrawPosition.x, EnemyDrawPosition.y].gravityDirection;
                            Vector2 collider = parameters.enemyData.colliderSize;
                            Gizmos.color = Color.yellow;
                            Vector2 centerPos = GetCenterPosition(gravityTileMap.GetCellCenterWorld((Vector3Int)grid[EnemyDrawPosition.x, EnemyDrawPosition.y].position));
                            Gizmos.DrawWireCube((Vector3)centerPos, new Vector3(Mathf.Abs(collider.x * gravDirection.y + collider.y * gravDirection.x), Mathf.Abs(collider.y * gravDirection.y + collider.x * gravDirection.x), 1));

                            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
                            collider = new Vector2(parameters.enemyData.colliderSize.x + parameters.colliderSizeBuffer, parameters.enemyData.colliderSize.y + parameters.colliderSizeBuffer / 2);
                            centerPos = centerPos - new Vector2(gravDirection.x * parameters.colliderSizeBuffer / 4, gravDirection.y * parameters.colliderSizeBuffer / 4);
                            Gizmos.DrawWireCube((Vector3)centerPos, new Vector3(Mathf.Abs(collider.x * gravDirection.y + collider.y * gravDirection.x), Mathf.Abs(collider.y * gravDirection.y + collider.x * gravDirection.x), 1));
                        }
                        else
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireCube(gravityTileMap.GetCellCenterWorld((Vector3Int)grid[EnemyDrawPosition.x, EnemyDrawPosition.y].position), new Vector3(0.98f, 0.98f, 1));
                        }
                    }
                }
            }
        }
        #endregion
        #endregion
        #endif
    }
}
