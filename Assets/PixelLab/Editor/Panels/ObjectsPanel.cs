#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class ObjectsPanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Create section fields
        // -----------------------------------------------------------------------

        private string _description   = "";
        private int    _directionType = 0;   // 0 = 1-Direction, 1 = 8-Direction
        private int    _width         = 64;
        private int    _height        = 64;
        private bool   _noBackground  = false;
        private int    _seed          = 0;
        private bool   _isMapObject   = false;

        private static readonly string[] DirectionLabels = { "1 Direction", "8 Directions" };

        // Advanced foldout fields
        private bool   _advancedFoldout = false;
        private int    _nFramesIdx      = 0;   // 0=1(default), 1=4, 2=16, 3=64
        private int    _objectViewIdx   = 0;   // 0=(default), 1=low top-down, 2=high top-down, 3=side
        private List<string>    _styleImagePaths    = new List<string>();
        private List<Texture2D> _styleImagePreviews = new List<Texture2D>();

        private static readonly string[]  NFramesLabels  = { "1 (default)", "4", "16", "64" };
        private static readonly int[]     NFramesValues  = { 1, 4, 16, 64 };
        private static readonly string[]  ObjectViewOptions = { "(default)", "low top-down", "high top-down", "side" };

        // -----------------------------------------------------------------------
        // Object list fields
        // -----------------------------------------------------------------------

        private List<(string id, string description, string createdAt, string tags)> _objects
            = new List<(string, string, string, string)>();

        private List<(string id, string description, string createdAt, string tags)> _pendingObjects
            = new List<(string, string, string, string)>();

        private Vector2 _listScrollPos;

        // Pagination
        private int _pageSize      = 20;
        private int _pageOffset    = 0;
        private int _lastPageCount = 0;

        // Inline tag editing state
        private string _editingTagsId    = "";
        private string _editingTagsValue = "";

        // Inline action panel state
        private string _actionRowId   = "";
        private string _actionRowMode = ""; // "animate" | "vary" | "review"

        // Animate form state
        private string _animDescription = "";
        private int    _animDirIdx      = 0;
        private int    _animFrameCount  = 8;
        private bool   _animNoBg        = false;

        // Vary form state
        private string _varyEditDesc = "";

        // Review form state
        private string _reviewIndicesCsv = "";
        private string _reviewCommonTag  = "";

        private static readonly string[] DirectionPickerOptions =
        {
            "south", "north", "east", "west",
            "south-east", "south-west", "north-east", "north-west"
        };

        // -----------------------------------------------------------------------
        // Error / status messages
        // -----------------------------------------------------------------------

        private string _createError   = "";
        private string _listError     = "";
        private string _actionStatus  = "";

        // -----------------------------------------------------------------------
        // Scratch for async→main-thread transfer
        // -----------------------------------------------------------------------

        private List<string> _pendingSavedPaths = new List<string>();

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public ObjectsPanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawPanelHeader("Object Generation / Management");

            if (!RequireClient())
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawCreateSection();

            EditorGUILayout.Space(12);
            DrawDivider();
            EditorGUILayout.Space(8);

            DrawListSection();

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Section 1: Create Object
        // -----------------------------------------------------------------------

        private void DrawCreateSection()
        {
            DrawSectionHeader("Create Object");

            // Description
            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));
            EditorGUILayout.Space(4);

            // Directions toolbar
            EditorGUILayout.LabelField("Directions");
            _directionType = GUILayout.Toolbar(_directionType, DirectionLabels);
            EditorGUILayout.Space(4);

            // Width / Height
            EditorGUILayout.BeginHorizontal();
            _width  = EditorGUILayout.IntField("Width",  _width,  GUILayout.ExpandWidth(true));
            _height = EditorGUILayout.IntField("Height", _height, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // No Background toggle + Map Object checkbox
            EditorGUILayout.BeginHorizontal();
            _noBackground = EditorGUILayout.Toggle("No Background", _noBackground);
            _isMapObject  = EditorGUILayout.Toggle("Map Object",    _isMapObject);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Seed (0 = random)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed (0 = random)", GUILayout.Width(120));
            _seed = EditorGUILayout.IntField(_seed);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

            // Advanced foldout
            _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "Advanced Settings", true);
            if (_advancedFoldout)
            {
                EditorGUI.indentLevel++;

                // n_frames — only relevant for 1-Direction
                if (_directionType == 0)
                {
                    _nFramesIdx = EditorGUILayout.Popup("N Frames", _nFramesIdx, NFramesLabels);
                }

                // view
                _objectViewIdx = EditorGUILayout.Popup("View", _objectViewIdx, ObjectViewOptions);

                // Style images (up to 4)
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Style Images (Max 4)", EditorStyles.boldLabel);

                int removeStyleIdx = -1;
                for (int i = 0; i < _styleImagePaths.Count; i++)
                {
                    int idx = i;
                    string    path = _styleImagePaths[i];
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
            EditorGUILayout.Space(2);

            // Error message
            if (!string.IsNullOrEmpty(_createError))
            {
                EditorGUILayout.HelpBox(_createError, MessageType.Error);
                EditorGUILayout.Space(4);
            }

            // Create button
            GUI.enabled = !IsLoading;
            string createLabel = IsLoading
                ? LoadingMessage
                : (_isMapObject ? "Generate Map Object" : "Generate Object");

            if (GUILayout.Button(createLabel, GUILayout.Height(32)))
            {
                _createError = "";
                StartCreateObject();
            }
            GUI.enabled = true;

            // Saved paths feedback
            if (SavedPaths.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("Saved files:\n" + string.Join("\n", SavedPaths), MessageType.Info);
            }

            DrawImagePreviews(160);
        }

        // -----------------------------------------------------------------------
        // Section 2: Object List
        // -----------------------------------------------------------------------

        private void DrawListSection()
        {
            DrawSectionHeader("Object List");

            // Pagination controls
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !IsLoading && _pageOffset > 0;
            if (GUILayout.Button("Prev", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                _pageOffset = Mathf.Max(0, _pageOffset - _pageSize);
                RefreshObjectList();
            }
            GUI.enabled = !IsLoading && _lastPageCount >= _pageSize;
            if (GUILayout.Button("Next", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                _pageOffset += _pageSize;
                RefreshObjectList();
            }
            GUI.enabled = true;
            EditorGUILayout.LabelField($"Showing {_pageOffset}–{_pageOffset + _lastPageCount}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Refresh button
            GUI.enabled = !IsLoading;
            if (GUILayout.Button("Refresh List", GUILayout.Height(28)))
            {
                _listError    = "";
                _actionStatus = "";
                RefreshObjectList();
            }
            GUI.enabled = true;

            // Status / error messages
            if (!string.IsNullOrEmpty(_listError))
            {
                EditorGUILayout.HelpBox(_listError, MessageType.Error);
                EditorGUILayout.Space(4);
            }

            if (!string.IsNullOrEmpty(_actionStatus))
            {
                EditorGUILayout.HelpBox(_actionStatus, MessageType.Info);
                EditorGUILayout.Space(4);
            }

            if (_objects.Count == 0)
            {
                EditorGUILayout.HelpBox("List is empty. Press the Refresh button to load.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(4);

            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.MaxHeight(320));

            foreach (var obj in _objects.ToArray())
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Row 1: ID + Description
                EditorGUILayout.BeginHorizontal();

                string displayId = obj.id.Length > 24
                    ? obj.id.Substring(0, 21) + "..."
                    : obj.id;
                EditorGUILayout.LabelField(displayId, EditorStyles.boldLabel, GUILayout.Width(160));

                string displayDesc = obj.description.Length > 36
                    ? obj.description.Substring(0, 33) + "..."
                    : obj.description;
                EditorGUILayout.LabelField(displayDesc, GUILayout.ExpandWidth(true));

                EditorGUILayout.EndHorizontal();

                // Row 2: Created At + Tags
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(obj.createdAt, EditorStyles.miniLabel, GUILayout.Width(140));

                string tagsDisplay = string.IsNullOrEmpty(obj.tags) ? "(no tags)" : obj.tags;
                EditorGUILayout.LabelField(tagsDisplay, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                EditorGUILayout.EndHorizontal();

                // Row 3: Action buttons
                EditorGUILayout.BeginHorizontal();

                GUILayout.FlexibleSpace();

                GUI.enabled = !IsLoading;

                // Delete button
                if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(52)))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Delete Object",
                        $"Delete object '{displayDesc}'?\nID: {obj.id}",
                        "Delete", "Cancel");

                    if (confirmed)
                    {
                        _editingTagsId = "";
                        DeleteObject(obj.id);
                    }
                }

                // Edit Tags toggle button
                string editTagsLabel = (_editingTagsId == obj.id) ? "Cancel" : "Edit Tags";
                if (GUILayout.Button(editTagsLabel, EditorStyles.miniButton, GUILayout.Width(68)))
                {
                    if (_editingTagsId == obj.id)
                    {
                        _editingTagsId    = "";
                        _editingTagsValue = "";
                    }
                    else
                    {
                        _editingTagsId    = obj.id;
                        _editingTagsValue = obj.tags;
                        // Close any open action panel for this row
                        if (_actionRowId == obj.id) { _actionRowId = ""; _actionRowMode = ""; }
                    }
                }

                // Animate / Vary / Review action toggle buttons
                string animLabel   = (_actionRowId == obj.id && _actionRowMode == "animate") ? "Cancel" : "Animate";
                string varyLabel   = (_actionRowId == obj.id && _actionRowMode == "vary")    ? "Cancel" : "Vary";
                string reviewLabel = (_actionRowId == obj.id && _actionRowMode == "review")  ? "Cancel" : "Review";

                if (GUILayout.Button(animLabel, EditorStyles.miniButton, GUILayout.Width(56)))
                {
                    if (_actionRowId == obj.id && _actionRowMode == "animate")
                    {
                        _actionRowId = ""; _actionRowMode = "";
                    }
                    else
                    {
                        _actionRowId   = obj.id;
                        _actionRowMode = "animate";
                        _editingTagsId = "";
                    }
                }

                if (GUILayout.Button(varyLabel, EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    if (_actionRowId == obj.id && _actionRowMode == "vary")
                    {
                        _actionRowId = ""; _actionRowMode = "";
                    }
                    else
                    {
                        _actionRowId   = obj.id;
                        _actionRowMode = "vary";
                        _editingTagsId = "";
                    }
                }

                if (GUILayout.Button(reviewLabel, EditorStyles.miniButton, GUILayout.Width(52)))
                {
                    if (_actionRowId == obj.id && _actionRowMode == "review")
                    {
                        _actionRowId = ""; _actionRowMode = "";
                    }
                    else
                    {
                        _actionRowId   = obj.id;
                        _actionRowMode = "review";
                        _editingTagsId = "";
                    }
                }

                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();

                // Inline tag editor (shown only for the selected object)
                if (_editingTagsId == obj.id)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Tags:", GUILayout.Width(36));
                    _editingTagsValue = EditorGUILayout.TextField(_editingTagsValue, GUILayout.ExpandWidth(true));

                    GUI.enabled = !IsLoading;
                    if (GUILayout.Button("Save", EditorStyles.miniButton, GUILayout.Width(44)))
                    {
                        string idSnapshot   = obj.id;
                        string tagsSnapshot = _editingTagsValue;
                        SaveObjectTags(idSnapshot, tagsSnapshot);
                    }
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Separate multiple tags with commas.", EditorStyles.miniLabel);
                }

                // Inline action form (animate / vary / review)
                if (_actionRowId == obj.id)
                {
                    EditorGUILayout.Space(2);

                    if (_actionRowMode == "animate")
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("Animate Object", EditorStyles.boldLabel);

                        EditorGUILayout.LabelField("Animation Description");
                        _animDescription = EditorGUILayout.TextField(_animDescription);

                        _animDirIdx = EditorGUILayout.Popup("Direction", _animDirIdx, DirectionPickerOptions);

                        _animFrameCount = EditorGUILayout.IntField("Frame Count (4-16, even)", _animFrameCount);
                        _animFrameCount = Mathf.Clamp(_animFrameCount, 4, 16);
                        if (_animFrameCount % 2 != 0) _animFrameCount++;

                        _animNoBg = EditorGUILayout.Toggle("No Background", _animNoBg);

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        GUI.enabled = !IsLoading;
                        if (GUILayout.Button("Run", EditorStyles.miniButton, GUILayout.Width(44)))
                        {
                            string idSnap   = obj.id;
                            string descSnap = _animDescription;
                            string dirSnap  = DirectionPickerOptions[_animDirIdx];
                            int    fcSnap   = _animFrameCount;
                            bool   noBgSnap = _animNoBg;
                            ResetActionPanel();
                            RunAnimateObject(idSnap, descSnap, dirSnap, fcSnap, noBgSnap);
                        }
                        if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            ResetActionPanel();
                        }
                        GUI.enabled = true;

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                    }
                    else if (_actionRowMode == "vary")
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("Vary Object", EditorStyles.boldLabel);

                        EditorGUILayout.LabelField("Edit Description");
                        _varyEditDesc = EditorGUILayout.TextField(_varyEditDesc);

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        GUI.enabled = !IsLoading;
                        if (GUILayout.Button("Run", EditorStyles.miniButton, GUILayout.Width(44)))
                        {
                            string idSnap   = obj.id;
                            string descSnap = _varyEditDesc;
                            ResetActionPanel();
                            RunVaryObject(idSnap, descSnap);
                        }
                        if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            ResetActionPanel();
                        }
                        GUI.enabled = true;

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                    }
                    else if (_actionRowMode == "review")
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.LabelField("Review Frames", EditorStyles.boldLabel);

                        EditorGUILayout.LabelField("Frame Indices (comma-separated)");
                        _reviewIndicesCsv = EditorGUILayout.TextField(_reviewIndicesCsv);

                        EditorGUILayout.LabelField("Common Tag (optional)");
                        _reviewCommonTag = EditorGUILayout.TextField(_reviewCommonTag);

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        GUI.enabled = !IsLoading;
                        if (GUILayout.Button("Select Frames", EditorStyles.miniButton, GUILayout.Width(90)))
                        {
                            string idSnap  = obj.id;
                            string csvSnap = _reviewIndicesCsv;
                            string tagSnap = _reviewCommonTag;
                            ResetActionPanel();
                            RunSelectFrames(idSnap, csvSnap, tagSnap);
                        }
                        if (GUILayout.Button("Dismiss Review", EditorStyles.miniButton, GUILayout.Width(96)))
                        {
                            string idSnap = obj.id;
                            ResetActionPanel();
                            RunDismissReview(idSnap);
                        }
                        if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(52)))
                        {
                            ResetActionPanel();
                        }
                        GUI.enabled = true;

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Async: Create Object
        // -----------------------------------------------------------------------

        private void StartCreateObject()
        {
            if (string.IsNullOrWhiteSpace(_description))
            {
                _createError = "Please enter a description.";
                return;
            }

            string desc       = _description;
            int    directions = _directionType == 0 ? 1 : 8;
            int    width      = Mathf.Max(1, _width);
            int    height     = Mathf.Max(1, _height);
            bool   noBg       = _noBackground;
            int    seed       = _seed;
            bool   mapObj     = _isMapObject;
            string outputDir  = Window.OutputDir;

            // Advanced field snapshots
            int    nFrames      = (_directionType == 0) ? NFramesValues[_nFramesIdx] : 1;
            int    viewIdx      = _objectViewIdx;
            var    stylePathsSnap = new List<string>(_styleImagePaths);

            LoadingMessage = "Generating object...";
            ClearResults();

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject();
                    if (directions != 1) extraParams["directions"] = directions;
                    if (noBg)            extraParams["no_background"] = true;
                    if (seed != 0)       extraParams["seed"] = seed;

                    // Advanced: n_frames (only for 1-direction, only when not default)
                    if (directions == 1 && nFrames != 1)
                        extraParams["n_frames"] = nFrames;

                    // Advanced: view (only when non-default)
                    if (viewIdx > 0)
                        extraParams["view"] = ObjectViewOptions[viewIdx];

                    // Advanced: style_images
                    var styleArray = new JArray();
                    foreach (string sp in stylePathsSnap)
                    {
                        if (!string.IsNullOrEmpty(sp))
                        {
                            JObject imgData = JObject.Parse(ImageUtils.ImageToBase64Json(sp));
                            ImageUtils.GetImageSize(sp, out int sw, out int sh);
                            styleArray.Add(new JObject
                            {
                                ["image"] = imgData,
                                ["size"]  = new JObject { ["width"] = sw, ["height"] = sh }
                            });
                        }
                    }
                    if (styleArray.Count > 0)
                        extraParams["style_images"] = styleArray;

                    JObject extraOrNull = extraParams.Count > 0 ? extraParams : null;

                    JObject response = mapObj
                        ? await Client.CreateMapObject(desc, width, height, extraOrNull)
                        : await Client.CreateObject(desc, width, height, extraOrNull);

                    // Handle background job polling
                    string jobId = response["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        LoadingMessage = "Waiting for background job...";
                        response = await Client.WaitForJob(jobId, 2f);
                    }

                    string responseJson = response.ToString(Newtonsoft.Json.Formatting.None);
                    string prefix       = mapObj ? "map-object" : "object";
                    _pendingSavedPaths  = ImageUtils.SaveImagesFromResponseJson(responseJson, outputDir, prefix);
                },
                onComplete: () =>
                {
                    SavedPaths.Clear();
                    SavedPaths.AddRange(_pendingSavedPaths);

                    ResultTextures.Clear();
                    foreach (string p in SavedPaths)
                        ResultTextures.Add(LoadTexture(p));
                },
                onError: ex =>
                {
                    _createError = $"Object generation failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Refresh object list
        // -----------------------------------------------------------------------

        private void RefreshObjectList()
        {
            LoadingMessage = "Loading list...";

            RunAsync(
                async () =>
                {
                    JObject result = await Client.ListObjects(_pageSize, _pageOffset);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects       = _pendingObjects;
                    _lastPageCount = _pendingObjects.Count;
                    _listError     = "";
                },
                onError: ex =>
                {
                    _listError = $"Failed to load list: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Delete object
        // -----------------------------------------------------------------------

        private void DeleteObject(string objectId)
        {
            LoadingMessage = "Deleting...";
            string idSnapshot = objectId;

            RunAsync(
                async () =>
                {
                    await Client.DeleteObject(idSnapshot);

                    // Refresh list on background thread
                    JObject result = await Client.ListObjects(_pageSize, _pageOffset);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects       = _pendingObjects;
                    _lastPageCount = _pendingObjects.Count;
                    _actionStatus  = "Object deleted successfully.";
                    _listError     = "";
                },
                onError: ex =>
                {
                    _listError = $"Delete failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Save object tags
        // -----------------------------------------------------------------------

        private void SaveObjectTags(string objectId, string tagsValue)
        {
            LoadingMessage = "Saving tags...";

            string[] tags = string.IsNullOrWhiteSpace(tagsValue)
                ? Array.Empty<string>()
                : tagsValue.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Trim whitespace from each tag
            for (int i = 0; i < tags.Length; i++)
                tags[i] = tags[i].Trim();

            string idSnapshot = objectId;

            RunAsync(
                async () =>
                {
                    await Client.UpdateObjectTags(idSnapshot, tags);

                    // Refresh list to reflect updated tags
                    JObject result = await Client.ListObjects(_pageSize, _pageOffset);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects       = _pendingObjects;
                    _lastPageCount = _pendingObjects.Count;
                    _editingTagsId = "";
                    _actionStatus  = "Tags updated successfully.";
                    _listError     = "";
                },
                onError: ex =>
                {
                    _listError = $"Tag update failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Animate object
        // -----------------------------------------------------------------------

        private void RunAnimateObject(string objectId, string animDesc, string direction, int frameCount, bool noBg)
        {
            LoadingMessage = "Animating object...";
            string outputDir = Window.OutputDir;

            RunAsync(
                async () =>
                {
                    var extras = new JObject();
                    extras["frame_count"]   = frameCount;
                    if (noBg) extras["no_background"] = true;

                    JObject response = await Client.AnimateObject(objectId, direction, animDesc, extras);

                    string jobId = response["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        LoadingMessage = "Waiting for background job...";
                        response = await Client.WaitForJob(jobId, 2f);
                    }

                    string responseJson = response.ToString(Newtonsoft.Json.Formatting.None);
                    _pendingSavedPaths  = ImageUtils.SaveImagesFromResponseJson(responseJson, outputDir, "obj-anim");
                },
                onComplete: () =>
                {
                    SavedPaths.AddRange(_pendingSavedPaths);
                    foreach (string p in _pendingSavedPaths)
                        ResultTextures.Add(LoadTexture(p));
                    _pendingSavedPaths.Clear();
                    _actionStatus = "Animation complete.";
                },
                onError: ex =>
                {
                    _actionStatus = $"Animate failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Vary object
        // -----------------------------------------------------------------------

        private void RunVaryObject(string objectId, string editDesc)
        {
            LoadingMessage = "Varying object...";
            string outputDir = Window.OutputDir;

            RunAsync(
                async () =>
                {
                    JObject response = await Client.VaryObject(objectId, editDesc);

                    string jobId = response["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        LoadingMessage = "Waiting for background job...";
                        response = await Client.WaitForJob(jobId, 2f);
                    }

                    string responseJson = response.ToString(Newtonsoft.Json.Formatting.None);
                    _pendingSavedPaths  = ImageUtils.SaveImagesFromResponseJson(responseJson, outputDir, "obj-vary");
                },
                onComplete: () =>
                {
                    SavedPaths.AddRange(_pendingSavedPaths);
                    foreach (string p in _pendingSavedPaths)
                        ResultTextures.Add(LoadTexture(p));
                    _pendingSavedPaths.Clear();
                    _actionStatus = "Vary complete.";
                },
                onError: ex =>
                {
                    _actionStatus = $"Vary failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Select object frames (review state)
        // -----------------------------------------------------------------------

        private void RunSelectFrames(string objectId, string indicesCsv, string commonTag)
        {
            LoadingMessage = "Selecting frames...";

            var indexList = new List<int>();
            if (!string.IsNullOrWhiteSpace(indicesCsv))
            {
                foreach (string s in indicesCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(s.Trim(), out int idx))
                        indexList.Add(idx);
                }
            }
            int[]  indices   = indexList.ToArray();
            string tagSnap   = string.IsNullOrWhiteSpace(commonTag) ? null : commonTag.Trim();
            string idSnapshot = objectId;

            RunAsync(
                async () =>
                {
                    await Client.SelectObjectFrames(idSnapshot, indices, tagSnap);

                    JObject result = await Client.ListObjects(_pageSize, _pageOffset);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects       = _pendingObjects;
                    _lastPageCount = _pendingObjects.Count;
                    _actionStatus  = "Frames selected successfully.";
                    _listError     = "";
                },
                onError: ex =>
                {
                    _actionStatus = $"Select frames failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Dismiss object review
        // -----------------------------------------------------------------------

        private void RunDismissReview(string objectId)
        {
            LoadingMessage = "Dismissing review...";
            string idSnapshot = objectId;

            RunAsync(
                async () =>
                {
                    await Client.DismissObjectReview(idSnapshot);

                    JObject result = await Client.ListObjects(_pageSize, _pageOffset);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects      = _pendingObjects;
                    _actionStatus = "Review dismissed.";
                    _listError    = "";
                },
                onError: ex =>
                {
                    _actionStatus = $"Dismiss review failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Helper: reset inline action panel and clear form fields
        // -----------------------------------------------------------------------

        private void ResetActionPanel()
        {
            _actionRowId      = "";
            _actionRowMode    = "";
            _animDescription  = "";
            _animDirIdx       = 0;
            _animFrameCount   = 8;
            _animNoBg         = false;
            _varyEditDesc     = "";
            _reviewIndicesCsv = "";
            _reviewCommonTag  = "";
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static List<(string id, string description, string createdAt, string tags)>
            ParseObjectList(JObject result)
        {
            var list = new List<(string, string, string, string)>();

            JArray data = result["data"] as JArray;
            if (data != null)
            {
                foreach (JToken item in data)
                {
                    string id          = item["id"]?.ToString()          ?? "";
                    string description = item["description"]?.ToString() ?? "";
                    string createdAt   = item["created_at"]?.ToString()  ?? "";

                    string tags = "";
                    JArray tagsArr = item["tags"] as JArray;
                    if (tagsArr != null)
                        tags = string.Join(", ", tagsArr.ToObject<string[]>() ?? Array.Empty<string>());

                    list.Add((id, description, createdAt, tags));
                }
            }

            return list;
        }

    }
}
#endif
