#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PixelLab.Editor
{
    public class PixelLabException : Exception
    {
        public int StatusCode;
        public PixelLabException(int statusCode, string message) : base($"[{statusCode}] {message}") { StatusCode = statusCode; }
    }

    public class PixelLabClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        public PixelLabClient(string apiKey, string baseUrl = "https://api.pixellab.ai/v2")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private async Task<JObject> PostJson(string path, JObject payload)
        {
            string url = _baseUrl + path;
            string json = payload?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _http.PostAsync(url, content);
            return await HandleResponse(response);
        }

        private async Task<JObject> PatchJson(string path, JObject payload)
        {
            string url = _baseUrl + path;
            string json = payload?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            HttpResponseMessage response = await _http.SendAsync(request);
            return await HandleResponse(response);
        }

        private async Task<JObject> GetJson(string path, Dictionary<string, string> queryParams = null)
        {
            string url = _baseUrl + path;
            if (queryParams != null && queryParams.Count > 0)
            {
                var sb = new StringBuilder("?");
                bool first = true;
                foreach (var kv in queryParams)
                {
                    if (!first) sb.Append('&');
                    sb.Append(Uri.EscapeDataString(kv.Key));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(kv.Value));
                    first = false;
                }
                url += sb.ToString();
            }
            HttpResponseMessage response = await _http.GetAsync(url);
            return await HandleResponse(response);
        }

        private async Task<JObject> DeleteJson(string path)
        {
            string url = _baseUrl + path;
            HttpResponseMessage response = await _http.DeleteAsync(url);
            return await HandleResponse(response);
        }

        private static async Task<JObject> HandleResponse(HttpResponseMessage response)
        {
            string body = await response.Content.ReadAsStringAsync();
            int statusCode = (int)response.StatusCode;

            if (statusCode == 200 || statusCode == 202)
            {
                if (string.IsNullOrWhiteSpace(body)) return new JObject();
                try { return JObject.Parse(body); }
                catch { return new JObject { ["raw"] = body }; }
            }

            string errorMessage = body;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var errorObj = JObject.Parse(body);
                    if (errorObj["error"] != null)
                        errorMessage = errorObj["error"].ToString();
                    else if (errorObj["detail"] != null)
                        errorMessage = errorObj["detail"].ToString();
                }
                catch
                {
                    // fallback to raw text
                }
            }

            throw new PixelLabException(statusCode, errorMessage);
        }

        private static JObject MergeExtras(JObject payload, JObject extraParams)
        {
            if (extraParams == null) return payload;
            foreach (var prop in extraParams.Properties())
                payload[prop.Name] = prop.Value;
            return payload;
        }

        private static JObject ImageSize(int width, int height) =>
            new JObject { ["width"] = width, ["height"] = height };

        // -------------------------------------------------------------------------
        // Account
        // -------------------------------------------------------------------------

        public Task<JObject> GetBalance() =>
            GetJson("/balance");

        // -------------------------------------------------------------------------
        // Background Jobs
        // -------------------------------------------------------------------------

        public Task<JObject> GetJob(string jobId) =>
            GetJson($"/background-jobs/{jobId}");

        public async Task<JObject> WaitForJob(string jobId, float pollIntervalSeconds = 2f)
        {
            await Task.Delay(1000);

            int retries403 = 0;
            int retries404 = 0;
            const int maxRetries = 3;

            while (true)
            {
                JObject result;
                try
                {
                    result = await GetJob(jobId);
                }
                catch (PixelLabException ex) when (ex.StatusCode == 403 && retries403 < maxRetries)
                {
                    retries403++;
                    await Task.Delay((int)(pollIntervalSeconds * 1000));
                    continue;
                }
                catch (PixelLabException ex) when (ex.StatusCode == 404 && retries404 < maxRetries)
                {
                    retries404++;
                    await Task.Delay((int)(pollIntervalSeconds * 1000));
                    continue;
                }

                retries403 = 0;
                retries404 = 0;

                string status = result["status"]?.ToString() ?? string.Empty;
                string statusLower = status.ToLowerInvariant();

                if (statusLower == "completed" || statusLower == "complete" ||
                    statusLower == "done" || statusLower == "success" || statusLower == "finished")
                {
                    return result;
                }

                if (statusLower == "failed" || statusLower == "error" || statusLower == "cancelled")
                {
                    var dataObj   = result["data"] as JObject;
                    var lastResp  = (result["last_response"] as JObject)
                                 ?? (dataObj?["last_response"] as JObject)
                                 ?? new JObject();
                    string detail = lastResp["detail"]?.ToString()
                                 ?? lastResp["error"]?.ToString()
                                 ?? string.Empty;
                    string msg;
                    if (detail.Contains("429") || detail.ToLowerInvariant().Contains("job limits"))
                    {
                        msg = "Job limit exceeded!\n\nYou have reached the concurrent job or daily limit of your PixelLab subscription plan.\nPlease check your remaining credits on the dashboard or try again later.";
                    }
                    else
                    {
                        msg = detail.Length > 0 ? detail : $"Job failed (status: {status})";
                    }
                    throw new PixelLabException(0, msg);
                }

                string lastResponseType = result["last_response"]?["type"]?.ToString()?.ToLowerInvariant() ?? string.Empty;
                if (lastResponseType == "message_done" || lastResponseType == "done" || lastResponseType == "completed")
                {
                    return result;
                }

                await Task.Delay((int)(pollIntervalSeconds * 1000));
            }
        }

        // -------------------------------------------------------------------------
        // Animate
        // -------------------------------------------------------------------------

        public Task<JObject> EditAnimationV2(string description, JArray frames, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["frames"] = frames,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/edit-animation-v2", payload);
        }

        public Task<JObject> Interpolation(JObject startImage, JObject endImage, string action, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["start_image"] = startImage,
                ["end_image"] = endImage,
                ["action"] = action,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/interpolation-v2", payload);
        }

        public Task<JObject> TransferOutfitV2(JObject referenceImage, JArray frames, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["reference_image"] = referenceImage,
                ["frames"] = frames,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/transfer-outfit-v2", payload);
        }

        public Task<JObject> AnimateWithSkeleton(JObject referenceImage, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["reference_image"] = referenceImage,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/animate-with-skeleton", payload);
        }

        public Task<JObject> AnimateWithText(string description, string action, JObject referenceImage, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["action"] = action,
                ["reference_image"] = referenceImage,
                ["image_size"] = ImageSize(64, 64)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/animate-with-text", payload);
        }

        public Task<JObject> AnimateWithTextV2(JObject referenceImage, string action, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["reference_image"] = referenceImage,
                ["action"] = action,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            // Default reference_image_size to output size if not provided (matches Python client behaviour)
            if (payload["reference_image_size"] == null)
                payload["reference_image_size"] = ImageSize(width, height);
            return PostJson("/animate-with-text-v2", payload);
        }

        public Task<JObject> AnimateWithTextV3(JObject firstFrame, string action, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["first_frame"] = firstFrame,
                ["action"] = action
            };
            MergeExtras(payload, extraParams);
            return PostJson("/animate-with-text-v3", payload);
        }

        public Task<JObject> EstimateSkeleton(JObject image)
        {
            var payload = new JObject
            {
                ["image"] = image
            };
            return PostJson("/estimate-skeleton", payload);
        }

        public Task<JObject> Generate8RotationsV3(JObject firstFrame, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["first_frame"] = firstFrame
            };
            MergeExtras(payload, extraParams);
            return PostJson("/generate-8-rotations-v3", payload);
        }

        // -------------------------------------------------------------------------
        // Character Management
        // -------------------------------------------------------------------------

        public Task<JObject> ListCharacters(int limit = 20, int offset = 0) =>
            GetJson("/characters", new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(),
                ["offset"] = offset.ToString()
            });

        public Task<JObject> GetCharacter(string characterId) =>
            GetJson($"/characters/{characterId}");

        public Task<JObject> DeleteCharacter(string characterId) =>
            DeleteJson($"/characters/{characterId}");

        public async Task<byte[]> ExportCharacterZip(string characterId)
        {
            string url = _baseUrl + $"/characters/{characterId}/zip";
            HttpResponseMessage response = await _http.GetAsync(url);
            int statusCode = (int)response.StatusCode;
            if (statusCode != 200 && statusCode != 202)
            {
                string body = await response.Content.ReadAsStringAsync();
                string errorMessage = body;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        var errorObj = JObject.Parse(body);
                        if (errorObj["error"] != null) errorMessage = errorObj["error"].ToString();
                        else if (errorObj["detail"] != null) errorMessage = errorObj["detail"].ToString();
                    }
                    catch { }
                }
                throw new PixelLabException(statusCode, errorMessage);
            }
            return await response.Content.ReadAsByteArrayAsync();
        }

        public Task<JObject> UpdateCharacterTags(string characterId, string[] tags)
        {
            var tagsArr = new JArray();
            if (tags != null)
            {
                foreach (var t in tags) tagsArr.Add(t);
            }
            var payload = new JObject
            {
                ["tags"] = tagsArr
            };
            return PatchJson($"/characters/{characterId}/tags", payload);
        }

        // -------------------------------------------------------------------------
        // Character From Template
        // -------------------------------------------------------------------------

        public Task<JObject> CreateCharacter4Dir(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-character-with-4-directions", payload);
        }

        public Task<JObject> CreateCharacter8Dir(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-character-with-8-directions", payload);
        }

        public Task<JObject> CreateCharacterAnimation(string characterId, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["character_id"] = characterId
            };
            MergeExtras(payload, extraParams);
            return PostJson("/characters/animations", payload);
        }

        // Backwards-compatible overload: keeps the original 2-arg signature alive for existing panels.
        public Task<JObject> CreateCharacterAnimation(string characterId, string templateAnimationId)
        {
            var extras = new JObject
            {
                ["template_animation_id"] = templateAnimationId
            };
            return CreateCharacterAnimation(characterId, extras);
        }

        public Task<JObject> AnimateCharacter(string characterId, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["character_id"] = characterId
            };
            MergeExtras(payload, extraParams);
            return PostJson("/animate-character", payload);
        }

        // -------------------------------------------------------------------------
        // Create Image
        // -------------------------------------------------------------------------

        public Task<JObject> GenerateImage(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/generate-image-v2", payload);
        }

        public Task<JObject> GenerateWithStyle(string description, JArray styleImages, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["style_images"] = styleImages,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/generate-with-style-v2", payload);
        }

        public Task<JObject> GenerateUI(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/generate-ui-v2", payload);
        }

        public Task<JObject> GenerateImagePixflux(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-image-pixflux", payload);
        }

        public Task<JObject> GenerateImagePixen(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-image-pixen", payload);
        }

        public Task<JObject> GenerateImageBitforge(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-image-bitforge", payload);
        }

        // -------------------------------------------------------------------------
        // Create Map
        // -------------------------------------------------------------------------

        public Task<JObject> CreateTileset(string lowerDescription, string upperDescription, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["lower_description"] = lowerDescription,
                ["upper_description"] = upperDescription
            };
            MergeExtras(payload, extraParams);
            return PostJson("/tilesets", payload);
        }

        public Task<JObject> ListTilesets(int limit = 20, int offset = 0) =>
            GetJson("/tilesets", new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(),
                ["offset"] = offset.ToString()
            });

        public Task<JObject> GetTileset(string tilesetId) =>
            GetJson($"/tilesets/{tilesetId}");

        public Task<JObject> CreateTilesetTopdown(string lowerDescription, string upperDescription, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["lower_description"] = lowerDescription,
                ["upper_description"] = upperDescription
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-tileset", payload);
        }

        public Task<JObject> CreateTilesetSidescroller(string lowerDescription, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["lower_description"] = lowerDescription
            };
            MergeExtras(payload, extraParams);
            return PostJson("/tilesets-sidescroller", payload);
        }

        public Task<JObject> CreateTilesetSidescrollerV2(string lowerDescription, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["lower_description"] = lowerDescription
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-tileset-sidescroller", payload);
        }

        public Task<JObject> CreateIsometricTile(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-isometric-tile", payload);
        }

        public Task<JObject> GetIsometricTile(string tileId) =>
            GetJson($"/isometric-tiles/{tileId}");

        public Task<JObject> ListIsometricTiles(int limit = 20, int offset = 0) =>
            GetJson("/isometric-tiles", new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(),
                ["offset"] = offset.ToString()
            });

        public Task<JObject> CreateTilesPro(string description, string tileType = "isometric", JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["tile_type"] = tileType
            };
            MergeExtras(payload, extraParams);
            return PostJson("/create-tiles-pro", payload);
        }

        public Task<JObject> GetTilesPro(string tileId) =>
            GetJson($"/tiles-pro/{tileId}");

        // -------------------------------------------------------------------------
        // Documentation
        // -------------------------------------------------------------------------

        public async Task<string> GetLlmsTxt()
        {
            string url = _baseUrl + "/llms.txt";
            HttpResponseMessage response = await _http.GetAsync(url);
            int statusCode = (int)response.StatusCode;
            string body = await response.Content.ReadAsStringAsync();
            if (statusCode != 200 && statusCode != 202)
            {
                throw new PixelLabException(statusCode, body);
            }
            return body;
        }

        // -------------------------------------------------------------------------
        // Edit
        // -------------------------------------------------------------------------

        public Task<JObject> EditImagesV2(JArray editImages, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["edit_images"] = editImages,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/edit-images-v2", payload);
        }

        public Task<JObject> EditImage(JObject image, string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["image"] = image,
                ["description"] = description,
                ["image_size"] = ImageSize(width, height),
                ["width"] = width,
                ["height"] = height
            };
            MergeExtras(payload, extraParams);
            return PostJson("/edit-image", payload);
        }

        // -------------------------------------------------------------------------
        // Image Operations
        // -------------------------------------------------------------------------

        public Task<JObject> ImageToPixelart(JObject image, int inputWidth, int inputHeight, int outputWidth, int outputHeight, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["image"] = image,
                ["image_size"] = ImageSize(inputWidth, inputHeight),
                ["output_size"] = ImageSize(outputWidth, outputHeight)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/image-to-pixelart", payload);
        }

        public Task<JObject> Resize(string description, JObject referenceImage, int refWidth, int refHeight, int targetWidth, int targetHeight, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["reference_image"] = referenceImage,
                ["reference_image_size"] = ImageSize(refWidth, refHeight),
                ["target_size"] = ImageSize(targetWidth, targetHeight)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/resize", payload);
        }

        public Task<JObject> RemoveBackground(JObject image, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["image"] = image,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/remove-background", payload);
        }

        // -------------------------------------------------------------------------
        // Inpaint
        // -------------------------------------------------------------------------

        public Task<JObject> InpaintV3(string description, JObject inpaintingImage, JObject maskImage, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["inpainting_image"] = inpaintingImage,
                ["mask_image"] = maskImage
            };
            MergeExtras(payload, extraParams);
            return PostJson("/inpaint-v3", payload);
        }

        public Task<JObject> Inpaint(string description, int width, int height, JObject inpaintingImage, JObject maskImage, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height),
                ["inpainting_image"] = inpaintingImage,
                ["mask_image"] = maskImage
            };
            MergeExtras(payload, extraParams);
            return PostJson("/inpaint", payload);
        }

        // -------------------------------------------------------------------------
        // Map Objects
        // -------------------------------------------------------------------------

        public Task<JObject> CreateMapObject(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/map-objects", payload);
        }

        // -------------------------------------------------------------------------
        // Object Management
        // -------------------------------------------------------------------------

        public Task<JObject> ListObjects(int limit = 20, int offset = 0) =>
            GetJson("/objects", new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(),
                ["offset"] = offset.ToString()
            });

        public Task<JObject> GetObject(string id) =>
            GetJson($"/objects/{id}");

        public Task<JObject> DeleteObject(string id) =>
            DeleteJson($"/objects/{id}");

        public Task<JObject> UpdateObjectTags(string id, string[] tags)
        {
            var tagsArr = new JArray();
            if (tags != null)
            {
                foreach (var t in tags) tagsArr.Add(t);
            }
            var payload = new JObject
            {
                ["tags"] = tagsArr
            };
            return PatchJson($"/objects/{id}/tags", payload);
        }

        // -------------------------------------------------------------------------
        // Objects (modern)
        // -------------------------------------------------------------------------

        public Task<JObject> CreateObject(string description, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["description"] = description,
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/objects", payload);
        }

        public Task<JObject> AnimateObject(string objectId, string direction, string animationDescription, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["object_id"] = objectId,
                ["direction"] = direction,
                ["animation_description"] = animationDescription
            };
            MergeExtras(payload, extraParams);
            return PostJson("/animate-object", payload);
        }

        public Task<JObject> VaryObject(string objectId, string editDescription, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["object_id"] = objectId,
                ["edit_description"] = editDescription
            };
            MergeExtras(payload, extraParams);
            return PostJson("/vary-object", payload);
        }

        public Task<JObject> SelectObjectFrames(string objectId, int[] indices, string commonTag = null)
        {
            var indicesArr = new JArray();
            if (indices != null)
            {
                foreach (var i in indices) indicesArr.Add(i);
            }
            var payload = new JObject
            {
                ["indices"] = indicesArr
            };
            if (!string.IsNullOrEmpty(commonTag))
                payload["common_tag"] = commonTag;
            return PostJson($"/objects/{objectId}/select-frames", payload);
        }

        public Task<JObject> DismissObjectReview(string objectId) =>
            PostJson($"/objects/{objectId}/dismiss-review", new JObject());

        // -------------------------------------------------------------------------
        // Rotate
        // -------------------------------------------------------------------------

        public Task<JObject> Rotate(JObject fromImage, int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["from_image"] = fromImage,
                ["image_size"] = ImageSize(width, height),
            };
            MergeExtras(payload, extraParams);
            return PostJson("/rotate", payload);
        }

        public Task<JObject> Generate8RotationsV2(int width, int height, JObject extraParams = null)
        {
            var payload = new JObject
            {
                ["image_size"] = ImageSize(width, height)
            };
            MergeExtras(payload, extraParams);
            return PostJson("/generate-8-rotations-v2", payload);
        }

        // Backwards-compatible alias for the v2 endpoint.
        public Task<JObject> Generate8Rotations(int width, int height, JObject extraParams = null) =>
            Generate8RotationsV2(width, height, extraParams);

        // -------------------------------------------------------------------------
        // IDisposable
        // -------------------------------------------------------------------------

        public void Dispose() { _http.Dispose(); }
    }
}
#endif
