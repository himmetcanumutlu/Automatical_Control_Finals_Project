using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

public class PIDDashboardUI : MonoBehaviour
{
    [Header("Target")]
    public CarSplineFollower car;

    [Header("UI Settings")]
    public int maxTableRows = 200;
    public int maxGraphPoints = 100;

    private TMP_Text telemetryText;
    private TMP_Text tableHeaderText;
    private TMP_Text tableContentText;
    private TMP_Text carWorldLabel;
    private TMP_Text explanationText;

    private TMP_InputField kpInput;
    private TMP_InputField kiInput;
    private TMP_InputField kdInput;
    private TMP_InputField targetSpeedInput;
    private TMP_InputField massInput;

    private ScrollRect tableScrollRect;
    private RectTransform tableContentRoot;

    private RectTransform graphArea;
    private RectTransform graphLineLayer;

    private readonly List<float> speedValues = new List<float>();
    private readonly List<float> errorValues = new List<float>();
    private readonly List<float> pidValues = new List<float>();
    private readonly List<float> targetSpeedValues = new List<float>();

    private readonly List<GameObject> graphObjects = new List<GameObject>();
    private readonly List<string> tableRows = new List<string>();

    private float sampleTimer = 0f;
    private int seconds = 0;

    void Start()
    {
        if (car == null)
        {
            Debug.LogError("PIDDashboardUI: Car alanına Car objesini bağlamalısın.");
            return;
        }

        CreateDashboard();
        FillInputsFromCar();
    }

    void Update()
    {
        if (car == null)
            return;

        UpdateTelemetry();
        UpdateCarWorldLabel();

        sampleTimer += Time.deltaTime;

        if (sampleTimer >= 1f)
        {
            sampleTimer = 0f;
            seconds++;

            AddTableRow();
            AddGraphPoint();
            DrawGraph();
        }
    }

    void CreateDashboard()
    {
        GameObject canvasObj = new GameObject("Auto_PID_Canvas");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        SetupEventSystem();

        CreateTopTitle(canvasObj.transform);

        GameObject settingsPanel = CreatePanel(
            canvasObj.transform,
            "PID Ayar Paneli",
            new Vector2(25, -70),
            new Vector2(460, 510),
            TextAnchor.UpperLeft
        );

        GameObject telemetryPanel = CreatePanel(
            canvasObj.transform,
            "Canli Degerler",
            new Vector2(-25, -70),
            new Vector2(500, 420),
            TextAnchor.UpperRight
        );

        GameObject explanationPanel = CreatePanel(
            canvasObj.transform,
            "Aciklama",
            new Vector2(-25, -505),
            new Vector2(500, 210),
            TextAnchor.UpperRight
        );

        GameObject dataPanel = CreatePanel(
            canvasObj.transform,
            "Tablo",
            new Vector2(25, 25),
            new Vector2(900, 360),
            TextAnchor.LowerLeft
        );

        GameObject graphPanel = CreatePanel(
            canvasObj.transform,
            "Grafik",
            new Vector2(-25, 25),
            new Vector2(900, 360),
            TextAnchor.LowerRight
        );

        CreateSettingsContent(settingsPanel.transform);
        CreateTelemetryContent(telemetryPanel.transform);
        CreateExplanationContent(explanationPanel.transform);
        CreateTableContent(dataPanel.transform);
        CreateGraphContent(graphPanel.transform);
        CreateCarWorldLabel(canvasObj.transform);
    }

