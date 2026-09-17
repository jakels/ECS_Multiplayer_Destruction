using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace ECSRBExample.ECS_Destructable_Geometry
{
    public class ChunkAuthoring : MonoBehaviour
    {
        public Color Color = Color.white;
        private class ChunkBaker : Baker<ChunkAuthoring>
        {
            public override void Bake(ChunkAuthoring authoring)
            {
                var  thisEntity = GetEntity(TransformUsageFlags.Dynamic);    
                AddComponent(thisEntity, new ChunkComponent
                {
                    simulateTime = 0f,
                    staticPosition = Vector3.zero
                });
                AddComponent(thisEntity, new ActiveChunkTag()
                {
                    test = 0
                });
                if(DestructableSystem.DebugChunkColouring)
                {
                    AddComponent(thisEntity, new URPMaterialPropertyBaseColor
                    {
                        Value = (Vector4)authoring.Color.linear
                    });   
                }
            }
        }
    }
}