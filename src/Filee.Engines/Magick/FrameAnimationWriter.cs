// A sequence of still images becomes one animation, preserving alpha and input order.
using Filee.Core.Presets;
using ImageMagick;

namespace Filee.Engines.Magick;

internal static class FrameAnimationWriter
{
    public static void Write(IReadOnlyList<string> paths, string output, string format, Preset preset,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (paths.Count == 0 || paths.Count > MediaOptions.MaxSlides)
            throw new ArgumentOutOfRangeException(nameof(paths));
        using var frames = new MagickImageCollection();
        var delay = (uint)Math.Max(2, Math.Round(preset.Media.SlideDuration * 100));
        long pixels = 0;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = new MagickImage(path);
            try
            {
                pixels += (long)frame.Width * frame.Height;
                if (pixels > 200_000_000)
                    throw new InvalidOperationException("The image sequence exceeds 200 megapixels. Use fewer or smaller images.");
                frame.AnimationDelay = delay;
                frame.AnimationTicksPerSecond = 100;
                frame.AnimationIterations = 0;
                frames.Add(frame);
            }
            catch { frame.Dispose(); throw; }
            progress?.Report(0.8 * frames.Count / paths.Count);
        }
        // A uniform transparent canvas avoids GIF frame offsets and keeps unequal-sized inputs aligned.
        var width = frames.Max(f => f.Width);
        var height = frames.Max(f => f.Height);
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frame.BackgroundColor = MagickColors.Transparent;
            frame.Extent(width, height, Gravity.Center);
            frame.Page = new MagickGeometry(width, height);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (format == "apng")
            Apng.Write(frames.ToList(), output, cancellationToken);
        else
        {
            frames.Quantize(new QuantizeSettings { Colors = 256 });
            frames.Optimize();
            frames.Write(output, MagickFormat.Gif);
        }
        progress?.Report(1);
    }
}
