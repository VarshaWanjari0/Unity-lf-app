using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RuntimeFileBrowser : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panel;
    public Text currentPathText;
    public Transform fileListContainer;
    public Button parentDirButton;
    public Button selectFolderButton;
    public Button closeButton;
    public Text titleText;

    private Action<string> onFileSelected;
    private Action<string> onFolderSelected;
    private string[] filterExtensions;
    private string currentDirectory;
    private bool selectFolderMode = false;

    public static RuntimeFileBrowser Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);
    }

    public void OpenFilePicker(string title, string[] extensions, Action<string> callback)
    {
        selectFolderMode = false;
        onFileSelected = callback;
        filterExtensions = extensions;
        if (titleText != null) titleText.text = title;
        if (selectFolderButton != null) selectFolderButton.gameObject.SetActive(false);

        ShowPanel(GetInitialDirectory());
    }

    public void OpenFolderPicker(string title, Action<string> callback)
    {
        selectFolderMode = true;
        onFolderSelected = callback;
        filterExtensions = null;
        if (titleText != null) titleText.text = title;
        if (selectFolderButton != null) selectFolderButton.gameObject.SetActive(true);

        ShowPanel(GetInitialDirectory());
    }

    private string GetInitialDirectory()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        string path = "/storage/emulated/0";
        if (Directory.Exists(path)) return path;
        #endif
        return Application.persistentDataPath;
    }

    private void ShowPanel(string dir)
    {
        if (panel != null) panel.SetActive(true);
        NavigateTo(dir);
    }

    public void NavigateTo(string dir)
    {
        if (!Directory.Exists(dir))
        {
            dir = Application.persistentDataPath;
        }

        currentDirectory = dir;
        if (currentPathText != null) currentPathText.text = currentDirectory;

        // Clear existing items
        if (fileListContainer != null)
        {
            foreach (Transform child in fileListContainer)
            {
                Destroy(child.gameObject);
            }
        }

        try
        {
            // List subdirectories
            string[] subdirs = Directory.GetDirectories(currentDirectory);
            foreach (string sd in subdirs)
            {
                string dirName = Path.GetFileName(sd);
                CreateItemButton("[DIR] " + dirName, Color.yellow, () => NavigateTo(sd));
            }

            // List files if in file picker mode
            if (!selectFolderMode)
            {
                string[] files = Directory.GetFiles(currentDirectory);
                foreach (string f in files)
                {
                    string ext = Path.GetExtension(f).ToLower();
                    if (MatchesFilter(ext))
                    {
                        string fileName = Path.GetFileName(f);
                        string selectedPath = f;
                        CreateItemButton(fileName, Color.white, () => SelectFile(selectedPath));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error opening directory " + dir + ": " + ex.Message);
        }
    }

    private bool MatchesFilter(string ext)
    {
        if (filterExtensions == null || filterExtensions.Length == 0) return true;
        foreach (string fExt in filterExtensions)
        {
            if (ext.Equals(fExt.ToLower())) return true;
        }
        return false;
    }

    private void CreateItemButton(string label, Color textCol, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("FileItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(fileListContainer, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 50);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(10, 0);
        txtRect.offsetMax = new Vector2(-10, 0);

        Text txt = txtObj.GetComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 18;
        txt.color = textCol;
        txt.alignment = TextAnchor.MiddleLeft;
    }

    public void OnParentDirClicked()
    {
        DirectoryInfo parent = Directory.GetParent(currentDirectory);
        if (parent != null)
        {
            NavigateTo(parent.FullName);
        }
    }

    public void OnFolderSelectClicked()
    {
        if (panel != null) panel.SetActive(false);
        onFolderSelected?.Invoke(currentDirectory);
    }

    private void SelectFile(string filePath)
    {
        if (panel != null) panel.SetActive(false);
        onFileSelected?.Invoke(filePath);
    }

    public void OnCloseClicked()
    {
        if (panel != null) panel.SetActive(false);
    }
}
