using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public GameObject grass;
    public GameObject cobblestone;
    public GameObject meshManager;
    public static List<GameObject> blocks = new List<GameObject>();

    void Start()
    {
        for (int x = -128; x < 128; x++)
        {
            for (int z = -128; z < 128; z++)
            {
                for (int y = 0; y < 42; y++)
                {
                    MeshManager.Spawn(cobblestone, new Vector3(x, y, z));
                }

                MeshManager.Spawn(grass, new Vector3(x, 42, z));
            }
        }
    }

    void Update()
    {
        
    }
}
