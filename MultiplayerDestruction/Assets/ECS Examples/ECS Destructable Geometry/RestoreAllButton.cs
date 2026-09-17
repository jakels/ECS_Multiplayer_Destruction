using Unity.Entities;
using UnityEngine;

public class RestoreAllButton : MonoBehaviour
{
    [ContextMenu("Restore All")]
    public void Restore()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        em.CreateEntity(typeof(RestoreRequest));
    }
}
