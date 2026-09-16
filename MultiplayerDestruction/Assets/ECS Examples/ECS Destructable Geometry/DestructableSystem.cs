using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics.Extensions;

namespace ECSRBExample.ECS_Destructable_Geometry
{
    public partial struct DestructableSystem : ISystem
    {
        public static readonly bool DebugChunkColouring = true;
        public static readonly float MaximumChunkSimTime = 5f;
        public static readonly float MaximumChunkScale = 1.5f;
        public static readonly float MaximumChunkStickDisplacement = 0.9f;
        // Falloff factor dictates how quickly the granted simulation time per chunk falls off as distance from the explosion increases,
        // a larger value will mean "weaker" explosions due to the settling occuring slower on more objects.
        // Effectively dictates where on the sim time fall off curve the result equals 1 second of sim time where that point is at a distance of,
        // 1 / SimTimeFalloffFactor. A val of 1 means its at a distance of 1 and a value of 2 means its at a distance of 0.5
        public static readonly float SimTimeFalloffFactor = 5f;
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            FractureStressedObjects(ref state);
        }

        private void FractureStressedObjects(ref SystemState state)
        {
            // Create an EntityCommandBuffer to record entity commands
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var em = state.EntityManager;
            
            // Loop over all the destructable components in the world
            foreach (var (destructableComponent, destructableEntity) in SystemAPI.Query<RefRW<DestructableGeometryComponent>>().WithEntityAccess())
            {
                // If the destructable object is already fractured then skip it
                if (destructableComponent.ValueRW.isFractured) { continue; }
                
                // Check if the destructable object's stress level is larger than its maximum
                if (destructableComponent.ValueRO.stress > destructableComponent.ValueRO.maxStress)
                {
                    // Fracture this stressed object   
                    FractureObject(ref state, destructableComponent, destructableEntity, em, ecb);
                    
                    // Destroy parent destructable entity
                    ecb.DestroyEntity(destructableEntity);
                }
            }
            
            foreach (var (chunkComponent, chunkEntity) in SystemAPI.Query<RefRW<ChunkComponent>>().WithEntityAccess())
            {
                chunkComponent.ValueRW.simulateTime -= (float)SystemAPI.Time.DeltaTime;
                if ((chunkComponent.ValueRO.simulateTime <= 0f && Vector3.Distance(chunkComponent.ValueRO.staticPosition, em.GetComponentData<LocalTransform>(chunkEntity).Position) < MaximumChunkStickDisplacement) || chunkComponent.ValueRO.simulateTime <= -MaximumChunkSimTime)
                {
                    // Disable entities physics simulation
                    em.SetComponentEnabled<Simulate>(chunkEntity, false); // stop simulating
                    // Set the chunk scale to the distance between its current position and the static position with a floor of 1f and a max of 3f
                    var chunkTransform = em.GetComponentData<LocalTransform>(chunkEntity);
                    var distance = Vector3.Distance(chunkComponent.ValueRO.staticPosition, chunkTransform.Position);
                    var newScale = Mathf.Clamp(distance * 10f, 1f, MaximumChunkScale);
                    chunkTransform.Scale = newScale;
                    em.SetComponentData(chunkEntity, chunkTransform);
                    if (DebugChunkColouring)
                    {
                        //em.SetComponentData(chunkEntity, new URPMaterialPropertyBaseColor { Value = new float4(0f,0f,0f,1f) });
                    }
                    if (chunkComponent.ValueRO.simulateTime <= -MaximumChunkSimTime)
                    {
                        ecb.RemoveComponent<ChunkComponent>(chunkEntity);
                    }
                }
                else
                {
                    em.SetComponentEnabled<Simulate>(chunkEntity, true);
                }
            }
            ecb.Playback(em);
            ecb.Dispose();
        }

        public void FractureObject(ref SystemState state, RefRW<DestructableGeometryComponent> destructableComponent, Entity destructableEntity, EntityManager em, EntityCommandBuffer ecb)
        {
            
            // Set the object's fractured state to true
            destructableComponent.ValueRW.isFractured = true;
            
            // Instantiate the fractured version of the destructable object immediately 
            var fracturedPrefab = em.Instantiate(destructableComponent.ValueRW.fracturedPrefab);
            
            // The transform values of the fractured entity will be at its transform position so we need to match it to the destructable entity's position
            
            ecb.SetComponent(fracturedPrefab, LocalTransform.FromPosition(destructableComponent.ValueRO.position));
            var linkedGroup = em.GetBuffer<LinkedEntityGroup>(fracturedPrefab);
            var listOfChunks = new NativeList<Entity>(Unity.Collections.Allocator.Temp);
            for (int i = 1; i < linkedGroup.Length; i++)             // skip 0, that's root
            {
                var chunk = linkedGroup[i].Value;
                if (em.HasComponent<PhysicsVelocity>(chunk))
                {
                    listOfChunks.Add(chunk);
                }
            }

            var wallTransform = em.GetComponentData<LocalTransform>(destructableEntity);
            var prefabRoot    = em.GetComponentData<LocalTransform>(destructableComponent.ValueRW.fracturedPrefab);
            foreach (var chunk in listOfChunks)
            {
                // Stop chunk from being simulated
                em.SetComponentEnabled<Simulate>(chunk, false);
                
                // Assign correct position
                var chunkTransform = em.GetComponentData<LocalTransform>(chunk);
                var local = prefabRoot.InverseTransformTransform(chunkTransform);
                var worldPosition = wallTransform.TransformTransform(local);
                em.SetComponentData(chunk, worldPosition);
                
                // Assign Velocity
                var xf = em.GetComponentData<LocalTransform>(chunk); // now world-space
                float3 posDiff = xf.Position - destructableComponent.ValueRO.stressPosition;
                float sqrDist = math.lengthsq(posDiff);
                bool insideRadius = !(sqrDist > destructableComponent.ValueRO.explosionRadius * destructableComponent.ValueRO.explosionRadius);
                sqrDist = math.max(sqrDist, 1e-8f);

                if (insideRadius)
                {
                    var pv = em.GetComponentData<PhysicsVelocity>(chunk);
                    var pm = em.GetComponentData<PhysicsMass>(chunk);
                    pv.Linear = posDiff * (destructableComponent.ValueRO.explosionForce / sqrDist) / (1f / (pm.InverseMass));
                    //float structureProtectionFactor = 1f / 8f;
                    //pv.Linear /= Mathf.Max((dcomps * structureProtectionFactor) * (dcomps * structureProtectionFactor), 1f);
                    pv.Angular = float3.zero;
                    em.SetComponentData(chunk, pv);
                }

                // Assign Static Position & sim time
                float dist = (Vector3.Distance(destructableComponent.ValueRO.stressPosition, em.GetComponentData<LocalTransform>(chunk).Position) * (1f));
                float calculatedSimTime = Mathf.Max(Mathf.Min((1f / (dist * SimTimeFalloffFactor)) /*- (1f / destructableComponent.ValueRO.explosionForce)*/, MaximumChunkSimTime), 0f);
                if (!insideRadius)
                {
                    calculatedSimTime = 0f;
                }
                if (DebugChunkColouring)
                {
                    em.SetComponentData(chunk, new URPMaterialPropertyBaseColor { Value = new float4(calculatedSimTime, calculatedSimTime, calculatedSimTime, 1f) });
                }
                ecb.SetComponent(chunk, new ChunkComponent()
                {
                    simulateTime = calculatedSimTime,
                    staticPosition = em.GetComponentData<LocalTransform>(chunk).Position
                });
            }
        }
    }
}