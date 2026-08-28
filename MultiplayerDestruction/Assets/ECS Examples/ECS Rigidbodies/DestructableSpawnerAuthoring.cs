using Unity.Entities;
using UnityEngine;

namespace ECSRBExample
{
    public class DestructableSpawnerAuthoring : MonoBehaviour
    {
        public GameObject chunkPrefab;
        public int width, height, depth;

        private class DestructableSpawnerBaker : Baker<DestructableSpawnerAuthoring>
        {
            public override void Bake(DestructableSpawnerAuthoring authoring)
            {
                var thisEntity = GetEntity(TransformUsageFlags.Dynamic);
                var chunkPrefabEntity = GetEntity(authoring.chunkPrefab, TransformUsageFlags.Dynamic);

                AddComponent(thisEntity, new DestructableSpawnerComponent
                {
                    chunkPrefab = chunkPrefabEntity,
                    width = authoring.width,
                    height = authoring.height,
                    depth = authoring.depth
                });
            }
        }
    }
}