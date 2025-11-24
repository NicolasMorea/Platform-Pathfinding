using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Entity.Pathfinding
{

    public abstract class Task
    {
        protected PathfinderHandler handler;
        public abstract void Execute();
        public abstract bool IsOver();
        public virtual void OnEntry() {}
        public virtual void OnFixedUpdate() {}
    }

    public class WalkTask : Task
    {
        public WalkTask(PathfinderHandler _handler, Vector3 _targetPos, bool _stops)
        {
            targetPos = _targetPos;
            handler = _handler;
            stops = _stops;
        }
        private Vector3 targetPos;
        private bool stops;
        public override void Execute()
        {
            handler.WalkTo(targetPos);
        }
        public override bool IsOver()
        {
            if (stops && handler.IsNearX(targetPos))
            {
                // Debug.Log("ready To Jump");
                return true;
            }
            return false;
        }
    }

    public class JumpTask : Task
    {
        public JumpTask(PathfinderHandler _handler, Vector2 _velocity)
        {
            velocity = _velocity;
            handler = _handler;
        }
        public Vector2 velocity;
        public override void Execute()
        {
            handler.SetVelocity(velocity);
            // Debug.Log("Jumping with velocity : " + velocity);
        }
        public override bool IsOver()
        {
            return true;
        }
    }

    public class TrajectoryTask : Task
    {
        public Vector2 velocity;
        public Vector2 accelleration;
        public Vector2 targetPos;
        public Vector2 originPos;
        public float time;
        public float startTime;

        public TrajectoryTask(PathfinderHandler _handler, Vector2 _velocity, Vector2 _accelleration, Vector2 _originPos,  Vector2 _targetPos, float _time)
        {
            velocity = _velocity;
            accelleration = _accelleration;
            handler = _handler;
            targetPos = _targetPos;
            originPos = _originPos;
            time = _time;
        }
        public override void OnEntry()
        {
            startTime = Time.time;
            handler.OnTrajectoryStart(originPos, velocity);
            
            base.OnEntry();
        }
        public override void Execute()
        {
        }
        public override void OnFixedUpdate()
        {
            if (accelleration != Vector2.zero)
            {
                handler.SetAccelleration(accelleration);
            }
        }
        public override bool IsOver()
        {
            if (handler.IsNearX(targetPos, true) && handler.IsNearY(targetPos, true))
            {
                handler.OnTrajectoryEnd();
                return true;
            }
            else if(Time.time - startTime > time)
            {
                handler.OnTrajectoryEnd();
                handler.AbortTaks();
                return true;
            }
            return false;
        }

    }

    public class PathTile
    {
        public PathTile(Vector2Int _gridPos, Vector2Int _parentGridPos, float _cost, Edge _edge = null)
        {
            gridPos = _gridPos;
            parentGridPos = _parentGridPos;
            cost = _cost;
            edge = _edge;
        }
        public Vector2Int gridPos;
        public Vector2Int parentGridPos;
        public Edge edge;
        public float distance;
        public float cost;
        public float totalCost
        {
            get
            {
                return distance + cost;
            }
        }
        public float inversedCost
        {
            get
            {
                return cost + distance*10;
            }
        }
        public void SetDistance(Vector2Int TargetCell)
        {
            distance = Mathf.Abs(gridPos.x - TargetCell.x) + Mathf.Abs(gridPos.y - TargetCell.y);
        }
    }
}