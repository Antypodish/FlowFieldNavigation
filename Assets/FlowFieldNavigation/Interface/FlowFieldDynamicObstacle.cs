using UnityEngine;
using Unity.Mathematics;
using System;

namespace FlowFieldNavigation
{
    public class FlowFieldDynamicObstacle : MonoBehaviour
    {
        [SerializeField] Vector2 _size;
        internal FlowFieldNavigationManager _navManager;
        internal int ObstacleIndex = -1;


        internal ObstacleRequest GetObstacleRequest()
        {
            float3 pos = transform.position;
            float2 pos2 = new float2(pos.x, pos.z);
            return new ObstacleRequest()
            {
                Position = pos2,
                HalfSize = _size / 2,
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;

            Vector3 position = Vector3.zero;
            Vector3 halfsize3 = new Vector3(_size.x, 0, _size.y) / 2;
            Vector3 bl = position - halfsize3;
            Vector3 tr = position + halfsize3;
            Vector3 tl = position + new Vector3(-halfsize3.x, 0, halfsize3.z);
            Vector3 br = position + new Vector3(halfsize3.x, 0, -halfsize3.z);
            
            Gizmos.DrawLine(bl, tl);
            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            
            Gizmos.DrawRay(bl, Vector3.up * 100);
            Gizmos.DrawRay(bl, Vector3.down * 100);
            Gizmos.DrawRay(tl, Vector3.up * 100);
            Gizmos.DrawRay(tl, Vector3.down * 100);
            Gizmos.DrawRay(tr, Vector3.up * 100);
            Gizmos.DrawRay(tr, Vector3.down * 100);
            Gizmos.DrawRay(br, Vector3.up * 100);
            Gizmos.DrawRay(br, Vector3.down * 100);
        }
    }
}
