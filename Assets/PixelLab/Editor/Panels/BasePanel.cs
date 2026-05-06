#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PixelLab.Editor
{
    /// <summary>
    /// Abstract base class for all PixelLab editor panels.
    /// Provides shared helpers for async operations, texture management, drag-drop,
    /// and image preview rendering.
    /// </summary>
    public abstract class BasePanel
    {
        // -----------------------------------------------------------------------
        // Shared design palette (matches DashboardPanel)
        // -----------------------------------------------------------------------

        protected static readonly Color BrandViolet = new Color(0.55f, 0.35f, 0.95f);
        protected static readonly Color BrandCyan   = new Color(0.30f, 0.85f, 0.95f);
        protected static readonly Color HeroBg      = new Color(0.13f, 0.13f, 0.17f);
        protected static readonly Color MutedText   = new Color(0.70f, 0.70f, 0.78f);
        protected static readonly Color BodyText    = new Color(0.92f, 0.92f, 0.96f);

        protected const float SpaceXS = 4f;
        protected const float SpaceS  = 8f;
        protected const float SpaceM  = 12f;
        protected const float SpaceL  = 16f;

        // -----------------------------------------------------------------------
        // Protected state
        // -----------------------------------------------------------------------

        protected PixelLabWindow Window;
        protected PixelLabClient Client => Window?.Client;

        protected Vector2 ScrollPos;

        protected bool   IsLoading      = false;
        protected string LoadingMessage = "Processing...";

        protected List<Texture2D> ResultTextures = new List<Texture2D>();
        protected List<string>    SavedPaths     = new List<string>();
        protected string          _errorMessage  = "";

        // -----------------------------------------------------------------------
        // Shared lazy-initialized styles (instance, built inside Draw calls)
        // -----------------------------------------------------------------------

        private GUIStyle _sharedHeaderTitleStyle;
        private GUIStyle _sharedHeaderSubStyle;
        private GUIStyle _sharedSectionLabelStyle;
        private GUIStyle _sharedPrimaryBtnStyle;
        private bool     _sharedStylesReady;

        private void EnsureSharedStyles()
        {
            if (_sharedStylesReady) return;
            _sharedStylesReady = true;

            _sharedHeaderTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = BodyText },
            };

            _sharedHeaderSubStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = MutedText },
            };

            _sharedSectionLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = BodyText },
            };

            _sharedPrimaryBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white },
                hover     = { textColor = Color.white },
                active    = { textColor = Color.white },
            };
        }

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------

        protected BasePanel(PixelLabWindow window)
        {
            Window = window;
        }

        // -----------------------------------------------------------------------
        // Abstract
        // -----------------------------------------------------------------------

        public abstract void Draw();

        // -----------------------------------------------------------------------
        // Client guard
        // -----------------------------------------------------------------------

        /// <summary>
        /// Draws a warning HelpBox when no client is connected and returns false.
        /// Returns true when a client is available so callers can short-circuit.
        /// </summary>
        protected bool RequireClient()
        {
            if (Client == null)
            {
                EditorGUILayout.HelpBox("Please enter an API key in Settings first.", MessageType.Warning);
                return false;
            }
            return true;
        }

        // -----------------------------------------------------------------------
        // Async helper
        // -----------------------------------------------------------------------

        /// <summary>
        /// Runs <paramref name="task"/> on a background thread.
        /// <para>
        /// Sets <see cref="IsLoading"/> to true immediately.  On completion (success
        /// or failure) the result callbacks are marshalled back to the main thread via
        /// <see cref="EditorApplication.delayCall"/>.
        /// </para>
        /// </summary>
        protected void RunAsync(Func<Task> task, Action onComplete = null, Action<Exception> onError = null)
        {
            IsLoading = true;
            Window.Repaint();

            Task.Run(async () =>
            {
                try
                {
                    await task();
                    EditorApplication.delayCall += () =>
                    {
                        IsLoading = false;
                        onComplete?.Invoke();
                        Window.Repaint();
                        // Refresh balance after every successful operation. DashboardPanel
                        // subscribes to OnBalanceRefreshRequested. Skip self-refresh for
                        // DashboardPanel itself to avoid recursion.
                        if (!(this is DashboardPanel))
                            Window.RefreshBalance();
                    };
                }
                catch (Exception ex)
                {
                    EditorApplication.delayCall += () =>
                    {
                        IsLoading = false;
                        onError?.Invoke(ex);
                        Window.Repaint();
                    };
                }
            });
        }

        // -----------------------------------------------------------------------
        // Texture helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Load a <see cref="Texture2D"/> from a path.
        /// Uses <see cref="AssetDatabase"/> when the path lives inside the project,
        /// otherwise falls back to <see cref="File.ReadAllBytes"/>.
        /// Returns <c>null</c> on failure.
        /// </summary>
        protected Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Project-relative path: let AssetDatabase handle it
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                // Normalise separators
                string assetPath = path.Replace('\\', '/');
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex != null) return tex;
            }

            // Fallback: raw file read (works for absolute paths or paths outside Assets)
            string absPath = path;
            if (!Path.IsPathRooted(path))
                absPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));

            if (!File.Exists(absPath)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(absPath);
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                    return tex;

                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PixelLab] LoadTexture failed for '{path}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Destroys all cached result textures and clears the result lists.
        /// </summary>
        protected void ClearResults()
        {
            foreach (var t in ResultTextures)
                if (t != null) UnityEngine.Object.DestroyImmediate(t);

            ResultTextures.Clear();
            SavedPaths.Clear();
        }

        // -----------------------------------------------------------------------
        // Image preview strip
        // -----------------------------------------------------------------------

        /// <summary>
        /// Draws a horizontal strip of image previews, each scaled to
        /// <paramref name="maxHeight"/> pixels.  If a corresponding saved path
        /// exists a small "Open in Project" button is shown beneath each image.
        /// </summary>
        protected void DrawImagePreviews(int maxHeight = 200)
        {
            if (ResultTextures.Count == 0) return;

            EditorGUILayout.Space(4);
            GUILayout.BeginHorizontal();

            for (int i = 0; i < ResultTextures.Count; i++)
            {
                Texture2D tex = ResultTextures[i];
                if (tex == null) continue;

                float aspect = (float)tex.width / Mathf.Max(1, tex.height);
                float drawW  = maxHeight * aspect;
                float drawH  = maxHeight;

                GUILayout.BeginVertical(GUILayout.Width(drawW));

                Rect previewRect = GUILayoutUtility.GetRect(drawW, drawH,
                    GUILayout.Width(drawW), GUILayout.Height(drawH));

                GUI.DrawTexture(previewRect, tex, ScaleMode.ScaleToFit);

                // Show "open in project" link when we have a saved path
                if (i < SavedPaths.Count && !string.IsNullOrEmpty(SavedPaths[i]))
                {
                    if (GUILayout.Button("Open in Project", EditorStyles.miniButton))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(SavedPaths[i]);
                        if (asset != null) EditorGUIUtility.PingObject(asset);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        // -----------------------------------------------------------------------
        // Common UI helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Draws a styled panel header banner — dark background with left violet stripe,
        /// title in large bold text, and an optional subtitle below.
        /// </summary>
        protected void DrawPanelHeader(string title, string subtitle = null)
        {
            EnsureSharedStyles();

            const float heroHeight = 64f;
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(heroHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, HeroBg);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), BrandViolet);
            }

            if (string.IsNullOrEmpty(subtitle))
            {
                Rect titleRect = new Rect(r.x + 16, r.y + (heroHeight - 22) * 0.5f, r.width - 28, 22);
                GUI.Label(titleRect, title, _sharedHeaderTitleStyle);
            }
            else
            {
                Rect titleRect = new Rect(r.x + 16, r.y + 10, r.width - 28, 24);
                GUI.Label(titleRect, title, _sharedHeaderTitleStyle);
                Rect subRect = new Rect(r.x + 16, r.y + 34, r.width - 28, 20);
                GUI.Label(subRect, subtitle, _sharedHeaderSubStyle);
            }
        }

        /// <summary>
        /// Draws a styled section sub-header with BodyText colour.
        /// </summary>
        protected void DrawSectionHeader(string title)
        {
            EnsureSharedStyles();
            EditorGUILayout.Space(SpaceS);
            EditorGUILayout.LabelField(title, _sharedSectionLabelStyle);
            EditorGUILayout.Space(SpaceXS);
        }

        /// <summary>
        /// Draws a BrandViolet-accented primary action button. Returns true when clicked.
        /// Respects GUI.enabled — fades when disabled.
        /// </summary>
        protected bool DrawPrimaryButton(string label, float height = 32f)
        {
            EnsureSharedStyles();

            Rect r = GUILayoutUtility.GetRect(GUIContent.none, _sharedPrimaryBtnStyle,
                GUILayout.Height(height), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                Color c = GUI.enabled
                    ? BrandViolet
                    : new Color(BrandViolet.r, BrandViolet.g, BrandViolet.b, 0.38f);
                EditorGUI.DrawRect(r, c);
            }

            if (r.Contains(Event.current.mousePosition) && GUI.enabled)
                Window.Repaint();

            return GUI.Button(r, label, _sharedPrimaryBtnStyle);
        }

        // Per-panel foldout state for optional sections (key = "panelType:label").
        private static readonly Dictionary<string, bool> _optionalFoldoutState =
            new Dictionary<string, bool>();

        /// <summary>
        /// Optional 섹션을 접을 수 있는 foldout으로 감쌉니다. 기본 닫힘.
        /// 사용: <c>if (DrawOptionalFoldout("Reference Image")) { ...optional UI... }</c>
        /// </summary>
        protected bool DrawOptionalFoldout(string label)
        {
            string key = GetType().Name + ":" + label;
            bool open = _optionalFoldoutState.TryGetValue(key, out bool v) && v;
            bool nowOpen = EditorGUILayout.Foldout(open, label, true);
            if (nowOpen != open) _optionalFoldoutState[key] = nowOpen;
            return nowOpen;
        }

        /// <summary>
        /// 1px 구분선을 그립니다. CharacterPanel/ObjectsPanel의 private DrawDivider()를 대체.
        /// </summary>
        protected static void DrawDivider()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f));
        }

        // -----------------------------------------------------------------------
        // Drag-and-drop helper
        // -----------------------------------------------------------------------

        /// <summary>
        /// Processes Unity drag-and-drop events over <paramref name="dropRect"/>.
        /// Returns the first dragged asset path when a drop is performed, otherwise
        /// returns <c>null</c>.
        /// </summary>
        protected string GetDraggedImagePath(Rect dropRect)
        {
            Event e = Event.current;

            if (e.type == EventType.DragUpdated && dropRect.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
            }
            else if (e.type == EventType.DragPerform && dropRect.Contains(e.mousePosition))
            {
                DragAndDrop.AcceptDrag();
                if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
                {
                    e.Use();
                    return DragAndDrop.paths[0];
                }
            }

            return null;
        }

        // -----------------------------------------------------------------------
        // Image picker field
        // -----------------------------------------------------------------------

        /// <summary>
        /// Draws a labeled image-picker using a Unity-native ObjectField (supports drag-and-drop
        /// from the Project window and the built-in asset picker), plus an optional path text
        /// field and Browse button for files outside the Assets/ folder.
        /// Updates <paramref name="path"/> and <paramref name="preview"/> in-place.
        /// </summary>
        /// <param name="label">Field label shown above the picker.</param>
        /// <param name="path">Current asset / file path (updated by this method).</param>
        /// <param name="preview">Cached preview texture (updated by this method).</param>
        /// <param name="height">Height of the ObjectField thumbnail in pixels.</param>
        // Tracks per-picker "show advanced (Path/Browse)" foldout state, keyed by label.
        private static readonly Dictionary<string, bool> _imagePickerAdvancedFoldout =
            new Dictionary<string, bool>();

        protected void DrawImagePicker(string label, ref string path, ref Texture2D preview, float height = 60)
        {
            DrawImagePicker(label, ref path, ref preview, null, height);
        }

        /// <summary>
        /// Image picker with an optional trailing action (e.g. a Remove button) drawn on
        /// the same row. Use this overload instead of wrapping <see cref="DrawImagePicker"/>
        /// in your own BeginHorizontal — nested horizontal groups break IMGUI layout.
        /// </summary>
        protected void DrawImagePicker(string label, ref string path, ref Texture2D preview,
            Action trailing, float height = 60)
        {
            // Compact single-line layout: [label] [ObjectField] [▸ advanced toggle] [trailing?]
            EditorGUILayout.BeginHorizontal();

            // Label on the left, fixed width so multiple pickers align
            EditorGUILayout.LabelField(label, GUILayout.Width(120));

            // Lazy-load preview from path if needed
            if (preview == null && !string.IsNullOrEmpty(path) &&
                path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                preview = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            // Texture2D-typed ObjectField: Unity's picker dialog will only list Texture2D
            // assets (no scripts, prefabs, materials, etc). Default single-line height —
            // explicit Height triggers the large thumbnail mode and breaks the row.
            Texture2D newTex = (Texture2D)EditorGUILayout.ObjectField(
                preview, typeof(Texture2D), false);

            if (newTex != preview)
            {
                preview = newTex;
                if (newTex == null)
                {
                    path = "";
                }
                else
                {
                    string assetPath = AssetDatabase.GetAssetPath(newTex);
                    if (!string.IsNullOrEmpty(assetPath)) path = assetPath;
                }
            }

            // Advanced toggle (▸/▾) — shows Path field + Browse button below
            string foldoutKey = label ?? "";
            bool showAdvanced = _imagePickerAdvancedFoldout.TryGetValue(foldoutKey, out bool v) && v;
            string arrow = showAdvanced ? "▾" : "▸";
            if (GUILayout.Button(arrow, EditorStyles.miniButton, GUILayout.Width(22)))
            {
                showAdvanced = !showAdvanced;
                _imagePickerAdvancedFoldout[foldoutKey] = showAdvanced;
            }

            // Caller-supplied trailing widget on the same row (e.g. "Remove" button)
            trailing?.Invoke();

            EditorGUILayout.EndHorizontal();

            // Advanced row: editable Path TextField + Browse button (hidden by default)
            if (!showAdvanced) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path", GUILayout.Width(36));

            string newPath = EditorGUILayout.TextField(path ?? "");
            if (newPath != path)
            {
                path    = newPath;
                preview = LoadTexture(path);
            }

            if (GUILayout.Button("Browse...", GUILayout.Width(70), GUILayout.Height(18)))
            {
                string startDir = string.IsNullOrEmpty(path)
                    ? Application.dataPath
                    : Path.GetDirectoryName(Path.GetFullPath(
                          Path.Combine(Application.dataPath, "..", path)));

                string selected = EditorUtility.OpenFilePanel("Select Image", startDir, "png,jpg,jpeg,webp");
                if (!string.IsNullOrEmpty(selected))
                {
                    // Convert absolute path to project-relative if possible
                    string dataPath    = Application.dataPath.Replace('\\', '/');
                    string normalized  = selected.Replace('\\', '/');
                    string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);

                    path = normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                        ? normalized.Substring(projectRoot.Length)
                        : normalized;

                    preview = LoadTexture(path);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
