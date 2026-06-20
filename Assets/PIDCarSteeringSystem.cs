using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Splines;
using UnityEngine.UI;

public class PIDCarSteeringSystem : MonoBehaviour
{
    [Header("Spline / Road")]
    public SplineContainer roadSpline;

    [Header("Car Settings")]
    public float carMass = 1200f;
    public float targetSpeed = 8f;
    public float currentSpeed = 0f;
    public float maxSpeed = 20f;
    public float motorPower = 900f;
    public float brakePower = 500f;
    public float carHeightOffset = 0.6f;

    [Header("Steering Settings")]
    public float maxSteeringRate = 120f;
    public float lookAheadDistance = 8f;
    public float centerPullStrength = 3.5f;
    public int splineSearchSamples = 180;

    [Header("PID Steering Values")]
    public float Kp = 3.5f;
    public float Ki = 0.04f;
    public float Kd = 1.2f;

    [Header("Live Output")]
    public float error;
    public float headingError;
    public float lateralError;
    public float pValue;
    public float iValue;
    public float dValue;
    public float pidOutput;
    public float torque;
    public int lapCount;

    TMP_Text telemetryText;
    TMP_Text tableText;
    TMP_Text explanationText;
    TMP_Text carLabel;

    TMP_InputField kpInput;
    TMP_InputField kiInput;
    TMP_InputField kdInput;
    TMP_InputField speedInput;
    TMP_InputField massInput;

    readonly List<string> tableRows = new List<string>();

    float previousError;
    float previousNearestT;
    float pathT;
    float sampleTimer;
    int seconds;

    RectTransform graphArea;
    readonly List<float> speedGraph = new List<float>();
    readonly List<float> errorGraph = new List<float>();
    readonly List<float> pidGraph = new List<float>();
    readonly List<GameObject> graphObjects = new List<GameObject>();

    void Start()
    {
        if (gameObject.GetComponent<CarVisualizer>() == null)
        {
            gameObject.AddComponent<CarVisualizer>();
        }
        if (roadSpline == null)
        {
            Debug.LogError("RoadSpline bağlanmadı. Car objesindeki PIDCarSteeringSystem scriptinde RoadSpline alanına RoadSpline objesini sürükle.");
            return;
        }

        SetupUI();
        ResetSimulation();
    }

    void Update()
    {
        if (roadSpline == null)
            return;

        float dt = Time.deltaTime;

        if (dt <= 0f)
            return;

        UpdateCar(dt);
        UpdateTelemetry();
        UpdateCarLabel();

        sampleTimer += dt;

        if (sampleTimer >= 1f)
        {
            sampleTimer = 0f;
            seconds++;

            AddTableRow();
            AddGraphData();
            DrawGraph();
        }
    }

    void UpdateCar(float dt)
    {
        float splineLength = Mathf.Max(roadSpline.CalculateLength(), 1f);

        bool pidIsActive =
            Mathf.Abs(Kp) > 0.0001f ||
            Mathf.Abs(Ki) > 0.0001f ||
            Mathf.Abs(Kd) > 0.0001f;

        float speedError = targetSpeed - currentSpeed;

        if (speedError >= 0f)
            torque = Mathf.Clamp(speedError * motorPower, 0f, 3000f);
        else
            torque = Mathf.Clamp(speedError * brakePower, -2500f, 0f);

        float acceleration = torque / Mathf.Max(carMass, 1f);

        currentSpeed += acceleration * dt;
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);

