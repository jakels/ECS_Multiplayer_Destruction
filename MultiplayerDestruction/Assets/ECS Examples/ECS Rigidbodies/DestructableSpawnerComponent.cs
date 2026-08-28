using Unity.Entities;

namespace ECSRBExample
{
    public struct DestructableSpawnerComponent : IComponentData
    {
        public Entity chunkPrefab;
        public int width;
        public int height;
        public int depth;
    }
}