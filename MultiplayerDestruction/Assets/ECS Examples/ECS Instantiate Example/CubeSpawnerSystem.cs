using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ECS
{
    [BurstCompile]
    public partial struct CubeSpawnerSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CubeSpawnerComponent>(out var entity)) return;
            
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;
            RefRW<CubeSpawnerComponent> spawner = SystemAPI.GetComponentRW<CubeSpawnerComponent>(entity);

            // Entity command buffer allows us to make operations on entities in a thread safe way
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            if (spawner.ValueRO.nextSpawnTime < elapsedTime)
            {
                Entity newEntity = ecb.Instantiate(spawner.ValueRO.prefab);
                
                spawner.ValueRW.nextSpawnTime = elapsedTime + 1f/spawner.ValueRO.spawnRate;
                spawner.ValueRW.spawnCount++;
                float spawnCount = (float)spawner.ValueRO.spawnCount;
                
                float3 newPos = new float3(0f, 0f, 0f);

                newPos.x = spawnCount % 16;
                newPos.z = math.floor(spawnCount / 16) % 16;
                newPos.y = math.floor(spawnCount / (16*16));
                
                
                ecb.SetComponent(newEntity, LocalTransform.FromPosition(newPos));
                
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }
    }
}
