/// <summary>
///* Definition of the Graph
///* The graph is saved in a ScriptableObject to be reusable
/// </summary>

using System.Collections.Generic;
using UnityEngine;

namespace Entity.Pathfinding
{
    [CreateAssetMenu()]
    [System.Serializable]
    public sealed class PathfindingGraph : ScriptableObject
    {
        [SerializeReference]
        public PathfindingParameters parameters;
        [SerializeReference]
        public List<Tile> tiles;
        public int numberOfEdges;
        public string gravityTilemapName;
        public string wallsTilemapName;

    }
}

