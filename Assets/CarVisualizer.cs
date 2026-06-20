using UnityEngine;
using System.IO;

public class CarVisualizer : MonoBehaviour
{
    [Header("Sprite Settings")]
    public string imageName = "top_down_car.png";
    public float carWidth = 2.4f;
    public float carLength = 5.0f;
    public float rotationOffset = 0f; // Eğer araba görseli ters veya yan bakıyorsa buradan ayarlanabilir

    void Start()
    {
        // Ana objenin kendi MeshRenderer'ını kapatıyoruz (mavi küpü gizlemek için)
        MeshRenderer parentRenderer = GetComponent<MeshRenderer>();
        if (parentRenderer != null)
        {
            parentRenderer.enabled = false;
        }

        // Varsa eski görselleri temizle
        Transform oldVisuals = transform.Find("CarVisuals");
        if (oldVisuals != null)
        {
            Destroy(oldVisuals.gameObject);
        }

        // Yeni görseller için bir kapsayıcı oluştur
        GameObject containerObj = new GameObject("CarVisuals");
        containerObj.transform.SetParent(transform, false);
        Transform visualsContainer = containerObj.transform;

        string path = Path.Combine(Application.dataPath, imageName);

        if (File.Exists(path))
        {
            CreateCarFromSprite(path, visualsContainer);
        }
        else
        {
            Debug.LogWarning($"Görsel bulunamadı: {path}. 3D şematik modele dönülüyor.");
            CreateProcedural3DCar(visualsContainer);
        }
    }

    void CreateCarFromSprite(string filePath, Transform container)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(fileData);

        // Beyaz arka planı şeffaf yap
        Color[] pixels = texture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            // Piksel beyaza çok yakınsa (arka plan) şeffaf yap
            if (pixels[i].r > 0.94f && pixels[i].g > 0.94f && pixels[i].b > 0.94f)
            {
                pixels[i] = Color.clear;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        // Sprite oluştur
        Sprite carSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

        GameObject spriteObj = new GameObject("CarSprite");
        spriteObj.transform.SetParent(container, false);

        // Yere yatay şekilde konumlandır (Zaten yanal kontrol y=0.6'da olduğu için y=-0.5f ile yola sıfırlarız)
        spriteObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        
        // Quad gibi yatay durması için X ekseninde 90 derece döndür
        spriteObj.transform.localRotation = Quaternion.Euler(90f, rotationOffset, 0f);

        SpriteRenderer sr = spriteObj.AddComponent<SpriteRenderer>();
        sr.sprite = carSprite;
        sr.sortingOrder = 5; // Yolun üstünde görünmesi için öncelik

        // Boyutlandırma
        float spriteWidth = carSprite.bounds.size.x;
        float spriteHeight = carSprite.bounds.size.y;

        if (spriteWidth > 0 && spriteHeight > 0)
        {
            float scaleX = carWidth / spriteWidth;
            float scaleY = carLength / spriteHeight;
            spriteObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }

    void CreateProcedural3DCar(Transform container)
    {
        // Eski 3D şematik araba modelimiz (yedek plan)
        Material bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = new Color(0.08f, 0.45f, 0.85f);
        bodyMat.SetFloat("_Metallic", 0.85f);
        bodyMat.SetFloat("_Glossiness", 0.85f);

        Material cabinMat = new Material(Shader.Find("Standard"));
        cabinMat.color = new Color(0.12f, 0.12f, 0.12f);
        cabinMat.SetFloat("_Metallic", 0.4f);
        cabinMat.SetFloat("_Glossiness", 0.9f);

        Material wheelMat = new Material(Shader.Find("Standard"));
        wheelMat.color = new Color(0.15f, 0.15f, 0.15f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Chassis";
        body.transform.SetParent(container, false);
        body.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        body.transform.localScale = new Vector3(1.4f, 0.5f, 3.2f);
        body.GetComponent<MeshRenderer>().material = bodyMat;
        Destroy(body.GetComponent<Collider>());

        GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cabin.name = "Cabin";
        cabin.transform.SetParent(container, false);
        cabin.transform.localPosition = new Vector3(0f, 0.22f, -0.2f);
        cabin.transform.localScale = new Vector3(1.2f, 0.45f, 1.6f);
        cabin.GetComponent<MeshRenderer>().material = cabinMat;
        Destroy(cabin.GetComponent<Collider>());
    }
}
