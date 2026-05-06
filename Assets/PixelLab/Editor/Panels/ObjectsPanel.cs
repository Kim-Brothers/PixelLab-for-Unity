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

        // -----------------------------------------------------------------------
        // Object list fields
        // -----------------------------------------------------------------------

        private List<(string id, string description, string createdAt, string tags)> _objects
            = new List<(string, string, string, string)>();

        private List<(string id, string description, string createdAt, string tags)> _pendingObjects
            = new List<(string, string, string, string)>();

        private Vector2 _listScrollPos;

        // Inline tag editing state
        private string _editingTagsId    = "";
        private string _editingTagsValue = "";

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

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Object Generation / Management", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

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
            EditorGUILayout.LabelField("Create Object", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

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
            EditorGUILayout.LabelField("Object List", EditorStyles.boldLabel);
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

            string desc      = _description;
            int    directions = _directionType == 0 ? 1 : 8;
            int    width     = Mathf.Max(1, _width);
            int    height    = Mathf.Max(1, _height);
            bool   noBg      = _noBackground;
            int    seed      = _seed;
            bool   mapObj    = _isMapObject;
            string outputDir = Window.OutputDir;

            LoadingMessage = "Generating object...";
            ClearResults();

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject();
                    if (directions != 1) extraParams["directions"] = directions;
                    if (noBg)           extraParams["no_background"] = true;
                    if (seed != 0)      extraParams["seed"] = seed;
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
                    JObject result = await Client.ListObjects(20, 0);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects   = _pendingObjects;
                    _listError = "";
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
                    JObject result = await Client.ListObjects(20, 0);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects      = _pendingObjects;
                    _actionStatus = "Object deleted successfully.";
                    _listError    = "";
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
                    JObject result = await Client.ListObjects(20, 0);
                    _pendingObjects = ParseObjectList(result);
                },
                onComplete: () =>
                {
                    _objects       = _pendingObjects;
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

        private static void DrawDivider()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f));
        }
    }
}
#endif
