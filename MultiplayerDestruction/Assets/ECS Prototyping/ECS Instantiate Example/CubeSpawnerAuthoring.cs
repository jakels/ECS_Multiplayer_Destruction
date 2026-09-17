using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS
{
    public class CubeSpawnerAuthoring : MonoBehaviour
    {
        public GameObject cubePrefab;
        public float spawnRate = 1f;

        class Baker : Baker<CubeSpawnerAuthoring>
        {
            public override void Bake(CubeSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var prefabEntity = GetEntity(authoring.cubePrefab, TransformUsageFlags.Dynamic);

                AddComponent(entity, new CubeSpawnerComponent
                {
                    prefab = prefabEntity,
                    position = authoring.transform.position,
                    nextSpawnTime = 0f,
                    spawnRate = authoring.spawnRate
                });
            }
        }
    }
}