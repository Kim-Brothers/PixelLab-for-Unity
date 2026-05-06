#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class TilesetPanel : BasePanel
    {
        // -----------------------------------------------------------------------
        // Tab 0 – Top-Down (CreateTileset)
        // -----------------------------------------------------------------------

        private string _topDownLower = "";
        private string _topDownUpper = "";
        private int    _topDownTileSize  = 16;
        private int    _topDownViewIndex = 0;
        private string _topDownSeed      = "";

        // -----------------------------------------------------------------------
        // Tab 1 – Side-Scroller (CreateTilesetSidescroller)
        // -----------------------------------------------------------------------

        private string _sideScrollerLower = "";
        private int    _sideScrollerTileSize = 16;
        private string _sideScrollerSeed     = "";

        // -----------------------------------------------------------------------
        // Tab 2 – Isometric (CreateIsometricTile)
        // -----------------------------------------------------------------------

        private string _isoDescription = "";
        private int    _isoWidth        = 128;
        private int    _isoHeight       = 128;
        private int    _isoShapeIndex = 2;
        private string _isoSeed        = "";

        // -----------------------------------------------------------------------
        // Tab 3 – Pro (CreateTilesPro)
        // -----------------------------------------------------------------------

        private string _proDescription = "";
        private int    _proTileTypeIndex = 0;
        private string _proNTiles    = "";
        private string _proTileSize  = "";
        private int    _proViewIndex = 0;
        private string _proSeed      = "";

        // Optional style images for Tiles Pro (max 4)
        private List<string>    _proStyleImagePaths    = new List<string>();
        private List<Texture2D> _proStyleImagePreviews = new List<Texture2D>();

        // -----------------------------------------------------------------------
        // Tab 4 – Browse (ListTilesets / ListIsometricTiles)
        // -----------------------------------------------------------------------

        private List<(string id, string desc, string created)> _tilesets    = new List<(string, string, string)>();
        private List<(string id, string desc, string created)> _isoTiles    = new List<(string, string, string)>();
        private Vector2 _browseScroll;
        private int     _browseSubTab    = 0;
        private int     _browsePageSize  = 20;
        private int     _browsePageOffset = 0;
        private int     _browseLastCount = 0;

        private static readonly string[] TileTypeOptions    = { "square_topdown", "hex", "hex_pointy", "isometric", "octagon" };
        private static readonly string[] TileSizeOptions    = { "16", "32" };
        private static readonly string[] TopDownViewOptions = { "low top-down", "high top-down" };
        private static readonly string[] ProTileViewOptions = { "low top-down", "high top-down", "side", "top-down" };
        private static readonly string[] IsoShapeOptions    = { "thin tile", "thick tile", "block" };

        // -----------------------------------------------------------------------
        // Shared UI state
        // -----------------------------------------------------------------------

        private int    _selectedTab  = 0;

        private static readonly string[] TabNames =
        {
            "Top-Down",
            "Side-Scroller",
            "Isometric",
            "Pro",
            "Browse"
        };

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        public TilesetPanel(PixelLabWindow window) : base(window) { }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawPanelHeader("Generate Tileset", "Tilesets and scenes — top-down, side-scroller, isometric, and pro.");

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
                case 0: DrawTopDown();        break;
                case 1: DrawSideScroller();   break;
                case 2: DrawIsometric();      break;
                case 3: DrawPro();            break;
                case 4: DrawBrowse();         break;
            }

            if (_selectedTab != 4)
            {
                if (!string.IsNullOrEmpty(_errorMessage))
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
                }

                DrawImagePreviews(160);
            }

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Tab 0 – Top-Down
        // -----------------------------------------------------------------------

        private void DrawTopDown()
        {
            EditorGUILayout.LabelField("Lower Description");
            _topDownLower = EditorGUILayout.TextField(_topDownLower);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Upper Description");
            _topDownUpper = EditorGUILayout.TextField(_topDownUpper);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tile Size", GUILayout.Width(60));
            _topDownTileSize = EditorGUILayout.IntPopup(_topDownTileSize, TileSizeOptions, new int[] { 16, 32 }, GUILayout.Width(60));
            EditorGUILayout.LabelField("View", GUILayout.Width(40));
            _topDownViewIndex = EditorGUILayout.Popup(_topDownViewIndex, TopDownViewOptions, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed (optional)", GUILayout.Width(80));
            _topDownSeed = EditorGUILayout.TextField(_topDownSeed, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Generate Tileset"))
                RunTopDown();
            GUI.enabled = true;
        }

        private void RunTopDown()
        {
            _errorMessage = "";
            ClearResults();

            string lower     = _topDownLower;
            string upper     = _topDownUpper;
            int    tileSize  = _topDownTileSize;
            int    viewIdx   = _topDownViewIndex;
            string seedStr   = _topDownSeed;
            string outputDir = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject
                    {
                        ["tile_size"] = new JObject { ["width"] = tileSize, ["height"] = tileSize },
                        ["view"]      = TopDownViewOptions[viewIdx]
                    };
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int tdSeed))
                        extraParams["seed"] = tdSeed;

                    JObject result = await Client.CreateTileset(lower, upper, extraParams);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "tileset");
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
        // Tab 1 – Side-Scroller
        // -----------------------------------------------------------------------

        private void DrawSideScroller()
        {
            EditorGUILayout.LabelField("Lower Description");
            _sideScrollerLower = EditorGUILayout.TextField(_sideScrollerLower);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tile Size", GUILayout.Width(60));
            _sideScrollerTileSize = EditorGUILayout.IntPopup(_sideScrollerTileSize, TileSizeOptions, new int[] { 16, 32 }, GUILayout.Width(60));
            EditorGUILayout.LabelField("Seed (optional)", GUILayout.Width(80));
            _sideScrollerSeed = EditorGUILayout.TextField(_sideScrollerSeed, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Generate Tileset"))
                RunSideScroller();
            GUI.enabled = true;
        }

        private void RunSideScroller()
        {
            _errorMessage = "";
            ClearResults();

            string lower     = _sideScrollerLower;
            int    tileSize  = _sideScrollerTileSize;
            string seedStr   = _sideScrollerSeed;
            string outputDir = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject { ["tile_size"] = new JObject { ["width"] = tileSize, ["height"] = tileSize } };
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int ssSeed))
                        extraParams["seed"] = ssSeed;

                    JObject result = await Client.CreateTilesetSidescroller(lower, extraParams);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "tileset_side");
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
        // Tab 2 – Isometric
        // -----------------------------------------------------------------------

        private void DrawIsometric()
        {
            EditorGUILayout.LabelField("Description");
            _isoDescription = EditorGUILayout.TextField(_isoDescription);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tile Shape");
            _isoShapeIndex = EditorGUILayout.Popup(_isoShapeIndex, IsoShapeOptions);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Seed (optional)", GUILayout.Width(80));
            _isoSeed = EditorGUILayout.TextField(_isoSeed, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Size", GUILayout.Width(40));
            EditorGUILayout.LabelField("W", GUILayout.Width(14));
            _isoWidth  = EditorGUILayout.IntField(_isoWidth,  GUILayout.Width(60));
            EditorGUILayout.LabelField("H", GUILayout.Width(14));
            _isoHeight = EditorGUILayout.IntField(_isoHeight, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Generate Tile"))
                RunIsometric();
            GUI.enabled = true;
        }

        private void RunIsometric()
        {
            _errorMessage = "";
            ClearResults();

            string desc      = _isoDescription;
            int    w         = _isoWidth;
            int    h         = _isoHeight;
            int    shapeIdx  = _isoShapeIndex;
            string seedStr   = _isoSeed;
            string outputDir = Window.OutputDir;

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject { ["isometric_tile_shape"] = IsoShapeOptions[shapeIdx] };
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int isoSeed))
                        extraParams["seed"] = isoSeed;

                    JObject result = await Client.CreateIsometricTile(desc, w, h, extraParams);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "iso_tile");
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
        // Tab 3 – Pro
        // -----------------------------------------------------------------------

        private void DrawPro()
        {
            EditorGUILayout.LabelField("Description");
            _proDescription = EditorGUILayout.TextField(_proDescription);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tile Type");
            _proTileTypeIndex = EditorGUILayout.Popup(_proTileTypeIndex, TileTypeOptions);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tile Count (optional)", GUILayout.Width(100));
            _proNTiles = EditorGUILayout.TextField(_proNTiles, GUILayout.Width(60));
            EditorGUILayout.LabelField("Tile Size (optional)", GUILayout.Width(110));
            _proTileSize = EditorGUILayout.TextField(_proTileSize, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Tile View", GUILayout.Width(60));
            _proViewIndex = EditorGUILayout.Popup(_proViewIndex, ProTileViewOptions, GUILayout.Width(120));
            EditorGUILayout.LabelField("Seed (optional)", GUILayout.Width(80));
            _proSeed = EditorGUILayout.TextField(_proSeed, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // Optional style images (max 4) — collapsed by default
            EditorGUILayout.Space(4);
            if (DrawOptionalFoldout("Style Images (optional, max 4)"))
            {
                int removeProStyleIdx = -1;
                for (int i = 0; i < _proStyleImagePaths.Count; i++)
                {
                    int idx = i;
                    string    path = _proStyleImagePaths[i];
                    Texture2D prev = _proStyleImagePreviews[i];
                    DrawImagePicker($"Style {i + 1}", ref path, ref prev, () =>
                    {
                        if (GUILayout.Button("Remove", GUILayout.Width(60)))
                            removeProStyleIdx = idx;
                    }, 60);
                    _proStyleImagePaths[i]    = path;
                    _proStyleImagePreviews[i] = prev;
                }
                if (removeProStyleIdx >= 0)
                {
                    _proStyleImagePaths.RemoveAt(removeProStyleIdx);
                    _proStyleImagePreviews.RemoveAt(removeProStyleIdx);
                }

                if (_proStyleImagePaths.Count < 4)
                {
                    if (GUILayout.Button("+ Add Style Image", GUILayout.Height(24)))
                    {
                        _proStyleImagePaths.Add("");
                        _proStyleImagePreviews.Add(null);
                    }
                }
            }

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (DrawPrimaryButton(IsLoading ? LoadingMessage : "Generate Tile"))
                RunPro();
            GUI.enabled = true;
        }

        private void RunPro()
        {
            _errorMessage = "";
            ClearResults();

            string desc        = _proDescription;
            string tileType    = TileTypeOptions[_proTileTypeIndex];
            string nTilesStr   = _proNTiles;
            string tileSizeStr = _proTileSize;
            int    viewIdx     = _proViewIndex;
            string seedStr     = _proSeed;
            string outputDir   = Window.OutputDir;

            // Snapshot style image paths for background thread
            var proStylePathsSnapshot = new List<string>(_proStyleImagePaths);

            List<string> paths = null;

            RunAsync(
                async () =>
                {
                    var extraParams = new JObject { ["tile_view"] = ProTileViewOptions[viewIdx] };
                    if (int.TryParse(nTilesStr, out int nTiles) && nTiles > 0)
                        extraParams["n_tiles"] = nTiles;
                    if (int.TryParse(tileSizeStr, out int tileSize) && tileSize > 0)
                        extraParams["tile_size"] = tileSize;
                    if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out int proSeed))
                        extraParams["seed"] = proSeed;

                    // Build style_images array when any paths are set
                    var styleArray = new JArray();
                    foreach (string sp in proStylePathsSnapshot)
                    {
                        if (!string.IsNullOrEmpty(sp))
                        {
                            JObject imgData = JObject.Parse(ImageUtils.ImageToBase64Json(sp));
                            ImageUtils.GetImageSize(sp, out int sw, out int sh);
                            styleArray.Add(new JObject
                            {
                                ["image"]  = imgData,
                                ["size"]   = new JObject { ["width"] = sw, ["height"] = sh }
                            });
                        }
                    }
                    if (styleArray.Count > 0)
                        extraParams["style_images"] = styleArray;

                    JObject result = await Client.CreateTilesPro(desc, tileType, extraParams);

                    string jobId = result["background_job_id"]?.ToString();
                    if (!string.IsNullOrEmpty(jobId))
                        result = await Client.WaitForJob(jobId);

                    paths = ImageUtils.SaveImagesFromResponseJson(
                        result.ToString(), outputDir, "tiles_pro");
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
        // Tab 4 – Browse
        // -----------------------------------------------------------------------

        private static readonly string[] BrowseSubTabNames = { "Tilesets", "Isometric Tiles" };

        private void DrawBrowse()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Browse Existing Tiles", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Sub-toolbar: Tilesets / Isometric Tiles
            int newSub = GUILayout.Toolbar(_browseSubTab, BrowseSubTabNames);
            if (newSub != _browseSubTab)
            {
                _browseSubTab     = newSub;
                _browsePageOffset = 0;
                _browseLastCount  = 0;
                _errorMessage     = "";
            }

            EditorGUILayout.Space(6);

            // Pagination + Refresh row
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !IsLoading && _browsePageOffset > 0;
            if (GUILayout.Button("Prev", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                _browsePageOffset = Mathf.Max(0, _browsePageOffset - _browsePageSize);
                RefreshBrowseList();
            }

            GUI.enabled = !IsLoading && _browseLastCount >= _browsePageSize;
            if (GUILayout.Button("Next", EditorStyles.miniButton, GUILayout.Width(40)))
            {
                _browsePageOffset += _browsePageSize;
                RefreshBrowseList();
            }

            GUI.enabled = true;
            EditorGUILayout.LabelField(
                $"Showing {_browsePageOffset}–{_browsePageOffset + _browseLastCount}",
                EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            GUI.enabled = !IsLoading;
            if (GUILayout.Button("Refresh", GUILayout.Width(64)))
            {
                _errorMessage = "";
                RefreshBrowseList();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Error / loading feedback
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);
                EditorGUILayout.Space(4);
            }

            // List
            List<(string id, string desc, string created)> list =
                _browseSubTab == 0 ? _tilesets : _isoTiles;

            if (list.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "List is empty. Press Refresh to load.",
                    MessageType.None);
            }
            else
            {
                string outputDir = Window.OutputDir;

                _browseScroll = EditorGUILayout.BeginScrollView(_browseScroll,
                    GUILayout.MaxHeight(400));

                foreach (var entry in list.ToArray())
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // ID + description row
                    EditorGUILayout.BeginHorizontal();
                    string displayId = entry.id.Length > 24
                        ? entry.id.Substring(0, 21) + "..."
                        : entry.id;
                    EditorGUILayout.LabelField(displayId, EditorStyles.boldLabel, GUILayout.Width(160));
                    string displayDesc = entry.desc.Length > 40
                        ? entry.desc.Substring(0, 37) + "..."
                        : entry.desc;
                    EditorGUILayout.LabelField(displayDesc, GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();

                    // Created at + Get Details button
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.created, EditorStyles.miniLabel, GUILayout.Width(160));
                    GUILayout.FlexibleSpace();

                    GUI.enabled = !IsLoading;
                    if (GUILayout.Button("Get Details", EditorStyles.miniButton, GUILayout.Width(80)))
                    {
                        string idSnap      = entry.id;
                        int    subTabSnap  = _browseSubTab;
                        List<string> paths = null;

                        RunAsync(
                            async () =>
                            {
                                JObject result = subTabSnap == 0
                                    ? await Client.GetTileset(idSnap)
                                    : await Client.GetIsometricTile(idSnap);

                                paths = ImageUtils.SaveImagesFromResponseJson(
                                    result.ToString(), outputDir, "tileset-detail");
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
                            ex => { _errorMessage = $"Get Details failed: {ex.Message}"; }
                        );
                    }
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.EndScrollView();

                // Show detail image previews below the list
                DrawImagePreviews(160);
            }
        }

        private void RefreshBrowseList()
        {
            int  subTab  = _browseSubTab;
            int  limit   = _browsePageSize;
            int  offset  = _browsePageOffset;

            LoadingMessage = "Loading list...";

            List<(string, string, string)> pending = null;

            RunAsync(
                async () =>
                {
                    JObject result = subTab == 0
                        ? await Client.ListTilesets(limit, offset)
                        : await Client.ListIsometricTiles(limit, offset);

                    var list = new List<(string, string, string)>();
                    JArray data = result["data"] as JArray;
                    if (data != null)
                    {
                        foreach (JToken item in data)
                        {
                            string id        = item["id"]?.ToString()          ?? "";
                            string desc      = item["description"]?.ToString() ?? "";
                            string createdAt = item["created_at"]?.ToString()  ?? "";
                            list.Add((id, desc, createdAt));
                        }
                    }
                    pending = list;
                },
                () =>
                {
                    if (subTab == 0)
                        _tilesets = pending ?? new List<(string, string, string)>();
                    else
                        _isoTiles = pending ?? new List<(string, string, string)>();

                    _browseLastCount = pending?.Count ?? 0;
                    _errorMessage    = "";
                },
                ex => { _errorMessage = $"Failed to load list: {ex.Message}"; }
            );
        }
    }
}
#endif
