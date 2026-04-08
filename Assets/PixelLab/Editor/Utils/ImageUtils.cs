#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    /// <summary>
    /// Image utility functions for the PixelLab Unity Editor integration.
    /// Mirrors the functionality of pixellab_tool/utils.py.
    /// </summary>
    public static class ImageUtils
    {
        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Convert a Unity asset path (or absolute path) to a PixelLab base64
        /// ImageData JSON object: { "type": "base64", "base64": "...", "format": "png"/"jpeg" }.
        /// </summary>
        public static string ImageToBase64Json(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentNullException(nameof(assetPath));

            string absolutePath = assetPath;
            if (!Path.IsPathRooted(assetPath))
            {
                absolutePath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", assetPath));
            }

            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"Image file not found: {absolutePath}");

            byte[] bytes = File.ReadAllBytes(absolutePath);
            string base64 = Convert.ToBase64String(bytes);

            // Python: fmt = path.suffix.lstrip(".").lower(); if fmt == "jpg": fmt = "jpeg"
            string ext = Path.GetExtension(absolutePath).ToLowerInvariant().TrimStart('.');
            string format = ext == "jpg" ? "jpeg" : ext;

            var obj = new JObject
            {
                ["type"] = "base64",
                ["base64"] = base64,
                ["format"] = format
            };

            return obj.ToString(Formatting.None);
        }

        /// <summary>
        /// Save all images found in an API JSON response.
        /// Mirrors Python utils.py save_images_from_response exactly:
        /// 1. Check last_response.images (early return)
        /// 2. Check last_response.quantized_image (early return)
        /// 3. Check last_response.image (early return)
        /// 4. Fall through to general data handling
        /// </summary>
        public static List<string> SaveImagesFromResponseJson(
            string responseJson, string outputDir, string prefix)
        {
            if (string.IsNullOrEmpty(responseJson))
                throw new ArgumentNullException(nameof(responseJson));
            if (string.IsNullOrEmpty(outputDir))
                throw new ArgumentNullException(nameof(outputDir));

            string absOutputDir = ResolveAbsolutePath(outputDir);
            Directory.CreateDirectory(absOutputDir);

            JObject root;
            try
            {
                root = JObject.Parse(responseJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PixelLab] Failed to parse response JSON: {ex.Message}");
                return new List<string>();
            }

            if (root["background_job_id"] != null)
            {
                Debug.LogWarning("[PixelLab] Response contains background_job_id – " +
                                 "caller should wait for the job to complete before saving images.");
                return new List<string>();
            }

            var saved = SaveImagesFromData(root, absOutputDir, prefix);
            RefreshAssets(absOutputDir);
            return saved;
        }

        /// <summary>
        /// Core image extraction logic matching Python save_images_from_response.
        /// </summary>
        private static List<string> SaveImagesFromData(
            JToken data, string absOutputDir, string prefix)
        {
            if (data == null || data.Type == JTokenType.Null)
                return new List<string>();

            // Python: Check for last_response with images (background job result)
            if (data.Type == JTokenType.Object && data["last_response"] != null)
            {
                JToken lastResp = data["last_response"];
                if (lastResp.Type == JTokenType.Object)
                {
                    // 1. last_response.images (dict or list) — checked FIRST
                    JToken images = lastResp["images"];
                    if (images != null && images.Type != JTokenType.Null)
                    {
                        var result = ExtractImages(images, absOutputDir, prefix);
                        if (result.Count > 0)
                            return result;
                    }

                    // 2. last_response.quantized_image (rgba_bytes with dimensions)
                    JToken quantized = lastResp["quantized_image"];
                    if (quantized != null && quantized.Type == JTokenType.Object
                        && quantized["base64"] != null && quantized["width"] != null)
                    {
                        int w = quantized["width"].Value<int>();
                        int h = quantized["height"]?.Value<int>() ?? w;
                        string path = SaveRgbaImage(quantized["base64"].ToString(), w, h,
                            absOutputDir, $"{prefix}_0");
                        if (path != null)
                            return new List<string> { path };
                    }

                    // 3. last_response.image (single image, could be rgba_bytes or base64)
                    JToken singleImg = lastResp["image"];
                    if (singleImg != null && singleImg.Type == JTokenType.Object
                        && singleImg["base64"] != null)
                    {
                        string imgType = singleImg["type"]?.ToString() ?? "";
                        string path;
                        if (imgType == "rgba_bytes" && singleImg["width"] != null)
                        {
                            int w = singleImg["width"].Value<int>();
                            int h = singleImg["height"]?.Value<int>() ?? w;
                            path = SaveRgbaImage(singleImg["base64"].ToString(), w, h,
                                absOutputDir, $"{prefix}_0");
                        }
                        else
                        {
                            path = SaveRawBase64(singleImg["base64"].ToString(),
                                absOutputDir, $"{prefix}_0");
                        }
                        if (path != null)
                            return new List<string> { path };
                    }
                }
            }

            // General data handling (no last_response, or last_response had nothing)
            var imageList = new List<JToken>();

            if (data.Type == JTokenType.Object)
            {
                // Single image at root: data has base64 directly
                if (data["base64"] != null)
                {
                    imageList.Add(data);
                }
                // Single image field (pixflux/bitforge sync response)
                else if (data["image"] != null && data["image"].Type == JTokenType.Object)
                {
                    imageList.Add(data["image"]);
                }
                // List of images in data.images
                else if (data["images"] != null)
                {
                    var result = ExtractImages(data["images"], absOutputDir, prefix);
                    if (result.Count > 0)
                        return result;
                    // If ExtractImages returned empty but images exists as a list, collect items
                    if (data["images"].Type == JTokenType.Array)
                    {
                        foreach (JToken img in data["images"])
                            imageList.Add(img);
                    }
                }
                // Frames from animation
                else if (data["frames"] != null && data["frames"].Type == JTokenType.Array)
                {
                    foreach (JToken frame in data["frames"])
                        imageList.Add(frame);
                }
                // Nested data — recurse (Python: return save_images_from_response(data["data"], ...))
                else if (data["data"] != null)
                {
                    return SaveImagesFromData(data["data"], absOutputDir, prefix);
                }
            }
            else if (data.Type == JTokenType.Array)
            {
                foreach (JToken item in data)
                    imageList.Add(item);
            }

            // Save collected images
            var saved = new List<string>();
            for (int i = 0; i < imageList.Count; i++)
            {
                JToken img = imageList[i];
                string path = SaveImageToken(img, absOutputDir, $"{prefix}_{i}");
                if (path != null)
                    saved.Add(path);
            }
            return saved;
        }

        /// <summary>
        /// Extract images from dict (direction-keyed) or list format.
        /// Mirrors Python _extract_images.
        /// </summary>
        private static List<string> ExtractImages(
            JToken imagesData, string absOutputDir, string prefix)
        {
            var saved = new List<string>();

            if (imagesData.Type == JTokenType.Object)
            {
                // Direction-keyed: {"south": {...}, "north": {...}, ...}
                foreach (var prop in ((JObject)imagesData).Properties())
                {
                    JToken img = prop.Value;
                    if (img.Type == JTokenType.Object && img["base64"] != null)
                    {
                        string safeName = prop.Name.Replace("-", "_");
                        string path = SaveImageToken(img, absOutputDir, $"{prefix}_{safeName}");
                        if (path != null)
                            saved.Add(path);
                    }
                }
            }
            else if (imagesData.Type == JTokenType.Array)
            {
                int i = 0;
                foreach (JToken img in imagesData)
                {
                    string path = SaveImageToken(img, absOutputDir, $"{prefix}_{i}");
                    if (path != null)
                    {
                        saved.Add(path);
                        i++;
                    }
                }
            }

            return saved;
        }

        /// <summary>
        /// Save a single base64-encoded image string to disk.
        /// </summary>
        public static string SaveBase64Image(
            string base64Data, string outputDir, string filename)
        {
            if (string.IsNullOrEmpty(base64Data))
                throw new ArgumentNullException(nameof(base64Data));
            if (string.IsNullOrEmpty(outputDir))
                throw new ArgumentNullException(nameof(outputDir));
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentNullException(nameof(filename));

            string absOutputDir = ResolveAbsolutePath(outputDir);
            Directory.CreateDirectory(absOutputDir);

            try
            {
                byte[] bytes = Convert.FromBase64String(base64Data);
                string filePath = Path.Combine(absOutputDir, $"{filename}.png");
                File.WriteAllBytes(filePath, bytes);

                string projectRelative = AbsoluteToAssetPath(filePath);
                AssetDatabase.ImportAsset(projectRelative,
                    ImportAssetOptions.ForceUpdate);

                return projectRelative;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[PixelLab] SaveBase64Image failed for '{filename}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert a raw RGBA bytes payload (base64-encoded) to a PNG byte array.
        /// </summary>
        public static byte[] RgbaBytesToPng(string base64Data, int width, int height)
        {
            if (string.IsNullOrEmpty(base64Data))
                throw new ArgumentNullException(nameof(base64Data));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

            byte[] rawBytes = Convert.FromBase64String(base64Data);

            int expected = width * height * 4;
            if (rawBytes.Length != expected)
            {
                throw new InvalidOperationException(
                    $"[PixelLab] RgbaBytesToPng: expected {expected} bytes for " +
                    $"{width}x{height} RGBA image, got {rawBytes.Length}.");
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(rawBytes);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(tex);
            else
                UnityEngine.Object.DestroyImmediate(tex);

            return png;
        }

        /// <summary>
        /// Read image dimensions from a file.
        /// </summary>
        public static void GetImageSize(string path, out int width, out int height)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string absolutePath = ResolveAbsolutePath(path);

            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"Image file not found: {absolutePath}");

            byte[] bytes = File.ReadAllBytes(absolutePath);
            var tex = new Texture2D(1, 1);
            tex.LoadImage(bytes);

            width = tex.width;
            height = tex.height;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(tex);
            else
                UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Trigger an AssetDatabase.Refresh for output directory.
        /// </summary>
        public static void RefreshAssets(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return;

            string absDir = ResolveAbsolutePath(dir);
            if (!Directory.Exists(absDir))
                return;

            AssetDatabase.Refresh(ImportAssetOptions.Default);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Resolve a project-relative or absolute path to absolute.
        /// </summary>
        private static string ResolveAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        /// <summary>
        /// Save a single ImageData token to disk.
        /// Handles: JObject with base64 (type optional, defaults to "base64"),
        ///          JValue string (raw base64).
        /// Python: height falls back to width for square images.
        /// </summary>
        private static string SaveImageToken(
            JToken token, string absOutputDir, string filename)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            // Handle raw base64 string (Python: isinstance(img, str))
            if (token.Type == JTokenType.String)
            {
                return SaveRawBase64(token.ToString(), absOutputDir, filename);
            }

            if (token.Type != JTokenType.Object)
                return null;

            string base64 = token["base64"]?.ToString();
            if (string.IsNullOrEmpty(base64))
                return null;

            // type is optional — Python uses img.get("type", "")
            string type = token["type"]?.ToString() ?? "";

            try
            {
                byte[] imageBytes;

                if (type == "rgba_bytes" && token["width"] != null)
                {
                    int width = token["width"].Value<int>();
                    // Python: h = img.get("height", w) — fallback to width
                    int height = token["height"]?.Value<int>() ?? width;

                    if (width <= 0 || height <= 0)
                    {
                        Debug.LogError(
                            $"[PixelLab] rgba_bytes image '{filename}' has invalid dimensions.");
                        return null;
                    }

                    imageBytes = RgbaBytesToPng(base64, width, height);
                }
                else
                {
                    // base64 type or unknown — just decode raw bytes
                    imageBytes = Convert.FromBase64String(base64);
                }

                string format = token["format"]?.ToString();
                string ext = (format == "jpeg" || format == "jpg") ? "jpg" : "png";

                string filePath = Path.Combine(absOutputDir, $"{filename}.{ext}");
                File.WriteAllBytes(filePath, imageBytes);

                return AbsoluteToAssetPath(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[PixelLab] Failed to save image '{filename}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save raw RGBA bytes as PNG.
        /// </summary>
        private static string SaveRgbaImage(
            string base64Data, int width, int height, string absOutputDir, string filename)
        {
            try
            {
                byte[] png = RgbaBytesToPng(base64Data, width, height);
                string filePath = Path.Combine(absOutputDir, $"{filename}.png");
                File.WriteAllBytes(filePath, png);
                return AbsoluteToAssetPath(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PixelLab] SaveRgbaImage failed for '{filename}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save a raw base64 string as PNG (Python: save_base64_image(img, path)).
        /// </summary>
        private static string SaveRawBase64(
            string base64Data, string absOutputDir, string filename)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64Data);
                string filePath = Path.Combine(absOutputDir, $"{filename}.png");
                File.WriteAllBytes(filePath, bytes);
                return AbsoluteToAssetPath(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PixelLab] SaveRawBase64 failed for '{filename}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert absolute path to project-relative Unity asset path.
        /// </summary>
        private static string AbsoluteToAssetPath(string absolutePath)
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string normalized = absolutePath.Replace('\\', '/');

            string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);

            if (normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(projectRoot.Length);

            return absolutePath;
        }
    }
}
#endif
