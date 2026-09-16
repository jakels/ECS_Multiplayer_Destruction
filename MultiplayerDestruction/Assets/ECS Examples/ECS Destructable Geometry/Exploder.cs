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
                else
                {
                    Debug.DrawLine(transform.position, hit.Position, Color.red, 0.1f);
                }   
            }
        }

        hits.Dispose();
        
        //Invoke(nameof(ExplosionForce), 0.1f);
    }

    [Header("Explosion settings")]
    public float explosionForce = 50f;
    public float explosionRadius = 15f;
    [Tooltip("0 = no lift, 1 = full upward bias, like Rigidbody.AddExplosionForce's upwardsModifier")]
    public float upwardsModifier = 0f;

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

    void ExplosionForce()
    {
        
        int count = _physicsBodyQuery.CalculateEntityCount();
        if (count == 0) return;

        var velocities = _physicsBodyQuery.ToComponentDataArray<PhysicsVelocity>(Allocator.Temp);
        var masses = _physicsBodyQuery.ToComponentDataArray<PhysicsMass>(Allocator.Temp);
        var colliders = _physicsBodyQuery.ToComponentDataArray<PhysicsCollider>(Allocator.Temp);
        var transforms = _physicsBodyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        float3 explosionPos = transform.position;
        float dt = Time.fixedDeltaTime;

        for (int i = 0; i < count; i++)
        {
            if (masses[i].InverseMass == 0f)
                continue; // kinematic/immovable body, nothing to apply

            var velocity = velocities[i];
            velocity.ApplyExplosionForce(
                masses[i], colliders[i],
                transforms[i].Position, transforms[i].Rotation,
                explosionForce, explosionPos, explosionRadius,
                dt, math.up(), upwardsModifier);

            velocities[i] = velocity;
        }

        _physicsBodyQuery.CopyFromComponentDataArray(velocities);

        velocities.Dispose();
        masses.Dispose();
        colliders.Dispose();
        transforms.Dispose();
    }

    // Optional: visualize the blast radius in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
