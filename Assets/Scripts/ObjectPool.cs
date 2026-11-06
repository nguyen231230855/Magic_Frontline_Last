using System.Collections.Generic;
using UnityEngine;

public static class ObjectPool
{
    static readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
    static readonly Dictionary<GameObject, GameObject> prefabs = new();

    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(prefab, out var pool))
            pools[prefab] = pool = new Stack<GameObject>();

        GameObject instance = pool.Count > 0 ? pool.Pop() : Object.Instantiate(prefab);
        prefabs[instance] = prefab;
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public static void Release(GameObject instance)
    {
        if (instance == null || !prefabs.TryGetValue(instance, out var prefab))
        {
            if (instance != null) Object.Destroy(instance);
            return;
        }

        instance.SetActive(false);
        pools[prefab].Push(instance);
    }
}
