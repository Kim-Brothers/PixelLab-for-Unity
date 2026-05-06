#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class EditPanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Tab 0 – Image Edit (EditImagesV2)
        // -----------------------------------------------------------------------

        private string    _editSourcePath    = "";
        private Texture2D _editSourcePreview = null;
        private string    _editDescription   = "";
        private int       _editWidth         = 128;
        private int       _editHeight        = 128;

        // -----------------------------------------------------------------------
        // Tab 1 – Inpaint (InpaintV3)
        // -----------------------------------------------------------------------

        private string    _inpaintSourcePath    = "";
        private Texture2D _inpaintSourcePreview = null;
        private string    _inpaintMaskPath      = "";
        private Texture2D _inpaintMaskPreview   = null;
        private string    _inpaintDescription   = "";

        // -----------------------------------------------------------------------
        // Tab 2 – ImageToPixelart
        // -----------------------------------------------------------------------

        private string    _pixelartSourcePath    = "";
        private Texture2D _pixelartSourcePreview = null;
        private int       _pixelartInputWidth    = 128;
        private int       _pixelartInputHeight   = 128;
        private int       _pixelartOutputWidth   = 64;
        private int       _pixelartOutputHeight  = 64;

        // -----------------------------------------------------------------------
        // Tab 3 – Resize
        // -----------------------------------------------------------------------

        private string    _resizeRefImagePath    = "";
        private Texture2D _resizeRefImagePreview = null;
        private string    _resizeDescription     = "";
        private int       _resizeRefWidth        = 64;
        private int       _resizeRefHeight       = 64;
        private int       _resizeTargetWidth     = 128;
        private int       _resizeTargetHeight    = 128;

        // -----------------------------------------------------------------------
        // Tab 4 – RemoveBackground
        // -----------------------------------------------------------------------

        private string    _removeBgSourcePath    = "";
        private Texture2D _removeBgSourcePreview = null;
        private int       _removeBgWidth         = 128;
        private int       _removeBgHeight        = 128;

        // -----------------------------------------------------------------------
        // Shared UI state
        // -----------------------------------------------------------------------

        private int    _selectedTab  = 0;

        private static readonly string[] TabNames = { "Image Edit", "Inpaint", "Pixelart", "Resize", "Remove BG" };

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public EditPanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawPanelHeader("Image Edit", "True inpainting, pixelart conversion, resize, and background removal.");

            if (!RequireClient())
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            int newTab = GUILayout.Toolbar(_selectedTab, TabNames);
            if (newTab != _selectedTab)
            {
                _selectedTab  = newTab;
                _errorMessage = "";
                ClearResults();
            }

            EditorGUILayout.Space(8);

            switch (_selectedTab)
            {
                case 0: DrawEditImages();     break;
                case 1: DrawInpaint();        break;
                case 2: DrawPixelart();       break;
                case 3: DrawResize();         break;
                case 4: DrawRemoveBackground(); break;
            }

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
            }

            DrawImagePreviews(160);

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Tab 0 – Image Edit
        // -----------------------------------------------------------------------

        private void DrawEditImages()
        {
            DrawImagePicker("Source Image", ref _editSourcePath, ref _editSourcePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Description");
            _editDescription = EditorGUILayout.TextArea(_editDescription, GUILayout.MinHeight(60));

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _editWidth  = EditorGUILayout.IntField(_editWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _editHeight = EditorGUILayout.IntField(_editHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Edit Image"))
                RunEditImages();
            GUI.enabled = true;
        }

        private void RunEditImages()
        {
            if (string.IsNullOrEmpty(_editSourcePath))
            {
                _errorMessage = "Please select a source image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string sourcePath = _editSourcePath;
            string desc       = _editDescription;
            int    w          = _editWidth;
            int    h          = _editHeight;
            string outputDir  = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JArray editImages = new JArray
                    {
                        new JObject
                        {
                            ["image"]       = JObject.Parse(ImageUtils.ImageToBase64Json(sourcePath)),
                            ["description"] = desc
                        }
                    };

                    JObject result = await Client.EditImagesV2(editImages, w, h);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "edit");
                },
                () =>
                {
                    if (paths != null)
                    {
                        SavedPaths.AddRange(paths);
                        foreach (string p in paths)
                        {
                            Texture2D tex = LoadTexture(p);
                            if (tex != null) ResultTextures.Add(tex);
                        }
                    }
                },
                ex => { _errorMessage = $"Error: {ex.Message}"; }
            );
        }

        // -----------------------------------------------------------------------
        // Tab 1 – Inpaint
        // -----------------------------------------------------------------------

        private void DrawInpaint()
        {
            DrawImagePicker("Source Image", ref _inpaintSourcePath, ref _inpaintSourcePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Mask Image: Use a grayscale image where areas to edit are white (255) and areas to keep are black (0).",
                MessageType.Info);
            DrawImagePicker("Mask Image (Grayscale)", ref _inpaintMaskPath, ref _inpaintMaskPreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Description");
            _inpaintDescription = EditorGUILayout.TextField(_inpaintDescription);

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Run Inpaint"))
                RunInpaint();
            GUI.enabled = true;
        }

        private void RunInpaint()
        {
            if (string.IsNullOrEmpty(_inpaintSourcePath))
            {
                _errorMessage = "Please select a source image.";
                return;
            }
            if (string.IsNullOrEmpty(_inpaintMaskPath))
            {
                _errorMessage = "Please select a mask image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string sourcePath = _inpaintSourcePath;
            string maskPath   = _inpaintMaskPath;
            string desc       = _inpaintDescription;
            string outputDir  = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject sourceImageData = JObject.Parse(ImageUtils.ImageToBase64Json(sourcePath));
                    JObject maskImageData   = JObject.Parse(ImageUtils.ImageToBase64Json(maskPath));
                    ImageUtils.GetImageSize(sourcePath, out int imgW, out int imgH);
                    var inpaintWrapper = new JObject
                    {
                        ["image"] = sourceImageData,
                        ["size"]  = new JObject { ["width"] = imgW, ["height"] = imgH }
                    };
                    var maskWrapper = new JObject
                    {
                        ["image"] = maskImageData,
                        ["size"]  = new JObject { ["width"] = imgW, ["height"] = imgH }
                    };

                    JObject result = await Client.InpaintV3(desc, inpaintWrapper, maskWrapper);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "inpaint");
                },
                () =>
                {
                    if (paths != null)
                    {
                        SavedPaths.AddRange(paths);
                        foreach (string p in paths)
                        {
                            Texture2D tex = LoadTexture(p);
                            if (tex != null) ResultTextures.Add(tex);
                        }
                    }
                },
                ex => { _errorMessage = $"Error: {ex.Message}"; }
            );
        }

        // -----------------------------------------------------------------------
        // Tab 2 – ImageToPixelart
        // -----------------------------------------------------------------------

        private void DrawPixelart()
        {
            DrawImagePicker("Source Image", ref _pixelartSourcePath, ref _pixelartSourcePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Input Size", GUILayout.Width(70));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _pixelartInputWidth  = EditorGUILayout.IntField(_pixelartInputWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _pixelartInputHeight = EditorGUILayout.IntField(_pixelartInputHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Size", GUILayout.Width(70));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _pixelartOutputWidth  = EditorGUILayout.IntField(_pixelartOutputWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _pixelartOutputHeight = EditorGUILayout.IntField(_pixelartOutputHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Convert to Pixelart"))
                RunPixelart();
            GUI.enabled = true;
        }

        private void RunPixelart()
        {
            if (string.IsNullOrEmpty(_pixelartSourcePath))
            {
                _errorMessage = "Please select a source image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string sourcePath = _pixelartSourcePath;
            int    inputW     = _pixelartInputWidth;
            int    inputH     = _pixelartInputHeight;
            int    outputW    = _pixelartOutputWidth;
            int    outputH    = _pixelartOutputHeight;
            string outputDir  = Window.OutputDir;

            // Auto-detect input dimensions if zero
            if (inputW <= 0 || inputH <= 0)
                ImageUtils.GetImageSize(sourcePath, out inputW, out inputH);

            int capturedInputW  = inputW;
            int capturedInputH  = inputH;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject imageData = JObject.Parse(ImageUtils.ImageToBase64Json(sourcePath));

                    JObject result = await Client.ImageToPixelart(
                        imageData, capturedInputW, capturedInputH, outputW, outputH);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "pixelart");
                },
                () =>
                {
                    if (paths != null)
                    {
                        SavedPaths.AddRange(paths);
                        foreach (string p in paths)
                        {
                            Texture2D tex = LoadTexture(p);
                            if (tex != null) ResultTextures.Add(tex);
                        }
                    }
                },
                ex => { _errorMessage = $"Error: {ex.Message}"; }
            );
        }

        // -----------------------------------------------------------------------
        // Tab 3 – Resize
        // -----------------------------------------------------------------------

        private void DrawResize()
        {
            DrawImagePicker("Reference Image", ref _resizeRefImagePath, ref _resizeRefImagePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Description");
            _resizeDescription = EditorGUILayout.TextField(_resizeDescription);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ref Size", GUILayout.Width(70));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _resizeRefWidth  = EditorGUILayout.IntField(_resizeRefWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _resizeRefHeight = EditorGUILayout.IntField(_resizeRefHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Size", GUILayout.Width(70));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _resizeTargetWidth  = EditorGUILayout.IntField(_resizeTargetWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _resizeTargetHeight = EditorGUILayout.IntField(_resizeTargetHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Resize Image"))
                RunResize();
            GUI.enabled = true;
        }

        private void RunResize()
        {
            if (string.IsNullOrEmpty(_resizeRefImagePath))
            {
                _errorMessage = "Please select a reference image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string refImagePath = _resizeRefImagePath;
            string desc         = _resizeDescription;
            int    refW         = _resizeRefWidth;
            int    refH         = _resizeRefHeight;
            int    targetW      = _resizeTargetWidth;
            int    targetH      = _resizeTargetHeight;
            string outputDir    = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject refImageData = JObject.Parse(ImageUtils.ImageToBase64Json(refImagePath));

                    JObject result = await Client.Resize(
                        desc, refImageData, refW, refH, targetW, targetH);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "resize");
                },
                () =>
                {
                    if (paths != null)
                    {
                        SavedPaths.AddRange(paths);
                        foreach (string p in paths)
                        {
                            Texture2D tex = LoadTexture(p);
                            if (tex != null) ResultTextures.Add(tex);
                        }
                    }
                },
                ex => { _errorMessage = $"Error: {ex.Message}"; }
            );
        }

        // -----------------------------------------------------------------------
        // Tab 4 – Remove Background
        // -----------------------------------------------------------------------

        private static readonly string[] RemoveBgTaskOptions = { "simple", "complex" };
        private int _removeBgTaskIndex = 0;

        private void DrawRemoveBackground()
        {
            DrawImagePicker("Source Image", ref _removeBgSourcePath, ref _removeBgSourcePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _removeBgWidth  = EditorGUILayout.IntField(_removeBgWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _removeBgHeight = EditorGUILayout.IntField(_removeBgHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Task");
            _removeBgTaskIndex = EditorGUILayout.Popup(_removeBgTaskIndex, RemoveBgTaskOptions);

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Remove Background"))
                RunRemoveBackground();
            GUI.enabled = true;
        }

        private void RunRemoveBackground()
        {
            if (string.IsNullOrEmpty(_removeBgSourcePath))
            {
                _errorMessage = "Please select a source image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string sourcePath = _removeBgSourcePath;
            int    w          = _removeBgWidth;
            int    h          = _removeBgHeight;
            string task       = RemoveBgTaskOptions[_removeBgTaskIndex];
            string outputDir  = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject imageData = JObject.Parse(ImageUtils.ImageToBase64Json(sourcePath));

                    var extras = new JObject
                    {
                        ["background_removal_task"] = task
                    };

                    JObject result = await Client.RemoveBackground(imageData, w, h, extras);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "removebg");
                },
                () =>
                {
                    if (paths != null)
                    {
                        SavedPaths.AddRange(paths);
                        foreach (string p in paths)
                        {
                            Texture2D tex = LoadTexture(p);
                            if (tex != null) ResultTextures.Add(tex);
                        }
                    }
                },
                ex => { _errorMessage = $"Error: {ex.Message}"; }
            );
        }
    }
}
#endif