        if (pidIsActive)
        {
            float oldPathT = pathT;

            pathT += (currentSpeed * dt) / splineLength;

            while (pathT >= 1f)
            {
                pathT -= 1f;
                lapCount++;
            }

            float targetT = pathT + lookAheadDistance / splineLength;

            if (targetT > 1f)
                targetT -= 1f;

            Vector3 centerPoint = GetSplinePoint(pathT);
            Vector3 targetPoint = GetSplinePoint(targetT);

            centerPoint.y = carHeightOffset;
            targetPoint.y = carHeightOffset;

            Vector3 beforeSnapPosition = transform.position;
            beforeSnapPosition.y = carHeightOffset;

            Vector3 toCenter = centerPoint - beforeSnapPosition;
            toCenter.y = 0f;

            lateralError = Vector3.Dot(toCenter, transform.right);

            Vector3 desiredDirection = targetPoint - centerPoint;
            desiredDirection.y = 0f;

            if (desiredDirection.sqrMagnitude < 0.001f)
                desiredDirection = transform.forward;

            desiredDirection.Normalize();

            headingError = Vector3.SignedAngle(transform.forward, desiredDirection, Vector3.up);

            float lateralAngle =
                Mathf.Atan2(lateralError, Mathf.Max(lookAheadDistance, 0.1f)) * Mathf.Rad2Deg;

            error = headingError + lateralAngle;

            pValue = Kp * error;

            iValue += Ki * error * dt;
            iValue = Mathf.Clamp(iValue, -80f, 80f);

            dValue = Kd * (error - previousError) / Mathf.Max(dt, 0.0001f);

            pidOutput = pValue + iValue + dValue;
            pidOutput = Mathf.Clamp(pidOutput, -maxSteeringRate, maxSteeringRate);

            transform.Rotate(Vector3.up, pidOutput * dt);

            // PID açıkken araç yolun merkez çizgisine oturur.
            // Böylece çizginin etrafında dolanmak yerine doğrudan çizginin üstünden ilerler.
            transform.position = centerPoint;

            previousError = error;
            previousNearestT = oldPathT;
        }
        else
        {
            // PID kapalıyken direksiyon çıkışı 0 olur ve araç kendi baktığı yönde düz gider.
            headingError = 0f;
            lateralError = 0f;
            error = 0f;
            pValue = 0f;
            iValue = 0f;
            dValue = 0f;
            pidOutput = 0f;

            transform.position += transform.forward * currentSpeed * dt;

            Vector3 pos = transform.position;
            pos.y = carHeightOffset;
            transform.position = pos;
        }
    }

    Vector3 GetSplinePoint(float t)
    {
        roadSpline.Evaluate(t, out float3 pos3, out float3 tangent3, out float3 up3);
        return new Vector3(pos3.x, pos3.y, pos3.z);
    }

    Vector3 GetSplineTangent(float t)
    {
        roadSpline.Evaluate(t, out float3 pos3, out float3 tangent3, out float3 up3);
        return new Vector3(tangent3.x, tangent3.y, tangent3.z);
    }

    float FindNearestSplineT(Vector3 worldPos, int samples)
    {
        float bestT = 0f;
        float bestDist = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;

            Vector3 p = GetSplinePoint(t);
            p.y = worldPos.y;

            float dist = Vector3.SqrMagnitude(worldPos - p);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestT = t;
            }
        }

        return bestT;
    }

    void ResetSimulation()
    {
        currentSpeed = 0f;
        torque = 0f;
        lapCount = 0;
        previousNearestT = 0f;
        pathT = 0f;

        ResetPID();

        roadSpline.Evaluate(0f, out float3 pos3, out float3 tan3, out float3 up3);

        transform.position = new Vector3(pos3.x, carHeightOffset, pos3.z);

        Vector3 fwd = new Vector3(tan3.x, 0f, tan3.z).normalized;

        if (fwd.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        seconds = 0;

        tableRows.Clear();
        speedGraph.Clear();
        errorGraph.Clear();
        pidGraph.Clear();

        if (tableText != null)
            tableText.text = "";
    }

    void ResetPID()
    {
        previousError = 0f;
        error = 0f;
        headingError = 0f;
        lateralError = 0f;
        pValue = 0f;
        iValue = 0f;
        dValue = 0f;
        pidOutput = 0f;
    }

    void SetupUI()
    {
        SetupEventSystem();

        GameObject canvasObj = new GameObject("Auto_PID_Canvas");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        CreateTitle(canvasObj.transform);

        GameObject leftPanel = CreatePanel(canvasObj.transform, "PID Ayar Paneli",
            new Vector2(12, -12),
            new Vector2(350, 485),
            true);

        GameObject rightTop = CreatePanel(canvasObj.transform, "Canli Degerler",
            new Vector2(-12, -12),
            new Vector2(390, 395),
            false);

        GameObject rightMid = CreatePanel(canvasObj.transform, "Aciklama",
            new Vector2(-12, -420),
            new Vector2(390, 155),
            false);

        GameObject bottomLeft = CreatePanel(canvasObj.transform, "Tablo",
            new Vector2(12, 12),
            new Vector2(940, 330),
            true,
            false);

        GameObject bottomRight = CreatePanel(canvasObj.transform, "Grafik",
            new Vector2(-12, 12),
            new Vector2(860, 330),
            false,
            false);

        CreateSettings(leftPanel.transform);
        CreateTelemetry(rightTop.transform);
        CreateExplanation(rightMid.transform);
        CreateTable(bottomLeft.transform);
        CreateGraph(bottomRight.transform);
        CreateCarLabel(canvasObj.transform);
    }

    void SetupEventSystem()
    {
        UnityEngine.EventSystems.EventSystem es =
            FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();

        if (es == null)
        {
            GameObject obj = new GameObject("EventSystem");
            obj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            obj.AddComponent<InputSystemUIInputModule>();
            return;
        }

        UnityEngine.EventSystems.StandaloneInputModule oldModule =
            es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        if (oldModule != null)
            Destroy(oldModule);

        if (es.GetComponent<InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    void CreateTitle(Transform parent)
    {
        TMP_Text txt = CreateText(parent,
            "PID Kontrollu Direksiyonlu\nArac Simulasyonu",
            20,
            TextAlignmentOptions.Center);

        RectTransform rect = txt.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(12f, -510f);
        rect.sizeDelta = new Vector2(350f, 60f);
    }

    GameObject CreatePanel(
        Transform parent,
        string title,
        Vector2 pos,
        Vector2 size,
        bool left,
        bool top = true)
    {
        GameObject panel = new GameObject(title);
        panel.transform.SetParent(parent, false);

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.62f);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        if (left)
        {
            rect.anchorMin = top ? new Vector2(0, 1) : new Vector2(0, 0);
            rect.anchorMax = top ? new Vector2(0, 1) : new Vector2(0, 0);
            rect.pivot = top ? new Vector2(0, 1) : new Vector2(0, 0);
        }
        else
        {
            rect.anchorMin = top ? new Vector2(1, 1) : new Vector2(1, 0);
            rect.anchorMax = top ? new Vector2(1, 1) : new Vector2(1, 0);
            rect.pivot = top ? new Vector2(1, 1) : new Vector2(1, 0);
        }

        rect.anchoredPosition = pos;

        TMP_Text titleText = CreateText(panel.transform, title, 20, TextAlignmentOptions.Center);

        RectTransform tRect = titleText.GetComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0, 1);
        tRect.anchorMax = new Vector2(1, 1);
        tRect.pivot = new Vector2(0.5f, 1);
        tRect.anchoredPosition = new Vector2(0, -8);
        tRect.sizeDelta = new Vector2(-16, 30);

        return panel;
    }

    void CreateSettings(Transform parent)
    {
        kpInput = CreateInput(parent, "Kp", new Vector2(18, -55));
        kiInput = CreateInput(parent, "Ki", new Vector2(18, -105));
        kdInput = CreateInput(parent, "Kd", new Vector2(18, -155));
        speedInput = CreateInput(parent, "Hedef Hiz", new Vector2(18, -205));
        massInput = CreateInput(parent, "Kutle", new Vector2(18, -255));

        kpInput.text = Kp.ToString(CultureInfo.InvariantCulture);
        kiInput.text = Ki.ToString(CultureInfo.InvariantCulture);
        kdInput.text = Kd.ToString(CultureInfo.InvariantCulture);
        speedInput.text = targetSpeed.ToString(CultureInfo.InvariantCulture);
        massInput.text = carMass.ToString(CultureInfo.InvariantCulture);

        CreateButton(parent, "Degerleri Uygula",
            new Vector2(18, -305),
            new Vector2(310, 32),
            ApplyValues,
            new Color(0.2f, 0.45f, 0.9f));

        CreateButton(parent, "PID Kapali",
            new Vector2(18, -345),
            new Vector2(145, 30),
            PIDOff,
            Color.gray);

        CreateButton(parent, "Zayif PID",
            new Vector2(183, -345),
            new Vector2(145, 30),
            PIDWeak,
            new Color(0.2f, 0.7f, 0.2f));

        CreateButton(parent, "Normal PID",
            new Vector2(18, -382),
            new Vector2(145, 30),
            PIDNormal,
            new Color(0.2f, 0.45f, 0.9f));

        CreateButton(parent, "Agresif PID",
            new Vector2(183, -382),
            new Vector2(145, 30),
            PIDAggressive,
            new Color(0.85f, 0.35f, 0.2f));

        CreateButton(parent, "Simulasyonu Sifirla",
            new Vector2(18, -419),
            new Vector2(310, 30),
            ResetSimulation,
            new Color(0.6f, 0.2f, 0.8f));

        CreateButton(parent, "Tabloyu PDF Kaydet",
            new Vector2(18, -454),
            new Vector2(310, 30),
            SaveTableAsPdf,
            new Color(0.1f, 0.55f, 0.55f));
    }

    TMP_InputField CreateInput(Transform parent, string label, Vector2 pos)
    {
        TMP_Text lbl = CreateText(parent, label + ":", 16, TextAlignmentOptions.Left);

        RectTransform lblRect = lbl.GetComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0, 1);
        lblRect.anchorMax = new Vector2(0, 1);
        lblRect.pivot = new Vector2(0, 1);
        lblRect.anchoredPosition = pos;
        lblRect.sizeDelta = new Vector2(125, 30);

        GameObject inputObj = new GameObject(label + " Input");
        inputObj.transform.SetParent(parent, false);

        Image bg = inputObj.AddComponent<Image>();
        bg.color = Color.white;

        RectTransform rect = inputObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(pos.x + 130, pos.y);
        rect.sizeDelta = new Vector2(180, 28);

        TMP_InputField input = inputObj.AddComponent<TMP_InputField>();

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(inputObj.transform, false);

        RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(6, 2);
        viewportRect.offsetMax = new Vector2(-6, -2);

        viewportObj.AddComponent<RectMask2D>();

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(viewportObj.transform, false);

        TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = 16;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = false;

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        input.textViewport = viewportRect;
        input.textComponent = text;
        input.targetGraphic = bg;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 8;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;

        return input;
    }

    void CreateButton(
        Transform parent,
        string text,
        Vector2 pos,
        Vector2 size,
        UnityEngine.Events.UnityAction action,
        Color color)
    {
        GameObject obj = new GameObject(text);
        obj.transform.SetParent(parent, false);

        Image img = obj.AddComponent<Image>();
        img.color = color;

        Button btn = obj.AddComponent<Button>();
        btn.onClick.AddListener(action);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        TMP_Text txt = CreateText(obj.transform, text, 14, TextAlignmentOptions.Center);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = Vector2.zero;
        txt.rectTransform.offsetMax = Vector2.zero;
    }

    void CreateTelemetry(Transform parent)
    {
        telemetryText = CreateText(parent, "", 16, TextAlignmentOptions.Left);

        RectTransform rect = telemetryText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -45);
        rect.sizeDelta = new Vector2(-24, 330);
    }

    void CreateExplanation(Transform parent)
    {
        explanationText = CreateText(parent,
            "Bu sistemde PID direksiyon kontrolu yapar.\n" +
            "Hata = yol merkezi ile arac yonu arasindaki farktir.\n" +
            "PID cikisi = direksiyon donme komutudur.\n" +
            "Kp=Ki=Kd=0 olursa arac duz gider.",
            14,
            TextAlignmentOptions.Left);

        RectTransform rect = explanationText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -42);
        rect.sizeDelta = new Vector2(-24, 100);
    }

    void CreateTable(Transform parent)
    {
        tableText = CreateText(parent, "", 13, TextAlignmentOptions.TopLeft);

        RectTransform rect = tableText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = new Vector2(14, 14);
        rect.offsetMax = new Vector2(-14, -44);
    }

    void CreateGraph(Transform parent)
    {
        TMP_Text legend = CreateText(
            parent,
            "<color=#00FF00>■</color> Hiz   <color=#FFFF00>■</color> Hata Aci   <color=#00FFFF>■</color> PID Cikisi",
            15,
            TextAlignmentOptions.Center
        );

        RectTransform legendRect = legend.GetComponent<RectTransform>();
        legendRect.anchorMin = new Vector2(0, 1);
        legendRect.anchorMax = new Vector2(1, 1);
        legendRect.pivot = new Vector2(0.5f, 1);
        legendRect.anchoredPosition = new Vector2(0, -36);
        legendRect.sizeDelta = new Vector2(-20, 24);

        GameObject graphObj = new GameObject("Graph");
        graphObj.transform.SetParent(parent, false);

        graphArea = graphObj.AddComponent<RectTransform>();
        graphArea.anchorMin = new Vector2(0, 0);
        graphArea.anchorMax = new Vector2(1, 1);
        graphArea.offsetMin = new Vector2(40, 35);
        graphArea.offsetMax = new Vector2(-40, -62);

        Image bg = graphObj.AddComponent<Image>();
        bg.color = new Color(1, 1, 1, 0.05f);

        CreateGraphGrid(graphArea, 8, 5);
    }


    void CreateGraphGrid(RectTransform parent, int verticalDivisions, int horizontalDivisions)
    {
        float widthStep = 1f / verticalDivisions;
        float heightStep = 1f / horizontalDivisions;

        for (int i = 0; i <= verticalDivisions; i++)
        {
            GameObject line = new GameObject("Grid Vertical " + i);
            line.transform.SetParent(parent, false);

            Image img = line.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.14f);

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i * widthStep, 0f);
            rect.anchorMax = new Vector2(i * widthStep, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1.2f, 0f);
        }

        for (int j = 0; j <= horizontalDivisions; j++)
        {
            GameObject line = new GameObject("Grid Horizontal " + j);
            line.transform.SetParent(parent, false);

            Image img = line.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, j == horizontalDivisions / 2 ? 0.28f : 0.14f);

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, j * heightStep);
            rect.anchorMax = new Vector2(1f, j * heightStep);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 1.2f);
        }
    }

    void CreateCarLabel(Transform parent)
    {
        carLabel = CreateText(parent, "", 18, TextAlignmentOptions.Center);

        RectTransform rect = carLabel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(240, 50);
    }

    TMP_Text CreateText(
        Transform parent,
        string text,
        int size,
        TextAlignmentOptions align)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);

        TMP_Text txt = obj.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = size;
        txt.color = Color.white;
        txt.alignment = align;
        txt.richText = true;

        return txt;
    }

    void ApplyValues()
    {
        Kp = Parse(kpInput.text, Kp);
        Ki = Parse(kiInput.text, Ki);
        Kd = Parse(kdInput.text, Kd);
        targetSpeed = Parse(speedInput.text, targetSpeed);
        carMass = Parse(massInput.text, carMass);

        ResetPID();
    }

    float Parse(string s, float fallback)
    {
        if (string.IsNullOrWhiteSpace(s))
            return fallback;

        s = s.Replace(",", ".");

        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            return val;

        return fallback;
    }

    void PIDOff()
    {
        Kp = 0;
        Ki = 0;
        Kd = 0;

        UpdateInputs();
        ResetPID();
    }

    void PIDWeak()
    {
        Kp = 1.2f;
        Ki = 0.01f;
        Kd = 0.35f;

        UpdateInputs();
        ResetPID();
    }

    void PIDNormal()
    {
        Kp = 3.5f;
        Ki = 0.04f;
        Kd = 1.2f;

        UpdateInputs();
        ResetPID();
    }

    void PIDAggressive()
    {
        Kp = 6f;
        Ki = 0.08f;
        Kd = 2.2f;

        UpdateInputs();
        ResetPID();
    }

    void UpdateInputs()
    {
        kpInput.text = Kp.ToString(CultureInfo.InvariantCulture);
        kiInput.text = Ki.ToString(CultureInfo.InvariantCulture);
        kdInput.text = Kd.ToString(CultureInfo.InvariantCulture);
    }

    void UpdateTelemetry()
    {
        telemetryText.text =
            "Kutle: " + carMass.ToString("0.0") + " kg\n" +
            "Hedef Hiz: " + targetSpeed.ToString("0.00") + " m/s\n" +
            "Mevcut Hiz: " + currentSpeed.ToString("0.00") + " m/s\n" +
            "Tork: " + torque.ToString("0.00") + " Nm\n" +
            "Tur Sayisi: " + lapCount + "\n\n" +
            "Kp: " + Kp.ToString("0.###") + "\n" +
            "Ki: " + Ki.ToString("0.###") + "\n" +
            "Kd: " + Kd.ToString("0.###") + "\n\n" +
            "Hata Aci: " + error.ToString("0.00") + " derece\n" +
            "Merkez Hata: " + lateralError.ToString("0.00") + "\n" +
            "P: " + pValue.ToString("0.00") + "\n" +
            "I: " + iValue.ToString("0.00") + "\n" +
            "D: " + dValue.ToString("0.00") + "\n" +
            "PID Cikisi: " + pidOutput.ToString("0.00");
    }

    void UpdateCarLabel()
    {
        if (Camera.main == null)
            return;

        Vector3 world = transform.position;
        Vector3 screen = Camera.main.WorldToScreenPoint(world);

        if (screen.z < 0f)
        {
            carLabel.text = "";
            return;
        }

        screen.y -= 70f; // Arabanın altına kaydır
        carLabel.rectTransform.position = screen;

        carLabel.text =
            "Hiz: " + currentSpeed.ToString("0.0") + " m/s\n" +
            "Tur: " + lapCount;
    }

    void AddTableRow()
    {
        string row =
            seconds + "s | " +
            "Tur:" + lapCount + " | " +
            "Kutle:" + carMass.ToString("0") + "kg | " +
            "Hedef:" + targetSpeed.ToString("0.00") + " | " +
            "Hiz:" + currentSpeed.ToString("0.00") + " | " +
            "Tork:" + torque.ToString("0.0") + " | " +
            "Hata:" + error.ToString("0.00") + " | " +
            "Merkez:" + lateralError.ToString("0.00") + " | " +
            "PID:" + pidOutput.ToString("0.0") + " | " +
            "Kp:" + Kp.ToString("0.###") + " | " +
            "Ki:" + Ki.ToString("0.###") + " | " +
            "Kd:" + Kd.ToString("0.###") + " | " +
            "P:" + pValue.ToString("0.0") + " | " +
            "I:" + iValue.ToString("0.0") + " | " +
            "D:" + dValue.ToString("0.0");

        tableRows.Add(row);

        if (tableRows.Count > 22)
            tableRows.RemoveAt(0);

        tableText.text = string.Join("\n", tableRows);
    }

    void AddGraphData()
    {
        speedGraph.Add(currentSpeed);
        errorGraph.Add(error);
        pidGraph.Add(pidOutput);

        Trim(speedGraph);
        Trim(errorGraph);
        Trim(pidGraph);
    }

    void Trim(List<float> list)
    {
        while (list.Count > 50)
            list.RemoveAt(0);
    }

    void DrawGraph()
    {
        foreach (GameObject obj in graphObjects)
            Destroy(obj);

        graphObjects.Clear();

        DrawLine(speedGraph, Color.green, 0, maxSpeed, "Hiz: " + currentSpeed.ToString("0.00") + " m/s");
        DrawLine(errorGraph, Color.yellow, -90, 90, "Hata: " + error.ToString("0.0") + "°");
        DrawLine(pidGraph, Color.cyan, -maxSteeringRate, maxSteeringRate, "PID: " + pidOutput.ToString("0.0"));
    }

    void DrawLine(List<float> values, Color color, float min, float max, string label)
    {
        if (values.Count < 2)
            return;

        float width = graphArea.rect.width;
        float height = graphArea.rect.height;

        Vector2 last = Vector2.zero;

        for (int i = 0; i < values.Count - 1; i++)
        {
            float x1 = (i / 49f) * width;
            float x2 = ((i + 1) / 49f) * width;

            float y1 = Mathf.InverseLerp(min, max, values[i]) * height;
            float y2 = Mathf.InverseLerp(min, max, values[i + 1]) * height;

            Vector2 a = new Vector2(x1, y1);
            Vector2 b = new Vector2(x2, y2);

            CreateGraphLine(a, b, color);

            last = b;
        }

        CreateGraphLabel(last, label, color);
    }

    void CreateGraphLine(Vector2 a, Vector2 b, Color color)
    {
        GameObject line = new GameObject("Line");
        line.transform.SetParent(graphArea, false);

        Image img = line.AddComponent<Image>();
        img.color = color;

        RectTransform rect = line.GetComponent<RectTransform>();

        Vector2 dir = b - a;

        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0.5f);

        rect.anchoredPosition = a;
        rect.sizeDelta = new Vector2(dir.magnitude, 4);

        rect.rotation = Quaternion.Euler(0, 0,
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        graphObjects.Add(line);
    }

    void CreateGraphLabel(Vector2 pos, string text, Color color)
    {
        GameObject obj = new GameObject("Graph Label");
        obj.transform.SetParent(graphArea, false);

        TMP_Text label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 14;
        label.color = color;
        label.alignment = TextAlignmentOptions.Left;

        RectTransform rect = label.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0.5f);

        rect.anchoredPosition = new Vector2(
            Mathf.Clamp(pos.x + 8f, 0f, graphArea.rect.width - 130f),
            Mathf.Clamp(pos.y, 12f, graphArea.rect.height - 12f)
        );

        rect.sizeDelta = new Vector2(155, 26);

        graphObjects.Add(obj);
    }

    void SaveTableAsPdf()
    {
        string folder = Application.dataPath + "/PID_Exports";

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string filePath = GetNextPdfFilePath(folder);

        StringBuilder content = new StringBuilder();

        content.AppendLine("PID KONTROLLU DIREKSIYONLU ARAC SIMULASYONU");
        content.AppendLine("");
        content.AppendLine("Kp: " + Kp);
        content.AppendLine("Ki: " + Ki);
        content.AppendLine("Kd: " + Kd);
        content.AppendLine("Arac Kutlesi: " + carMass + " kg");
        content.AppendLine("Hedef Hiz: " + targetSpeed + " m/s");
        content.AppendLine("");
        content.AppendLine("ZAMAN BAZLI TABLO");
        content.AppendLine("--------------------------------------------------");

        foreach (string row in tableRows)
            content.AppendLine(row);

        WriteSimplePdf(filePath, content.ToString());

        Debug.Log("PDF Kaydedildi: " + filePath);
    }

    string GetNextPdfFilePath(string folder)
    {
        int index = 1;

        while (true)
        {
            string fileName = "PID_Tablo_Ciktisi_" + index + ".pdf";
            string filePath = Path.Combine(folder, fileName);

            if (!File.Exists(filePath))
                return filePath;

            index++;
        }
    }

    void WriteSimplePdf(string filePath, string text)
    {
        text = text.Replace("\\", "\\\\")
                   .Replace("(", "\\(")
                   .Replace(")", "\\)");

        string[] lines = text.Split('\n');

        StringBuilder stream = new StringBuilder();

        stream.AppendLine("BT");
        stream.AppendLine("/F1 8 Tf");
        stream.AppendLine("35 805 Td");

        foreach (string line in lines)
        {
            string safeLine = line.TrimEnd('\r');

            if (safeLine.Length > 115)
                safeLine = safeLine.Substring(0, 115);

            stream.AppendLine("(" + safeLine + ") Tj");
            stream.AppendLine("0 -11 Td");
        }

        stream.AppendLine("ET");

        string streamText = stream.ToString();

        List<string> objs = new List<string>();

        objs.Add("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        objs.Add("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        objs.Add("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");
        objs.Add("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>\nendobj\n");
        objs.Add("5 0 obj\n<< /Length " + streamText.Length + " >>\nstream\n" + streamText + "\nendstream\nendobj\n");

        StringBuilder pdf = new StringBuilder();

        pdf.AppendLine("%PDF-1.4");

        List<int> offsets = new List<int>();
        offsets.Add(0);

        foreach (string obj in objs)
        {
            offsets.Add(pdf.Length);
            pdf.Append(obj);
        }

        int xref = pdf.Length;

        pdf.AppendLine("xref");
        pdf.AppendLine("0 6");
        pdf.AppendLine("0000000000 65535 f ");

        for (int i = 1; i < offsets.Count; i++)
            pdf.AppendLine(offsets[i].ToString("0000000000") + " 00000 n ");

        pdf.AppendLine("trailer");
        pdf.AppendLine("<< /Size 6 /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xref.ToString());
        pdf.AppendLine("%%EOF");

        File.WriteAllText(filePath, pdf.ToString(), Encoding.ASCII);
    }
}
