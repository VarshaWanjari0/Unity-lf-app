using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainApp : MonoBehaviour
{
    [Header("UI Controls")]
    public InputField meshNameInput;
    public InputField scaleXInput, scaleYInput, scaleZInput;
    public InputField rotXInput, rotYInput, rotZInput;
    public Toggle genNormalsToggle;
    public Toggle genTangentsToggle;
    public Toggle preserveUvToggle;
    public Toggle optimizeMeshToggle;
    public Toggle keepVertexOrderToggle;
    public Text statusLogText;

    private Mesh currentLoadedMesh;
    private List<Material> currentMaterials = new List<Material>();
    private List<Texture2D> currentTextures = new List<Texture2D>();
    private GameObject previewInstance;
    private ModelPreviewController previewController;

    private void Awake()
    {
        EnsureSceneSetup();
        EnsureUIConstructed();
    }

    private void Start()
    {
        LogStatus("Unity Mesh Builder Tool Ready.\nImport a .OBJ or .GLB/.GLTF model to begin.");
    }

    private void EnsureSceneSetup()
    {
        if (Camera.main == null)
        {
            GameObject camObj = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0, 2, -5);
            camObj.transform.LookAt(Vector3.zero);
            Camera cam = camObj.GetComponent<Camera>();
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        if (FindObjectOfType<Light>() == null)
        {
            GameObject lightObj = new GameObject("Directional Light", typeof(Light));
            Light light = lightObj.GetComponent<Light>();
            light.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        previewController = FindObjectOfType<ModelPreviewController>();
        if (previewController == null)
        {
            GameObject pcObj = new GameObject("ModelPreviewController", typeof(ModelPreviewController));
            previewController = pcObj.GetComponent<ModelPreviewController>();
        }
    }

    private void EnsureUIConstructed()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null && meshNameInput != null) return; // UI already configured

        // Programmatically construct complete uGUI layout if missing
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Background / Layout Panels
        GameObject panelObj = CreateUIElement("LeftPanel", canvas.transform, new Vector2(0, 0), new Vector2(0.42f, 1.0f), new Vector2(0, 0), new Vector2(0, 0));
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.12f, 0.92f);

        VerticalLayoutGroup vlg = panelObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // App Title
        CreateText(panelObj.transform, font, "Unity Mesh Builder Tool", 24, Color.cyan, FontStyle.Bold, 40);

        // Action Buttons
        CreateButton(panelObj.transform, font, "Import Model (.OBJ / .GLB)", OnImportModelClicked);
        CreateButton(panelObj.transform, font, "Import Textures Folder", OnImportTexturesClicked);

        // Settings Header
        CreateText(panelObj.transform, font, "-- Settings --", 18, Color.yellow, FontStyle.Bold, 25);

        // Mesh Name Input
        CreateText(panelObj.transform, font, "Target Mesh Name:", 14, Color.white, FontStyle.Normal, 20);
        meshNameInput = CreateInputField(panelObj.transform, font, "Mesh Name (e.g. Cargodoor_left)");

        // Scale Inputs
        CreateText(panelObj.transform, font, "Scale (X, Y, Z):", 14, Color.white, FontStyle.Normal, 20);
        GameObject scaleRow = CreateHorizontalGroup(panelObj.transform, 30);
        scaleXInput = CreateInputField(scaleRow.transform, font, "1.0");
        scaleYInput = CreateInputField(scaleRow.transform, font, "1.0");
        scaleZInput = CreateInputField(scaleRow.transform, font, "1.0");

        // Rotation Inputs
        CreateText(panelObj.transform, font, "Rotation Euler (X, Y, Z):", 14, Color.white, FontStyle.Normal, 20);
        GameObject rotRow = CreateHorizontalGroup(panelObj.transform, 30);
        rotXInput = CreateInputField(rotRow.transform, font, "0.0");
        rotYInput = CreateInputField(rotRow.transform, font, "0.0");
        rotZInput = CreateInputField(rotRow.transform, font, "0.0");

        // Toggles
        genNormalsToggle = CreateToggle(panelObj.transform, font, "Recalculate Normals", false);
        genTangentsToggle = CreateToggle(panelObj.transform, font, "Generate Tangents", true);
        preserveUvToggle = CreateToggle(panelObj.transform, font, "Preserve UVs", true);
        optimizeMeshToggle = CreateToggle(panelObj.transform, font, "Optimize Mesh", true);
        keepVertexOrderToggle = CreateToggle(panelObj.transform, font, "Keep Original Vertex Order", true);

        // Action Buttons
        CreateButton(panelObj.transform, font, "Preview Mesh", OnPreviewClicked);
        CreateButton(panelObj.transform, font, "Export UnityPy ZIP", OnExportZipClicked);

        // Status Output Box
        GameObject statusObj = CreateUIElement("StatusLog", panelObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        LayoutElement le = statusObj.AddComponent<LayoutElement>();
        le.preferredHeight = 250;
        Image statusBg = statusObj.AddComponent<Image>();
        statusBg.color = new Color(0.05f, 0.05f, 0.07f, 1f);

        GameObject statusTextObj = CreateUIElement("Text", statusObj.transform, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
        statusLogText = statusTextObj.AddComponent<Text>();
        statusLogText.font = font;
        statusLogText.fontSize = 13;
        statusLogText.color = Color.green;
        statusLogText.alignment = TextAnchor.UpperLeft;

        // Construct Runtime File Browser UI if missing
        EnsureFileBrowserConstructed(canvas, font);
    }

    private void EnsureFileBrowserConstructed(Canvas canvas, Font font)
    {
        if (RuntimeFileBrowser.Instance != null) return;

        GameObject fbObj = new GameObject("FileBrowser", typeof(RectTransform), typeof(RuntimeFileBrowser));
        fbObj.transform.SetParent(canvas.transform, false);

        RuntimeFileBrowser fb = fbObj.GetComponent<RuntimeFileBrowser>();

        GameObject panel = CreateUIElement("Panel", fbObj.transform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Vector2.zero, Vector2.zero);
        Image pImg = panel.AddComponent<Image>();
        pImg.color = new Color(0.15f, 0.15f, 0.18f, 0.98f);
        fb.panel = panel;

        // Title text
        GameObject titleObj = CreateUIElement("Title", panel.transform, new Vector2(0, 0.92f), new Vector2(1, 1), new Vector2(10, -5), new Vector2(-10, -5));
        Text titleTxt = titleObj.AddComponent<Text>();
        titleTxt.font = font;
        titleTxt.fontSize = 20;
        titleTxt.color = Color.cyan;
        fb.titleText = titleTxt;

        // Current Path text
        GameObject pathObj = CreateUIElement("PathText", panel.transform, new Vector2(0, 0.86f), new Vector2(1, 0.92f), new Vector2(10, 0), new Vector2(-10, 0));
        Text pathTxt = pathObj.AddComponent<Text>();
        pathTxt.font = font;
        pathTxt.fontSize = 14;
        pathTxt.color = Color.white;
        fb.currentPathText = pathTxt;

        // Scroll View
        GameObject scrollObj = CreateUIElement("ScrollView", panel.transform, new Vector2(0, 0.1f), new Vector2(1, 0.85f), new Vector2(10, 0), new Vector2(-10, 0));
        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();

        GameObject viewObj = CreateUIElement("Viewport", scrollObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        viewObj.AddComponent<RectMask2D>();
        Image viewImg = viewObj.AddComponent<Image>();
        viewImg.color = new Color(0.08f, 0.08f, 0.1f, 0.8f);

        GameObject contentObj = CreateUIElement("Content", viewObj.transform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup cvlg = contentObj.AddComponent<VerticalLayoutGroup>();
        cvlg.childControlWidth = true;
        cvlg.childControlHeight = false;
        cvlg.spacing = 5;

        ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentObj.GetComponent<RectTransform>();
        scroll.viewport = viewObj.GetComponent<RectTransform>();

        fb.fileListContainer = contentObj.transform;

        // Bottom Bar Buttons
        GameObject botRow = CreateUIElement("BottomRow", panel.transform, new Vector2(0, 0), new Vector2(1, 0.09f), new Vector2(10, 5), new Vector2(-10, -5));
        HorizontalLayoutGroup hlg = botRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childControlWidth = true;

        Button parentBtn = CreateButton(botRow.transform, font, "Parent Directory", fb.OnParentDirClicked);
        fb.parentDirButton = parentBtn;

        Button selFolderBtn = CreateButton(botRow.transform, font, "Select Folder", fb.OnFolderSelectClicked);
        fb.selectFolderButton = selFolderBtn;

        Button closeBtn = CreateButton(botRow.transform, font, "Close", fb.OnCloseClicked);
        fb.closeButton = closeBtn;

        panel.SetActive(false);
    }

    private GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return go;
    }

    private Text CreateText(Transform parent, Font font, string content, int fontSize, Color col, FontStyle style, float height)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        Text txt = go.GetComponent<Text>();
        txt.font = font;
        txt.text = content;
        txt.fontSize = fontSize;
        txt.color = col;
        txt.fontStyle = style;
        txt.alignment = TextAnchor.MiddleLeft;
        return txt;
    }

    private Button CreateButton(Transform parent, Font font, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);
        btnObj.GetComponent<LayoutElement>().preferredHeight = 45;

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.25f, 0.45f, 0.75f, 1f);

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRect = txtObj.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;

        Text txt = txtObj.GetComponent<Text>();
        txt.font = font;
        txt.text = label;
        txt.fontSize = 15;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;

        return btn;
    }

    private InputField CreateInputField(Transform parent, Font font, string placeholderStr)
    {
        GameObject go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 40;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.22f, 1f);

        InputField input = go.GetComponent<InputField>();

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(go.transform, false);
        RectTransform tRect = txtObj.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.offsetMin = new Vector2(8, 0);
        tRect.offsetMax = new Vector2(-8, 0);

        Text txt = txtObj.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        input.textComponent = txt;

        GameObject phObj = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phObj.transform.SetParent(go.transform, false);
        RectTransform pRect = phObj.GetComponent<RectTransform>();
        pRect.anchorMin = Vector2.zero;
        pRect.anchorMax = Vector2.one;
        pRect.offsetMin = new Vector2(8, 0);
        pRect.offsetMax = new Vector2(-8, 0);

        Text phTxt = phObj.GetComponent<Text>();
        phTxt.font = font;
        phTxt.text = placeholderStr;
        phTxt.fontSize = 14;
        phTxt.color = Color.gray;
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.alignment = TextAnchor.MiddleLeft;

        input.placeholder = phTxt;
        return input;
    }

    private Toggle CreateToggle(Transform parent, Font font, string label, bool defaultVal)
    {
        GameObject go = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(Toggle), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 35;

        HorizontalLayoutGroup hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childControlWidth = false;

        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bgObj.transform.SetParent(go.transform, false);
        bgObj.GetComponent<LayoutElement>().preferredWidth = 25;
        bgObj.GetComponent<LayoutElement>().preferredHeight = 25;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = Color.gray;

        GameObject chkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        chkObj.transform.SetParent(bgObj.transform, false);
        RectTransform cRect = chkObj.GetComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0.15f, 0.15f);
        cRect.anchorMax = new Vector2(0.85f, 0.85f);
        Image chkImg = chkObj.GetComponent<Image>();
        chkImg.color = Color.green;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        txtObj.transform.SetParent(go.transform, false);
        txtObj.GetComponent<LayoutElement>().preferredWidth = 250;
        Text txt = txtObj.GetComponent<Text>();
        txt.font = font;
        txt.text = label;
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        Toggle toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = chkImg;
        toggle.isOn = defaultVal;

        return toggle;
    }

    private GameObject CreateHorizontalGroup(Transform parent, float height)
    {
        GameObject go = new GameObject("HGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        HorizontalLayoutGroup hlg = go.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = true;
        return go;
    }

    // App Functionality Logic
    public void OnImportModelClicked()
    {
        if (RuntimeFileBrowser.Instance == null) return;

        RuntimeFileBrowser.Instance.OpenFilePicker(
            "Select 3D Model File (.obj, .glb, .gltf)",
            new string[] { ".obj", ".glb", ".gltf" },
            (filePath) =>
            {
                try
                {
                    string ext = Path.GetExtension(filePath).ToLower();
                    if (ext == ".obj")
                    {
                        var res = ObjLoader.Load(filePath);
                        currentLoadedMesh = res.mesh;
                        currentMaterials = res.materials;
                        currentTextures = res.textures;
                    }
                    else if (ext == ".glb" || ext == ".gltf")
                    {
                        var res = GlbLoader.Load(filePath);
                        currentLoadedMesh = res.mesh;
                        currentMaterials = res.materials;
                        currentTextures = res.textures;
                    }

                    if (currentLoadedMesh != null)
                    {
                        meshNameInput.text = currentLoadedMesh.name;
                        LogStatus("Successfully loaded model: " + currentLoadedMesh.name +
                                  "\nVertices: " + currentLoadedMesh.vertexCount +
                                  "\nTriangles: " + (currentLoadedMesh.triangles.Length / 3) +
                                  "\nSubmeshes: " + currentLoadedMesh.subMeshCount +
                                  "\nMaterials: " + currentMaterials.Count +
                                  "\nTextures: " + currentTextures.Count);

                        UpdatePreviewObject();
                    }
                }
                catch (Exception ex)
                {
                    LogStatus("Failed to load model: " + ex.Message);
                }
            });
    }

    public void OnImportTexturesClicked()
    {
        if (RuntimeFileBrowser.Instance == null) return;

        RuntimeFileBrowser.Instance.OpenFolderPicker(
            "Select Texture Folder (PNG/JPG)",
            (folderPath) =>
            {
                try
                {
                    string[] files = Directory.GetFiles(folderPath, "*.*");
                    currentTextures.Clear();

                    foreach (string f in files)
                    {
                        string ext = Path.GetExtension(f).ToLower();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            byte[] bytes = File.ReadAllBytes(f);
                            Texture2D tex = new Texture2D(2, 2);
                            if (tex.LoadImage(bytes))
                            {
                                tex.name = Path.GetFileName(f);
                                currentTextures.Add(tex);
                            }
                        }
                    }

                    LogStatus("Loaded " + currentTextures.Count + " textures from folder:\n" + folderPath);
                }
                catch (Exception ex)
                {
                    LogStatus("Failed to load textures: " + ex.Message);
                }
            });
    }

    public void OnPreviewClicked()
    {
        if (currentLoadedMesh == null)
        {
            LogStatus("No mesh loaded to preview.");
            return;
        }

        Mesh processed = GetProcessedMesh();
        UpdatePreviewObject(processed);
        LogStatus("Preview refreshed with current settings.");
    }

    public void OnExportZipClicked()
    {
        if (currentLoadedMesh == null)
        {
            LogStatus("Error: Please import a 3D model first.");
            return;
        }

        try
        {
            Mesh processed = GetProcessedMesh();
            string targetName = meshNameInput.text;
            if (string.IsNullOrEmpty(targetName)) targetName = currentLoadedMesh.name;

            string exportDir = Path.Combine(Application.persistentDataPath, "Exports");
            string zipPath = UnityPyExporter.ExportToZip(processed, currentMaterials, currentTextures, exportDir, targetName);

            LogStatus("EXPORT SUCCESS!\nSaved UnityPy ZIP to:\n" + zipPath);
        }
        catch (Exception ex)
        {
            LogStatus("Export failed: " + ex.Message);
        }
    }

    private Mesh GetProcessedMesh()
    {
        MeshProcessor.ProcessingSettings settings = new MeshProcessor.ProcessingSettings
        {
            scale = GetScaleFromInput(),
            rotationEuler = GetRotationFromInput(),
            generateNormals = genNormalsToggle != null && genNormalsToggle.isOn,
            generateTangents = genTangentsToggle != null && genTangentsToggle.isOn,
            preserveUV = preserveUvToggle != null && preserveUvToggle.isOn,
            optimizeMesh = optimizeMeshToggle != null && optimizeMeshToggle.isOn,
            keepOriginalVertexOrder = keepVertexOrderToggle != null && keepVertexOrderToggle.isOn
        };

        return MeshProcessor.ProcessMesh(currentLoadedMesh, settings);
    }

    private Vector3 GetScaleFromInput()
    {
        float x = ParseFloatInput(scaleXInput, 1.0f);
        float y = ParseFloatInput(scaleYInput, 1.0f);
        float z = ParseFloatInput(scaleZInput, 1.0f);
        return new Vector3(x, y, z);
    }

    private Vector3 GetRotationFromInput()
    {
        float x = ParseFloatInput(rotXInput, 0.0f);
        float y = ParseFloatInput(rotYInput, 0.0f);
        float z = ParseFloatInput(rotZInput, 0.0f);
        return new Vector3(x, y, z);
    }

    private float ParseFloatInput(InputField field, float fallback)
    {
        if (field != null && float.TryParse(field.text, out float val))
            return val;
        return fallback;
    }

    private void UpdatePreviewObject(Mesh meshToDisplay = null)
    {
        Mesh mesh = meshToDisplay ?? currentLoadedMesh;
        if (mesh == null) return;

        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }

        previewInstance = new GameObject("PreviewMeshObj", typeof(MeshFilter), typeof(MeshRenderer));
        MeshFilter mf = previewInstance.GetComponent<MeshFilter>();
        MeshRenderer mr = previewInstance.GetComponent<MeshRenderer>();

        mf.sharedMesh = mesh;

        if (currentMaterials != null && currentMaterials.Count > 0)
        {
            mr.sharedMaterials = currentMaterials.ToArray();
        }
        else
        {
            Material defaultMat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            defaultMat.color = Color.lightGray;
            mr.sharedMaterial = defaultMat;
        }

        if (previewController != null)
        {
            previewController.FocusOnObject(previewInstance);
        }
    }

    private void LogStatus(string message)
    {
        Debug.Log("[UnityMeshBuilder] " + message);
        if (statusLogText != null)
        {
            statusLogText.text = message;
        }
    }
}