    void SetupEventSystem()
    {
        UnityEngine.EventSystems.EventSystem existingEventSystem =
            FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();

        if (existingEventSystem == null)
        {
            GameObject eventObj = new GameObject("EventSystem");
            eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventObj.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            UnityEngine.EventSystems.StandaloneInputModule oldModule =
                existingEventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            if (oldModule != null)
            {
                Destroy(oldModule);
            }

            InputSystemUIInputModule newModule =
                existingEventSystem.GetComponent<InputSystemUIInputModule>();

            if (newModule == null)
            {
                existingEventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }
    }

    void CreateTopTitle(Transform parent)
    {
        TMP_Text title = CreateText(
            parent,
            "PID Kontrollu\nArac Simulasyonu",
            24,
            TextAlignmentOptions.Center
        );

        RectTransform rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(25f, -600f);
        rect.sizeDelta = new Vector2(460f, 80f);
    }

    GameObject CreatePanel(Transform parent, string title, Vector2 anchoredPos, Vector2 size, TextAnchor anchor)
    {
        GameObject panel = new GameObject(title);
        panel.transform.SetParent(parent, false);

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.68f);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        if (anchor == TextAnchor.UpperLeft)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
        }
        else if (anchor == TextAnchor.UpperRight)
        {
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
        }
        else if (anchor == TextAnchor.LowerLeft)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 0);
            rect.pivot = new Vector2(0, 0);
        }
        else
        {
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
        }

        rect.anchoredPosition = anchoredPos;

        TMP_Text titleText = CreateText(panel.transform, title, 24, TextAlignmentOptions.Center);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -10);
        titleRect.sizeDelta = new Vector2(-20, 36);

        return panel;
    }

    void CreateSettingsContent(Transform parent)
    {
        kpInput = CreateInputRow(parent, "Kp", new Vector2(25, -70));
        kiInput = CreateInputRow(parent, "Ki", new Vector2(25, -125));
        kdInput = CreateInputRow(parent, "Kd", new Vector2(25, -180));
        targetSpeedInput = CreateInputRow(parent, "Hedef Hiz", new Vector2(25, -235));
        massInput = CreateInputRow(parent, "Kutle", new Vector2(25, -290));

        CreateButton(parent, "PID Degerlerini Uygula", new Vector2(25, -350), new Vector2(400, 42), ApplyPIDValues, new Color(0.2f, 0.45f, 0.9f, 1f));

        CreateButton(parent, "PID Kapali", new Vector2(25, -400), new Vector2(190, 36), PresetOff, new Color(0.5f, 0.5f, 0.5f, 1f));
        CreateButton(parent, "Zayif PID", new Vector2(235, -400), new Vector2(190, 36), PresetWeak, new Color(0.25f, 0.6f, 0.25f, 1f));

        CreateButton(parent, "Normal PID", new Vector2(25, -445), new Vector2(190, 36), PresetNormal, new Color(0.2f, 0.45f, 0.9f, 1f));
        CreateButton(parent, "Agresif PID", new Vector2(235, -445), new Vector2(190, 36), PresetAggressive, new Color(0.8f, 0.35f, 0.2f, 1f));
    }

    TMP_InputField CreateInputRow(Transform parent, string label, Vector2 pos)
    {
        TMP_Text labelText = CreateText(parent, label + ":", 22, TextAlignmentOptions.Left);

        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(0, 1);
        labelRect.pivot = new Vector2(0, 1);
        labelRect.anchoredPosition = pos;
        labelRect.sizeDelta = new Vector2(150, 42);

        GameObject inputObj = new GameObject(label + " Input");
        inputObj.transform.SetParent(parent, false);

        Image inputBg = inputObj.AddComponent<Image>();
        inputBg.color = Color.white;

        TMP_InputField input = inputObj.AddComponent<TMP_InputField>();

        RectTransform inputRect = inputObj.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0, 1);
        inputRect.anchorMax = new Vector2(0, 1);
        inputRect.pivot = new Vector2(0, 1);
        inputRect.anchoredPosition = new Vector2(pos.x + 165, pos.y);
        inputRect.sizeDelta = new Vector2(235, 40);

        TMP_Text text = CreateText(inputObj.transform, "", 22, TextAlignmentOptions.Left);
        text.color = Color.black;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(10, 0);
        text.rectTransform.offsetMax = new Vector2(-10, 0);

        TMP_Text placeholder = CreateText(inputObj.transform, "0", 22, TextAlignmentOptions.Left);
        placeholder.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = new Vector2(10, 0);
        placeholder.rectTransform.offsetMax = new Vector2(-10, 0);

        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 8;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;

        return input;
    }

    void CreateButton(Transform parent, string text, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, Color color)
    {
        GameObject buttonObj = new GameObject(text);
        buttonObj.transform.SetParent(parent, false);

        Image img = buttonObj.AddComponent<Image>();
        img.color = color;

        Button button = buttonObj.AddComponent<Button>();
        button.onClick.AddListener(action);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        TMP_Text btnText = CreateText(buttonObj.transform, text, 19, TextAlignmentOptions.Center);
        btnText.color = Color.white;
        btnText.rectTransform.anchorMin = Vector2.zero;
        btnText.rectTransform.anchorMax = Vector2.one;
        btnText.rectTransform.offsetMin = Vector2.zero;
        btnText.rectTransform.offsetMax = Vector2.zero;
    }

    void CreateTelemetryContent(Transform parent)
    {
        telemetryText = CreateText(parent, "", 21, TextAlignmentOptions.Left);

        RectTransform rect = telemetryText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -55);
        rect.sizeDelta = new Vector2(-40, 330);
    }

    void CreateExplanationContent(Transform parent)
    {
        explanationText = CreateText(parent, "", 18, TextAlignmentOptions.Left);
        explanationText.text =
            "Hata = Hedef Hiz - Mevcut Hiz\n" +
            "PID Cikisi = Kp*e + Ki*∫e dt + Kd*de/dt\n" +
            "PID cikisi motor torku olarak uygulanir.\n" +
            "Kp, Ki, Kd ve kutle degistirilerek sistem cevabi gozlenir.";

        RectTransform rect = explanationText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = new Vector2(0, -55);
        rect.sizeDelta = new Vector2(-35, 145);
    }

    void CreateCarWorldLabel(Transform parent)
    {
        carWorldLabel = CreateText(parent, "", 22, TextAlignmentOptions.Center);
        carWorldLabel.color = Color.white;

        RectTransform rect = carWorldLabel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260, 60);
    }

    void CreateTableContent(Transform parent)
    {
        tableHeaderText = CreateText(
            parent,
            "<mspace=0.75em>t     | Tur | Hedef | Hiz   | Kutle | Ivme  | Hata  | Tork    | PID     | P       | I       | D</mspace>",
            16,
            TextAlignmentOptions.Left
        );

        RectTransform headerRect = tableHeaderText.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.anchoredPosition = new Vector2(0, -50);
        headerRect.sizeDelta = new Vector2(-40, 28);

        GameObject scrollObj = new GameObject("TableScrollView");
        scrollObj.transform.SetParent(parent, false);

        Image bg = scrollObj.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.05f);

        tableScrollRect = scrollObj.AddComponent<ScrollRect>();
        tableScrollRect.horizontal = false;
        tableScrollRect.vertical = true;
        tableScrollRect.movementType = ScrollRect.MovementType.Clamped;
        tableScrollRect.scrollSensitivity = 30f;

        RectTransform scrollRect = scrollObj.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(20, 20);
        scrollRect.offsetMax = new Vector2(-20, -85);

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);

        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0, 0);
        viewportRect.anchorMax = new Vector2(1, 1);
        viewportRect.offsetMin = new Vector2(5, 5);
        viewportRect.offsetMax = new Vector2(-18, -5);

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);

        tableContentRoot = content.AddComponent<RectTransform>();
        tableContentRoot.anchorMin = new Vector2(0, 1);
        tableContentRoot.anchorMax = new Vector2(1, 1);
        tableContentRoot.pivot = new Vector2(0.5f, 1);
        tableContentRoot.anchoredPosition = Vector2.zero;
        tableContentRoot.sizeDelta = new Vector2(0, 100);

        tableContentText = CreateText(content.transform, "", 16, TextAlignmentOptions.Left);
        tableContentText.enableWordWrapping = false;
        tableContentText.overflowMode = TextOverflowModes.Overflow;

        RectTransform textRect = tableContentText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 1);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.pivot = new Vector2(0.5f, 1);
        textRect.anchoredPosition = new Vector2(0, 0);
        textRect.sizeDelta = new Vector2(0, 100);

        tableScrollRect.viewport = viewportRect;
        tableScrollRect.content = tableContentRoot;

        GameObject scrollbarObj = new GameObject("Scrollbar");
        scrollbarObj.transform.SetParent(scrollObj.transform, false);

        Image scrollbarBg = scrollbarObj.AddComponent<Image>();
        scrollbarBg.color = new Color(1f, 1f, 1f, 0.18f);

        Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform scrollbarRect = scrollbarObj.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = new Vector2(1, 1);
        scrollbarRect.pivot = new Vector2(1, 1);
        scrollbarRect.offsetMin = new Vector2(-14, 5);
        scrollbarRect.offsetMax = new Vector2(-2, -5);

        GameObject slidingArea = new GameObject("Sliding Area");
        slidingArea.transform.SetParent(scrollbarObj.transform, false);

        RectTransform slidingAreaRect = slidingArea.AddComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = Vector2.zero;
        slidingAreaRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(slidingArea.transform, false);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.2f, 0.6f, 1f, 0.9f);

        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0, 0);
        handleRect.anchorMax = new Vector2(1, 0.2f);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        tableScrollRect.verticalScrollbar = scrollbar;
        tableScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
    }

    void CreateGraphContent(Transform parent)
    {
        TMP_Text legend = CreateText(
            parent,
            "<color=#00FF00>■</color> Hiz   <color=#FF5555>■</color> Hedef Hiz   <color=#FFFF00>■</color> Hata   <color=#00FFFF>■</color> PID",
            18,
            TextAlignmentOptions.Center
        );

        RectTransform legendRect = legend.GetComponent<RectTransform>();
        legendRect.anchorMin = new Vector2(0, 1);
        legendRect.anchorMax = new Vector2(1, 1);
        legendRect.pivot = new Vector2(0.5f, 1);
        legendRect.anchoredPosition = new Vector2(0, -48);
        legendRect.sizeDelta = new Vector2(-20, 26);

        GameObject graphBgObj = new GameObject("GraphBackground");
        graphBgObj.transform.SetParent(parent, false);

        Image bg = graphBgObj.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.05f);

        graphArea = graphBgObj.GetComponent<RectTransform>();
        graphArea.anchorMin = new Vector2(0, 0);
        graphArea.anchorMax = new Vector2(1, 1);
        graphArea.offsetMin = new Vector2(35, 35);
        graphArea.offsetMax = new Vector2(-35, -85);

        CreateGraphGrid(graphArea, 8, 6);

        GameObject lineLayerObj = new GameObject("GraphLineLayer");
        lineLayerObj.transform.SetParent(graphArea, false);

        graphLineLayer = lineLayerObj.AddComponent<RectTransform>();
        graphLineLayer.anchorMin = Vector2.zero;
        graphLineLayer.anchorMax = Vector2.one;
        graphLineLayer.offsetMin = Vector2.zero;
        graphLineLayer.offsetMax = Vector2.zero;

        TMP_Text bottomInfo = CreateText(
            parent,
            "Cizgiler normalize edilmis olcekte gosterilir.",
            16,
            TextAlignmentOptions.Center
        );

        RectTransform bottomRect = bottomInfo.GetComponent<RectTransform>();
        bottomRect.anchorMin = new Vector2(0, 0);
        bottomRect.anchorMax = new Vector2(1, 0);
        bottomRect.pivot = new Vector2(0.5f, 0);
        bottomRect.anchoredPosition = new Vector2(0, 8);
        bottomRect.sizeDelta = new Vector2(-20, 24);
    }

    void CreateGraphGrid(RectTransform parent, int verticalDivisions, int horizontalDivisions)
    {
        float width = 1f / verticalDivisions;
        float height = 1f / horizontalDivisions;

        for (int i = 0; i <= verticalDivisions; i++)
        {
            GameObject line = new GameObject("GridV_" + i);
            line.transform.SetParent(parent, false);

            Image img = line.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, i == 0 ? 0.35f : 0.15f);

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i * width, 0f);
            rect.anchorMax = new Vector2(i * width, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1.5f, 0f);
        }

        for (int j = 0; j <= horizontalDivisions; j++)
        {
            GameObject line = new GameObject("GridH_" + j);
            line.transform.SetParent(parent, false);

            Image img = line.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, j == horizontalDivisions / 2 ? 0.28f : 0.15f);

            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, j * height);
            rect.anchorMax = new Vector2(1f, j * height);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 1.5f);
        }
    }

    TMP_Text CreateText(Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject("Text");
        obj.transform.SetParent(parent, false);

        TMP_Text tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.richText = true;

        return tmp;
    }

    void FillInputsFromCar()
    {
        kpInput.text = car.Kp.ToString("0.###");
        kiInput.text = car.Ki.ToString("0.###");
        kdInput.text = car.Kd.ToString("0.###");
        targetSpeedInput.text = car.targetSpeed.ToString("0.###");
        massInput.text = car.carMass.ToString("0.###");
    }

    void ApplyPIDValues()
    {
        car.Kp = ParseAndClamp(kpInput.text, 0f, 2000f, car.Kp);
        car.Ki = ParseAndClamp(kiInput.text, 0f, 200f, car.Ki);
        car.Kd = ParseAndClamp(kdInput.text, 0f, 500f, car.Kd);
        car.targetSpeed = ParseAndClamp(targetSpeedInput.text, 0f, car.maxSpeed, car.targetSpeed);
        car.carMass = ParseAndClamp(massInput.text, 100f, 5000f, car.carMass);

        car.ResetPID();

        FillInputsFromCar();
        ClearData();
    }

    float ParseAndClamp(string text, float min, float max, float fallback)
    {
        if (float.TryParse(text, out float value))
        {
            return Mathf.Clamp(value, min, max);
        }

        return fallback;
    }

    void PresetOff()
    {
        SetPIDPreset(0f, 0f, 0f);
    }

    void PresetWeak()
    {
        SetPIDPreset(300f, 5f, 50f);
    }

    void PresetNormal()
    {
        SetPIDPreset(800f, 20f, 150f);
    }

    void PresetAggressive()
    {
        SetPIDPreset(1400f, 40f, 300f);
    }

    void SetPIDPreset(float kp, float ki, float kd)
    {
        car.Kp = kp;
        car.Ki = ki;
        car.Kd = kd;
        car.ResetPID();

        FillInputsFromCar();
        ClearData();
    }

    void UpdateTelemetry()
    {
        float acceleration = car.carMass > 0f ? car.torque / car.carMass : 0f;

        telemetryText.text =
            "Kutle: " + car.carMass.ToString("0.0") + " kg\n" +
            "Hedef Hiz: " + car.targetSpeed.ToString("0.00") + " m/s\n" +
            "Mevcut Hiz: " + car.currentSpeed.ToString("0.00") + " m/s\n" +
            "Ivme: " + acceleration.ToString("0.000") + " m/s²\n" +
            "Tork: " + car.torque.ToString("0.00") + " Nm\n" +
            "Tur Sayisi: " + car.lapCount + "\n" +
            "Yol Ilerlemesi: %" + (car.splineProgress * 100f).ToString("0.0") + "\n\n" +
            "Hata: " + car.error.ToString("0.00") + "\n" +
            "P: " + car.pValue.ToString("0.00") + "\n" +
            "I: " + car.iValue.ToString("0.00") + "\n" +
            "D: " + car.dValue.ToString("0.00") + "\n" +
            "PID Cikisi: " + car.pidOutput.ToString("0.00");
    }

    void UpdateCarWorldLabel()
    {
        if (Camera.main == null || carWorldLabel == null || car == null)
            return;

        Vector3 worldPos = car.transform.position;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0f)
        {
            carWorldLabel.text = "";
            return;
        }

        screenPos.y -= 70f; // Arabanın altına kaydır
        carWorldLabel.rectTransform.position = screenPos;
        carWorldLabel.text =
            "Hiz: " + car.currentSpeed.ToString("0.0") + " m/s\n" +
            "Tur: " + car.lapCount;
    }

    void AddTableRow()
    {
        float acceleration = car.carMass > 0f ? car.torque / car.carMass : 0f;

        string rowCore =
            $"{seconds,5}s | " +
            $"{car.lapCount,3} | " +
            $"{car.targetSpeed,5:0.00} | " +
            $"{car.currentSpeed,5:0.00} | " +
            $"{car.carMass,5:0} | " +
            $"{acceleration,5:0.000} | " +
            $"{car.error,5:0.00} | " +
            $"{car.torque,7:0.0} | " +
            $"{car.pidOutput,7:0.0} | " +
            $"{car.pValue,7:0.0} | " +
            $"{car.iValue,7:0.0} | " +
            $"{car.dValue,7:0.0}";

        string row = "<mspace=0.75em>" + rowCore + "</mspace>";

        tableRows.Add(row);

        while (tableRows.Count > maxTableRows)
        {
            tableRows.RemoveAt(0);
        }

        tableContentText.text = string.Join("\n", tableRows);

        Canvas.ForceUpdateCanvases();

        float preferredHeight = tableContentText.preferredHeight + 20f;
        tableContentRoot.sizeDelta = new Vector2(0f, preferredHeight);

        tableScrollRect.verticalNormalizedPosition = 0f;
    }

    void AddGraphPoint()
    {
        speedValues.Add(car.currentSpeed);
        errorValues.Add(car.error);
        pidValues.Add(car.pidOutput);
        targetSpeedValues.Add(car.targetSpeed);

        TrimList(speedValues);
        TrimList(errorValues);
        TrimList(pidValues);
        TrimList(targetSpeedValues);
    }

    void TrimList(List<float> list)
    {
        while (list.Count > maxGraphPoints)
        {
            list.RemoveAt(0);
        }
    }

    void DrawGraph()
    {
        if (graphArea == null || graphLineLayer == null)
            return;

        foreach (GameObject obj in graphObjects)
        {
            Destroy(obj);
        }

        graphObjects.Clear();

        DrawLineGraph(speedValues, new Color(0f, 1f, 0f, 1f), 0f, Mathf.Max(1f, car.maxSpeed));
        DrawLineGraph(targetSpeedValues, new Color(1f, 0.35f, 0.35f, 1f), 0f, Mathf.Max(1f, car.maxSpeed));

        float errorScale = Mathf.Max(1f, car.targetSpeed);
        DrawLineGraph(errorValues, new Color(1f, 1f, 0f, 1f), -errorScale, errorScale);

        DrawLineGraph(pidValues, new Color(0f, 1f, 1f, 1f), -3000f, 3000f);
    }

    void DrawLineGraph(List<float> values, Color color, float minValue, float maxValue)
    {
        if (values.Count < 2 || graphArea == null || graphLineLayer == null)
            return;

        float width = graphArea.rect.width;
        float height = graphArea.rect.height;

        if (width <= 0f || height <= 0f)
            return;

        int visibleCount = Mathf.Min(values.Count, maxGraphPoints);

        for (int i = 0; i < visibleCount - 1; i++)
        {
            float x1 = (i / (float)(maxGraphPoints - 1)) * width;
            float x2 = ((i + 1) / (float)(maxGraphPoints - 1)) * width;

            float y1 = Mathf.InverseLerp(minValue, maxValue, values[i]) * height;
            float y2 = Mathf.InverseLerp(minValue, maxValue, values[i + 1]) * height;

            y1 = Mathf.Clamp(y1, 0f, height);
            y2 = Mathf.Clamp(y2, 0f, height);

            CreateGraphLine(new Vector2(x1, y1), new Vector2(x2, y2), color, 3f);
        }
    }

    void CreateGraphLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        GameObject lineObj = new GameObject("Graph Line");
        lineObj.transform.SetParent(graphLineLayer, false);

        Image img = lineObj.AddComponent<Image>();
        img.color = color;

        RectTransform rect = lineObj.GetComponent<RectTransform>();

        Vector2 direction = end - start;
        float distance = direction.magnitude;

        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = start;
        rect.sizeDelta = new Vector2(distance, thickness);
        rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        graphObjects.Add(lineObj);
    }

    void ClearData()
    {
        seconds = 0;
        sampleTimer = 0f;

        tableRows.Clear();
        speedValues.Clear();
        errorValues.Clear();
        pidValues.Clear();
        targetSpeedValues.Clear();

        if (tableContentText != null)
        {
            tableContentText.text = "";
        }

        if (tableContentRoot != null)
        {
            tableContentRoot.sizeDelta = new Vector2(0f, 100f);
        }

        foreach (GameObject obj in graphObjects)
        {
            Destroy(obj);
        }

        graphObjects.Clear();
    }
}