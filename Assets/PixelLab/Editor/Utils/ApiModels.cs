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

    [System.Serializable]
    public class CreateImagePixenRequest
    {
        public string    description;
        public ImageSize image_size;
        public string    outline;
        public string    detail;
        public string    view;
        public string    direction;
        public bool      no_background;
        public int       seed;
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

    [System.Serializable]
    public class AnimateCharacterRequest
    {
        public string character_id;
        public string animation_name;
        public string description;
        public string action_description;
        public bool   async_mode;
        public string mode;
        public string template_animation_id;
        public int    frame_count;
        public float  text_guidance_scale;
        public string outline;
        public string shading;
        public string detail;
        public bool   isometric;
        public int    seed;
    }

    // ---------------------------------------------------------------------------
    // Request payloads – Animation
    // ---------------------------------------------------------------------------

    [System.Serializable]
    public class AnimateWithTextRequest
    {
        /// <summary>Hardcoded to 64x64 per spec.</summary>
        public ImageSize image_size;
        public string    description;
        public string    action;
        public string    view;
        public string    direction;
        public ImageData reference_image;
        public string    negative_description;
        public float     text_guidance_scale;
        public float     image_guidance_scale;
        public int       n_frames;
        public int       start_frame_index;
        public ImageData[] init_images;
        public float     init_image_strength;
        public ImageData[] inpainting_images;
        public ImageData[] mask_images;
        public ImageData color_image;
        public int       seed;
    }

    [System.Serializable]
    public class AnimateWithTextV3Request
    {
        public ImageData first_frame;
        public ImageData last_frame;
        public string    action;
        public int       frame_count;
        public int       seed;
        public bool      no_background;
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
        public int       seed;
        public bool      no_background;
    }

    [System.Serializable]
    public class TransferOutfitV2Request
    {
        public ImageData reference_image;
        public ImageData[] frames;
        public ImageSize image_size;
        public int       seed;
        public bool      no_background;
    }

    [System.Serializable]
    public class EditAnimationV2Request
    {
        public string      description;
        public ImageData[] frames;
        public ImageSize   image_size;
        public int         seed;
        public bool        no_background;
    }

    [System.Serializable]
    public class EstimateSkeletonRequest
    {
        public ImageData image;
    }

    [System.Serializable]
    public class Generate8RotationsV3Request
    {
        public ImageData first_frame;
        public bool      no_background;
        public int       seed;
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
    public class EditImageRequest
    {
        public ImageData image;
        public ImageSize image_size;
        public string    description;
        public int       width;
        public int       height;
        public int       seed;
        public bool      no_background;
        public float     text_guidance_scale;
        public ImageData color_image;
    }

    [System.Serializable]
    public class InpaintV3Request
    {
        public string    description;
        public ImageData inpainting_image;
        public ImageData mask_image;
    }

    [System.Serializable]
    public class InpaintRequest
    {
        public string    description;
        public string    negative_description;
        public ImageSize image_size;
        public float     text_guidance_scale;
        public float     extra_guidance_scale;
        public string    outline;
        public string    shading;
        public string    detail;
        public string    view;
        public string    direction;
        public bool      isometric;
        public bool      oblique_projection;
        public bool      no_background;
        public ImageData init_image;
        public float     init_image_strength;
        public ImageData inpainting_image;
        public ImageData mask_image;
        public ImageData color_image;
        public int       seed;
    }

    [System.Serializable]
    public class RemoveBackgroundRequest
    {
        public ImageData image;
        public ImageSize image_size;
        public string    background_removal_task;
        public string    text;
        public int       seed;
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
    public class CreateObjectRequest
    {
        public string      description;
        public int         directions;
        public ImageSize   image_size;
        public string      view;
        public int         n_frames;
        public ImageData[] style_images;
        public string      object_view;
        public string[]    item_descriptions;
        public ImageData   reference_image;
        public bool        no_background;
        public int         seed;
        public string      variation_of;
        public string      group_id;
    }

    [System.Serializable]
    public class AnimateObjectRequest
    {
        public string object_id;
        public string direction;
        public string animation_description;
        public int    frame_count;
        public bool   no_background;
        public string animation_name;
        public bool   wait_for_source;
    }

    [System.Serializable]
    public class VaryObjectRequest
    {
        public string object_id;
        public string edit_description;
        public bool   no_background;
        public int    seed;
    }

    [System.Serializable]
    public class SelectFramesRequest
    {
        public int[]  indices;
        public string common_tag;
    }

    [System.Serializable]
    public class UpdateTagsRequest
    {
        public string[] tags;
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

    [System.Serializable]
    public class ObjectListResponse
    {
        public ObjectItem[] data;
    }

    [System.Serializable]
    public class ObjectItem
    {
        public string   id;
        public string   description;
        public string   created_at;
        public string[] tags;
    }

    [System.Serializable]
    public class TilesetListResponse
    {
        public TilesetItem[] data;
    }

    [System.Serializable]
    public class TilesetItem
    {
        public string id;
        public string description;
        public string created_at;
    }

    [System.Serializable]
    public class IsometricTileListResponse
    {
        public IsometricTileItem[] data;
    }

    [System.Serializable]
    public class IsometricTileItem
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
        /// Approximate USD cost per operation (estimated at ~$0.001 per credit).
        /// Keys match the operation identifiers used in the Python SDK.
        /// </summary>
        public static readonly Dictionary<string, string> OPERATION_COSTS =
            new Dictionary<string, string>
            {
                // Generation
                { "generate_image",              "~$0.001–0.005"                       },
                { "generate_with_style",         "~$0.005"                             },
                { "generate_ui",                 "~$0.003"                             },
                { "generate_image_pixflux",      "~$0.001–0.005"                       },
                { "generate_image_pixen",        "~$0.001–0.005 (size-dependent)"      },
                { "generate_image_bitforge",     "~$0.001–0.003 (size-dependent)"      },

                // Characters
                { "create_character_4dir",       "~$0.008"                             },
                { "create_character_8dir",       "~$0.016"                             },
                { "character_animation",         "~$0.010"                             },
                { "animate_character",           "~$0.010"                             },

                // Animation
                { "animate_with_text",           "~$0.005–0.020 (n_frames × per-frame)" },
                { "animate_with_text_v2",        "~$0.005"                             },
                { "animate_with_text_v3",        "~$0.005"                             },
                { "animate_with_skeleton",       "~$0.005"                             },
                { "interpolation",               "~$0.003"                             },
                { "transfer_outfit_v2",          "~$0.005 × n_frames"                  },
                { "edit_animation_v2",           "~$0.005 × n_frames"                  },
                { "estimate_skeleton",           "~$0.001 (small inference call)"      },
                { "generate_8_rotations_v3",     "~$0.008"                             },

                // Editing
                { "edit_images_v2",              "~$0.003"                             },
                { "edit_image",                  "~$0.001–0.003 (size-dependent)"      },
                { "inpaint_v3",                  "~$0.003"                             },
                { "inpaint",                     "~$0.002–0.004 (mask size-dependent)" },
                { "image_to_pixelart",           "~$0.001 (input/output size-dependent)" },
                { "resize",                      "~$0.001 (target size-dependent)"     },
                { "remove_background",           "~$0.001 (size + simple/complex)"     },

                // Tilesets / Maps
                { "create_tileset",              "~$0.010"                             },
                { "create_tileset_topdown",      "~$0.010"                             },
                { "create_tileset_sidescroller", "~$0.005"                             },
                { "create_isometric_tile",       "~$0.003"                             },
                { "create_tiles_pro",            "~$0.005"                             },

                // Objects & Rotations
                { "create_map_object",           "~$0.003"                             },
                { "create_object",               "~$0.003 × directions × n_frames"     },
                { "animate_object",              "~$0.005 × frame_count"               },
                { "vary_object",                 "~$0.003 per variation"               },
                { "rotate",                      "~$0.002"                             },
                { "generate_8_rotations",        "~$0.008"                             },
                { "generate_8_rotations_v2",     "~$0.008"                             },
            };
    }
}

#endif
