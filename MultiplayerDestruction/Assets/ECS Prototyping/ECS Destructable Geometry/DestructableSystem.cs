using System;
using System.Linq;
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
        public static readonly bool DebugChunkColouring = false;
        public static readonly float MaximumChunkSimTime = 5f;
        public static readonly float MaximumChunkScale = 1.5f;
        public static readonly float MaximumChunkStickDisplacement = 0.9f;
        public static readonly float MaximumChunkPermittedDistanceMultiple = 5f;
        public static readonly bool KillStaticChunksOnExplode = true;
        public static readonly bool KillStaticChunksOnExpolodeChecksDistance = false;
        public static readonly bool RemovePhysicsOnStaticChunks = true;
        public static readonly bool KillAstrayStaticChunks = true;
        // Falloff factor dictates how quickly the granted simulation time per chunk falls off as distance from the explosion increases,
        // a larger value will mean "weaker" explosions due to the settling occuring slower on more objects.
        // Effectively dictates where on the sim time fall off curve the result equals 1 second of sim time where that point is at a distance of,
        // 1 / SimTimeFalloffFactor. A val of 1 means its at a distance of 1 and a value of 2 means its at a distance of 0.5
        public static readonly float SimTimeFalloffFactor = 5f;
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            FractureStressedObjects(ref state);

            Entity restoreRequestEntity = Entity.Null;
            foreach (var (req, entity) in SystemAPI.Query<RefRO<RestoreRequest>>().WithEntityAccess())
            {
                restoreRequestEntity = entity;
            }

            if (restoreRequestEntity != Entity.Null)
            {
                state.EntityManager.DestroyEntity(restoreRequestEntity);
                RestoreAll(ref state);
            }
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
                    //ecb.DestroyEntity(destructableEntity);
                    ecb.SetEnabled(destructableEntity, false);
                }
            }
            
            foreach (var (chunkTag, chunkComponent, chunkEntity) in SystemAPI.Query<RefRW<ActiveChunkTag>, RefRW<ChunkComponent>>().WithEntityAccess())
            {
                chunkComponent.ValueRW.simulateTime -= (float)SystemAPI.Time.DeltaTime;
                if ((chunkComponent.ValueRW.simulateTime <= 0f && Vector3.Distance(chunkComponent.ValueRW.staticPosition, em.GetComponentData<LocalTransform>(chunkEntity).Position) < MaximumChunkStickDisplacement) || chunkComponent.ValueRW.simulateTime <= -MaximumChunkSimTime)
                {
                    // Disable entities physics simulation
                    chunkComponent.ValueRW.distanceFromStaticAtRest = Vector3.Distance(chunkComponent.ValueRW.staticPosition, em.GetComponentData<LocalTransform>(chunkEntity).Position);
                    if (KillAstrayStaticChunks && chunkComponent.ValueRW.distanceFromStaticAtRest > MaximumChunkPermittedDistanceMultiple * MaximumChunkStickDisplacement)
                    {
                        ecb.DestroyEntity(chunkEntity);
                        continue;
                    }
                    em.SetComponentEnabled<Simulate>(chunkEntity, false); // stop simulating
                    ecb.RemoveComponent<ActiveChunkTag>(chunkEntity);
                    // Set the chunk scale to the distance between its current position and the static position with a floor of 1f and a max of 3f
                    var chunkTransform = em.GetComponentData<LocalTransform>(chunkEntity);
                    var distance = Vector3.Distance(chunkComponent.ValueRW.staticPosition, chunkTransform.Position);
                    var newScale = Mathf.Clamp(distance * 10f, 1f, MaximumChunkScale);
                    chunkTransform.Scale = newScale;
                    em.SetComponentData(chunkEntity, chunkTransform);
                    if (RemovePhysicsOnStaticChunks && chunkComponent.ValueRW.simulateTime <= -MaximumChunkSimTime && chunkComponent.ValueRW.distanceFromStaticAtRest > MaximumChunkStickDisplacement)
                    {
                        
                        // Fully retire the chunk from the dynamics world instead of just
                        // pausing it: without PhysicsVelocity/PhysicsMass it becomes a
                            // static body, so it drops out of the per-frame dynamic broadphase
                                // rebuild for good instead of just skipping the solver.
                        ecb.RemoveComponent<PhysicsVelocity>(chunkEntity);
                        ecb.RemoveComponent<PhysicsMass>(chunkEntity);
                        if (em.HasComponent<PhysicsDamping>(chunkEntity))
                        {
                            ecb.RemoveComponent<PhysicsDamping>(chunkEntity);
                        }
                        if (em.HasComponent<PhysicsGravityFactor>(chunkEntity))
                        {
                            ecb.RemoveComponent<PhysicsGravityFactor>(chunkEntity);
                        }
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
            destructableComponent.ValueRW.fracturedInstance = fracturedPrefab;
            
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
                bool appliedForce = ApplyExplosionForce(em, chunk, destructableComponent.ValueRO.stressPosition, destructableComponent.ValueRO.explosionRadius, destructableComponent.ValueRO.explosionForce);

                // Assign Static Position & sim time
                float calculatedSimTime = CalculateSimTime(destructableComponent.ValueRO.stressPosition, chunk, em);
                if (!appliedForce)
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

        public static bool ApplyExplosionForce(EntityManager em, Entity chunk, float3 stressPosition, float explosionRadius, float explosionForce)
        {
            var xf = em.GetComponentData<LocalTransform>(chunk); // now world-space
            float3 posDiff = xf.Position - stressPosition;
            float sqrDist = math.lengthsq(posDiff);
            bool insideRadius = !(sqrDist > explosionRadius * explosionRadius);
            sqrDist = math.max(sqrDist, 1e-8f);

            if (insideRadius)
            {
                var pv = em.GetComponentData<PhysicsVelocity>(chunk);
                var pm = em.GetComponentData<PhysicsMass>(chunk);
                pv.Linear = posDiff * (explosionForce / sqrDist) / (1f / (pm.InverseMass));
                //float structureProtectionFactor = 1f / 8f;
                //pv.Linear /= Mathf.Max((dcomps * structureProtectionFactor) * (dcomps * structureProtectionFactor), 1f);
                pv.Angular = float3.zero;
                em.SetComponentData(chunk, pv);
                return true;
            }
            return false;
        }

        public static float CalculateSimTime(Vector3 stressPos, Entity chunk, EntityManager em)
        {
            float dist = (Vector3.Distance(stressPos, em.GetComponentData<LocalTransform>(chunk).Position) * (1f));
            float calculatedSimTime = Mathf.Max(Mathf.Min((1f / (dist * SimTimeFalloffFactor)) /*- (1f / destructableComponent.ValueRO.explosionForce)*/, MaximumChunkSimTime), 0f);
            return calculatedSimTime;
        }
        
        public void RestoreAll(ref SystemState state)
        {
            Debug.Log("Restoring all Destructable Entities");
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            foreach (var (destructableGeometryComponent, entity) in SystemAPI.Query<RefRW<DestructableGeometryComponent>>().WithEntityAccess().WithOptions(EntityQueryOptions.IncludeDisabledEntities))
            {
                RestoreObject(entity, state.EntityManager, ecb);
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void RestoreObject(Entity destructableEntity, EntityManager em, EntityCommandBuffer ecb)
        {
            var d = em.GetComponentData<DestructableGeometryComponent>(destructableEntity);
            if (!d.isFractured) return;

            if (em.Exists(d.fracturedInstance))
                ecb.DestroyEntity(d.fracturedInstance);   // kills the whole chunk LinkedEntityGroup at once
            Debug.Log($"Reset {destructableEntity.Index} to unfractured state");
            d.isFractured = false;
            d.stress = 0f;
            ecb.SetComponent(destructableEntity, d);
            ecb.SetEnabled(destructableEntity, true);
        }
    }
}