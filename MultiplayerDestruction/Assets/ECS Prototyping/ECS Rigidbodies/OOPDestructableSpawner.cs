using UnityEngine;

namespace ECSRBExample
{
    public class OOPDestructableSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")] [SerializeField]
        private GameObject chunkPrefab;

        [SerializeField] private int width = 10;
        [SerializeField] private int height = 10;
        [SerializeField] private int depth = 10;

        private void Start()
        {
            SpawnChunks();
        }

        private void SpawnChunks()
        {
            int totalChunks = width * height * depth;

            for (int spawnCount = 0; spawnCount < totalChunks; spawnCount++)
            {
                Vector3 newPos = Vector3.zero;

                newPos.x = (spawnCount % width) + 0.01f;
                newPos.z = (Mathf.Floor(spawnCount / (float)width) % depth) + 0.01f;
                newPos.y = Mathf.Floor(spawnCount / ((float)depth * (float)width)) + 0.01f;

                Instantiate(chunkPrefab, newPos, Quaternion.identity);
            }
        }
    }
}