#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace PixelLab.Editor
{
    public class AboutPanel : BasePanel
    {
        private GUIStyle _bodyStyle;
        private GUIStyle _linkStyle;
        private GUIStyle _cardTitleStyle;
        private GUIStyle _cardDescStyle;
        private GUIStyle _footerStyle;
        private bool _stylesReady;

        private const float CardHeight = 90f;

        private struct AssetInfo
        {
            public string Name;
            public string Desc;
            public string Url;
        }

        private static readonly AssetInfo[] OtherAssets =
        {
            new AssetInfo
            {
                Name = "Your Asset Name Here",
                Desc = "Short description of what this asset does for Unity developers.",
                Url  = "https://assetstore.unity.com/"
            },
            new AssetInfo
            {
                Name = "Another Asset Name",
                Desc = "Another short description. Update these entries with your real assets.",
                Url  = "https://assetstore.unity.com/"
            },
        };

        public AboutPanel(PixelLabWindow window) : base(window) { }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = BodyText },
            };

            _linkStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.50f, 0.70f, 0.95f) },
                hover  = { textColor = new Color(0.70f, 0.85f, 1f) },
            };

            _cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = BodyText },
            };

            _cardDescStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = MutedText },
            };

            _footerStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = MutedText },
            };
        }

        public override void Draw()
        {
            EnsureStyles();

            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawHero();
            EditorGUILayout.Space(SpaceM);

            DrawPluginInfo();
            EditorGUILayout.Space(SpaceL);

            DrawOtherAssets();
            EditorGUILayout.Space(SpaceM);

            DrawDivider();
            EditorGUILayout.Space(SpaceXS);
            GUILayout.Label("PixelLab for Unity — third-party plugin", _footerStyle);
            EditorGUILayout.Space(SpaceS);

            EditorGUILayout.EndScrollView();
        }

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
            Rect subRect   = new Rect(r.x + 16, r.y + 34, r.width - 28, 20);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = BodyText },
            };
            GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal   = { textColor = MutedText },
            };

            GUI.Label(titleRect, "PixelLab for Unity", titleStyle);
            GUI.Label(subRect, "Third-party editor plugin • Not affiliated with PixelLab AI", subStyle);
        }

        private void DrawPluginInfo()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label(
                "PixelLab for Unity is an unofficial editor extension that integrates the PixelLab v2 API into the Unity Editor. " +
                "Generate, animate, rotate, edit, and tile pixel art without leaving Unity.\n\n" +
                "This plugin is not affiliated with or endorsed by PixelLab AI.",
                _bodyStyle);

            EditorGUILayout.Space(SpaceXS);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("API Reference →", _linkStyle))
                Application.OpenURL("https://www.pixellab.ai/pixellab-api");
            if (GUILayout.Button("Documentation →", _linkStyle))
                Application.OpenURL("https://www.pixellab.ai/docs");
            if (GUILayout.Button("pixellab.ai →", _linkStyle))
                Application.OpenURL("https://www.pixellab.ai/");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawOtherAssets()
        {
            GUIStyle sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal   = { textColor = BodyText },
            };
            GUILayout.Label("More Assets by the Developer", sectionStyle);
            EditorGUILayout.Space(SpaceS);

            const int cols = 2;
            int total    = OtherAssets.Length;
            int fullRows = total / cols;
            int remainder = total % cols;

            int idx = 0;
            for (int row = 0; row < fullRows; row++)
            {
                Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(CardHeight), GUILayout.ExpandWidth(true));

                float gap   = SpaceXS;
                float cardW = (rowRect.width - gap * (cols - 1)) / cols;

                for (int c = 0; c < cols; c++)
                {
                    Rect cardRect = new Rect(rowRect.x + c * (cardW + gap),
                        rowRect.y, cardW, rowRect.height);
                    DrawAssetCard(OtherAssets[idx], idx, cardRect);
                    idx++;
                }

                EditorGUILayout.Space(SpaceXS);
            }

            if (remainder > 0)
            {
                Rect rowRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                    GUILayout.Height(CardHeight), GUILayout.ExpandWidth(true));
                DrawAssetCard(OtherAssets[idx], idx, rowRect);
                EditorGUILayout.Space(SpaceXS);
            }
        }

        private void DrawAssetCard(AssetInfo asset, int idx, Rect r)
        {
            Color stripeColor = ((idx & 1) == 0) ? BrandViolet : BrandCyan;

            if (Event.current.type == EventType.Repaint)
            {
                EditorStyles.helpBox.Draw(r, GUIContent.none, false, false, false, false);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), stripeColor);
            }

            const float padL = 12f;
            const float padR = 10f;
            Rect titleRect = new Rect(r.x + padL, r.y + 8,  r.width - padL - padR, 18);
            Rect descRect  = new Rect(r.x + padL, r.y + 28, r.width - padL - padR, 36);
            Rect linkRect  = new Rect(r.x + padL, r.y + 68, r.width - padL - padR, 16);

            GUI.Label(titleRect, asset.Name, _cardTitleStyle);
            GUI.Label(descRect,  asset.Desc, _cardDescStyle);
            GUI.Label(linkRect,  "View on Asset Store →", _linkStyle);

            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                Application.OpenURL(asset.Url);
                e.Use();
            }
        }
    }
}
#endif
