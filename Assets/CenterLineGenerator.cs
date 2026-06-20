using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CenterLineGenerator : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer roadSpline;

    [Header("Line Settings")]
    public int lineCount = 40;
    public float lineLength = 2f;
    public float lineWidth = 0.25f;
    public float lineHeight = 0.04f;
    public float yOffset = 0.03f;

    [Header("Material")]
    public Material lineMaterial;

    void Start()
    {
        GenerateCenterLines();
    }

    public void GenerateCenterLines()
    {
        if (roadSpline == null)
        {
            Debug.LogWarning("RoadSpline eksik. RoadSpline objesini bu scriptteki Road Spline kutusuna bırak.");
            return;
        }

        // Eski çizgileri temizle
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        for (int i = 0; i < lineCount; i++)
        {
            float t = i / (float)lineCount;

            roadSpline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);

            Vector3 pos = new Vector3(position.x, position.y + yOffset, position.z);
            Vector3 forward = new Vector3(tangent.x, tangent.y, tangent.z).normalized;

            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "CenterLine_" + i;
            line.transform.SetParent(transform);

            line.transform.position = pos;
            line.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            line.transform.localScale = new Vector3(lineWidth, lineHeight, lineLength);

            if (lineMaterial != null)
            {
                line.GetComponent<MeshRenderer>().material = lineMaterial;
            }
        }
    }
}