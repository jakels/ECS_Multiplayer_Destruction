using System;
using ECSRBExample.ECS_Destructable_Geometry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using Ray = UnityEngine.Ray;
using RaycastHit = UnityEngine.RaycastHit;

public class Exploder : MonoBehaviour
{
    [ContextMenu("Explode")]
    public void Explode()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var physicsWorldSingleton = world.EntityManager
            .CreateEntityQuery(typeof(PhysicsWorldSingleton))
            .GetSingleton<PhysicsWorldSingleton>();

        var collisionWorld = physicsWorldSingleton.CollisionWorld;

        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
        var filter = new CollisionFilter
        {
            BelongsTo = ~0u,    // Belong to all layers
            CollidesWith = ~0u, // Collide with all layers
            GroupIndex = 0
        };
        if (collisionWorld.OverlapSphere(transform.position, explosionRadius,  ref hits, filter))
        {
            //
            foreach (var hit in hits)
            {
                Entity hitEntity = hit.Entity;
                if (world.EntityManager.HasComponent<DestructableGeometryComponent>(hitEntity))
                {
                    //Debug.DrawLine(transform.position, hit.Position, Color.green, 0.1f);
                    var comp = world.EntityManager.GetComponentData<DestructableGeometryComponent>(hitEntity);
                    comp.stress += explosionForce / math.max(1f, math.distance(transform.position, hit.Position));
                    comp.stressPosition = transform.position;
                    comp.explosionRadius = explosionRadius;
                    comp.explosionForce = explosionForce;
                    world.EntityManager.SetComponentData(hitEntity, comp);
                }
                if (world.EntityManager.HasComponent<ChunkComponent>(hitEntity))
                {
                    //Debug.DrawLine(transform.position, hit.Position, Color.green, 0.1f);
                    if (world.EntityManager.HasComponent<ActiveChunkTag>(hitEntity) == false)
                    {
                        world.EntityManager.DestroyEntity(hitEntity);
                        continue;
                    }
                    var comp = world.EntityManager.GetComponentData<ChunkComponent>(hitEntity);
                    comp.simulateTime = DestructableSystem.CalculateSimTime(transform.position, hitEntity, world.EntityManager);
                    world.EntityManager.AddComponent<ActiveChunkTag>(hitEntity);
                    DestructableSystem.ApplyExplosionForce(world.EntityManager, hitEntity, transform.position, explosionRadius, explosionForce);
                    comp.staticPosition = world.EntityManager.GetComponentData<LocalTransform>(hitEntity).Position;
                    world.EntityManager.SetComponentEnabled<Simulate>(hitEntity, true);
                    world.EntityManager.SetComponentData(hitEntity, comp);
                }
            }
        }

        hits.Dispose();
        
        //Invoke(nameof(ExplosionForce), 0.1f);
    }

    [Header("Explosion settings")]
    public float explosionForce = 50f;
    public float explosionRadius = 15f;

    EntityManager _em;
    EntityQuery _physicsBodyQuery;

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;

        _physicsBodyQuery = _em.CreateEntityQuery(
            ComponentType.ReadWrite<PhysicsVelocity>(),
            ComponentType.ReadOnly<PhysicsMass>(),
            ComponentType.ReadOnly<PhysicsCollider>(),
            ComponentType.ReadOnly<LocalTransform>());
    }

    // Optional: visualize the blast radius in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
