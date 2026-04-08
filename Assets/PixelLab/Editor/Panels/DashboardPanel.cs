#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace PixelLab.Editor
{
    public class DashboardPanel : BasePanel
    {
        private float  _usdBalance  = 0f;
        private float  _credits     = 0f;
        private string _balanceText = "Balance not loaded";
        private Vector2 _costScroll;

        // Scratch fields used to pass async results back to onComplete on the main thread
        private float _pendingUsd     = 0f;
        private float _pendingCredits = 0f;

        public DashboardPanel(PixelLabWindow window) : base(window) { }

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dashboard", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            if (!RequireClient())
            {
                EditorGUILayout.EndScrollView();
                return;
            }

            // Balance box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Balance", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_balanceText, EditorStyles.largeLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            GUI.enabled = !IsLoading;
            if (GUILayout.Button(IsLoading ? "Loading balance..." : "Refresh Balance", GUILayout.Height(28)))
                RefreshBalance();
            GUI.enabled = true;

            EditorGUILayout.Space(14);

            // Cost reference table
            EditorGUILayout.LabelField("Estimated Cost by Operation", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _costScroll = EditorGUILayout.BeginScrollView(_costScroll, GUILayout.Height(300));
            foreach (var kv in PixelLabConstants.OPERATION_COSTS)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(kv.Key,   GUILayout.MinWidth(200));
                EditorGUILayout.LabelField(kv.Value, EditorStyles.boldLabel, GUILayout.Width(100));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndScrollView();
        }

        public void RefreshBalance()
        {
            LoadingMessage = "Loading balance...";

            RunAsync(
                async () =>
                {
                    JObject result = await Client.GetBalance();

                    // Parse on the background thread; store in scratch fields for onComplete
                    _pendingUsd     = result["usd_balance"]?.Value<float>() ?? 0f;
                    _pendingCredits = result["balance"]?.Value<float>()     ?? 0f;
                },
                onComplete: () =>
                {
                    _usdBalance  = _pendingUsd;
                    _credits     = _pendingCredits;
                    _balanceText = $"${_usdBalance:F4} USD  ({_credits:F2} credits)";
                    Window.SetCredits($"${_usdBalance:F4}");
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
