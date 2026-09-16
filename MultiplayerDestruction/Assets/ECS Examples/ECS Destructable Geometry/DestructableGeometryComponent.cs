using Unity.Entities;
using Unity.Mathematics;

namespace ECSRBExample.ECS_Destructable_Geometry
{
    public struct DestructableGeometryComponent : IComponentData
    {
        public Entity fracturedPrefab;
        public bool isFractured;
        public float stress;
        public float maxStress;
        public float3 position;
        public float3 rotation;
        public float3 stressPosition;
        public float explosionRadius;
        public float explosionForce;
    }
}