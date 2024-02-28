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
        internal AgentReferance AgentReferance;
        [SerializeField] internal float Radius;
        [SerializeField] internal float Speed;
        [SerializeField] internal float LandOffset;

        public int GetPathIndex()
        {
            return _navigationManager.Interface.GetPathIndex(this);
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
        public void SetUseNavigationMovementFlag(bool set)
        {
            if(_navigationManager == null) { return; }
            _navigationManager.Interface.SetUseNavigationMovementFlag(this, set);
        }
        internal AgentInput GetAgentInput()
        {
            return new AgentInput()
            {
                LandOffset = LandOffset,
                Speed = Speed,
                Radius = Radius,
            };
        }
        private void OnDestroy()
        {
            RequestUnsubscription();
        }
        private void OnDisable()
        {
            RequestUnsubscription();
        }
    }


}