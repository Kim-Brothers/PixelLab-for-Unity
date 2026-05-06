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
        // Tab 3 – AnimateWithTextV3
        // -----------------------------------------------------------------------

        private string    _v3FirstFramePath    = "";
        private Texture2D _v3FirstFramePreview = null;
        private string    _v3LastFramePath     = "";
        private Texture2D _v3LastFramePreview  = null;
        private string    _v3Action            = "";
        private int       _v3FrameCount        = 8;
        private bool      _v3NoBackground      = false;

        // -----------------------------------------------------------------------
        // Tab 4 – AnimateWithSkeleton
        // -----------------------------------------------------------------------

        private string    _skelRefImagePath    = "";
        private Texture2D _skelRefImagePreview = null;
        private int       _skelWidth           = 64;
        private int       _skelHeight          = 64;

        // -----------------------------------------------------------------------
        // Tab 5 – EditAnimationV2
        // -----------------------------------------------------------------------

        private List<string>    _editAnimFramePaths    = new List<string>();
        private List<Texture2D> _editAnimFramePreviews = new List<Texture2D>();
        private string    _editAnimDescription   = "";
        private int       _editAnimWidth         = 64;
        private int       _editAnimHeight        = 64;

        // -----------------------------------------------------------------------
        // Tab 6 – TransferOutfitV2
        // -----------------------------------------------------------------------

        private string    _outfitRefImagePath    = "";
        private Texture2D _outfitRefImagePreview = null;
        private List<string>    _outfitFramePaths    = new List<string>();
        private List<Texture2D> _outfitFramePreviews = new List<Texture2D>();
        private int       _outfitWidth           = 64;
        private int       _outfitHeight          = 64;

        // -----------------------------------------------------------------------
        // Tab 7 – EstimateSkeleton
        // -----------------------------------------------------------------------

        private string    _estSkelImagePath    = "";
        private Texture2D _estSkelImagePreview = null;

        // -----------------------------------------------------------------------
        // Tab 8 – Generate8RotationsV3
        // -----------------------------------------------------------------------

        private string    _rot8v3FirstFramePath    = "";
        private Texture2D _rot8v3FirstFramePreview = null;
        private bool      _rot8v3NoBackground      = false;
        private int       _rot8v3Seed              = 0;

        // -----------------------------------------------------------------------
        // Shared UI state
        // -----------------------------------------------------------------------

        private int    _selectedTab = 0;

        private static readonly string[] TabNames =
        {
            "Text Animation",
            "Character Animation",
            "Interpolation",
            "AnimV3",
            "Skeleton Anim",
            "Edit Anim",
            "Outfit Transfer",
            "Est. Skeleton",
            "8 Rotations V3"
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
                case 3: DrawAnimV3();             break;
                case 4: DrawSkeletonAnim();       break;
                case 5: DrawEditAnim();           break;
                case 6: DrawOutfitTransfer();     break;
                case 7: DrawEstimateSkeleton();   break;
                case 8: Draw8RotationsV3();       break;
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

            if (string.IsNullOrWhiteSpace(_action))
            {
                _errorMessage = "Please enter an action description.";
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

        // -----------------------------------------------------------------------
        // Tab 3 – AnimateWithTextV3
        // -----------------------------------------------------------------------

        private void DrawAnimV3()
        {
            DrawImagePicker("First Frame", ref _v3FirstFramePath, ref _v3FirstFramePreview);
            EditorGUILayout.Space(4);
            DrawImagePicker("Last Frame (Optional)", ref _v3LastFramePath, ref _v3LastFramePreview);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Action Description");
            _v3Action = EditorGUILayout.TextField(_v3Action);
            EditorGUILayout.Space(4);
            _v3FrameCount   = EditorGUILayout.IntField("Frame Count", _v3FrameCount);
            _v3NoBackground = EditorGUILayout.Toggle("No Background", _v3NoBackground);
            EditorGUILayout.Space(8);
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Animation V3", GUILayout.Height(30)))
                RunAnimV3();
            GUI.enabled = true;
        }

        private void RunAnimV3()
        {
            if (string.IsNullOrEmpty(_v3FirstFramePath))
            {
                _errorMessage = "Please select a first frame image.";
                return;
            }
            _errorMessage = "";
            ClearResults();
            string firstPath  = _v3FirstFramePath;
            string lastPath   = _v3LastFramePath;
            string action     = _v3Action;
            int    frameCount = _v3FrameCount;
            bool   noBg       = _v3NoBackground;
            string outputDir  = Window.OutputDir;
            List<string> paths = null;
            RunAsync(async () =>
            {
                JObject firstFrame = JObject.Parse(ImageUtils.ImageToBase64Json(firstPath));
                var extra = new JObject();
                if (!string.IsNullOrEmpty(lastPath))
                    extra["last_frame"] = JObject.Parse(ImageUtils.ImageToBase64Json(lastPath));
                if (frameCount > 0) extra["frame_count"] = frameCount;
                if (noBg) extra["no_background"] = true;
                JObject result = await Client.AnimateWithTextV3(firstFrame, action, extra);
                string jobId = result["background_job_id"]?.ToString();
                if (!string.IsNullOrEmpty(jobId)) result = await Client.WaitForJob(jobId);
                paths = ImageUtils.SaveImagesFromResponseJson(result.ToString(), outputDir, "anim_v3");
            },
            () =>
            {
                if (paths != null) { SavedPaths.AddRange(paths); foreach (string p in paths) { var t = LoadTexture(p); if (t != null) ResultTextures.Add(t); } }
            },
            ex => { _errorMessage = $"Error: {ex.Message}"; });
        }

        // -----------------------------------------------------------------------
        // Tab 4 – AnimateWithSkeleton
        // -----------------------------------------------------------------------

        private void DrawSkeletonAnim()
        {
            DrawImagePicker("Reference Image", ref _skelRefImagePath, ref _skelRefImagePreview);
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _skelWidth  = EditorGUILayout.IntField(_skelWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _skelHeight = EditorGUILayout.IntField(_skelHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Animate with Skeleton", GUILayout.Height(30)))
                RunSkeletonAnim();
            GUI.enabled = true;
        }

        private void RunSkeletonAnim()
        {
            if (string.IsNullOrEmpty(_skelRefImagePath)) { _errorMessage = "Please select a reference image."; return; }
            _errorMessage = ""; ClearResults();
            string refPath = _skelRefImagePath; int w = _skelWidth; int h = _skelHeight; string outputDir = Window.OutputDir;
            List<string> paths = null;
            RunAsync(async () =>
            {
                JObject refImage = JObject.Parse(ImageUtils.ImageToBase64Json(refPath));
                JObject result = await Client.AnimateWithSkeleton(refImage, w, h);
                string jobId = result["background_job_id"]?.ToString();
                if (!string.IsNullOrEmpty(jobId)) result = await Client.WaitForJob(jobId);
                paths = ImageUtils.SaveImagesFromResponseJson(result.ToString(), outputDir, "skel_anim");
            },
            () => { if (paths != null) { SavedPaths.AddRange(paths); foreach (string p in paths) { var t = LoadTexture(p); if (t != null) ResultTextures.Add(t); } } },
            ex => { _errorMessage = $"Error: {ex.Message}"; });
        }

        // -----------------------------------------------------------------------
        // Tab 5 – EditAnimationV2
        // -----------------------------------------------------------------------

        private void DrawEditAnim()
        {
            EditorGUILayout.LabelField("Animation Frames (add images below)", EditorStyles.boldLabel);
            for (int i = 0; i < _editAnimFramePaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string p = _editAnimFramePaths[i]; Texture2D prev = _editAnimFramePreviews[i];
                DrawImagePicker($"Frame {i + 1}", ref p, ref prev, 50);
                _editAnimFramePaths[i] = p; _editAnimFramePreviews[i] = prev;
                if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(22))) { _editAnimFramePaths.RemoveAt(i); _editAnimFramePreviews.RemoveAt(i); break; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add Frame", GUILayout.Height(22))) { _editAnimFramePaths.Add(""); _editAnimFramePreviews.Add(null); }
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Edit Description");
            _editAnimDescription = EditorGUILayout.TextField(_editAnimDescription);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _editAnimWidth  = EditorGUILayout.IntField(_editAnimWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _editAnimHeight = EditorGUILayout.IntField(_editAnimHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Edit Animation", GUILayout.Height(30)))
                RunEditAnim();
            GUI.enabled = true;
        }

        private void RunEditAnim()
        {
            if (_editAnimFramePaths.Count == 0) { _errorMessage = "Please add at least one frame."; return; }
            _errorMessage = ""; ClearResults();
            var framePathsSnapshot = new List<string>(_editAnimFramePaths);
            string desc = _editAnimDescription; int w = _editAnimWidth; int h = _editAnimHeight; string outputDir = Window.OutputDir;
            List<string> paths = null;
            RunAsync(async () =>
            {
                var frames = new JArray();
                foreach (string fp in framePathsSnapshot)
                    if (!string.IsNullOrEmpty(fp)) frames.Add(JObject.Parse(ImageUtils.ImageToBase64Json(fp)));
                JObject result = await Client.EditAnimationV2(desc, frames, w, h);
                string jobId = result["background_job_id"]?.ToString();
                if (!string.IsNullOrEmpty(jobId)) result = await Client.WaitForJob(jobId);
                paths = ImageUtils.SaveImagesFromResponseJson(result.ToString(), outputDir, "edit_anim");
            },
            () => { if (paths != null) { SavedPaths.AddRange(paths); foreach (string p in paths) { var t = LoadTexture(p); if (t != null) ResultTextures.Add(t); } } },
            ex => { _errorMessage = $"Error: {ex.Message}"; });
        }

        // -----------------------------------------------------------------------
        // Tab 6 – TransferOutfitV2
        // -----------------------------------------------------------------------

        private void DrawOutfitTransfer()
        {
            DrawImagePicker("Reference (Outfit Source)", ref _outfitRefImagePath, ref _outfitRefImagePreview);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Target Frames", EditorStyles.boldLabel);
            for (int i = 0; i < _outfitFramePaths.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string p = _outfitFramePaths[i]; Texture2D prev = _outfitFramePreviews[i];
                DrawImagePicker($"Frame {i + 1}", ref p, ref prev, 50);
                _outfitFramePaths[i] = p; _outfitFramePreviews[i] = prev;
                if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(22))) { _outfitFramePaths.RemoveAt(i); _outfitFramePreviews.RemoveAt(i); break; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add Target Frame", GUILayout.Height(22))) { _outfitFramePaths.Add(""); _outfitFramePreviews.Add(null); }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _outfitWidth  = EditorGUILayout.IntField(_outfitWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _outfitHeight = EditorGUILayout.IntField(_outfitHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Transfer Outfit", GUILayout.Height(30)))
                RunOutfitTransfer();
            GUI.enabled = true;
        }

        private void RunOutfitTransfer()
        {
            if (string.IsNullOrEmpty(_outfitRefImagePath)) { _errorMessage = "Please select a reference image."; return; }
            if (_outfitFramePaths.Count == 0) { _errorMessage = "Please add at least one target frame."; return; }
            _errorMessage = ""; ClearResults();
            string refPath = _outfitRefImagePath;
            var framePathsSnap = new List<string>(_outfitFramePaths);
            int w = _outfitWidth; int h = _outfitHeight; string outputDir = Window.OutputDir;
            List<string> paths = null;
            RunAsync(async () =>
            {
                JObject refImage = JObject.Parse(ImageUtils.ImageToBase64Json(refPath));
                var frames = new JArray();
                foreach (string fp in framePathsSnap)
                    if (!string.IsNullOrEmpty(fp)) frames.Add(JObject.Parse(ImageUtils.ImageToBase64Json(fp)));
                JObject result = await Client.TransferOutfitV2(refImage, frames, w, h);
                string jobId = result["background_job_id"]?.ToString();
                if (!string.IsNullOrEmpty(jobId)) result = await Client.WaitForJob(jobId);
                paths = ImageUtils.SaveImagesFromResponseJson(result.ToString(), outputDir, "outfit");
            },
            () => { if (paths != null) { SavedPaths.AddRange(paths); foreach (string p in paths) { var t = LoadTexture(p); if (t != null) ResultTextures.Add(t); } } },
            ex => { _errorMessage = $"Error: {ex.Message}"; });
        }

        // -----------------------------------------------------------------------
        // Tab 7 – EstimateSkeleton
        // -----------------------------------------------------------------------

        private void DrawEstimateSkeleton()
        {
            EditorGUILayout.HelpBox("Extracts skeleton keypoints from a character image. Results are shown as JSON in the status area.", MessageType.Info);
            DrawImagePicker("Character Image", ref _estSkelImagePath, ref _estSkelImagePreview);
            EditorGUILayout.Space(8);
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Estimate Skeleton", GUILayout.Height(30)))
                RunEstimateSkeleton();
            GUI.enabled = true;
        }

        private void RunEstimateSkeleton()
        {
            if (string.IsNullOrEmpty(_estSkelImagePath)) { _errorMessage = "Please select an image."; return; }
            _errorMessage = ""; ClearResults();
            string imgPath = _estSkelImagePath;
            string outputDir = Window.OutputDir;
            List<string> paths = null;
            RunAsync(async () =>
            {
                JObject image = JObject.Parse(ImageUtils.ImageToBase64Json(imgPath));
                JObject result = await Client.EstimateSkeleton(image);
                string jobId = result["background_job_id"]?.ToString();
                if (!string.IsNullOrEmpty(jobId)) result = await Client.WaitForJob(jobId);
                paths = ImageUtils.SaveImagesFromResponseJson(result.ToString(), outputDir, "skeleton");
            },
            () => { if (paths != null) { SavedPaths.AddRange(paths); foreach (string p in paths) { var t = LoadTexture(p); if (t != null) ResultTextures.Add(t); } } },
            ex => { _errorMessage = $"Error: {ex.Message}"; });
        }

        // -----------------------------------------------------------------------
        // Tab 8 – Generate8RotationsV3
        // -----------------------------------------------------------------------

        private void Draw8RotationsV3()
        {
            DrawImagePicker("First Frame", ref _rot8v3FirstFramePath, ref _rot8v3FirstFramePreview);
            EditorGUILayout.Space(4);
            _rot8v3NoBackground = EditorGUILayout.Toggle("No Background", _rot8v3NoBackground);
            _rot8v3Seed         = EditorGUILayout.IntField("Seed (0 = Random)", _rot8v3Seed);
            EditorGUILayout.Space(8);
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate 8 Rotations V3", GUILayout.Height(30)))
                Run8RotationsV3();
            GUI.enabled = true;
        }

        private void Run8RotationsV3()
        {
            if (string.IsNullOrEmpty(_rot8v3FirstFramePath)) { _errorMessage = "Please select a first frame image."; return; }
            _errorMessage = ""; ClearResults();
            string firstPath = _rot8v3FirstFramePath;
            bool   noBg      = _rot8v3NoBackground;
            int    seed      = _rot8v3Seed;
            string outputDir = Window.OutputDir;
            List<string> paths = null;
            RunAsync(async () =>
            {
                JObject firstFrame = JObject.Parse(ImageUtils.ImageToBase64Json(firstPath));
                var extra = new JObject();
                if (noBg) extra["no_background"] = true;
                if (seed != 0) extra["seed"] = seed;
                JObject result = await Client.Generate8RotationsV3(firstFrame, extra.Count > 0 ? extra : null);
                string jobId = result["background_job_id"]?.ToString();
                if (!string.IsNullOrEmpty(jobId)) result = await Client.WaitForJob(jobId);
                paths = ImageUtils.SaveImagesFromResponseJson(result.ToString(), outputDir, "rot8v3");
            },
            () => { if (paths != null) { SavedPaths.AddRange(paths); foreach (string p in paths) { var t = LoadTexture(p); if (t != null) ResultTextures.Add(t); } } },
            ex => { _errorMessage = $"Error: {ex.Message}"; });
        }
    }
}
#endif
