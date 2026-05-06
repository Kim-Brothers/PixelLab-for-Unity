#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class GeneratePanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Generation type
        // -----------------------------------------------------------------------

        private static readonly string[] GenTypeLabels = { "Image", "Style", "UI" };
        private int _genType = 0; // 0=Image, 1=Style, 2=UI

        // -----------------------------------------------------------------------
        // Model selector (Image type only)
        // -----------------------------------------------------------------------

        private static readonly string[] ModelLabels = { "Pro", "PixFlux", "Pixen", "BitForge" };
        private int _model = 0; // 0=Pro, 1=PixFlux, 2=Pixen, 3=BitForge

        // -----------------------------------------------------------------------
        // Basic parameters
        // -----------------------------------------------------------------------

        private string _description  = "";
        private int    _width        = 128;
        private int    _height       = 128;
        private bool   _noBackground = false;
        private int    _seed         = 0;

        // -----------------------------------------------------------------------
        // Advanced parameters (PixFlux / BitForge)
        // -----------------------------------------------------------------------

        private bool   _advancedFoldout       = false;
        private string _negativeDescription   = "";
        private string _textGuidanceScaleStr  = "";

        private static readonly string[] OutlineOptions  = { "None", "single color black outline", "single color outline", "selective outline", "lineless" };
        private static readonly string[] ShadingOptions  = { "None", "flat shading", "low shading", "medium shading", "high shading", "highly detailed shading" };
        private static readonly string[] DetailOptions   = { "None", "low detail", "medium detail", "highly detailed" };
        private static readonly string[] ViewOptions     = { "None", "side", "low top-down", "high top-down" };
        private static readonly string[] DirectionOptions = { "None", "south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west" };

        private int  _outlineIndex   = 0;
        private int  _shadingIndex   = 0;
        private int  _detailIndex    = 0;
        private int  _viewIndex      = 0;
        private int  _directionIndex = 0;
        private bool _isometric      = false;

        // -----------------------------------------------------------------------
        // Style images (Style type only, up to 4)
        // -----------------------------------------------------------------------

        private List<string>    _styleImagePaths    = new List<string>();
        private List<Texture2D> _styleImagePreviews = new List<Texture2D>();

        // -----------------------------------------------------------------------
        // Reference images (Pro model / Image type only, up to 4)
        // -----------------------------------------------------------------------

        private List<string>    _referenceImagePaths    = new List<string>();
        private List<Texture2D> _referenceImagePreviews = new List<Texture2D>();

        // -----------------------------------------------------------------------
        // BitForge style_image (single, optional)
        // -----------------------------------------------------------------------

        private string    _bitforgeStylePath    = "";
        private Texture2D _bitforgeStylePreview = null;

        // -----------------------------------------------------------------------
        // Scratch fields for async→main-thread transfer
        // -----------------------------------------------------------------------

        private List<string> _pendingSavedPaths = new List<string>();
        private float        _pendingUsd        = 0f;
        private float        _pendingCredits    = 0f;
        private string       _pendingUsageText  = "";

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public GeneratePanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawPanelHeader("Image Generation");

            if (!RequireClient())
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            // Generation type toolbar
            EditorGUILayout.LabelField("Generation Type");
            _genType = GUILayout.Toolbar(_genType, GenTypeLabels);
            EditorGUILayout.Space(4);

            // Model selector — only for Image type
            if (_genType == 0)
            {
                EditorGUILayout.LabelField("Model");
                _model = GUILayout.Toolbar(_model, ModelLabels);
                EditorGUILayout.Space(4);
            }

            // Description
            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));
            EditorGUILayout.Space(4);

            // Width / Height
            EditorGUILayout.BeginHorizontal();
            _width  = EditorGUILayout.IntField("Width (px)", _width);
            _height = EditorGUILayout.IntField("Height (px)", _height);
            EditorGUILayout.EndHorizontal();

            // No Background + Seed
            _noBackground = EditorGUILayout.Toggle("No Background", _noBackground);
            _seed         = EditorGUILayout.IntField("Seed (0 = Random)", _seed);
            EditorGUILayout.Space(4);

            // Advanced foldout — only for PixFlux / BitForge
            if (_genType == 0 && _model > 0)
            {
                _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "Advanced Settings", true);
                if (_advancedFoldout)
                {
                    EditorGUI.indentLevel++;

                    _negativeDescription  = EditorGUILayout.TextField("Negative Description", _negativeDescription);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Text Guidance", GUILayout.Width(120));
                    _textGuidanceScaleStr = EditorGUILayout.TextField(_textGuidanceScaleStr, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();

                    _outlineIndex   = EditorGUILayout.Popup("Outline",    _outlineIndex,   OutlineOptions);
                    _shadingIndex   = EditorGUILayout.Popup("Shading",    _shadingIndex,   ShadingOptions);
                    _detailIndex    = EditorGUILayout.Popup("Detail",     _detailIndex,    DetailOptions);
                    _viewIndex      = EditorGUILayout.Popup("View",       _viewIndex,      ViewOptions);
                    _directionIndex = EditorGUILayout.Popup("Direction",  _directionIndex, DirectionOptions);
                    _isometric      = EditorGUILayout.Toggle("Isometric", _isometric);

                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space(4);
            }

            // Style images — only for Style type
            if (_genType == 1)
            {
                EditorGUILayout.LabelField("Style Images (Max 4)", EditorStyles.boldLabel);

                int removeStyleIdx = -1;
                for (int i = 0; i < _styleImagePaths.Count; i++)
                {
                    int idx = i;
                    string path    = _styleImagePaths[i];
                    Texture2D prev = _styleImagePreviews[i];
                    DrawImagePicker($"Style {i + 1}", ref path, ref prev, () =>
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(60)))
                            removeStyleIdx = idx;
                    }, 60);
                    _styleImagePaths[i]    = path;
                    _styleImagePreviews[i] = prev;
                }
                if (removeStyleIdx >= 0)
                {
                    _styleImagePaths.RemoveAt(removeStyleIdx);
                    _styleImagePreviews.RemoveAt(removeStyleIdx);
                }

                if (_styleImagePaths.Count < 4)
                {
                    if (GUILayout.Button("+ Add Style Image", GUILayout.Height(24)))
                    {
                        _styleImagePaths.Add("");
                        _styleImagePreviews.Add(null);
                    }
                }

                EditorGUILayout.Space(4);
            }

            // BitForge style image — only for BitForge model (genType==0, model==3), collapsed
            if (_genType == 0 && _model == 3 && DrawOptionalFoldout("Style Image (optional)"))
            {
                DrawImagePicker("Style Image", ref _bitforgeStylePath, ref _bitforgeStylePreview, 60);
                EditorGUILayout.Space(4);
            }

            // Reference images — only for Pro model (genType==0, model==0), collapsed
            if (_genType == 0 && _model == 0 && DrawOptionalFoldout("Reference Images (Max 4, optional)"))
            {
                int removeRefIdx = -1;
                for (int i = 0; i < _referenceImagePaths.Count; i++)
                {
                    int idx = i;
                    string path    = _referenceImagePaths[i];
                    Texture2D prev = _referenceImagePreviews[i];
                    DrawImagePicker($"Ref {i + 1}", ref path, ref prev, () =>
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(60)))
                            removeRefIdx = idx;
                    }, 60);
                    _referenceImagePaths[i]    = path;
                    _referenceImagePreviews[i] = prev;
                }
                if (removeRefIdx >= 0)
                {
                    _referenceImagePaths.RemoveAt(removeRefIdx);
                    _referenceImagePreviews.RemoveAt(removeRefIdx);
                }

                if (_referenceImagePaths.Count < 4)
                {
                    if (GUILayout.Button("+ Add Reference Image", GUILayout.Height(24)))
                    {
                        _referenceImagePaths.Add("");
                        _referenceImagePreviews.Add(null);
                    }
                }

                EditorGUILayout.Space(4);
            }

            // Error message
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
                EditorGUILayout.Space(4);
            }

            // Generate button
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate", GUILayout.Height(32)))
            {
                _errorMessage = "";
                StartGenerate();
            }
            GUI.enabled = true;

            // Results
            if (SavedPaths.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Saved files:\n" + string.Join("\n", SavedPaths), MessageType.Info);
            }

            DrawImagePreviews(200);

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Generate logic
        // -----------------------------------------------------------------------

        private void StartGenerate()
        {
            if (string.IsNullOrWhiteSpace(_description))
            {
                _errorMessage = "Please enter a description.";
                return;
            }

            if (_width <= 0 || _height <= 0)
            {
                _errorMessage = "Width/Height must be greater than 0.";
                return;
            }

            // Snapshot mutable state for the background thread
            string     desc        = _description;
            int        width       = _width;
            int        height      = _height;
            bool       noBg        = _noBackground;
            int        seed        = _seed;
            int        genType     = _genType;
            int        model       = _model;
            string     negDesc     = _negativeDescription;
            string     guidanceStr = _textGuidanceScaleStr;
            int        outline     = _outlineIndex;
            int        shading     = _shadingIndex;
            int        detail      = _detailIndex;
            int        view        = _viewIndex;
            int        direction   = _directionIndex;
            bool       isometric   = _isometric;
            string     outputDir   = Window.OutputDir;

            // Snapshot style image paths
            var stylePathsSnapshot = new List<string>(_styleImagePaths);

            // Snapshot reference image paths (Pro model only)
            var refPathsSnapshot = new List<string>(_referenceImagePaths);

            // Snapshot BitForge style image path
            string bitforgeStylePath = _bitforgeStylePath;

            LoadingMessage = "Generating image...";
            ClearResults();

            RunAsync(
                async () =>
                {
                    // Build extra params
                    var extra = new JObject();
                    if (noBg) extra["no_background"] = true;
                    if (seed != 0) extra["seed"] = seed;

                    // Advanced options apply to PixFlux / BitForge (model > 0) in Image mode
                    bool useAdvanced = (genType == 0 && model > 0);
                    if (useAdvanced)
                    {
                        if (!string.IsNullOrEmpty(negDesc))
                            extra["negative_description"] = negDesc;
                        if (!string.IsNullOrEmpty(guidanceStr) && float.TryParse(guidanceStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float guidanceVal))
                            extra["text_guidance_scale"] = guidanceVal;
                        if (outline > 0) extra["outline"]   = OutlineOptions[outline];
                        if (shading > 0) extra["shading"]   = ShadingOptions[shading];
                        if (detail  > 0) extra["detail"]    = DetailOptions[detail];
                        if (view    > 0) extra["view"]      = ViewOptions[view];
                        if (direction > 0) extra["direction"] = DirectionOptions[direction];
                        if (isometric) extra["isometric"] = true;
                    }

                    JObject response;

                    if (genType == 0)
                    {
                        // Image generation
                        switch (model)
                        {
                            case 1:
                                response = await Client.GenerateImagePixflux(desc, width, height, extra);
                                break;
                            case 2:
                                response = await Client.GenerateImagePixen(desc, width, height, extra);
                                break;
                            case 3:
                                // Attach style_image when provided
                                if (!string.IsNullOrEmpty(bitforgeStylePath))
                                {
                                    JObject styleImgData = JObject.Parse(ImageUtils.ImageToBase64Json(bitforgeStylePath));
                                    ImageUtils.GetImageSize(bitforgeStylePath, out int styleW, out int styleH);
                                    extra["style_image"] = new JObject
                                    {
                                        ["image"]  = styleImgData,
                                        ["size"]   = new JObject { ["width"] = styleW, ["height"] = styleH }
                                    };
                                }
                                response = await Client.GenerateImageBitforge(desc, width, height, extra);
                                break;
                            default:
                                // Pro model — attach reference_images if provided
                                var refsArray = new JArray();
                                foreach (string rp in refPathsSnapshot)
                                {
                                    if (!string.IsNullOrEmpty(rp))
                                    {
                                        JObject imgData = JObject.Parse(ImageUtils.ImageToBase64Json(rp));
                                        ImageUtils.GetImageSize(rp, out int refW, out int refH);
                                        refsArray.Add(new JObject { ["image"] = imgData, ["width"] = refW, ["height"] = refH });
                                    }
                                }
                                if (refsArray.Count > 0)
                                    extra["reference_images"] = refsArray;
                                response = await Client.GenerateImage(desc, width, height, extra);
                                break;
                        }
                    }
                    else if (genType == 1)
                    {
                        // Style generation — build style images JArray
                        var styleArray = new JArray();
                        foreach (string path in stylePathsSnapshot)
                        {
                            if (!string.IsNullOrEmpty(path))
                            {
                                JObject imgData = JObject.Parse(ImageUtils.ImageToBase64Json(path));
                                ImageUtils.GetImageSize(path, out int styleW, out int styleH);
                                styleArray.Add(new JObject { ["image"] = imgData, ["width"] = styleW, ["height"] = styleH });
                            }
                        }
                        response = await Client.GenerateWithStyle(desc, styleArray, width, height, extra);
                    }
                    else
                    {
                        // UI generation
                        response = await Client.GenerateUI(desc, width, height, extra);
                    }

                    // Handle background job
                    string jobId = response["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        LoadingMessage = "Waiting for background job...";
                        response = await Client.WaitForJob(jobId, 2f);
                    }

                    // Extract usage info from response (if present)
                    JToken usageToken    = response["usage"];
                    _pendingUsageText    = usageToken?.ToString(Newtonsoft.Json.Formatting.None) ?? "";

                    // Save images
                    string responseJson = response.ToString(Newtonsoft.Json.Formatting.None);
                    _pendingSavedPaths = ImageUtils.SaveImagesFromResponseJson(responseJson, outputDir, "generate");

                    // Fetch updated balance
                    JObject balance = await Client.GetBalance();
                    (_pendingUsd, _pendingCredits) = PixelLabWindow.ParseBalanceResponse(balance);
                },
                onComplete: () =>
                {
                    SavedPaths.Clear();
                    SavedPaths.AddRange(_pendingSavedPaths);

                    ResultTextures.Clear();
                    foreach (string p in SavedPaths)
                    {
                        var tex = LoadTexture(p);
                        ResultTextures.Add(tex);
                    }

                    if (!string.IsNullOrEmpty(_pendingUsageText))
                        Window.SetCost(_pendingUsageText);
                    Window.SetCredits($"${_pendingUsd:F4}");
                },
                onError: ex =>
                {
                    _errorMessage = $"Generation failed: {ex.Message}";
                }
            );
        }
    }
}
#endif
