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
        /// Pure C# implementation — safe to call from background threads.
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

            // Build filtered scanlines: prepend 0 (None filter) to each row
            int stride = width * 4;
            byte[] filtered = new byte[height * (stride + 1)];
            for (int y = 0; y < height; y++)
            {
                filtered[y * (stride + 1)] = 0; // filter type: None
                Array.Copy(rawBytes, y * stride, filtered, y * (stride + 1) + 1, stride);
            }

            // Compress with zlib (2-byte header + DEFLATE + Adler32)
            byte[] compressed = ZlibCompress(filtered);

            // Assemble PNG
            using var png = new MemoryStream();
            // Signature
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            // IHDR chunk
            var ihdr = new byte[13];
            WriteInt32BE(ihdr, 0, width);
            WriteInt32BE(ihdr, 4, height);
            ihdr[8] = 8;  // bit depth
            ihdr[9] = 6;  // color type: RGBA
            ihdr[10] = 0; // compression
            ihdr[11] = 0; // filter
            ihdr[12] = 0; // interlace: none
            WritePngChunk(png, new byte[] { 0x49, 0x48, 0x44, 0x52 }, ihdr); // "IHDR"
            // IDAT chunk
            WritePngChunk(png, new byte[] { 0x49, 0x44, 0x41, 0x54 }, compressed); // "IDAT"
            // IEND chunk
            WritePngChunk(png, new byte[] { 0x49, 0x45, 0x4E, 0x44 }, Array.Empty<byte>()); // "IEND"
            return png.ToArray();
        }

        /// <summary>
        /// Read image dimensions from a file.
        /// Pure C# header parser — safe to call from background threads.
        /// Supports PNG, JPEG, and WebP formats.
        /// </summary>
        public static void GetImageSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string absolutePath = ResolveAbsolutePath(path);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"Image file not found: {absolutePath}");

            using var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[30];
            int read = fs.Read(header, 0, header.Length);
            if (read < 4) return;

            // PNG: signature 8 bytes, then IHDR chunk (4 len + 4 type + width[4] + height[4])
            if (read >= 24
                && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            {
                width  = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return;
            }

            // JPEG: starts with FF D8
            if (read >= 2 && header[0] == 0xFF && header[1] == 0xD8)
            {
                fs.Seek(0, SeekOrigin.Begin);
                byte[] allBytes = new byte[fs.Length];
                fs.Read(allBytes, 0, allBytes.Length);
                for (int i = 2; i < allBytes.Length - 8; i++)
                {
                    if (allBytes[i] != 0xFF) continue;
                    byte marker = allBytes[i + 1];
                    // SOF0 (0xC0), SOF1 (0xC1), SOF2 (0xC2)
                    if (marker == 0xC0 || marker == 0xC1 || marker == 0xC2)
                    {
                        height = (allBytes[i + 5] << 8) | allBytes[i + 6];
                        width  = (allBytes[i + 7] << 8) | allBytes[i + 8];
                        return;
                    }
                    // Skip this segment
                    if (i + 3 < allBytes.Length)
                    {
                        int segLen = (allBytes[i + 2] << 8) | allBytes[i + 3];
                        i += segLen + 1;
                    }
                }
                return;
            }

            // WebP: RIFF....WEBP
            if (read >= 12
                && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
            {
                // VP8 (lossy): "VP8 " chunk at offset 12
                if (read >= 30 && header[12] == 'V' && header[13] == 'P' && header[14] == '8' && header[15] == ' ')
                {
                    width  = ((header[27] << 8) | header[26]) & 0x3FFF;
                    height = ((header[29] << 8) | header[28]) & 0x3FFF;
                    return;
                }
                // VP8L (lossless): "VP8L" chunk
                if (read >= 25 && header[12] == 'V' && header[13] == 'P' && header[14] == '8' && header[15] == 'L')
                {
                    if (header[20] == 0x2F)
                    {
                        uint bits = (uint)(header[21] | (header[22] << 8) | (header[23] << 16) | (header[24] << 24));
                        width  = (int)(bits & 0x3FFF) + 1;
                        height = (int)((bits >> 14) & 0x3FFF) + 1;
                        return;
                    }
                }
            }

            // Fallback: unknown format, dimensions remain 0
            Debug.LogWarning($"[PixelLab] GetImageSize: unrecognized format for '{Path.GetFileName(absolutePath)}'. Dimensions will be 0.");
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

        // -----------------------------------------------------------------------
        // PNG encoding helpers (pure C#, no Unity API — thread-safe)
        // -----------------------------------------------------------------------

        private static byte[] ZlibCompress(byte[] data)
        {
            using var result = new MemoryStream();
            // zlib header: CMF=0x78 (deflate, window=32K), FLG=0x9C (check bits, no dict, default compression)
            result.WriteByte(0x78);
            result.WriteByte(0x9C);
            using (var deflate = new System.IO.Compression.DeflateStream(result, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }
            // Adler32 checksum (big-endian)
            uint adler = Adler32(data);
            result.WriteByte((byte)(adler >> 24));
            result.WriteByte((byte)(adler >> 16));
            result.WriteByte((byte)(adler >> 8));
            result.WriteByte((byte)adler);
            return result.ToArray();
        }

        private static void WritePngChunk(MemoryStream stream, byte[] typeBytes, byte[] data)
        {
            int length = data.Length;
            // Length (4 bytes, big-endian)
            stream.WriteByte((byte)(length >> 24));
            stream.WriteByte((byte)(length >> 16));
            stream.WriteByte((byte)(length >> 8));
            stream.WriteByte((byte)length);
            // Type (4 bytes)
            stream.Write(typeBytes, 0, 4);
            // Data
            if (data.Length > 0)
                stream.Write(data, 0, data.Length);
            // CRC32 over type + data
            uint crc = Crc32(typeBytes, 0xFFFFFFFF);
            if (data.Length > 0)
                crc = Crc32Append(data, crc);
            crc ^= 0xFFFFFFFF;
            stream.WriteByte((byte)(crc >> 24));
            stream.WriteByte((byte)(crc >> 16));
            stream.WriteByte((byte)(crc >> 8));
            stream.WriteByte((byte)crc);
        }

        private static void WriteInt32BE(byte[] buf, int offset, int value)
        {
            buf[offset]     = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)value;
        }

        private static uint Adler32(byte[] data)
        {
            const uint MOD_ADLER = 65521;
            uint a = 1, b = 0;
            foreach (byte bt in data)
            {
                a = (a + bt) % MOD_ADLER;
                b = (b + a) % MOD_ADLER;
            }
            return (b << 16) | a;
        }

        private static readonly uint[] Crc32Table = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                table[i] = c;
            }
            return table;
        }

        private static uint Crc32(byte[] data, uint crc)
        {
            foreach (byte b in data)
                crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static uint Crc32Append(byte[] data, uint crc)
        {
            foreach (byte b in data)
                crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }
    }
}
#endif
