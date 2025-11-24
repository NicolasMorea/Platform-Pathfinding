
#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Gravity;
using UnityEditor;

namespace Entity.Pathfinding
{
    [CustomEditor(typeof(PathfindingManager))]
    public class customPathfindingManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            PathfindingManager pathfindingManager = (PathfindingManager)target;

            if (pathfindingManager.parameters.enemyData != null)
            {
                GUILayout.Space(15);

                EditorGUILayout.LabelField("Entity Size : ", pathfindingManager.parameters.enemyData.entitySize.ToString());
                EditorGUILayout.LabelField("Collider Size : ", pathfindingManager.parameters.enemyData.colliderSize.ToString());
                EditorGUILayout.LabelField("Max Jump Velocity : ", pathfindingManager.parameters.enemyData.maxJumpVelocity.ToString());
                EditorGUILayout.LabelField("Max Acceleration : ", pathfindingManager.parameters.enemyData.maxAcceleration.ToString());
            }

            GUILayout.Space(15);
            if (!pathfindingManager.buttonVerif && GUILayout.Button("Generate Graph"))
            {
                if (pathfindingManager.GraphExists()) pathfindingManager.buttonVerif = true;
                else pathfindingManager.TryInitGraph();
            }
            if (pathfindingManager.buttonVerif)
            {
                if (GUILayout.Button("Are you sure? (graph already exists)"))
                {
                    pathfindingManager.TryInitGraph();
                    pathfindingManager.buttonVerif = false;
                }
                if (GUILayout.Button("No")) pathfindingManager.buttonVerif = false;
            }

            if (GUILayout.Button("Load Graph")) pathfindingManager.LoadGraph();

            if (GUILayout.Button("Default Parameters")) pathfindingManager.SetDefault();
        }
    }
}

#endif