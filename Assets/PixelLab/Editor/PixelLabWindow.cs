#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;

namespace PixelLab.Editor
{
    public class PixelLabWindow : EditorWindow
    {
        // -----------------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------------

        private const string PrefApiKey      = "PixelLab_ApiKey";
        private const string PrefOutputDir   = "PixelLab_OutputDir";
        private const string PrefSelectedPanel = "PixelLab_SelectedPanel";
        private const string DefaultOutput   = "Assets/PixelLab/Output";

        private const float SidebarWidth   = 160f;
        private const float NavButtonHeight = 36f;
        private const float StatusBarHeight = 22f;

        // -----------------------------------------------------------------------
        // Menu
        // -----------------------------------------------------------------------

        [MenuItem("Tools/PixelLab")]
        public static void ShowWindow()
        {
            var window = GetWindow<PixelLabWindow>("PixelLab");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        // -----------------------------------------------------------------------
        // Public state (panels read these)
        // -----------------------------------------------------------------------

        public PixelLabClient Client    { get; private set; }
        public string         OutputDir { get; private set; } = DefaultOutput;

        // -----------------------------------------------------------------------
        // Private UI state
        // -----------------------------------------------------------------------

        private string _statusText  = "Not connected";
        private string _creditText  = "";
        private string _lastCostText = "";

        private int      _selectedPanel = 0;
        private string[] _panelNames    = { "Dashboard", "Generate", "Character", "Animation", "Tileset", "Edit", "Rotation", "Settings" };

        private BasePanel[] _panels;

        // Cached GUIStyles — built once in OnGUI after skin is available
        private GUIStyle  _sidebarBtnStyle;
        private GUIStyle  _sidebarBtnActiveStyle;
        private GUIStyle  _statusLabelStyle;
        private bool      _stylesInitialised;
        private Texture2D _texSidebarNormal;
        private Texture2D _texSidebarHover;
        private Texture2D _texSidebarActive;
        private Texture2D _texStatusBar;

        // -----------------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------------

        private void OnEnable()
        {
            _stylesInitialised = false;

            _panels = new BasePanel[]
            {
                new DashboardPanel(this),   // 0 Dashboard
                new GeneratePanel(this),    // 1 Generate
                new CharacterPanel(this),   // 2 Character
                new AnimationPanel(this),   // 3 Animation
                new TilesetPanel(this),     // 4 Tileset
                new EditPanel(this),        // 5 Edit
                new RotatePanel(this),      // 6 Rotation
                new SettingsPanel(this),    // 7 Settings
            };

            // Restore settings and auto-connect if key exists
            string savedKey    = EditorPrefs.GetString(PrefApiKey, "");
            string savedOutput = EditorPrefs.GetString(PrefOutputDir, DefaultOutput);
            _selectedPanel     = EditorPrefs.GetInt(PrefSelectedPanel, 0);

            OutputDir = string.IsNullOrEmpty(savedOutput) ? DefaultOutput : savedOutput;

            if (!string.IsNullOrEmpty(savedKey))
                Connect(savedKey, OutputDir);
        }

        private void OnDisable()
        {
            Client?.Dispose();
            Client = null;

            // Destroy cached style textures to prevent leaks
            if (_texSidebarNormal != null) DestroyImmediate(_texSidebarNormal);
            if (_texSidebarHover  != null) DestroyImmediate(_texSidebarHover);
            if (_texSidebarActive != null) DestroyImmediate(_texSidebarActive);
            if (_texStatusBar     != null) DestroyImmediate(_texStatusBar);
            _texSidebarNormal = _texSidebarHover = _texSidebarActive = _texStatusBar = null;
            _stylesInitialised = false;
        }

        // -----------------------------------------------------------------------
        // OnGUI
        // -----------------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            HandleKeyboardNav();

            // Reserve the status bar rect at the bottom before layout begins
            Rect statusRect = new Rect(0, position.height - StatusBarHeight, position.width, StatusBarHeight);

            // Main split: sidebar | content  (leave room for status bar)
            float contentHeight = position.height - StatusBarHeight;

            GUILayout.BeginHorizontal(GUILayout.Height(contentHeight));
            {
                DrawSidebar(contentHeight);
                DrawContent();
            }
            GUILayout.EndHorizontal();

            DrawStatusBar(statusRect);
        }

        // -----------------------------------------------------------------------
        // Sidebar
        // -----------------------------------------------------------------------

        private void DrawSidebar(float height)
        {
            Rect sidebarRect = GUILayoutUtility.GetRect(SidebarWidth, height,
                GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));

            // Dark background
            EditorGUI.DrawRect(sidebarRect, new Color(0.18f, 0.18f, 0.18f));

            GUILayout.BeginArea(sidebarRect);
            GUILayout.BeginVertical();

