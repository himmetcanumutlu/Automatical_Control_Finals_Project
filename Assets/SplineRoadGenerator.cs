using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SplineRoadGenerator : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer roadSpline;

    [Header("Road Settings")]
    public float roadWidth = 18f;
    public int resolution = 120;
    public float yOffset = 0.02f;

    private Mesh roadMesh;

    void Start()
    {
        GenerateRoad();
    }

    void OnValidate()
    {
        if (Application.isPlaying == false)
        {
            GenerateRoad();
        }
    }

    public void GenerateRoad()
    {
        if (roadSpline == null)
            return;

        MeshFilter meshFilter = GetComponent<MeshFilter>();

        roadMesh = new Mesh();
        roadMesh.name = "Generated Road Mesh";

        Vector3[] vertices = new Vector3[(resolution + 1) * 2];
        Vector2[] uvs = new Vector2[(resolution + 1) * 2];
        int[] triangles = new int[resolution * 6];

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;

            roadSpline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);

            Vector3 pos = new Vector3(position.x, position.y + yOffset, position.z);
            Vector3 forward = new Vector3(tangent.x, tangent.y, tangent.z).normalized;

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 leftPoint = pos - right * roadWidth * 0.5f;
            Vector3 rightPoint = pos + right * roadWidth * 0.5f;

            vertices[i * 2] = leftPoint;
            vertices[i * 2 + 1] = rightPoint;

            uvs[i * 2] = new Vector2(0, t * 10f);
            uvs[i * 2 + 1] = new Vector2(1, t * 10f);

            if (i < resolution)
            {
                int triIndex = i * 6;
                int vertIndex = i * 2;

                triangles[triIndex] = vertIndex;
                triangles[triIndex + 1] = vertIndex + 2;
                triangles[triIndex + 2] = vertIndex + 1;

                triangles[triIndex + 3] = vertIndex + 1;
                triangles[triIndex + 4] = vertIndex + 2;
                triangles[triIndex + 5] = vertIndex + 3;
            }
        }

        roadMesh.vertices = vertices;
        roadMesh.triangles = triangles;
        roadMesh.uv = uvs;

        roadMesh.RecalculateNormals();
        roadMesh.RecalculateBounds();

        meshFilter.sharedMesh = roadMesh;
    }
}