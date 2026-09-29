using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    public Transform playerCamera;
    public GameObject grass;
    public GameObject cobblestone;
    public GameObject highlight;

    private Material highlightMaterial;

    void Start()
    {
        highlightMaterial = highlight.GetComponent<Renderer>().material;
    }

    void Update()
    {
        if (playerCamera.position.y < 0 || playerCamera.position.y >= 64 || playerCamera.position.x < -128 || playerCamera.position.x >= 128 || playerCamera.position.z < -128 || playerCamera.position.z >= 128)
        {
            highlight.SetActive(false);
            return;
        }

        RaycastHit hit;

        float downAmount = Mathf.Clamp01((-playerCamera.forward.y - 0.15f) / 0.85f);
        float reach = 3f + downAmount;

        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, reach))
        {
            Vector3Int blockPos = Vector3Int.FloorToInt(hit.point - hit.normal * 0.5f);

            if (!MeshManager.Exists(blockPos))
            {
                highlight.SetActive(false);
                return;
            }

            highlight.SetActive(true);
            Vector3 faceCenter = (Vector3)blockPos + new Vector3(0.5f, 0.5f, 0.5f) + hit.normal * 0.501f;

            highlight.transform.position = faceCenter;
            highlight.transform.rotation = Quaternion.LookRotation(-hit.normal);

            float alpha = Mathf.Sin(Time.realtimeSinceStartup * 10f) * 0.2f + 0.4f;

            highlightMaterial.SetFloat("_Alpha", alpha);

            if (Input.GetMouseButtonDown(1))
            {
                MeshManager.Remove(blockPos);
                highlight.SetActive(false);
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Vector3Int direction = Vector3Int.RoundToInt(hit.normal);
                Vector3Int newBlockPos = blockPos + direction;

                if (newBlockPos.y >= 64 || newBlockPos.y < 0 || newBlockPos.x >= 128 || newBlockPos.x < -128 || newBlockPos.z >= 128 || newBlockPos.z < -128) { }
                else
                {
                    if (newBlockPos.y == 42)
                    {
                        MeshManager.Spawn(grass, newBlockPos);
                    }
                    else
                    {
                        MeshManager.Spawn(cobblestone, newBlockPos);
                    }
                }

                highlight.SetActive(false);
                return;
            }
        }
        else
        {
            highlight.SetActive(false);
        }
    }
}