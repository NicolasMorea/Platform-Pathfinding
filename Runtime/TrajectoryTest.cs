/// <summary>
///* Purely for debug for the pathfinding manager and visualizing trajectories
/// </summary>

using System.Collections.Generic;
using Gravity;
using UnityEngine;

namespace Entity.Pathfinding
{
    public class TrajectoryTest : MonoBehaviour
    {
        public GravityHandler GravityHandler;
        public Rigidbody2D rb;
        public Transform lauch;
        public Transform target;
        public Vector2Int gravityDirection;
        public float gravity;
        public bool showJumpTrajectory;
        public bool showFallTrajectory;
        public float vMax;
        Vector3 a;
        Vector3 deltaP;
        float b1;
        float discriminant;
        float T_min;
        float T_lowEnergy;
        float T_max;
        List<float> T;
        float Timer = -5f;
        int currentIndex = 0;

        [Header("fall trajectory")]
        public Vector2 initialVelocity = Vector2.zero;
        public float fallTime = 0f;
        public Vector2 fallAcc = Vector2.zero;
        void FixedUpdate()
        {
            if (Time.time - Timer < 2f)
            {
                if (showFallTrajectory)
                {
                    rb.AddForce(fallAcc + new Vector2(gravityDirection.x, gravityDirection.y) * gravity, ForceMode2D.Force);
                }
                return;
            }
        }
        void Update()
        {
            if (Time.time - Timer < 2f)
            {
                return;
            }
            else
            {
                if (showJumpTrajectory)
                {
                    GravityHandler.SetGravityMult(gravity / 10);
                    transform.position = lauch.position;
                    rb.linearVelocity = (target.position - lauch.position) / T[currentIndex] - a * T[currentIndex] / 2.0f;
                    currentIndex += 1;
                    if (currentIndex >= T.Count)
                    {
                        currentIndex = 0;
                    }
                }
                if (showFallTrajectory)
                {
                    transform.position = lauch.position;
                    rb.linearVelocity = initialVelocity;
                }
                Timer = Time.time;
            }
        }
        void OnValidate()
        {
            if (target == null || lauch == null || gravityDirection == Vector2Int.zero || gravity == 0f || vMax == 0f)
            {
                return;
            }
            
            if (showJumpTrajectory)
            {
                a = new Vector3(gravityDirection.x, gravityDirection.y, 0f) * gravity;
                deltaP = target.position - lauch.position;
                b1 = Vector3.Dot(deltaP, a) + vMax * vMax;
                discriminant = b1 * b1 - Vector3.Dot(a, a) * Vector3.Dot(deltaP, deltaP);
                T_min = Mathf.Sqrt((b1 - Mathf.Sqrt(discriminant)) * 2 / Vector3.Dot(a, a));
                T_max = Mathf.Sqrt((b1 + Mathf.Sqrt(discriminant)) * 2 / Vector3.Dot(a, a));
                T_lowEnergy = Mathf.Sqrt(Mathf.Sqrt(4.0f * Vector3.Dot(deltaP, deltaP) / Vector3.Dot(a, a)));
                T = new List<float>();
                T.Add(T_min);
                T.Add(T_lowEnergy);
                T.Add(T_max);
            }

            if (showFallTrajectory)
            {
                fallTime = ComputeFallTrajectoryTime(lauch.position, target.position, gravityDirection, initialVelocity);
                fallAcc = GetAccelerationFromFall(lauch.position, target.position, gravityDirection, initialVelocity, fallTime);
            }
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
        Vector3 position(float t, Vector3 launchVelocity)
        {
            return lauch.position + launchVelocity * t + a * t * t / 2.0f;
        }

        void OnDrawGizmos()
        {
            if (target == null || lauch == null || gravityDirection == Vector2Int.zero || gravity == 0f || vMax == 0f)
            {
                return;
            }
            if (showJumpTrajectory)
            {
                for (int i = 0; i < T.Count; i++)
                {
                    if (i == 0)
                    {
                        Gizmos.color = Color.blue;
                    }
                    else if (i == 1)
                    {
                        Gizmos.color = Color.green;
                    }
                    else if (i == 2)
                    {
                        Gizmos.color = Color.red;
                    }
                    Vector3 launchVelocity = (target.position - lauch.position) / T[i] - a * T[i] / 2.0f;

                    Vector3 prev = lauch.position;
                    for (float j = 0.0f; j < T[i]; j += 0.1f)
                    {
                        Vector3 pos = position(j, launchVelocity);
                        Gizmos.DrawLine(prev, pos);
                        prev = pos;
                    }
                }
            }
            if (showFallTrajectory)
            {
                Gizmos.color = Color.blue;
                Vector3 prev = lauch.position;
                for (float j = 0.0f; j < fallTime; j += 0.1f)
                {
                    Vector3 pos = lauch.position + (Vector3)initialVelocity * j + (Vector3)(new Vector2(gravityDirection.x, gravityDirection.y) * gravity + fallAcc) * j * j / 2.0f;
                    Gizmos.DrawLine(prev, pos);
                    prev = pos;
                }
            }
        }
    }
}