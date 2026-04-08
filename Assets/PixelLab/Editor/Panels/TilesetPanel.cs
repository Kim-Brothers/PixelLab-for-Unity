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
            "Pro"
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
            if (!RequireClient()) return;

            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Generate Tileset", EditorStyles.boldLabel);
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
                case 0: DrawTopDown();        break;
                case 1: DrawSideScroller();   break;
                case 2: DrawIsometric();      break;
                case 3: DrawPro();            break;
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
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Tileset", GUILayout.Height(30)))
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
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Tileset", GUILayout.Height(30)))
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
                    var extraParams = new JObject { ["tile_size"] = tileSize };
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
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Tile", GUILayout.Height(30)))
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

            EditorGUILayout.Space(8);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Generate Tile", GUILayout.Height(30)))
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
    }
}
#endif
