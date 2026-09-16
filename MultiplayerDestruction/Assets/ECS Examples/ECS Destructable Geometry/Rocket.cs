using System;
using UnityEngine;
using Unity.Entities;
using Unity.Physics;
using Unity.Mathematics;

public class Rocket : MonoBehaviour
{
    public float speed = 50f;
    private Vector3 lastPosition;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddRelativeForce(Vector3.up * 1000f, ForceMode.Impulse);
        }
        lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        CheckECSCollision();
        lastPosition = transform.position;
    }

    private void CheckECSCollision()
    {
        // Access default ECS World and Physics System
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        PhysicsWorldSingleton physicsWorld = world.EntityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton))
            .GetSingleton<PhysicsWorldSingleton>();

        // Cast a ray from last position to current position
        Vector3 direction = transform.position - lastPosition;
        float distance = direction.magnitude;

        if (distance <= 0f) return;

        RaycastInput input = new RaycastInput
        {
            Start = lastPosition,
            End = transform.position + transform.up * 2f,
            Filter = CollisionFilter.Default
        };

        if (physicsWorld.CastRay(input, out Unity.Physics.RaycastHit hit))
        {
            // Collision with ECS Entity detected!
            Entity hitEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;

            TriggerExplosion();
        }
    }

    private void OnTriggerEnter(UnityEngine.Collider other)
    {
        // Standard GameObject Collisions
        TriggerExplosion();
    }

    private void TriggerExplosion()
    {
        Exploder exploder = GetComponent<Exploder>();
        if (exploder != null)
        {
            exploder.Explode();
        }
        Destroy(gameObject);
    }
}