using Unity.Entities;
using UnityEngine;

namespace ECSRBExample.ECS_Destructable_Geometry
{
    public class StaticDestructableAuthoring : MonoBehaviour
    {
        public GameObject fracturedPrefab;
        public float maxStress = 1f;
        
        class Baker : Baker<StaticDestructableAuthoring>
        {
            public override void Bake(StaticDestructableAuthoring authoring)
            {
                var thisEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(thisEntity, new DestructableGeometryComponent
                {
                    fracturedPrefab = GetEntity(authoring.fracturedPrefab, TransformUsageFlags.Dynamic),
                    position = GetComponent<Transform>().position,
                    rotation = GetComponent<Transform>().rotation.eulerAngles,
                    maxStress = authoring.maxStress,
                    explosionForce = 0f
                });
            }
        }
    }
}