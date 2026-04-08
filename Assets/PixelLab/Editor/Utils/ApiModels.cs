#if UNITY_EDITOR
using System.Collections.Generic;

namespace PixelLab.Editor
{
    // ---------------------------------------------------------------------------
    // Shared primitives
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class ImageSize
    {
        public int width;
        public int height;

        public ImageSize() { }

        public ImageSize(int width, int height)
        {
            this.width  = width;
            this.height = height;
        }
    }

    [System.Serializable]
    public class ImageData
    {
        /// <summary>"base64" is the only type currently used by the API.</summary>
        public string type;
        public string base64;
        /// <summary>e.g. "png", "webp"</summary>
        public string format;
        public int    width;
        public int    height;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Generation
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class GenerateImageRequest
    {
        public string    description;
        public ImageSize image_size;
        public int       seed;
        public bool      no_background;
        public string    negative_description;
        public float     text_guidance_scale;
        /// <summary>e.g. "none", "thin", "slightly thin", "medium", "slightly thick", "thick", "very thick"</summary>
        public string    outline;
        /// <summary>e.g. "none", "basic", "side", "soft", "cel", "hard"</summary>
        public string    shading;
        /// <summary>e.g. "none", "low", "medium", "high", "very high"</summary>
        public string    detail;
        /// <summary>e.g. "front", "side", "three-quarter", "back"</summary>
        public string    view;
        /// <summary>e.g. "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east"</summary>
        public string    direction;
        public bool      isometric;
    }

    [System.Serializable]
    public class GenerateWithStyleRequest
    {
        public string      description;
        public ImageSize   image_size;
        public ImageData[] style_images;
    }

    [System.Serializable]
    public class GenerateUIRequest
    {
        public string    description;
        public ImageSize image_size;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Characters
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class CreateCharacter4DirRequest
    {
        public string    description;
        public ImageSize image_size;
    }

    [System.Serializable]
    public class CreateCharacter8DirRequest
    {
        public string    description;
        public ImageSize image_size;
    }

    [System.Serializable]
    public class CharacterAnimationRequest
    {
        public string character_id;
        public string template_animation_id;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Animation
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class AnimateWithTextV3Request
    {
        public ImageData reference_image;
        public string    action;
        public ImageSize image_size;
        public ImageSize reference_image_size;
    }

    [System.Serializable]
    public class AnimateWithSkeletonRequest
    {
        public ImageData reference_image;
        public ImageSize image_size;
    }

    [System.Serializable]
    public class InterpolationRequest
    {
        public ImageData start_image;
        public ImageData end_image;
        public string    action;
        public ImageSize image_size;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Editing
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class EditImagesV2Request
    {
        public EditImageItem[] edit_images;
        public ImageSize       image_size;
    }

    [System.Serializable]
    public class EditImageItem
    {
        public ImageData image;
        public string    description;
    }

    [System.Serializable]
    public class InpaintV3Request
    {
        public string    description;
        public ImageData inpainting_image;
        public ImageData mask_image;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Tilesets
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class CreateTilesetRequest
    {
        public string lower_description;
        public string upper_description;
    }

    [System.Serializable]
    public class CreateTilesetSidescrollerRequest
    {
        public string lower_description;
    }

    [System.Serializable]
    public class CreateIsometricTileRequest
    {
        public string    description;
        public ImageSize image_size;
    }

    [System.Serializable]
    public class CreateTilesProRequest
    {
        public string description;
        /// <summary>"hex", "square", or "isometric"</summary>
        public string tile_type;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Objects & Rotations
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class CreateMapObjectRequest
    {
        public string    description;
        public ImageSize image_size;
    }

    [System.Serializable]
    public class CreateObject4DirRequest
    {
        public string    description;
        public ImageSize image_size;
    }

    [System.Serializable]
    public class RotateRequest
    {
        public ImageData from_image;
        public ImageSize image_size;
        public string    from_direction;
        public string    to_direction;
    }

    [System.Serializable]
    public class Generate8RotationsRequest
    {
        public ImageSize image_size;
        public ImageData reference_image;
        public string    description;
        /// <summary>"reference" or "text"</summary>
        public string    generation_method;
    }

    // ---------------------------------------------------------------------------
    // Responses
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class BalanceResponse
    {
        public float balance;
        public float usd_balance;
    }

    [System.Serializable]
    public class ApiResponse
    {
        public string          status;
        public string          background_job_id;
        public string[]        background_job_ids;
        public ApiResponseData data;
    }

    [System.Serializable]
    public class ApiResponseData
    {
        public string     background_job_id;
        public ImageData  image;
        public ImageData[] images;
        public ImageData[] frames;
    }

    [System.Serializable]
    public class JobResponse
    {
        public string          status;
        public JobLastResponse last_response;
    }

    [System.Serializable]
    public class JobLastResponse
    {
        public string          type;
        public string          status;
        public ImageData       image;
        public ImageData[]     images;
        public ImageData[]     frames;
        public DirectionImages direction_images;
    }

    [System.Serializable]
    public class DirectionImages
    {
        public ImageData south;
        public ImageData north;
        public ImageData east;
        public ImageData west;
        public ImageData south_east;
        public ImageData south_west;
        public ImageData north_east;
        public ImageData north_west;
    }

    [System.Serializable]
    public class CharacterListResponse
    {
        public CharacterItem[] data;
    }

    [System.Serializable]
    public class CharacterItem
    {
        public string id;
        public string description;
        public string created_at;
    }

    // ---------------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------------

    public static class PixelLabConstants
    {
        public const string BASE_URL = "https://api.pixellab.ai/v2";

        public static readonly string[] ANIMATION_TEMPLATES =
        {
            "breathing-idle",
            "walking",
            "running",
            "jumping",
            "attack",
            "slash",
            "cross-punch",
            "flying-kick",
            "casting-spell",
            "fireball",
            "shooting-arrow",
            "crouching",
            "crouched-walking",
            "drinking",
            "falling-back-death",
            "death",
            "hurt",
            "fight-stance-idle-8-frames",
            "backflip"
        };

        public static readonly string[] SIZE_PRESETS =
        {
            "32x32",
            "48x48",
            "64x64",
            "96x96",
            "128x128",
            "256x256"
        };

        /// <summary>
        /// Approximate credit costs per operation.
        /// Keys match the operation identifiers used in the Python SDK.
        /// </summary>
        public static readonly Dictionary<string, string> OPERATION_COSTS =
            new Dictionary<string, string>
            {
                // Generation
                { "generate_image",              "1–5 credits" },
                { "generate_with_style",         "5 credits"   },
                { "generate_ui",                 "3 credits"   },

                // Characters
                { "create_character_4dir",       "8 credits"   },
                { "create_character_8dir",       "16 credits"  },
                { "character_animation",         "10 credits"  },

                // Animation
                { "animate_with_text_v3",        "5 credits"   },
                { "animate_with_skeleton",       "5 credits"   },
                { "interpolation",               "3 credits"   },

                // Editing
                { "edit_images_v2",              "3 credits"   },
                { "inpaint_v3",                  "3 credits"   },

                // Tilesets
                { "create_tileset",              "10 credits"  },
                { "create_tileset_sidescroller", "5 credits"   },
                { "create_isometric_tile",       "3 credits"   },
                { "create_tiles_pro",            "5 credits"   },

                // Objects & Rotations
                { "create_map_object",           "3 credits"   },
                { "create_object_4dir",          "8 credits"   },
                { "rotate",                      "2 credits"   },
                { "generate_8_rotations",        "8 credits"   },
            };
    }
}

#endif
