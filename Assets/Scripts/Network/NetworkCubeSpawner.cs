using Unity.Netcode;
using UnityEngine;

public class NetworkCubeSpawner : MonoBehaviour
{
    public GameObject cubePrefab;

    public void SpawnCube()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("Only host can spawn");
            return;
        }

        GameObject cube = Instantiate(
            cubePrefab,
            new Vector3(0, 1, 0),
            Quaternion.identity
        );

        cube.GetComponent<NetworkObject>().Spawn();

        Debug.Log("Spawned network cube");
    }
}