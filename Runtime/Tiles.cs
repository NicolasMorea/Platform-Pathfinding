/// <summary>
///* Definition of a Tile used in the Pathfinding Graph
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace Entity.Pathfinding
{
    public enum TileType
    {
        Walkable,
        Obstacle,
        Void,
        Empty,
    }
    [System.Serializable]
    public class Tile
    {
        public Tile(Vector2Int _position, Vector2Int _gravityDirection)
        {
            position = _position;
            gravityDirection = _gravityDirection;
            edges = new List<Edge>();
        }
        public int platformIndex = -1;
        public TileType type = TileType.Empty;
        public Vector2Int position;
        public Vector2Int gravityDirection;
        public Edge AddEdge(Vector2Int target, Vector2Int source, float cost, EdgeType type)
        {
            edges.Add(new Edge(target, source, cost, type));
            return edges[edges.Count - 1];
        }
        public Edge AddPremadeEdge(Edge edge)
        {
            edges.Add(new Edge(edge.targetPos, edge.sourcePos, edge.cost, edge.type));
            Edge newEdge = edges[edges.Count - 1];
            foreach(Waypoint waypoint in edge.waypoints)
            {
                newEdge.waypoints.Add(new Waypoint(waypoint.position, waypoint.initialSpeed, waypoint.acceleration, waypoint.time, waypoint.gravityDirection));
            }
            return newEdge;
        }
        public List<Edge> edges;
        public bool hasEdgeTo(Vector2Int target)
        {
            foreach (Edge edge in edges)
            {
                if (edge.targetPos == target)
                {
                    return true;
                }
            }
            return false;
        }
        public Edge getEdgeTo(Vector2Int target)
        {
            foreach (Edge edge in edges)
            {
                if (edge.targetPos == target)
                {
                    return edge;
                }
            }
            return null;
        }
    }
    public enum EdgeType
    {
        Walk,
        Fall,
        Jump,
    }
    [System.Serializable]
    public class Edge
    {
        public Vector2Int targetPos;
        public Vector2Int sourcePos;
        public EdgeType type;
        public float cost;
        public List<Waypoint> waypoints;
        public Vector3 temp;
        public Edge(Vector2Int _target, Vector2Int _source, float _cost, EdgeType _type)
        {
            targetPos = _target;
            sourcePos = _source;
            cost = _cost;
            type = _type;
            if (type != EdgeType.Walk)
            {
                waypoints = new List<Waypoint>();
            }
        }
        public Edge Clone(){
            Edge newEdge = new Edge(targetPos, sourcePos, cost, type);
            foreach(Waypoint waypoint in waypoints)
            {
                newEdge.waypoints.Add(new Waypoint(waypoint.position, waypoint.initialSpeed, waypoint.acceleration, waypoint.time, waypoint.gravityDirection));
            }
            return newEdge;
        }
    }
    [System.Serializable]
    public class Waypoint
    {
        public Vector3 position;
        public Vector2 initialSpeed;
        public Vector2 acceleration;
        public float time;
        public Vector2Int gravityDirection;

        public Waypoint(Vector3 _position, Vector2 _initialSpeed, Vector2 _acceleration, float _time, Vector2Int _gravityDirection)
        {
            position = _position;
            initialSpeed = _initialSpeed;
            acceleration = _acceleration;
            time = _time;
            gravityDirection = _gravityDirection;
        }
    }
}