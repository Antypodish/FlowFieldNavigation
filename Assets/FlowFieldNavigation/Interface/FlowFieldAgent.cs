using System;
using System.Diagnostics.CodeAnalysis;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

using UnityEngine;

namespace FlowFieldNavigation
{

    public class FlowFieldAgent : MonoBehaviour
    {
        internal FlowFieldNavigationManager _navigationManager;
        internal int AgentDataIndex = -1;   //-1 means not subscribed yet
        [SerializeField] internal float Radius;
        [SerializeField] internal float Speed;
        [SerializeField] internal float LandOffset;

        [HideInInspector] public Transform Transform;

        private void Start()
        {
            Transform = transform;
        }
        public int GetPathIndex()
        {
            return _navigationManager.Interface.GetPathIndex(AgentDataIndex);
        }
        public float GetSpeed()
        {
            if (_navigationManager == null) { return 0; }
            return _navigationManager.Interface.GetSpeed(this);
        }
        public AgentStatus GetStatus()
        {
            if (_navigationManager == null) { return 0; }
            return _navigationManager.Interface.GetStatus(this);
        }
        public void RequestSubscription()
        {
            if (_navigationManager == null) { return; }
            _navigationManager.Interface.RequestSubscription(this);
        }
        public void RequestUnsubscription()
        {
            if (_navigationManager == null) { return; }
            _navigationManager.Interface.RequestUnsubscription(this);
        }
        public void SetHoldGround()
        {
            if (_navigationManager == null) { return; }
            _navigationManager.Interface.SetHoldGround(this);
        }
        public void SetStopped()
        {
            if (_navigationManager == null) { return; }
            _navigationManager.Interface.SetStopped(this);
        }
        public void SetSpeed(float speed)
        {
            if (_navigationManager == null) { return; }
            _navigationManager.Interface.SetSpeed(this, speed);
        }
        public Vector3 GetCurrentDirection()
        {
            if (_navigationManager == null) { return Vector3.zero; }
            return _navigationManager.Interface.GetCurrentDirection(this);
        }
        private void OnDestroy()
        {
            RequestUnsubscription();
        }
    }


}