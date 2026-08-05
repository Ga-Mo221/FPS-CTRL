using UnityEngine;

public static class Utils
{
    public static Vector3 GetRandomSpawnPoint()
    {
        return new Vector3(Random.Range(-20f, 20f), 0.2f, Random.Range(-20f, 20f));
    }

    public static void SetRenderLayerInChildren(Transform[] transform, int layerNumber)
    {
        for (var i = transform.Length - 1; i >= 0; i--)
            transform[i].gameObject.layer = layerNumber;
    }
}
