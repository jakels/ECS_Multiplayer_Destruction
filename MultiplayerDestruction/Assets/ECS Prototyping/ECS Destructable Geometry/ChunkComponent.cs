using Unity.Entities;
using Unity.Mathematics;

namespace ECSRBExample.ECS_Destructable_Geometry
{
    public struct ChunkComponent : IComponentData
    {
        public float simulateTime;
        public float3 staticPosition;
        public float distanceFromStaticAtRest;
    }
}