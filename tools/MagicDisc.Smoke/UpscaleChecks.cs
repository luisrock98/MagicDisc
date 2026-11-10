using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Core.Settings;
using Filee.Engines;
using Filee.Engines.Infrastructure;
using Filee.Engines.Upscale;
using ImageMagick;

static class UpscaleChecks
{
    public static void CheckMigration(string root)
    {
        foreach (var full in new[] { false, true })
        {
            var path = Path.Combine(root, Guid.NewGuid().ToString("N"));
            var old = new UserDataStore(path);
            old.Load();
            old.Presets.RemoveAll(p => p.Id == BuiltInData.UpscaleId);
            var images = old.Profiles.Single(p => p.Id == "images");
            images.BackPresetIds!.Remove(BuiltInData.UpscaleId);
            if (full)
                images.BackPresetIds = old.Presets.Where(p => !p.IsTool && !p.IsGroup).Take(12).Select(p => p.Id).ToList();
            var before = images.BackPresetIds.ToArray();
            var half = old.Presets.Single(p => p.Id == "half-size").Clone();
            old.Settings.SchemaVersion = 74;
            old.SaveSettings();
            old.SaveLibrary();
            var upgraded = new UserDataStore(path);
            upgraded.Load();
            Require(upgraded.Presets.Single(p => p.Id == BuiltInData.UpscaleId).IsUpscaleTool, "Upgrade restores AI tool");
            var back = upgraded.Profiles.Single(p => p.Id == "images").BackPresetIds!;
            Require(back.Take(before.Length).SequenceEqual(before), "Custom wheel order preserved");
            Require(back.Count == (full ? 12 : before.Length + 1), "Full wheel remains intact");
            var retained = upgraded.Presets.Single(p => p.Id == "half-size");
            Require(retained.Kind == half.Kind && retained.Image.ResizePercent == half.Image.ResizePercent,
                "Fixed resize preset unchanged");
            upgraded.Load();
            Require(upgraded.Presets.Count(p => p.Id == BuiltInData.UpscaleId) == 1, "Upgrade is idempotent");
        }
        Console.WriteLine("PASS AI upgrade, custom/full wheels and unchanged resize preset");
    }

    public static async Task CheckConversionAsync(string root, string cache)
    {
        const string modelId = "RealESRGAN_General_x4_v3";
        if (!File.Exists(Path.Combine(cache, "upscayl-macos.zip")) || !File.Exists(Path.Combine(cache, modelId + ".bin")))
        {
            Console.WriteLine("SKIP real AI inference: pinned engine/model downloads not cached");
            return;
        }
        using var http = new HttpClient(new PinnedDownloads(cache));
        var package = EngineDownloads.Packages.Single(p => p.Id == "upscayl-bin");
        var installer = new EngineInstaller(http, EngineEnvironment.DownloadRoot);
        await installer.InstallAsync(package, null, CancellationToken.None);
        Require(installer.IsInstalled(package), "AI engine installed and verified");
        var components = UpscaleModels.Components;
        var modelPackage = new EnginePackage("upscale-model-" + modelId, [modelId + "-param", modelId + "-bin"], 3_000_000, ["upscaler"]);
        await new EngineInstaller(http, UpscaleModels.Root, components).InstallAsync(modelPackage, null, CancellationToken.None);
        Require(UpscaleModels.IsInstalled(modelId), "AI model installed and verified");
        var engines = EngineRegistry.CreateAll(new EngineEnvironment(root));
        Require(engines.Any(e => e.Id == "upscaler" && e.GetStatus().IsAvailable), "Registered AI engine discovers installation");
        await using var queue = new JobQueue(new ConverterCatalog(engines));
        var input = Path.Combine(root, "frame01.png");
        foreach (var scale in new[] { 2, 3, 4 })
        {
            var preset = UpscaleTool.ConversionOf(BuiltInData.UpscalePresets().Single(), "png", modelId, scale);
            preset.Output.Location = OutputLocation.SameFolder;
            preset.Output.FileNamePattern = "{name}_ai_" + scale;
            preset.Output.Conflict = ConflictPolicy.Overwrite;
            var job = new ConversionJob([input], preset, "AI " + scale);
            await queue.RunAsync(job);
            Require(job.State == JobState.Completed, "AI conversion: " + string.Join("; ", job.Files.Select(f => f.ErrorKey + " " + f.ErrorDetail)));
            using var image = new MagickImage(job.Files.Single().Outputs.Single());
            Require(image.Width == 64 * scale && image.Height == 64 * scale, "Actual AI output dimensions");
            Console.WriteLine($"PASS real AI inference {scale}×: {image.Width} × {image.Height}");
        }
    }

    static void Require(bool value, string why)
    {
        if (!value) throw new Exception(why);
    }

    sealed class PinnedDownloads(string root) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var name = Path.GetFileName(request.RequestUri!.AbsolutePath);
            if (name.EndsWith("macos.zip")) name = "upscayl-macos.zip";
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(File.ReadAllBytes(Path.Combine(root, name)))
            });
        }
    }
}