            // Logo / title
            GUILayout.Space(8);
            GUILayout.Label("PixelLab", EditorStyles.boldLabel);
            GUILayout.Space(6);

            for (int i = 0; i < _panelNames.Length; i++)
            {
                GUIStyle style = (i == _selectedPanel) ? _sidebarBtnActiveStyle : _sidebarBtnStyle;
                if (GUILayout.Button(_panelNames[i], style, GUILayout.Height(NavButtonHeight)))
                {
                    _selectedPanel = i;
                    EditorPrefs.SetInt(PrefSelectedPanel, i);
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // -----------------------------------------------------------------------
        // Content
        // -----------------------------------------------------------------------

        private void DrawContent()
        {
            // Content area fills the remaining horizontal space
            GUILayout.BeginVertical();

            if (_panels != null && _selectedPanel >= 0 && _selectedPanel < _panels.Length)
                _panels[_selectedPanel].Draw();
            else
                EditorGUILayout.LabelField("Please select a panel.");

            GUILayout.EndVertical();
        }

        // -----------------------------------------------------------------------
        // Status bar
        // -----------------------------------------------------------------------

        private void DrawStatusBar(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            // Build status string
            string text = _statusText;
            if (!string.IsNullOrEmpty(_creditText))
                text += "   |   " + _creditText;
            if (!string.IsNullOrEmpty(_lastCostText))
                text += "   |   Last cost: " + _lastCostText;

            GUI.Label(new Rect(rect.x + 8, rect.y + 3, rect.width - 16, rect.height), text, _statusLabelStyle);
        }

        // -----------------------------------------------------------------------
        // Keyboard navigation
        // -----------------------------------------------------------------------

        private void HandleKeyboardNav()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.LeftArrow)
            {
                _selectedPanel = (_selectedPanel - 1 + _panelNames.Length) % _panelNames.Length;
                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.RightArrow)
            {
                _selectedPanel = (_selectedPanel + 1) % _panelNames.Length;
                e.Use();
                Repaint();
            }
        }

        // -----------------------------------------------------------------------
        // Style initialisation (must happen inside OnGUI after skin is ready)
        // -----------------------------------------------------------------------

        private void EnsureStyles()
        {
            if (_stylesInitialised) return;
            _stylesInitialised = true;

            // Base sidebar button
            _sidebarBtnStyle = new GUIStyle(GUI.skin.button)
            {
                alignment  = TextAnchor.MiddleLeft,
                fontSize   = 13,
                border     = new RectOffset(0, 0, 0, 0),
                margin     = new RectOffset(0, 0, 0, 0),
                padding    = new RectOffset(14, 8, 0, 0),
                fixedWidth = SidebarWidth,
            };
            _texSidebarNormal                     = MakeSolidTex(new Color(0.18f, 0.18f, 0.18f));
            _texSidebarHover                      = MakeSolidTex(new Color(0.22f, 0.22f, 0.22f));
            _sidebarBtnStyle.normal.background    = _texSidebarNormal;
            _sidebarBtnStyle.hover.background     = _texSidebarHover;
            _sidebarBtnStyle.normal.textColor     = new Color(0.8f, 0.8f, 0.8f);
            _sidebarBtnStyle.hover.textColor      = Color.white;

            // Active sidebar button
            _sidebarBtnActiveStyle = new GUIStyle(_sidebarBtnStyle);
            _texSidebarActive                        = MakeSolidTex(new Color(0.25f, 0.5f, 0.9f, 0.8f));
            _sidebarBtnActiveStyle.normal.background = _texSidebarActive;
            _sidebarBtnActiveStyle.normal.textColor  = Color.white;
            _sidebarBtnActiveStyle.fontStyle         = FontStyle.Bold;

            // Status bar label
            _statusLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };
        }

        // -----------------------------------------------------------------------
        // Public API used by panels
        // -----------------------------------------------------------------------

        public void SetStatus(string text)  { _statusText   = text;  Repaint(); }
        public void SetCredits(string text) { _creditText   = text;  Repaint(); }
        public void SetCost(string text)    { _lastCostText = text;  Repaint(); }

        public void Connect(string apiKey, string outputDir)
        {
            Client?.Dispose();
            Client    = new PixelLabClient(apiKey);
            OutputDir = outputDir;
            SetStatus("Connected");
        }

        public void Disconnect()
        {
            Client?.Dispose();
            Client = null;
            SetStatus("Not connected");
            SetCredits("");
        }

        // -----------------------------------------------------------------------
        // Utility
        // -----------------------------------------------------------------------

        /// <summary>Create a 1x1 solid-colour Texture2D for use in GUIStyle backgrounds.</summary>
        private static Texture2D MakeSolidTex(Color c)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }
    }
}
#endif
