#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class DashboardPanel : BasePanel
    {
        private float   _usdBalance  = 0f;
        private string  _balanceText = "Balance not loaded";
        private Vector2 _costScroll;
        private bool    _costFoldout  = false;
        private bool    _aboutFoldout = false;

        private float _pendingUsd = 0f;

        // -----------------------------------------------------------------------
        // Palette — vibrant violet/cyan accents over a dark base. Disclaimer text
        // is what carries the "unofficial" message; the visual style itself is
        // free to look polished.
        // -----------------------------------------------------------------------
        private static readonly Color HeroBg      = new Color(0.13f, 0.13f, 0.17f);
        private static readonly Color BrandViolet = new Color(0.55f, 0.35f, 0.95f);
        private static readonly Color BrandCyan   = new Color(0.30f, 0.85f, 0.95f);
        private static readonly Color HoverGlow   = new Color(0.75f, 0.55f, 1.00f);
        private static readonly Color MutedText   = new Color(0.70f, 0.70f, 0.78f);
        private static readonly Color BodyText    = new Color(0.92f, 0.92f, 0.96f);

        private GUIStyle _heroTitleStyle;
        private GUIStyle _heroSubtitleStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _featureTitleStyle;
        private GUIStyle _featureDescStyle;
        private GUIStyle _balanceLabelStyle;
        private GUIStyle _balanceValueStyle;
        private GUIStyle _topUpButtonStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _linkStyle;
        private GUIStyle _aboutBodyStyle;

        private bool _stylesReady;

        // 4/8/12/16 spacing system
        private const float SpaceXS = 4f;
        private const float SpaceS  = 8f;
        private const float SpaceM  = 12f;
        private const float SpaceL  = 16f;

        // Card layout constants — fixed height keeps every card identical no
        // matter how the description wraps; width still follows panel width.
        private const float CardHeight    = 72f;
        private const float CardDescHeight = 36f; // ~2 wrapped lines

        private struct Feature
        {
            public string Title;
            public string PanelName;
            public string Desc;
        }

        // Cards double as click-through navigation into the matching panel.
        // Single accent applied uniformly — the page is a tool, not a brand showcase.
        private static readonly Feature[] Features =
        {
            new Feature { Title = "Generate",  PanelName = "Generate",
                Desc = "Text-to-pixel generation with style-consistent results." },
            new Feature { Title = "Character", PanelName = "Character",
                Desc = "Style-matched character spritesheets from a reference." },
            new Feature { Title = "Animation", PanelName = "Animation",
                Desc = "One-click, skeleton-based, and text-driven animations." },
            new Feature { Title = "Tileset",   PanelName = "Tileset",
                Desc = "Tilesets and scenes — up to 400×400 pixels." },
            new Feature { Title = "Edit",      PanelName = "Edit",
                Desc = "True inpainting that preserves your original style." },
            new Feature { Title = "Rotation",  PanelName = "Rotation",
                Desc = "4 & 8 directional views with isometric support." },
            new Feature { Title = "Objects",   PanelName = "Objects",
                Desc = "Items, props, and UI parts: buttons, health bars, menus." },
        };

        public DashboardPanel(PixelLabWindow window) : base(window)
        {
            window.OnConnected               += AutoRefreshAfterConnect;
            window.OnBalanceRefreshRequested += AutoRefreshIfClient;
        }

        private void AutoRefreshAfterConnect() { if (Window?.Client != null) RefreshBalance(); }
        private void AutoRefreshIfClient()     { if (Window?.Client != null) RefreshBalance(); }

        // -----------------------------------------------------------------------
        // GUIStyle init
        // -----------------------------------------------------------------------
        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _heroTitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = BodyText },
            };

            _heroSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = MutedText },
            };

            _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = BodyText },
            };

            _featureTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = BodyText },
            };

            _featureDescStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = MutedText },
            };

            _balanceLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal   = { textColor = MutedText },
            };

            _balanceValueStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize  = 22,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = BrandCyan },
            };

            _topUpButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white },
                hover     = { textColor = Color.white },
            };

            _mutedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = MutedText },
            };

            _linkStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.50f, 0.70f, 0.95f) },
                hover  = { textColor = new Color(0.70f, 0.85f, 1f) },
            };

            _aboutBodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = BodyText },
            };
        }

        // -----------------------------------------------------------------------
        // Draw
        // -----------------------------------------------------------------------
        public override void Draw()
        {
            EnsureStyles();

            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawHero();
            EditorGUILayout.Space(SpaceM);

            if (Client != null) DrawBalanceCard();
            else                DrawConnectPrompt();

            EditorGUILayout.Space(SpaceL);

            EditorGUILayout.LabelField("Tools", _sectionHeaderStyle);
            EditorGUILayout.Space(SpaceS);
            DrawFeatureGrid();
            EditorGUILayout.Space(SpaceM);

            DrawAboutFoldout();
            EditorGUILayout.Space(SpaceS);

            DrawCostReference();
            EditorGUILayout.Space(SpaceM);

            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Hero — minimal utility header. Solid dark fill, thin left stripe, plain
        // title. No gradient, no badge, no decorative dots.
        // -----------------------------------------------------------------------
        private void DrawHero()
        {
            const float heroHeight = 64f;
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                GUILayout.Height(heroHeight), GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, HeroBg);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), BrandViolet);
            }

            Rect titleRect = new Rect(r.x + 16, r.y + 10, r.width - 28, 24);
            GUI.Label(titleRect, "PixelLab for Unity", _heroTitleStyle);

            Rect subRect = new Rect(r.x + 16, r.y + 34, r.width - 28, 20);
            GUI.Label(subRect,
                "Pixel art generation tools, right inside your editor.",
                _heroSubtitleStyle);
        }

        // -----------------------------------------------------------------------
        // Balance card — neutral styling, no colored top border, plain text values
        // -----------------------------------------------------------------------
        private void DrawBalanceCard()
        {
            Rect outer = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (Event.current.type == EventType.Repaint && outer.height > 1)
            {
                EditorGUI.DrawRect(new Rect(outer.x, outer.y, outer.width, 2), BrandViolet);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical();
                GUILayout.Label("ACCOUNT BALANCE", _balanceLabelStyle);
                GUILayout.Label(_balanceText, _balanceValueStyle);
                GUILayout.Label("≈ 1 credit = $0.001 USD", _mutedStyle);
                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginVertical(GUILayout.Width(150));
                GUILayout.Space(SpaceS);

                GUI.enabled = !IsLoading;
                if (GUILayout.Button(IsLoading ? "Loading…" : "Refresh Balance",
                        GUILayout.Height(28)))
                    RefreshBalance();
                GUI.enabled = true;

                Rect topUpRect = GUILayoutUtility.GetRect(GUIContent.none, _topUpButtonStyle,
                    GUILayout.Height(28));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(topUpRect, BrandViolet);
                if (GUI.Button(topUpRect, "Top up ↗", _topUpButtonStyle))
                    Application.OpenURL("https://www.pixellab.ai/");

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawConnectPrompt()
        {
            EditorGUILayout.HelpBox(
                "Enter your PixelLab API key in the Settings panel to start generating pixel art.",
                MessageType.Info);

            EditorGUILayout.Space(SpaceXS);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Settings →", GUILayout.Height(24), GUILayout.Width(150)))
            {
                int idx = Window.GetPanelIndex("Settings");
                if (idx >= 0) Window.SelectPanel(idx);
            }
            EditorGUILayout.EndHorizontal();
        }

        // -----------------------------------------------------------------------
        // Feature grid — uniform card sizing across rows. 2-column grid; the
        // trailing odd card spans the full row instead of leaving an empty slot.
        // -----------------------------------------------------------------------
        private void DrawFeatureGrid()
        {
            const int cols = 2;
            int total     = Features.Length;
            int fullRows  = total / cols;
            int remainder = total % cols;

            int idx = 0;
            for (int row = 0; row < fullRows; row++)
            {
                // Row reserved as a single fixed-height block so both cards land
                // on identical Y bounds regardless of inner content.
                Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(CardHeight), GUILayout.ExpandWidth(true));

                float gap      = SpaceXS;
                float cardW    = (rowRect.width - gap * (cols - 1)) / cols;

                for (int c = 0; c < cols; c++)
                {
                    Rect cardRect = new Rect(rowRect.x + c * (cardW + gap),
                        rowRect.y, cardW, rowRect.height);
                    DrawFeatureCard(Features[idx], idx, cardRect);
                    idx++;
                }

                EditorGUILayout.Space(SpaceXS);
            }

            // Trailing odd card spans the full row width.
            if (remainder > 0)
            {
                Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(CardHeight), GUILayout.ExpandWidth(true));
                DrawFeatureCard(Features[idx], idx, rowRect);
                EditorGUILayout.Space(SpaceXS);
            }
        }

        private void DrawFeatureCard(Feature f, int idx, Rect r)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Color baseAccent  = ((idx & 1) == 0) ? BrandViolet : BrandCyan;
            Color stripeColor = hover ? HoverGlow : baseAccent;

            if (Event.current.type == EventType.Repaint)
            {
                // Card body — borrow the helpBox visual so it matches surrounding panels
                EditorStyles.helpBox.Draw(r, GUIContent.none, false, false, false, false);
                // Left accent stripe
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), stripeColor);
            }

            // Inner text laid out with explicit rects — no GUILayout reflow.
            const float padL = 12f;
            const float padR = 10f;
            Rect titleRect = new Rect(r.x + padL, r.y + 8, r.width - padL - padR, 18);
            Rect descRect  = new Rect(r.x + padL, r.y + 28, r.width - padL - padR,
                                      r.height - 34);

            GUI.Label(titleRect, f.Title, _featureTitleStyle);
            GUI.Label(descRect,  f.Desc,  _featureDescStyle);

            // Click-through navigation + cursor hint
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (hover) Window.Repaint();

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                int panelIdx = Window.GetPanelIndex(f.PanelName);
                if (panelIdx >= 0)
                {
                    Window.SelectPanel(panelIdx);
                    e.Use();
                }
            }
        }

        // -----------------------------------------------------------------------
        // About — folded by default. Includes explicit "unofficial" disclaimer.
        // -----------------------------------------------------------------------
        private void DrawAboutFoldout()
        {
            _aboutFoldout = EditorGUILayout.Foldout(_aboutFoldout, "About", true);
            if (!_aboutFoldout) return;

            EditorGUILayout.Space(SpaceXS);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(
                "Editor extension that wraps the PixelLab v2 API. Generate, animate, " +
                "rotate, edit, and tile pixel art inside Unity using your own PixelLab account.",
                _aboutBodyStyle);
            EditorGUILayout.EndVertical();
        }

        // -----------------------------------------------------------------------
        // Cost reference — collapsible
        // -----------------------------------------------------------------------
        private void DrawCostReference()
        {
            _costFoldout = EditorGUILayout.Foldout(_costFoldout,
                "Estimated cost by operation", true);
            if (!_costFoldout) return;

            EditorGUILayout.Space(SpaceXS);
            _costScroll = EditorGUILayout.BeginScrollView(_costScroll,
                GUILayout.Height(240));

            foreach (var kv in PixelLabConstants.OPERATION_COSTS)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kv.Key,   GUILayout.MinWidth(200));
                EditorGUILayout.LabelField(kv.Value, EditorStyles.boldLabel,
                    GUILayout.MinWidth(160));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // -----------------------------------------------------------------------
        // Footer — disclaimer + link, no brand language
        // -----------------------------------------------------------------------
        private void DrawFooter()
        {
            DrawDivider();
            EditorGUILayout.Space(SpaceXS);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Uses PixelLab v2 API", _mutedStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("pixellab.ai →", _linkStyle))
                Application.OpenURL("https://www.pixellab.ai/");
            EditorGUILayout.EndHorizontal();
        }

        // -----------------------------------------------------------------------
        // Balance refresh
        // -----------------------------------------------------------------------
        public void RefreshBalance()
        {
            LoadingMessage = "Loading balance...";

            RunAsync(
                async () =>
                {
                    JObject result = await Client.GetBalance();
                    (_pendingUsd, _) = PixelLabWindow.ParseBalanceResponse(result);
                },
                onComplete: () =>
                {
                    _usdBalance  = _pendingUsd;
                    _balanceText = $"${_usdBalance:F4} USD";
                    Window.SetCredits($"${_usdBalance:F4} USD");
                },
                onError: ex =>
                {
                    _balanceText = $"Failed to load balance: {ex.Message}";
                }
            );
        }
    }
}
#endif
