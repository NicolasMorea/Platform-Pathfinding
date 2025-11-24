/// <summary>
///* The component handling the pathfinding algorithm at runtime
/// </summary>

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using Entity.Enemy;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Entity.Pathfinding
{
    public enum PTBehaviour
    {
        GoToTarget,
        RunAwayFromTarget,
    }

    public class PathfinderHandler : MonoBehaviour
    {
        [Header("Needed Components")]
        [SerializeField] private PathfindingGraph graph;
        [SerializeField] private Tilemap gravityTilemap;
        [SerializeField] private Transform agent;
        [SerializeField] private Transform target;
        [SerializeField] private EnemyHandler context;
        private Rigidbody2D RB;

        [Header("Runtime behaviour")]
        public bool tryFindPath = false;
        [SerializeField] private PTBehaviour behaviour;
        [SerializeField] private List<Edge> path;
        private Edge currentEdge;
        private List<Task> tasks;
        private Pathfinding.Tile[,] grid;
        private PathTile[,] costGrid;
        [SerializeField] private Vector2 nearWaypointThreshold;
        [SerializeField] private Vector2 nearWaypointAirThreshold;

        [Header("RunAway")]
        [SerializeField] private int runAwayDistance;
        [SerializeField] private int runAwayTresholdDistance;

        [Header("Debug")]
        [SerializeField] private bool debugPathFind = false;
        [SerializeField] private bool drawPath = false;
        [SerializeField] private bool drawExploredTiles = false;
        private Vector3Int unityAgentCell;
        private Vector3Int unityTargetCell;
        private Vector3Int LastUnityObjective;
        private Vector3Int lastUnityTargetCell;
        private Vector3Int lastUnityAgentCell;
        private BoundsInt bounds;
        private List<Vector2Int> exploredTilesDebug;
        private Vector2 accellToAdd = Vector2.zero;
        private bool newPath = false;
        private bool success = false;

        #region Callbacks
        private void Awake()
        {
            RB = context.RB;
            if (!CheckParam())
            {
                Debug.Log("Cannot find path because components are not correct");
                gameObject.SetActive(false);
            }
            else
            {
                GenerateGridFromList();
            }

            ResetPF();
        }

        public void SetTarget(Transform _target)
        {
            target = _target;
        }  

        private void Update()
        {
            if (tryFindPath)
            {
                unityTargetCell = GetUnityTargetCell(target.position);
                unityAgentCell = gravityTilemap.WorldToCell(agent.position);
                GetPath();

                if (newPath && path.Count > 0)
                {
                    newPath = false;
                    ProcessEdge(path[0]);
                }

                ProcessTask();
                lastUnityAgentCell = unityAgentCell;
                lastUnityTargetCell = unityTargetCell;
            }
        }

        void FixedUpdate()
        {
            if (tasks.Count > 0)
            {
                tasks[0].OnFixedUpdate();
            }
        }

        #endregion

        #region Movement
        public void WalkTo(Vector3 _target)
        {
            context.HorizontalVelocity = graph.parameters.enemyData.awareSpeed * Vector3.Dot(transform.right, (_target - agent.position).normalized);
        }

        public void SetVelocity(Vector2 _velocity)
        {
            RB.linearVelocity = _velocity;
        }

        public void SetAccelleration(Vector2 _accelleration)
        {
            RB.AddForce(_accelleration * RB.mass, ForceMode2D.Force);
        }

        public void Teleport(Vector3 _target)
        {
            Debug.Log("Teleport");
            agent.position = _target;
        }

        public bool IsNearX(Vector3 _target, bool isInAir = false)
        {
            if (isInAir)
            {
                return Mathf.Abs(Vector2.Dot((Vector2)(agent.position - _target), transform.right)) <= nearWaypointAirThreshold.x;
            }
            return Mathf.Abs(Vector2.Dot((Vector2)(agent.position - _target), transform.right)) <= nearWaypointThreshold.x;
        }

        public bool IsNearY(Vector3 _target, bool isInAir = false)
        {
            if (isInAir)
            {
                return Mathf.Abs(Vector2.Dot((Vector2)(agent.position - _target), transform.up)) <= nearWaypointAirThreshold.y;
            }
            return Mathf.Abs(Vector2.Dot((Vector2)(agent.position - _target),  transform.up)) <= nearWaypointThreshold.y;
        }

        public void OnTrajectoryStart(Vector2 originPos, Vector2 velocity)
        {
            Teleport(originPos);
            if (IsNearX(originPos, true) && IsNearY(originPos, true))
            {
                Debug.Log("Teleporting to : " + originPos + " and set velocity : " + velocity);
            }
            SetVelocity(velocity);
        }

        public void OnTrajectoryEnd()
        {
        }

        #endregion

        #region Beheviours
        private bool GetPath()
        {
            switch (behaviour)
            {
                case PTBehaviour.GoToTarget:
                    return TrySeekFollowPath();
                case PTBehaviour.RunAwayFromTarget:
                    return TrySeekAwayPath();
            }
            return false;
        }
        private bool TrySeekFollowPath()
        {
            if (context.IsGrounded)
            {
                // check if the agent reached the target
                if (unityAgentCell == unityTargetCell)
                {
                    path.Clear();
                    if (!success)
                    {
                        if (debugPathFind) Debug.Log("Pathfind success, agent " + agent.gameObject.name + " reached target");
                        success = true;
                    }
                    SetVelocity(Vector2.zero);
                    return true;
                }
                else if (success)
                {
                    success = false;
                }

                // if the target and the agent cells are walkable, we can seek a path
                if (GetTileType(unityTargetCell) == TileType.Walkable)
                {
                    LastUnityObjective = unityTargetCell;
                    // mustProcessAlone = false;
                    if (lastUnityAgentCell != unityAgentCell || lastUnityTargetCell != unityTargetCell)
                    {
                        return SetPath(PathToTarget());
                    }
                    return false;
                }
                else
                {
                    if (IsCellValid(LastUnityObjective) && LastUnityObjective == unityTargetCell)
                    {
                        if (debugPathFind) Debug.Log("agent " + agent.gameObject.name + " reached the last known position of the targetn partial success");
                        SetVelocity(Vector2.zero);
                        success = true;
                        path.Clear();
                        return true;
                    }
                    return false;
                }
            }
            else
            {
                Debug.Log("not grounded");
            }
            return false;
        }
        private bool TrySeekAwayPath()
        {
            if (context.IsGrounded && (Mathf.Abs(unityAgentCell.x - unityTargetCell.x) + Mathf.Abs(unityAgentCell.y - unityTargetCell.y)) < runAwayTresholdDistance)
            {
                if (unityAgentCell == unityTargetCell)
                {
                    path.Clear();
                    if (!success)
                    {
                        Debug.Log("error");
                        success = true;
                    }
                    SetVelocity(Vector2.zero);
                    return true;
                }
                else if (success)
                {
                    success = false;
                }
                if (grid[unityTargetCell.x - bounds.min.x, unityTargetCell.y - bounds.min.y].type == TileType.Walkable)
                {
                    LastUnityObjective = unityTargetCell;
                    if (lastUnityAgentCell != unityAgentCell || lastUnityTargetCell != unityTargetCell)
                    {
                        return SetPath(PathAwayFromTarget());
                    }
                }
                else
                {
                    if (LastUnityObjective == unityTargetCell)
                    {
                        success = true;
                        Debug.Log("partial success");
                        SetVelocity(Vector2.zero);
                        path.Clear();
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion

        #region RunTime

        private bool SetPath(List<Edge> _path)
        {
            if (_path.Count > 0)
            {
                if (debugPathFind) Debug.Log("set new path with " + _path.Count + " edges");
                newPath = true;
                path = _path;
                return true;
            }
            if (debugPathFind) Debug.Log("path computed is empty, no new path");
            return false;
        }
        private void Unstuck()
        {
            //check if there is a walkable tile to the right or the left
            //if there is, move to it
            Vector2 rightDir = transform.right;
            Vector3Int pos = unityAgentCell + new Vector3Int(Mathf.RoundToInt(rightDir.x), Mathf.RoundToInt(rightDir.y), 0);
            if (grid[pos.x - bounds.min.x, pos.y - bounds.min.y].type == TileType.Walkable)
            {
                tasks.Add(new WalkTask(this, gravityTilemap.GetCellCenterWorld(pos), true));
                return;
            }
            pos = unityAgentCell - new Vector3Int(Mathf.RoundToInt(rightDir.x), Mathf.RoundToInt(rightDir.y), 0);
            if (grid[pos.x - bounds.min.x, pos.y - bounds.min.y].type == TileType.Walkable)
            {
                tasks.Add(new WalkTask(this, gravityTilemap.GetCellCenterWorld(pos), true));
                return;
            }

        }

        private Vector3Int GetUnityTargetCell(Vector3 pos)
        {
            Vector2Int gravityDir = Vector2Int.zero;
            Vector3Int cell = gravityTilemap.WorldToCell(pos);
            Vector3Int currentCell;
            for (int i = 0; i < 3; i++)
            {
                if (i == 0)
                {
                    gravityDir = grid[cell.x - bounds.min.x, cell.y - bounds.min.y].gravityDirection;
                }
                currentCell = cell - (Vector3Int)gravityDir * i;
                Vector3Int gridCell = UnityToGrid(currentCell);
                if (grid[gridCell.x, gridCell.y].type == TileType.Walkable && grid[gridCell.x, gridCell.y].gravityDirection == gravityDir)
                {
                    return currentCell;
                }
            }
            return cell;
        }

        public void AbortTaks()
        {
            Debug.Log("Aborting tasks");
            tasks.Clear();
        }

        private void ProcessTask()
        {
            if (tasks.Count > 0)
            {
                tasks[0].Execute();
                if (tasks[0].IsOver())
                {
                    if (tasks.Count > 0)
                    {
                        tasks.RemoveAt(0);
                        if (tasks.Count > 0)
                        {
                            tasks[0].OnEntry();
                        }
                    }
                    if (tasks.Count == 0)
                    {
                        // mustProcessAlone = false;
                        if (path.Count > 0)
                        {
                            path.RemoveAt(0);
                            newPath = true;
                        }
                    }
                }
            }
        }

        private void ProcessEdge(Edge edge)
        {
            if (edge != currentEdge)
            {
                // Debug.Log("New edge");
                currentEdge = edge;
                tasks.Clear();
                if (edge.type == EdgeType.Walk)
                {
                    tasks.Add(new WalkTask(this, gravityTilemap.GetCellCenterWorld(new Vector3Int(edge.targetPos.x, edge.targetPos.y, 0)), true));
                }
                else if (edge.type == EdgeType.Jump || edge.type == EdgeType.Fall)
                {
                    tasks.Add(new WalkTask(this, edge.waypoints[0].position, true));
                    for (int i = 0; i < edge.waypoints.Count; i++)
                    {
                        if (i == edge.waypoints.Count - 1)
                        {
                            tasks.Add(new TrajectoryTask(this, edge.waypoints[i].initialSpeed, edge.waypoints[i].acceleration, edge.waypoints[i].position, gravityTilemap.GetCellCenterWorld(new Vector3Int(edge.targetPos.x, edge.targetPos.y, 0)), edge.waypoints[i].time));
                        }
                        else
                        {
                            tasks.Add(new TrajectoryTask(this, edge.waypoints[i].initialSpeed, edge.waypoints[i].acceleration, edge.waypoints[i].position, edge.waypoints[i + 1].position, edge.waypoints[i].time));
                        }
                    }
                }
                tasks[0].OnEntry();
            }
        }

        private void GenerateGridFromList()
        {
            bounds = gravityTilemap.cellBounds;
            if (graph != null && graph.tiles != null)
            {
                grid = new Pathfinding.Tile[bounds.max.x - bounds.min.x, bounds.max.y - bounds.min.y];
                foreach (Pathfinding.Tile tile in graph.tiles)
                {
                    grid[tile.position.x - bounds.min.x, tile.position.y - bounds.min.y] = tile;
                }
            }
            else
            {
                Debug.LogError("Graph or tiles are null");
                gameObject.SetActive(false);
            }
        }

        private void ResetPF()
        {
            tasks = new List<Task>();
            if (path != null) path.Clear();
            if (exploredTilesDebug != null) exploredTilesDebug.Clear();
            currentEdge = null;
            LastUnityObjective = new Vector3Int(-1, -1, -1);
            unityTargetCell = new Vector3Int(-1, -1, -1);
            unityAgentCell = new Vector3Int(-1, -1, -1);
            lastUnityAgentCell = new Vector3Int(-1, -1, -1);
            lastUnityTargetCell = new Vector3Int(-1, -1, -1);
            success = false;
            newPath = false;
        }

        #endregion

        #region PathFinding

        private List<Edge> PathToTarget()
        {
            Vector3Int gridAgentCell = UnityToGrid(unityAgentCell);
            Vector3Int gridTargetCell = UnityToGrid(unityTargetCell);
            if (grid[gridAgentCell.x, gridAgentCell.y].type == TileType.Obstacle || grid[gridTargetCell.x, gridTargetCell.y].type == TileType.Obstacle)
            {
                if (debugPathFind) Debug.Log("Agent or target is on an obstacle, can't find path");
                return new List<Edge>();
            }
            else if (grid[gridAgentCell.x, gridAgentCell.y].type != TileType.Walkable || grid[gridTargetCell.x, gridTargetCell.y].type != TileType.Walkable)
            {
                if (debugPathFind) Debug.Log("Agent or target is not on a walkable tile, can't find path");
                return new List<Edge>();
            }
            else
            {
                exploredTilesDebug = new List<Vector2Int>();
                costGrid = new PathTile[grid.GetLength(0), grid.GetLength(1)];
                List<Edge> currentPath = new List<Edge>();
                var activeTiles = new List<PathTile>();
                costGrid[gridAgentCell.x, gridAgentCell.y] = new PathTile((Vector2Int)gridAgentCell, (Vector2Int)gridAgentCell, 0);
                costGrid[gridAgentCell.x, gridAgentCell.y].SetDistance((Vector2Int)gridTargetCell);
                activeTiles.Add(costGrid[gridAgentCell.x, gridAgentCell.y]);

                var visitedTiles = new List<PathTile>();
                while (activeTiles.Any())
                {
                    var checkTile = activeTiles.OrderBy(x => x.totalCost).First();
                    if (checkTile.gridPos == (Vector2Int)gridTargetCell)
                    {
                        var currentTile = checkTile;
                        while (currentTile.gridPos != (Vector2Int)gridAgentCell)
                        {
                            currentPath.Add(currentTile.edge);
                            currentTile = costGrid[currentTile.parentGridPos.x, currentTile.parentGridPos.y];
                        }
                        newPath = true;
                        return currentPath.Reverse<Edge>().ToList();
                    }
                    visitedTiles.Add(checkTile);
                    activeTiles.Remove(checkTile);
                    foreach (PathTile neighboor in GetNeighboors(checkTile))
                    {
                        if (visitedTiles.Any(x => x.gridPos == neighboor.gridPos))
                        {
                            continue;
                        }
                        if (activeTiles.Any(x => x.gridPos == neighboor.gridPos))
                        {
                            var existingTile = activeTiles.First(x => x.gridPos == neighboor.gridPos);
                            if (existingTile.cost > neighboor.cost)
                            {
                                activeTiles.Remove(existingTile);
                                activeTiles.Add(neighboor);
                                costGrid[neighboor.gridPos.x, neighboor.gridPos.y] = neighboor;
                            }
                        }
                        else
                        {
                            costGrid[neighboor.gridPos.x, neighboor.gridPos.y] = neighboor;
                            activeTiles.Add(neighboor);
                            exploredTilesDebug.Add(neighboor.gridPos);
                        }
                    }
                }
                if (debugPathFind) Debug.Log("No path found");
                return new List<Edge>();
            }
        }

        private List<Edge> PathAwayFromTarget()
        {
            // Debug.Log("Path away from target");
            Vector3Int gridAgentCell = UnityToGrid(unityAgentCell);
            Vector3Int gridTargetCell = UnityToGrid(unityTargetCell);
            List<Edge> currentPath = new List<Edge>();
            exploredTilesDebug = new List<Vector2Int>();
            costGrid = new PathTile[grid.GetLength(0), grid.GetLength(1)];
            if (grid[gridAgentCell.x, gridAgentCell.y].type == TileType.Obstacle || grid[gridTargetCell.x, gridTargetCell.y].type == TileType.Obstacle)
            {
                return currentPath;
            }
            else if (grid[gridAgentCell.x, gridAgentCell.y].type != TileType.Walkable || grid[gridTargetCell.x, gridTargetCell.y].type != TileType.Walkable)
            {
                return currentPath;
            }
            else
            {
                var activeTiles = new List<PathTile>();
                costGrid[gridAgentCell.x, gridAgentCell.y] = new PathTile((Vector2Int)gridAgentCell, (Vector2Int)gridAgentCell, 0);
                costGrid[gridAgentCell.x, gridAgentCell.y].SetDistance((Vector2Int)gridTargetCell);
                activeTiles.Add(costGrid[gridAgentCell.x, gridAgentCell.y]);

                var visitedTiles = new List<PathTile>();
                while (activeTiles.Any())
                {
                    var checkTile = activeTiles.OrderBy(x => x.inversedCost).First();
                    if (Mathf.Abs(checkTile.gridPos.x - gridTargetCell.x) + Mathf.Abs(checkTile.gridPos.y - gridTargetCell.y) > runAwayDistance)
                    {
                        var currentTile = checkTile;
                        while (currentTile.gridPos != (Vector2Int)gridAgentCell)
                        {
                            currentPath.Add(currentTile.edge);
                            currentTile = costGrid[currentTile.parentGridPos.x, currentTile.parentGridPos.y];
                        }
                        newPath = true;
                        return currentPath.Reverse<Edge>().ToList();
                    }
                    visitedTiles.Add(checkTile);
                    activeTiles.Remove(checkTile);
                    foreach (PathTile neighboor in GetNeighboors(checkTile))
                    {
                        if (neighboor.gridPos == (Vector2Int)gridTargetCell)
                        {
                            continue;
                        }
                        if (visitedTiles.Any(x => x.gridPos == neighboor.gridPos))
                        {
                            continue;
                        }
                        if (activeTiles.Any(x => x.gridPos == neighboor.gridPos))
                        {
                            var existingTile = activeTiles.First(x => x.gridPos == neighboor.gridPos);
                            if (existingTile.cost > neighboor.cost)
                            {
                                activeTiles.Remove(existingTile);
                                activeTiles.Add(neighboor);
                                costGrid[neighboor.gridPos.x, neighboor.gridPos.y] = neighboor;
                            }
                        }
                        else
                        {
                            costGrid[neighboor.gridPos.x, neighboor.gridPos.y] = neighboor;
                            activeTiles.Add(neighboor);
                            exploredTilesDebug.Add(neighboor.gridPos);
                        }
                    }
                }
                var bestTile = visitedTiles.OrderBy(x => x.distance).Last();
                while (bestTile.gridPos != (Vector2Int)gridAgentCell)
                {
                    currentPath.Add(bestTile.edge);
                    bestTile = costGrid[bestTile.parentGridPos.x, bestTile.parentGridPos.y];
                }
                newPath = true;
                return currentPath.Reverse<Edge>().ToList();
            }
        }

        private List<PathTile> GetNeighboors(PathTile tile)
        {
            var neighboors = new List<PathTile>();
            if (grid[tile.gridPos.x, tile.gridPos.y].edges != null)
            {
                foreach (Edge edge in grid[tile.gridPos.x, tile.gridPos.y].edges)
                {
                    Vector2Int pos = UnityToGrid2(edge.targetPos);
                    PathTile newTile = new PathTile(pos, tile.gridPos, tile.cost + edge.cost, edge);
                    newTile.SetDistance((Vector2Int)unityTargetCell);
                    neighboors.Add(newTile);
                }
            }
            return neighboors;
        }

        #endregion


        #region Utility

        private bool CheckParam()
        {
            if (graph == null)
            {
                Debug.LogError("Graph is null");
                return false;
            }
            if (graph.tiles == null)
            {
                Debug.LogError("Graph tiles is null");
                return false;
            }
            if (gravityTilemap == null)
            {
                Debug.LogError("Gravity tilemap is null");
                return false;
            }
            if (agent == null)
            {
                Debug.LogError("Agent is null");
                return false;
            }
            if (target == null)
            {
                Debug.LogWarning("Target is null");
                return false;
            }
            if (graph.parameters == null)
            {
                Debug.LogWarning("Graph parameters are null");
                return false;
            }
            if (graph.parameters.enemyData == null)
            {
                Debug.LogWarning("Enemy data is null");
                return false;
            }
            if (nearWaypointThreshold.x == 0 || nearWaypointThreshold.y == 0)
            {
                Debug.LogWarning("Near waypoint treshold has a 0 value");
            }
            // check if application is playing, if so check the runtime parameters

            if (Application.isPlaying)
            {
                if (RB == null)
                {
                    Debug.LogError("Rigidbody in pathfind handler in +" + gameObject.name + " is null");
                    return false;
                }
                if (graph.parameters.enemyData != context.enemyData)
                {
                    Debug.LogWarning("Enemy data is not the same in graph and tree");
                }
            }

            return true;
        }

        public void PathButton()
        {
            if (CheckParam())
            {
                GenerateGridFromList();
                unityTargetCell = GetUnityTargetCell(target.position);
                unityAgentCell = gravityTilemap.WorldToCell(agent.position);
                if (GetPath())
                {
                    Debug.Log("Path found");
                }
                else
                {
                    Debug.Log("No path found");
                }
            }
            else
            {
                Debug.Log("Missing or uncorrect parameters, can't compute path");
            }
        }

        private Vector3 TrajectoryPosition(float t, Vector3 launchVelocity, Vector3 a, Vector3 lauch)
        {
            return lauch + launchVelocity * t + a * t * t / 2.0f;
        }

        private TileType GetTileType(Vector3Int unityCellPos)
        {
            if (IsCellValid(unityCellPos) == false)
            {
                return TileType.Empty;
            }
            if (grid == null)
            {
                if (debugPathFind) Debug.LogWarning("trying to get a cell type but the grid is null");
                return TileType.Empty;
            }
            if (unityCellPos.x < bounds.min.x || unityCellPos.x >= bounds.max.x || unityCellPos.y < bounds.min.y || unityCellPos.y >= bounds.max.y)
            {
                return TileType.Empty;
            }
            Vector3Int gridPos = new Vector3Int(unityCellPos.x - bounds.min.x, unityCellPos.y - bounds.min.y, 0);
            if (grid[gridPos.x, gridPos.y] == null)
            {
                return TileType.Empty;
            }
            return grid[gridPos.x, gridPos.y].type;
        }

        private Vector3Int UnityToGrid(Vector3Int unityCellPos)
        {
            return new Vector3Int(unityCellPos.x - bounds.min.x, unityCellPos.y - bounds.min.y, 0);
        }
        private Vector3Int GridToUnity(Vector3Int gridCellPos)
        {
            return new Vector3Int(gridCellPos.x + bounds.min.x, gridCellPos.y + bounds.min.y, 0);
        }
        private Vector2Int UnityToGrid2(Vector3Int unityCellPos)
        {
            return new Vector2Int(unityCellPos.x - bounds.min.x, unityCellPos.y - bounds.min.y);
        }
        private Vector2Int GridToUnity2(Vector3Int gridCellPos)
        {
            return new Vector2Int(gridCellPos.x + bounds.min.x, gridCellPos.y + bounds.min.y);
        }
        private Vector2Int UnityToGrid2(Vector2Int unityCellPos)
        {
            return new Vector2Int(unityCellPos.x - bounds.min.x, unityCellPos.y - bounds.min.y);
        }
        private Vector2Int GridToUnity2(Vector2Int gridCellPos)
        {
            return new Vector2Int(gridCellPos.x + bounds.min.x, gridCellPos.y + bounds.min.y);
        }
        private bool IsCellValid(Vector3Int cell)
        {
            // z value is not zero if the cel is not initialized so it works
            return cell.z != -1 && cell.x != -1 && cell.y != -1;
        }

        #endregion

        #region Gizmos
        private void DrawTrajectory(Vector3 launch, Vector2Int gravityDirection, Vector2 additionalAcc, Vector2 initialVelocity, float T)
        {
            Vector3 pos = launch;
            Vector3 lastPos;
            Vector3 a = new Vector3(gravityDirection.x * 10f + additionalAcc.x, gravityDirection.y * 10f + additionalAcc.y, 0f);
            for (float t = 0f; t < T; t += 0.06f)
            {
                lastPos = pos;
                pos = launch + (Vector3)initialVelocity * t + 0.5f * a * t * t;
                Gizmos.DrawLine(lastPos, pos);
            }
            Vector3 finalPos = launch + (Vector3)initialVelocity * T + 0.5f * a * T * T;
            Gizmos.DrawLine(pos, finalPos);
        }
        private void OnDrawGizmos()
        {
            if (drawPath && CheckParam())
            {
                if (!Application.isPlaying)
                {
                    if (path != null)
                    {
                        Gizmos.color = Color.green;
                    }
                    else
                    {
                        Gizmos.color = Color.red;
                    }
                    Gizmos.DrawWireSphere(gravityTilemap.GetCellCenterWorld(new Vector3Int(unityAgentCell.x, unityAgentCell.y, 0)), 0.5f);
                    Gizmos.DrawWireSphere(gravityTilemap.GetCellCenterWorld(new Vector3Int(unityTargetCell.x, unityTargetCell.y, 0)), 0.5f);

                }
                if (exploredTilesDebug != null && drawExploredTiles)
                {
                    int i = 0;
                    foreach (Vector2Int pos in exploredTilesDebug)
                    {
                        // Gizmos.DrawWireSphere(gravityTilemap.GetCellCenterWorld((Vector3Int)grid[pos.x, pos.y].position), 0.2f);
#if UNITY_EDITOR
                        Handles.Label(gravityTilemap.GetCellCenterWorld((Vector3Int)grid[pos.x, pos.y].position), i.ToString());
#endif
                        i++;
                    }
                }
                if (path != null)
                {
                    Gizmos.color = Color.green;
                    foreach (Edge edge in path)
                    {
                        if (edge == null || bounds == null || grid == null || gravityTilemap == null)
                        {
                            return;
                        }

                        if (edge.type == EdgeType.Walk)
                        {
                            Gizmos.DrawLine(gravityTilemap.GetCellCenterWorld((Vector3Int)grid[edge.sourcePos.x - bounds.min.x, edge.sourcePos.y - bounds.min.y].position), gravityTilemap.GetCellCenterWorld((Vector3Int)grid[edge.targetPos.x - bounds.min.x, edge.targetPos.y - bounds.min.y].position));
                        }
                        else
                        {
                            for (int i = 0; i < edge.waypoints.Count; i++)
                            {
                                DrawTrajectory(edge.waypoints[i].position, edge.waypoints[i].gravityDirection, edge.waypoints[i].acceleration, edge.waypoints[i].initialSpeed, edge.waypoints[i].time);
                                if (i > 0)
                                {
                                    Gizmos.color = new Color(0f, 0.8f, 0.9f, 1f);
                                    Gizmos.DrawSphere(edge.waypoints[i].position, 0.1f);
                                }
                            }
                        }
                    }
                }
            }

            if (Application.isPlaying && debugPathFind && tryFindPath)
            {
                if (path != null && context.IsGrounded && GetTileType(unityAgentCell) == TileType.Walkable && IsCellValid(unityAgentCell))
                {
                    Gizmos.color = Color.green;
                }
                else
                {
                    Gizmos.color = Color.red;
                }
                Gizmos.DrawWireSphere(gravityTilemap.GetCellCenterWorld(new Vector3Int(unityAgentCell.x, unityAgentCell.y, 0)), 0.5f);
                if (path != null && GetTileType(unityTargetCell) == TileType.Walkable && IsCellValid(unityTargetCell))
                {
                    Gizmos.color = Color.green;
                }
                else
                {
                    Gizmos.color = Color.red;
                }
                Gizmos.DrawWireSphere(gravityTilemap.GetCellCenterWorld(new Vector3Int(unityTargetCell.x, unityTargetCell.y, 0)), 0.5f);
#if UNITY_EDITOR
                if (tasks.Count > 0)
                {
                    Handles.Label(agent.transform.position, tasks[0].GetType().ToString());
                }
#endif
            }
        }

        #endregion
    }


#if UNITY_EDITOR
    [CustomEditor(typeof(PathfinderHandler))]
    public class customPathFinderEditor : Editor

    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            PathfinderHandler pathfinding = (PathfinderHandler)target;
            if (GUILayout.Button("Generate Path"))
            {
                pathfinding.PathButton();
            }
        }
    }
#endif

}