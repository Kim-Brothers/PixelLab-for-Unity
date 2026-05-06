#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class SettingsPanel : BasePanel
    {
        // EditorPrefs keys
        private const string PrefApiKey    = "PixelLab_ApiKey";
        private const string PrefOutputDir = "PixelLab_OutputDir";
        private const string PrefBaseUrl   = "PixelLab_BaseUrl";
        private const string DefaultBaseUrl = "https://api.pixellab.ai/v2";

        private string _apiKey      = "";
        private string _outputDir   = "Assets/PixelLab/Output";
        private string _baseUrl     = "https://api.pixellab.ai/v2";
        private bool   _showKey     = false;
        private string _testResult  = "";
        private bool   _testSuccess = false;

        // Scratch fields used to pass async results back to onComplete on the main thread
        private float  _pendingUsd     = 0f;
        private float  _pendingCredits = 0f;

        public SettingsPanel(PixelLabWindow window) : base(window)
        {
            _apiKey    = EditorPrefs.GetString(PrefApiKey,    "");
            _outputDir = EditorPrefs.GetString(PrefOutputDir, "Assets/PixelLab/Output");
            _baseUrl   = EditorPrefs.GetString(PrefBaseUrl,   DefaultBaseUrl);
            if (string.IsNullOrEmpty(_baseUrl)) _baseUrl = DefaultBaseUrl;
        }

        public override void Draw()
        {
            ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

            DrawPanelHeader("API Settings");

            // API Key field with show/hide toggle
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("API Key");
            if (_showKey)
                _apiKey = EditorGUILayout.TextField(_apiKey);
            else
                _apiKey = EditorGUILayout.PasswordField(_apiKey);
            if (GUILayout.Button(_showKey ? "Hide" : "Show", GUILayout.Width(52)))
                _showKey = !_showKey;
            EditorGUILayout.EndHorizontal();

            // Output directory with folder picker
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Output Directory");
            _outputDir = EditorGUILayout.TextField(_outputDir);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Output Directory", Application.dataPath, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    // Convert to project-relative path if the folder is inside the project
                    string dataPath    = Application.dataPath.Replace('\\', '/');
                    string norm        = selected.Replace('\\', '/');
                    string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);
                    _outputDir = norm.StartsWith(projectRoot)
                        ? norm.Substring(projectRoot.Length)
                        : norm;
                }
            }
            EditorGUILayout.EndHorizontal();

            // Base URL (advanced — leave at default unless targeting a different endpoint)
            // TODO: default sizes for RotatePanel and AnimationPanel should be reviewed here
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Base URL");
            _baseUrl = EditorGUILayout.TextField(_baseUrl);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Save and Test Connection buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
                SaveSettings();

            GUI.enabled = !string.IsNullOrEmpty(_apiKey) && !IsLoading;
            if (GUILayout.Button(IsLoading ? LoadingMessage : "Test Connection", GUILayout.Height(30)))
                TestConnection();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Test result feedback
            if (!string.IsNullOrEmpty(_testResult))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_testResult, _testSuccess ? MessageType.Info : MessageType.Error);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString(PrefApiKey,    _apiKey);
            EditorPrefs.SetString(PrefOutputDir, _outputDir);
            EditorPrefs.SetString(PrefBaseUrl,   string.IsNullOrEmpty(_baseUrl) ? DefaultBaseUrl : _baseUrl);

            if (!string.IsNullOrEmpty(_apiKey))
                Window.Connect(_apiKey, _outputDir, string.IsNullOrEmpty(_baseUrl) ? DefaultBaseUrl : _baseUrl);
            else
                Window.Disconnect();
        }

        private void TestConnection()
        {
            LoadingMessage = "Testing connection...";

            // Snapshot the key at call time so the lambda is not affected by later edits
            string keySnapshot = _apiKey;

            RunAsync(
                async () =>
                {
                    // Use a temporary client with the current (unsaved) key
                    using var tempClient = new PixelLabClient(keySnapshot);
                    JObject result = await tempClient.GetBalance();

                    // Parse on the background thread; store in scratch fields for onComplete
                    (_pendingUsd, _pendingCredits) = PixelLabWindow.ParseBalanceResponse(result);
                },
                onComplete: () =>
                {
                    _testResult  = $"Connection successful! Balance: ${_pendingUsd:F4}";
                    _testSuccess = true;
                    Window.SetCredits($"${_pendingUsd:F4}");
                },
                onError: ex =>
                {
                    _testResult  = $"Connection failed: {ex.Message}";
                    _testSuccess = false;
                }
            );
        }
    }
}
#endif
