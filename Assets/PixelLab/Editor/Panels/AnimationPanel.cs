#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class AnimationPanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Tab 0 – Text Animation (AnimateWithTextV2)
        // -----------------------------------------------------------------------

        private string    _refImagePath    = "";
        private Texture2D _refImagePreview = null;
        private string    _action          = "";
        private int       _width           = 128;
        private int       _height          = 128;
        private int       _refWidth        = 128;
        private int       _refHeight       = 128;

        // -----------------------------------------------------------------------
        // Tab 1 – Character Animation (CreateCharacterAnimation)
        // -----------------------------------------------------------------------

        private string _characterId      = "";
        private int    _templateIndex    = 0;

        // -----------------------------------------------------------------------
        // Tab 2 – Interpolation
        // -----------------------------------------------------------------------

        private string    _startImagePath    = "";
        private Texture2D _startImagePreview = null;
        private string    _endImagePath      = "";
        private Texture2D _endImagePreview   = null;
        private string    _interpAction      = "";
        private int       _interpWidth       = 128;
        private int       _interpHeight      = 128;

        // -----------------------------------------------------------------------
        // Shared UI state
        // -----------------------------------------------------------------------

        private int    _selectedTab = 0;

        private static readonly string[] TabNames =
        {
            "Text Animation",
            "Character Animation",
            "Interpolation"
        };

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public AnimationPanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            if (!RequireClient()) return;

            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

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
                case 0: DrawTextAnimation();      break;
                case 1: DrawCharacterAnimation(); break;
                case 2: DrawInterpolation();      break;
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
        // Tab 0 – Text Animation
        // -----------------------------------------------------------------------

        private void DrawTextAnimation()
        {
            DrawImagePicker("Reference Image", ref _refImagePath, ref _refImagePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Action Description");
            _action = EditorGUILayout.TextField(_action);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Size", GUILayout.Width(80));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _width  = EditorGUILayout.IntField(_width,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _height = EditorGUILayout.IntField(_height, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Reference Image Size", GUILayout.Width(130));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _refWidth  = EditorGUILayout.IntField(_refWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _refHeight = EditorGUILayout.IntField(_refHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Animation", GUILayout.Height(30)))
                RunTextAnimation();
            GUI.enabled = true;
        }

        private void RunTextAnimation()
        {
            if (string.IsNullOrEmpty(_refImagePath))
            {
                _errorMessage = "Please select a reference image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string refPath   = _refImagePath;
            string action    = _action;
            int    w         = _width;
            int    h         = _height;
            int    rw        = _refWidth;
            int    rh        = _refHeight;
            string outputDir = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject refImage = JObject.Parse(ImageUtils.ImageToBase64Json(refPath));

                    var extraParams = new JObject
                    {
                        ["reference_image_size"] = new JObject { ["width"] = rw, ["height"] = rh }
                    };

                    JObject result = await Client.AnimateWithTextV2(refImage, action, w, h, extraParams);

                    // Handle background job
                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "animation");
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
        // Tab 1 – Character Animation
        // -----------------------------------------------------------------------

        private void DrawCharacterAnimation()
        {
            EditorGUILayout.LabelField("Character ID");
            _characterId = EditorGUILayout.TextField(_characterId);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Animation Template");
            _templateIndex = EditorGUILayout.Popup(_templateIndex, PixelLabConstants.ANIMATION_TEMPLATES);

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Animation", GUILayout.Height(30)))
                RunCharacterAnimation();
            GUI.enabled = true;
        }

        private void RunCharacterAnimation()
        {
            if (string.IsNullOrEmpty(_characterId))
            {
                _errorMessage = "Please enter a character ID.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string charId    = _characterId;
            string templateId = PixelLabConstants.ANIMATION_TEMPLATES[_templateIndex];
            string outputDir  = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject result = await Client.CreateCharacterAnimation(charId, templateId);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "char_anim");
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
        // Tab 2 – Interpolation
        // -----------------------------------------------------------------------

        private void DrawInterpolation()
        {
            DrawImagePicker("Start Image", ref _startImagePath, ref _startImagePreview);
            EditorGUILayout.Space(4);
            DrawImagePicker("End Image", ref _endImagePath, ref _endImagePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Action Description");
            _interpAction = EditorGUILayout.TextField(_interpAction);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _interpWidth  = EditorGUILayout.IntField(_interpWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _interpHeight = EditorGUILayout.IntField(_interpHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Interpolation", GUILayout.Height(30)))
                RunInterpolation();
            GUI.enabled = true;
        }

        private void RunInterpolation()
        {
            if (string.IsNullOrEmpty(_startImagePath) || string.IsNullOrEmpty(_endImagePath))
            {
                _errorMessage = "Please select both a start image and an end image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string startPath = _startImagePath;
            string endPath   = _endImagePath;
            string action    = _interpAction;
            int    w         = _interpWidth;
            int    h         = _interpHeight;
            string outputDir = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject startImageData = JObject.Parse(ImageUtils.ImageToBase64Json(startPath));
                    JObject endImageData   = JObject.Parse(ImageUtils.ImageToBase64Json(endPath));

                    // API expects {image: ImageData, size: {width, height}} wrappers, not raw ImageData
                    ImageUtils.GetImageSize(startPath, out int startW, out int startH);
                    ImageUtils.GetImageSize(endPath,   out int endW,   out int endH);

                    var startImage = new JObject
                    {
                        ["image"] = startImageData,
                        ["size"]  = new JObject { ["width"] = startW, ["height"] = startH }
                    };
                    var endImage = new JObject
                    {
                        ["image"] = endImageData,
                        ["size"]  = new JObject { ["width"] = endW, ["height"] = endH }
                    };

                    JObject result = await Client.Interpolation(startImage, endImage, action, w, h);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "interp");
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
