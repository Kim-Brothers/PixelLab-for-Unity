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
    public class CharacterPanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Create section
        // -----------------------------------------------------------------------

        private string _description = "";
        private int    _directionType = 0;  // 0 = 4-Direction, 1 = 8-Direction
        private int    _sizeIndex     = 4;  // default "128x128" (index 4)
        private int    _viewIndex      = 0;  // 0=side (default), 1=low top-down, 2=high top-down, 3=perspective
        private int    _templateIndex  = 0;  // 0=mannequin (default), 1=bear, 2=cat, 3=dog, 4=horse, 5=lion
        private int    _detailIndex    = 1;  // 0=low, 1=medium (default), 2=high
        private int    _outlineIndex   = 0;  // 0=none (default), 1=thin, 2=medium, 3=thick
        private int    _shadingIndex   = 0;  // 0=none (default), 1=soft, 2=hard, 3=flat
        private string _seed           = "";
        private bool   _isometric      = false;

        private static readonly string[] DirectionLabels = { "4-Direction", "8-Direction" };
        private static readonly string[] ViewOptions     = { "side", "low top-down", "high top-down", "perspective" };
        private static readonly string[] TemplateOptions = { "mannequin", "bear", "cat", "dog", "horse", "lion" };
        private static readonly string[] DetailOptions   = { "low", "medium", "high" };
        private static readonly string[] OutlineOptions  = { "none", "thin", "medium", "thick" };
        private static readonly string[] ShadingOptions  = { "none", "soft", "hard", "flat" };

        // -----------------------------------------------------------------------
        // Character list
        // -----------------------------------------------------------------------

        private List<(string id, string description, string createdAt)> _characters
            = new List<(string, string, string)>();

        private Vector2 _listScrollPos;

        // -----------------------------------------------------------------------
        // Error / status messages
        // -----------------------------------------------------------------------

        private string _createError  = "";
        private string _listError    = "";
        private string _actionStatus = "";

        // -----------------------------------------------------------------------
        // Scratch fields for async→main-thread transfer
        // -----------------------------------------------------------------------

        private List<string>                                              _pendingSavedPaths  = new List<string>();
        private List<(string id, string description, string createdAt)>  _pendingCharacters  = new List<(string, string, string)>();

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public CharacterPanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Character Generation / Management", EditorStyles.boldLabel);
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
        // Create section
        // -----------------------------------------------------------------------

        private void DrawCreateSection()
        {
            EditorGUILayout.LabelField("Create Character", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Description
            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(60));
            EditorGUILayout.Space(4);

            // Direction selector
            EditorGUILayout.LabelField("Direction Count");
            _directionType = GUILayout.Toolbar(_directionType, DirectionLabels);
            EditorGUILayout.Space(4);

            // Size selector
            _sizeIndex = EditorGUILayout.Popup("Size", _sizeIndex, PixelLabConstants.SIZE_PRESETS);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            _viewIndex     = EditorGUILayout.Popup("View",     _viewIndex,     ViewOptions);
            _templateIndex = EditorGUILayout.Popup("Template", _templateIndex, TemplateOptions);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _detailIndex  = EditorGUILayout.Popup("Detail",  _detailIndex,  DetailOptions);
            _outlineIndex = EditorGUILayout.Popup("Outline", _outlineIndex, OutlineOptions);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _shadingIndex = EditorGUILayout.Popup("Shading", _shadingIndex, ShadingOptions);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _isometric = EditorGUILayout.Toggle("Isometric", _isometric);
            EditorGUILayout.LabelField("Seed (Optional)", GUILayout.Width(95));
            _seed = EditorGUILayout.TextField(_seed, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // Error message
            if (!string.IsNullOrEmpty(_createError))
            {
                EditorGUILayout.HelpBox(_createError, MessageType.Error);
                EditorGUILayout.Space(4);
            }

            // Create button
            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Character", GUILayout.Height(32)))
            {
                _createError = "";
                StartCreateCharacter();
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
        // Character list section
        // -----------------------------------------------------------------------

        private void DrawListSection()
        {
            EditorGUILayout.LabelField("Character List", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Refresh button
            GUI.enabled = !IsLoading;
            if (GUILayout.Button("Refresh Character List", GUILayout.Height(28)))
            {
                _listError    = "";
                _actionStatus = "";
                RefreshCharacterList();
            }
            GUI.enabled = true;

            // List errors / status
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

            if (_characters.Count == 0)
            {
                EditorGUILayout.HelpBox("List is empty. Press the Refresh button to load.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(4);

            // Scroll list of characters
            _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos, GUILayout.MaxHeight(260));

            foreach (var ch in _characters.ToArray()) // ToArray to allow removal during iteration
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                // ID (truncated)
                string displayId = ch.id.Length > 24 ? ch.id.Substring(0, 21) + "..." : ch.id;
                EditorGUILayout.LabelField(displayId, EditorStyles.boldLabel, GUILayout.Width(160));

                // Description (truncated)
                string displayDesc = ch.description.Length > 36
                    ? ch.description.Substring(0, 33) + "..."
                    : ch.description;
                EditorGUILayout.LabelField(displayDesc, GUILayout.ExpandWidth(true));

                EditorGUILayout.EndHorizontal();

                // Created at + action buttons
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(ch.createdAt, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

                // Delete button
                GUI.enabled = !IsLoading;
                if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(48)))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Delete Character",
                        $"Delete character '{displayDesc}'?\nID: {ch.id}",
                        "Delete", "Cancel");

                    if (confirmed)
                        DeleteCharacter(ch.id);
                }

                // Export ZIP button
                if (GUILayout.Button("Export ZIP", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    ExportCharacterZip(ch.id);
                }
                GUI.enabled = true;

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Async: Create character
        // -----------------------------------------------------------------------

        private void StartCreateCharacter()
        {
            if (string.IsNullOrWhiteSpace(_description))
            {
                _createError = "Please enter a description.";
                return;
            }

            string desc      = _description;
            int    dirType   = _directionType;
            string sizePreset = PixelLabConstants.SIZE_PRESETS[_sizeIndex];
            string outputDir = Window.OutputDir;

            ParseSizePreset(sizePreset, out int width, out int height);

            int    viewIdx     = _viewIndex;
            int    templateIdx = _templateIndex;
            int    detailIdx   = _detailIndex;
            int    outlineIdx  = _outlineIndex;
            int    shadingIdx  = _shadingIndex;
            string seedStr     = _seed;
            bool   isometric   = _isometric;

            LoadingMessage = "Generating character...";
            ClearResults();

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject();
                    if (viewIdx != 0) extraParams["view"] = ViewOptions[viewIdx];
                    if (templateIdx != 0) extraParams["template_id"] = TemplateOptions[templateIdx];
                    if (detailIdx != 1) extraParams["detail"] = DetailOptions[detailIdx];
                    if (outlineIdx != 0) extraParams["outline"] = OutlineOptions[outlineIdx];
                    if (shadingIdx != 0) extraParams["shading"] = ShadingOptions[shadingIdx];
                    if (isometric) extraParams["isometric"] = true;
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int seedVal)) extraParams["seed"] = seedVal;
                    JObject extraOrNull = extraParams.Count > 0 ? extraParams : null;

                    JObject response = dirType == 0
                        ? await Client.CreateCharacter4Dir(desc, width, height, extraOrNull)
                        : await Client.CreateCharacter8Dir(desc, width, height, extraOrNull);

                    // Handle background job
                    string jobId = response["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                    {
                        LoadingMessage = "Waiting for background job...";
                        response = await Client.WaitForJob(jobId, 2f);
                    }

                    string responseJson = response.ToString(Newtonsoft.Json.Formatting.None);
                    _pendingSavedPaths = ImageUtils.SaveImagesFromResponseJson(responseJson, outputDir, "character");
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
                    _createError = $"Character generation failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Refresh character list
        // -----------------------------------------------------------------------

        private void RefreshCharacterList()
        {
            LoadingMessage = "Loading list...";

            RunAsync(
                async () =>
                {
                    JObject result = await Client.ListCharacters(20, 0);

                    var list = new List<(string id, string description, string createdAt)>();
                    JArray data = result["data"] as JArray;
                    if (data != null)
                    {
                        foreach (JToken item in data)
                        {
                            string id          = item["id"]?.ToString()          ?? "";
                            string description = item["description"]?.ToString() ?? "";
                            string createdAt   = item["created_at"]?.ToString()  ?? "";
                            list.Add((id, description, createdAt));
                        }
                    }

                    _pendingCharacters = list;
                },
                onComplete: () =>
                {
                    _characters = _pendingCharacters;
                    _listError  = "";
                },
                onError: ex =>
                {
                    _listError = $"Failed to load list: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Delete character
        // -----------------------------------------------------------------------

        private void DeleteCharacter(string characterId)
        {
            LoadingMessage = "Deleting...";
            string idSnapshot = characterId;

            RunAsync(
                async () =>
                {
                    await Client.DeleteCharacter(idSnapshot);
                    // Refresh the list on the background thread
                    JObject result = await Client.ListCharacters(20, 0);

                    var list = new List<(string id, string description, string createdAt)>();
                    JArray data = result["data"] as JArray;
                    if (data != null)
                    {
                        foreach (JToken item in data)
                        {
                            string id          = item["id"]?.ToString()          ?? "";
                            string description = item["description"]?.ToString() ?? "";
                            string createdAt   = item["created_at"]?.ToString()  ?? "";
                            list.Add((id, description, createdAt));
                        }
                    }

                    _pendingCharacters = list;
                },
                onComplete: () =>
                {
                    _characters   = _pendingCharacters;
                    _actionStatus = "Character deleted successfully.";
                    _listError    = "";
                },
                onError: ex =>
                {
                    _listError = $"Delete failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Async: Export ZIP
        // -----------------------------------------------------------------------

        private void ExportCharacterZip(string characterId)
        {
            LoadingMessage       = "Exporting ZIP...";
            string idSnapshot    = characterId;
            string outputDir     = Window.OutputDir;

            RunAsync(
                async () =>
                {
                    byte[] zipBytes = await Client.ExportCharacterZip(idSnapshot);

                    // Resolve absolute output path
                    string absOutputDir = outputDir;
                    if (!Path.IsPathRooted(absOutputDir))
                    {
                        absOutputDir = Path.GetFullPath(
                            Path.Combine(Application.dataPath, "..", absOutputDir));
                    }

                    Directory.CreateDirectory(absOutputDir);
                    string zipPath = Path.Combine(absOutputDir, $"{idSnapshot}.zip");
                    File.WriteAllBytes(zipPath, zipBytes);
                },
                onComplete: () =>
                {
                    _actionStatus = $"ZIP saved: {outputDir}/{idSnapshot}.zip";
                },
                onError: ex =>
                {
                    _listError = $"ZIP export failed: {ex.Message}";
                }
            );
        }

        // -----------------------------------------------------------------------
        // Utilities
        // -----------------------------------------------------------------------

        private static void ParseSizePreset(string preset, out int width, out int height)
        {
            width  = 128;
            height = 128;

            if (string.IsNullOrEmpty(preset)) return;

            int sep = preset.IndexOf('x');
            if (sep < 0) return;

            if (int.TryParse(preset.Substring(0, sep), out int w) &&
                int.TryParse(preset.Substring(sep + 1), out int h))
            {
                width  = w;
                height = h;
            }
        }

        private static void DrawDivider()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f));
        }
    }
}
#endif
