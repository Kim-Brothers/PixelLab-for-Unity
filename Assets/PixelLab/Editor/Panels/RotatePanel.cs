#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class RotatePanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Tab 0 – Single Rotation (Rotate)
        // -----------------------------------------------------------------------

        private string    _rotateSourcePath    = "";
        private Texture2D _rotateSourcePreview = null;

        // Direction (index 0 = "Auto" = not sent)
        private static readonly string[] FromDirectionOptions =
        {
            "Auto", "north", "north-east", "east", "south-east",
            "south", "south-west", "west", "north-west"
        };
        private static readonly string[] ToDirectionOptions =
        {
            "north", "north-east", "east", "south-east",
            "south", "south-west", "west", "north-west"
        };

        // View (index 0 = "Auto" = not sent)
        private static readonly string[] ViewOptions =
        {
            "Auto", "low top-down", "high top-down", "side"
        };

        private int    _fromDirectionIndex = 5; // "south"
        private int    _toDirectionIndex   = 3; // "south-east"
        private int    _fromViewIndex      = 0; // "Auto"
        private int    _toViewIndex        = 0; // "Auto"

        private string _directionChangeStr = "";
        private string _viewChangeStr      = "";
        private bool   _isometric          = false;
        private bool   _obliqueProjection  = false;
        private string _seedStr            = "";
        private string _guidanceStr        = "";

        // Init image
        private string    _initImagePath     = "";
        private Texture2D _initImagePreview  = null;
        private string    _initStrengthStr   = "";

        // Color reference image
        private string    _colorImagePath    = "";
        private Texture2D _colorImagePreview = null;

        // -----------------------------------------------------------------------
        // Tab 1 – 8-Direction Generation (Generate8Rotations)
        // -----------------------------------------------------------------------

        private string    _genRefImagePath     = "";
        private Texture2D _genRefImagePreview  = null;
        private int       _genWidth            = 64;
        private int       _genHeight           = 64;
        private int       _genMethodIndex      = 0;
        private int       _genViewIndex        = 0;
        private string    _genDescription      = "";
        private string    _genStyleDescription = "";
        private bool      _genNoBackground     = false;
        private string    _genSeed             = "";

        private static readonly string[] GenerationMethodOptions =
        {
            "rotate_character",
            "create_with_style",
            "create_from_concept"
        };

        private static readonly string[] GenViewOptions =
        {
            "low top-down",
            "high top-down",
            "side"
        };

        // -----------------------------------------------------------------------
        // Shared UI state
        // -----------------------------------------------------------------------

        private int    _selectedTab = 0;
        private static readonly string[] TabNames = { "Single Rotation", "8-Direction Generation" };

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public RotatePanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            if (!RequireClient()) return;

            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Rotation", EditorStyles.boldLabel);
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
                case 0: DrawSingleRotate();  break;
                case 1: Draw8Rotations();    break;
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
        // Tab 0 – Single Rotation
        // -----------------------------------------------------------------------

        private void DrawSingleRotate()
        {
            DrawImagePicker("Image", ref _rotateSourcePath, ref _rotateSourcePreview);

            EditorGUILayout.Space(4);

            // Direction row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source Direction", GUILayout.Width(110));
            _fromDirectionIndex = EditorGUILayout.Popup(_fromDirectionIndex, FromDirectionOptions);
            EditorGUILayout.LabelField("Target Direction", GUILayout.Width(110));
            _toDirectionIndex = EditorGUILayout.Popup(_toDirectionIndex, ToDirectionOptions);
            EditorGUILayout.EndHorizontal();

            // View row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Source View", GUILayout.Width(80));
            _fromViewIndex = EditorGUILayout.Popup(_fromViewIndex, ViewOptions);
            EditorGUILayout.LabelField("Target View", GUILayout.Width(80));
            _toViewIndex = EditorGUILayout.Popup(_toViewIndex, ViewOptions);
            EditorGUILayout.EndHorizontal();

            // Angle row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Rotation Angle", GUILayout.Width(95));
            _directionChangeStr = EditorGUILayout.TextField(_directionChangeStr, GUILayout.Width(60));
            EditorGUILayout.LabelField("Tilt Angle", GUILayout.Width(70));
            _viewChangeStr = EditorGUILayout.TextField(_viewChangeStr, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Projection row
            EditorGUILayout.BeginHorizontal();
            _isometric         = EditorGUILayout.ToggleLeft("Isometric",          _isometric, GUILayout.Width(90));
            _obliqueProjection = EditorGUILayout.ToggleLeft("Oblique Projection", _obliqueProjection, GUILayout.Width(130));
            EditorGUILayout.EndHorizontal();

            // Seed & guidance
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed", GUILayout.Width(40));
            _seedStr = EditorGUILayout.TextField(_seedStr, GUILayout.Width(80));
            EditorGUILayout.LabelField("Image Guidance", GUILayout.Width(105));
            _guidanceStr = EditorGUILayout.TextField(_guidanceStr, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Init image
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Init Image (Optional)");
            DrawImagePicker("", ref _initImagePath, ref _initImagePreview);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Init Image Strength", GUILayout.Width(130));
            _initStrengthStr = EditorGUILayout.TextField(_initStrengthStr, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Color reference image
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Color Reference Image (Optional)");
            DrawImagePicker("", ref _colorImagePath, ref _colorImagePreview);

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Rotation", GUILayout.Height(30)))
                RunSingleRotate();
            GUI.enabled = true;
        }

        private void RunSingleRotate()
        {
            if (string.IsNullOrEmpty(_rotateSourcePath))
            {
                _errorMessage = "Please select an image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string sourcePath      = _rotateSourcePath;
            int    fromDirIdx      = _fromDirectionIndex;
            int    toDirIdx        = _toDirectionIndex;
            int    fromViewIdx     = _fromViewIndex;
            int    toViewIdx       = _toViewIndex;
            string dirChangeStr    = _directionChangeStr;
            string viewChangeStr   = _viewChangeStr;
            bool   isometric       = _isometric;
            bool   obliqueProj     = _obliqueProjection;
            string seedStr         = _seedStr;
            string guidanceStr     = _guidanceStr;
            string initImagePath   = _initImagePath;
            string initStrengthStr = _initStrengthStr;
            string colorImagePath  = _colorImagePath;
            string outputDir       = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject fromImage = JObject.Parse(ImageUtils.ImageToBase64Json(sourcePath));
                    ImageUtils.GetImageSize(sourcePath, out int imgW, out int imgH);

                    var extra = new JObject();

                    // Directions
                    if (fromDirIdx != 0)
                        extra["from_direction"] = FromDirectionOptions[fromDirIdx];
                    extra["to_direction"] = ToDirectionOptions[toDirIdx];

                    // Views
                    if (fromViewIdx != 0)
                        extra["from_view"] = ViewOptions[fromViewIdx];
                    if (toViewIdx != 0)
                        extra["to_view"] = ViewOptions[toViewIdx];

                    // Angles
                    if (!string.IsNullOrEmpty(dirChangeStr) && int.TryParse(dirChangeStr, out int dirChange))
                        extra["direction_change"] = dirChange;
                    if (!string.IsNullOrEmpty(viewChangeStr) && int.TryParse(viewChangeStr, out int viewChange))
                        extra["view_change"] = viewChange;

                    // Projection
                    if (isometric)     extra["isometric"]           = true;
                    if (obliqueProj)   extra["oblique_projection"]  = true;

                    // Seed & guidance
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int seed))
                        extra["seed"] = seed;
                    if (!string.IsNullOrEmpty(guidanceStr) &&
                        float.TryParse(guidanceStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float guidance))
                        extra["image_guidance_scale"] = guidance;

                    // Init image
                    if (!string.IsNullOrEmpty(initImagePath))
                    {
                        extra["init_image"] = JObject.Parse(ImageUtils.ImageToBase64Json(initImagePath));
                        if (!string.IsNullOrEmpty(initStrengthStr) && int.TryParse(initStrengthStr, out int initStrength))
                            extra["init_image_strength"] = initStrength;
                    }

                    // Color reference image
                    if (!string.IsNullOrEmpty(colorImagePath))
                        extra["color_image"] = JObject.Parse(ImageUtils.ImageToBase64Json(colorImagePath));

                    JObject result = await Client.Rotate(fromImage, imgW, imgH, extra);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "rotate");
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
        // Tab 1 – 8-Direction Generation
        // -----------------------------------------------------------------------

        private void Draw8Rotations()
        {
            DrawImagePicker("Reference Image", ref _genRefImagePath, ref _genRefImagePreview);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Generation Method");
            _genMethodIndex = EditorGUILayout.Popup(_genMethodIndex, GenerationMethodOptions);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("View");
            _genViewIndex = EditorGUILayout.Popup(_genViewIndex, GenViewOptions);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Description (Optional)");
            _genDescription = EditorGUILayout.TextField(_genDescription);

            EditorGUILayout.LabelField("Style Description (Optional)");
            _genStyleDescription = EditorGUILayout.TextField(_genStyleDescription);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _genWidth  = EditorGUILayout.IntField(_genWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _genHeight = EditorGUILayout.IntField(_genHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed (Optional)", GUILayout.Width(110));
            _genSeed = EditorGUILayout.TextField(_genSeed, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            _genNoBackground = EditorGUILayout.Toggle("Remove Background", _genNoBackground);

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "8-Direction Generation", GUILayout.Height(30)))
                RunGenerate8Rotations();
            GUI.enabled = true;
        }

        private void RunGenerate8Rotations()
        {
            if (string.IsNullOrEmpty(_genRefImagePath))
            {
                _errorMessage = "Please select a reference image.";
                return;
            }

            _errorMessage = "";
            ClearResults();

            string refImagePath     = _genRefImagePath;
            int    methodIdx        = _genMethodIndex;
            int    viewIdx          = _genViewIndex;
            string description      = _genDescription;
            string styleDescription = _genStyleDescription;
            bool   noBackground     = _genNoBackground;
            string seedStr          = _genSeed;
            int    w                = _genWidth;
            int    h                = _genHeight;
            string outputDir        = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    JObject refImageData = JObject.Parse(ImageUtils.ImageToBase64Json(refImagePath));
                    ImageUtils.GetImageSize(refImagePath, out int refW, out int refH);

                    // API expects {image: ImageData, width: N, height: N} (flat, not nested)
                    var refImageWrapper = new JObject
                    {
                        ["image"]  = refImageData,
                        ["width"]  = refW,
                        ["height"] = refH
                    };

                    var extraParams = new JObject
                    {
                        ["reference_image"] = refImageWrapper,
                        ["view"]            = GenViewOptions[viewIdx]
                    };

                    // method key omitted for default "rotate_character"
                    if (methodIdx != 0)
                        extraParams["method"] = GenerationMethodOptions[methodIdx];

                    if (!string.IsNullOrEmpty(description))
                        extraParams["description"] = description;
                    if (!string.IsNullOrEmpty(styleDescription))
                        extraParams["style_description"] = styleDescription;
                    if (noBackground)
                        extraParams["no_background"] = true;
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int seed))
                        extraParams["seed"] = seed;

                    JObject result = await Client.Generate8Rotations(w, h, extraParams);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "rotations");
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
