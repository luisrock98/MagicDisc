// MagicDisc's public feature boundary. Shared readers keep their internal format table.
using Filee.Core.Presets;
using Filee.Core.Profiles;

namespace Filee.Core.Formats;

/// <summary>The image, animation, video and PDF workflows offered by MagicDisc.</summary>
public static class MediaScope
{
    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "bmp", "webp", "gif", "apng", "tiff", "heic", "avif", "psd", "psb",
        "ico", "icns", "svg", "svgz", "pdf", "txt", "mp4", "mov", "m4v", "webm", "mkv", "avi",
    };

    /// <summary>Whether a format is available as an input or output in this product.</summary>
    public static bool AcceptsFormat(string id) => Formats.Contains(id);

    /// <summary>Retains media tools and removes unavailable children from groups.</summary>
    public static List<Preset> FilterPresets(List<Preset> all)
    {
        var tools = new HashSet<string>
        {
            "tool-crop", "tool-clip", "tool-hide", "tool-image-edit", "tool-pdf-crop", "tool-pdf-pages", "tool-pdf-add", "tool-upscale",
        };
        var deny = new HashSet<string> { "translate", "tool-sheet-studio", "caption-video", "extract-subtitles", "live-photo-iphone", "motion-photo-android" };
        var result = all.Where(p => !p.IsGroup && !deny.Contains(p.Id) &&
            (p.IsTool ? tools.Contains(p.Id) : p.IsCopyToFolder || p.TargetFormat == BuiltInData.SameAsSource || AcceptsFormat(p.TargetFormat))).ToList();
        if (result.All(p => p.Id != "to-apng"))
            result.Add(new Preset { Id = "to-apng", Name = "APNG", TargetFormat = "apng" });
        foreach (var format in new[] { "gif", "apng" })
            result.Add(new Preset
            {
                Id = "sequence-" + format, NameKey = "preset.sequence_" + format, TargetFormat = format,
                Media = { Slideshow = true, SlideSeconds = 0.1, Crossfade = false },
                Output = { FileNamePattern = "{name}_animation" },
            });
        result.Add(new Preset
        {
            Id = "ocr-text", NameKey = "preset.ocr_text", TargetFormat = "txt",
            Pdf = { Ocr = true, OcrLanguage = "chi_sim+eng" },
            Output = { FileNamePattern = "{name}_text" },
        });
        // Build nested groups from leaves to parents; never leave a dangling preset id.
        var pending = all.Where(p => p.IsGroup).ToList();
        for (var pass = 0; pass < 4; pass++)
        {
            var ids = result.Select(p => p.Id).ToHashSet();
            foreach (var p in pending.ToList())
            {
                var children = p.SubPresetIds.Where(ids.Contains).ToList();
                if (children.Count == 0) continue;
                p.SubPresetIds = children;
                result.Add(p);
                pending.Remove(p);
            }
        }
        return result;
    }

    /// <summary>Useful defaults; users can continue editing the wheel and presets.</summary>
    public static List<ToolbarProfile> CreateProfiles()
    {
        List<string> Ext(FormatCategory category) => FormatRegistry.Supported
            .Where(f => f.Category == category).SelectMany(f => f.Extensions).ToList();
        return
        [
            new() { Id = "animations", NameKey = "profile.animations", Extensions = ["gif", "apng"],
                PresetIds = ["to-mp4", "to-apng", "to-webp", "to-gif", "compress-image", "half-size"],
                BackPresetIds = ["tool-crop", "rotate", "flip", "to-png", "remove-metadata"] },
            new() { Id = "images", NameKey = "profile.images", Extensions = Ext(FormatCategory.Image).Where(e => e is not ("gif" or "apng")).Concat(Ext(FormatCategory.Vector)).ToList(),
                PresetIds = ["to-bmp", "to-webp", "to-pdf", "compress-image", "to-jpg", "to-png"],
                BackPresetIds = ["tool-crop", "rotate", "remove-background", "trim-transparent", "tool-image-edit", "merge-images", "merge-pdf", "ocr-text", "sequence-gif", "sequence-apng", "tool-upscale"] },
            new() { Id = "video", NameKey = "profile.video", Extensions = Ext(FormatCategory.Video),
                PresetIds = ["to-gif", "to-mp4", "to-apng", "to-mov", "compress-video", "tool-clip"],
                BackPresetIds = ["extract-frames-png", "extract-frames-jpg", "merge-videos", "mp4-720", "mp4-hevc", "to-webm"] },
            new() { Id = "pdf", NameKey = "profile.pdf", Extensions = ["pdf"],
                PresetIds = ["merge-pdf", "to-png", "to-jpg", "compress-pdf", "searchable-pdf", "ocr-text"],
                BackPresetIds = ["pdf-edit", "pdf-split", "pdf-long-image", "remove-metadata"] },
            new() { Id = "mixed", NameKey = "profile.mixed", IsFallback = true,
                Extensions = FormatRegistry.Supported.SelectMany(f => f.Extensions).ToList(),
                PresetIds = ["to-png", "to-jpg", "to-webp", "to-pdf", "merge-pdf", "to-mp4"], BackPresetIds = [] },
        ];
    }
}
