using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ECSRBExample
{
    [BurstCompile]
    public partial struct DestructableSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // don't call OnUpdate until the subscene has actually streamed in
            state.RequireForUpdate<DestructableSpawnerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;

            // Entity command buffer allows us to make operations on entities in a thread safe way
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            if (!SystemAPI.TryGetSingletonEntity<DestructableSpawnerComponent>(out var entity)) return;
            RefRW<DestructableSpawnerComponent>
                spawner = SystemAPI.GetComponentRW<DestructableSpawnerComponent>(entity);



            for (int spawnCount = 0;
                 spawnCount < spawner.ValueRO.width * spawner.ValueRO.height * spawner.ValueRO.depth;
                 spawnCount++)
            {
                Entity newEntity = ecb.Instantiate(spawner.ValueRO.chunkPrefab);
                float3 newPos = new float3(0f, 0f, 0f);

                newPos.x = (spawnCount % spawner.ValueRO.width) + 0.01f;
                newPos.z = (math.floor(spawnCount / (float)spawner.ValueRO.width) % spawner.ValueRO.depth) + 0.01f;
                newPos.y = (math.floor(spawnCount / ((float)spawner.ValueRO.depth * (float)spawner.ValueRO.width))) +
                           0.01f;

                ecb.SetComponent(newEntity, LocalTransform.FromPosition(newPos));
            }


            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}