using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Engines;
using Filee.Engines.Infrastructure;
using ImageMagick;
using ImageMagick.Drawing;
using PdfSharp.Pdf.IO;

var root = Path.GetFullPath(args.FirstOrDefault() ?? "build/smoke-output");
Directory.CreateDirectory(root);
var pinCache = Path.GetFullPath("build/magicdisc-downloads");
UpscaleChecks.CheckMigration(Path.Combine(root, "migration"));
if (File.Exists(Path.Combine(pinCache, "ffmpeg-darwin-arm64.gz")))
{
    var badRoot = Path.Combine(root, "bad-engine-install");
    var installPackage = EngineDownloads.Packages.Single(p => p.Id == "ffmpeg");
    using (var badHttp = new HttpClient(new CachedDownloads(pinCache, corrupt: true)))
    {
        var badInstaller = new EngineInstaller(badHttp, badRoot);
        try { await badInstaller.InstallAsync(installPackage, null, CancellationToken.None); throw new Exception("Bad hash accepted"); }
        catch (InvalidDataException) { }
        RequireInstalled(!badInstaller.IsInstalled(installPackage));
    }
    using (var cancelHttp = new HttpClient(new CachedDownloads(pinCache)))
    {
        var cancelInstaller = new EngineInstaller(cancelHttp, Path.Combine(root, "cancelled-engine-install"));
        using var stop = new CancellationTokenSource(); stop.Cancel();
        try { await cancelInstaller.InstallAsync(installPackage, null, stop.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { }
        RequireInstalled(!cancelInstaller.IsInstalled(installPackage));
    }
    Console.WriteLine("PASS engine checksum rejection and cancellation");
    var engineRoot = Path.Combine(root, "engines");
    EngineEnvironment.DownloadRoot = engineRoot;
    var package = EngineDownloads.Packages.Single(p => p.Id == "ffmpeg");
    using var http = new HttpClient(new CachedDownloads(pinCache));
    var installer = new EngineInstaller(http, engineRoot);
    await installer.InstallAsync(package, null, CancellationToken.None);
    RequireInstalled(installer.IsInstalled(package) && installer.IsUpToDate(package));
    var tools = Filee.Engines.Media.FfmpegConverter.Locate();
    RequireInstalled(tools is not null && tools.Value.Ffmpeg.StartsWith(engineRoot));
    Console.WriteLine("PASS pinned FFmpeg install, permissions, markers and discovery");
}
void RequireInstalled(bool condition) { if (!condition) throw new Exception("Pinned FFmpeg installation failed"); }
var engines = EngineRegistry.CreateAll(new EngineEnvironment(root));
var catalog = new ConverterCatalog(engines);
await using var queue = new JobQueue(catalog, EngineRegistry.FindPdfMerger(engines), mediaJoiner: EngineRegistry.FindMediaJoiner(engines));
var presets = BuiltInData.CreatePresets();
var ids = presets.Select(p => p.Id).ToHashSet();
void Require(bool condition, string why) { if (!condition) throw new Exception(why); }
foreach (var profile in BuiltInData.CreateProfiles())
    foreach (var id in profile.PresetIds.Concat(profile.BackPresetIds ?? [])) Require(ids.Contains(id), $"Dangling preset {id}");
foreach (var preset in presets.Where(p => p.IsGroup))
    foreach (var id in preset.SubPresetIds) Require(ids.Contains(id), $"Dangling child {id}");
Require(!catalog.CreatePlanner().CanConvert("docx", "pdf"), "Office route should be unavailable");
Require(!FormatRegistry.Supported.Any(f => f.Category is FormatCategory.Cad or FormatCategory.Document), "Unrelated formats exposed");
Console.WriteLine("PASS scope and wheel references");
var paths = new List<string>();
foreach (var (name, color, width) in new[] { ("frame01", MagickColors.Red, 64u), ("frame02", MagickColors.Green, 32u), ("frame03", MagickColors.Blue, 64u) })
{
    var path = Path.Combine(root, name + ".png");
    using var picture = new MagickImage(color, width, 64);
    picture.Write(path);
    paths.Add(path);
}
async Task<List<string>> Convert(IReadOnlyList<string> input, Preset preset, string label)
{
    preset.Output.Location = OutputLocation.SameFolder;
    preset.Output.FileNamePattern = "{name}_" + label;
    preset.Output.Conflict = ConflictPolicy.Overwrite;
    var job = new ConversionJob(input, preset, label);
    await queue.RunAsync(job);
    Require(job.State == JobState.Completed, label + ": " + string.Join("; ", job.Files.Select(f => f.ErrorKey + " " + f.ErrorDetail)));
    var outputs = job.Files.SelectMany(f => f.Outputs).Distinct().ToList();
    Require(outputs.Count > 0 && outputs.All(File.Exists), label + " missing output");
    Console.WriteLine("PASS " + label);
    return outputs;
}
var pdfs = await Convert(paths.Take(2).ToList(), new Preset { TargetFormat = "pdf" }, "pdf");
await Convert([paths[0]], new Preset { TargetFormat = "bmp" }, "bmp");
await Convert([paths[0]], new Preset { TargetFormat = "webp" }, "webp");
await Convert([paths[0]], presets.Single(p => p.Id == "compress-image").Clone(), "png_compress");
var gifs = await Convert(paths, presets.Single(p => p.Id == "sequence-gif").Clone(), "sequence");
using (var animation = new MagickImageCollection(gifs[0]))
{
    Require(animation.Count == 3, "GIF frame count");
    Require(animation.All(f => f.AnimationDelay == 10), "GIF frame timing");
    Require(animation[0].Width == 64 && animation[0].Height == 64, "GIF canvas size");
}
var apng = await Convert(paths, presets.Single(p => p.Id == "sequence-apng").Clone(), "sequence");
using (var animation = Filee.Engines.Magick.Apng.Read(apng[0])) Require(animation.Count == 3, "APNG frame count");
await Convert(gifs, new Preset { TargetFormat = "apng" }, "gif_apng");
var mp4 = await Convert(gifs, new Preset { TargetFormat = "mp4" }, "gif_mp4");
await Convert(mp4, new Preset { TargetFormat = "gif" }, "mp4_gif");
await Convert(mp4, new Preset { TargetFormat = "apng" }, "mp4_apng");
var mov = await Convert(mp4, new Preset { TargetFormat = "mov" }, "mov");
await Convert(mov, new Preset { TargetFormat = "gif" }, "mov_gif");
await Convert(gifs, presets.Single(p => p.Id == "compress-image").Clone(), "gif_compress");
var merged = await Convert(pdfs, presets.Single(p => p.Id == "merge-pdf").Clone(), "merged");
using (var pdf = PdfReader.Open(merged[0], PdfDocumentOpenMode.Import)) Require(pdf.PageCount == 2, "PDF merge page count");
var ocrSource = Path.Combine(root, "ocr.png");
using (var picture = new MagickImage(MagickColors.White, 800, 200))
{
    new Drawables().Font("Arial").FontPointSize(56).FillColor(MagickColors.Black).Text(25, 110, "MAGIC DISC 123").Draw(picture);
    picture.Write(ocrSource);
}
var ocrPreset = presets.Single(p => p.Id == "ocr-text").Clone(); ocrPreset.Pdf.OcrLanguage = "eng";
var text = await Convert([ocrSource], ocrPreset, "ocr");
Require(File.ReadAllText(text[0]).Contains("MAGIC", StringComparison.OrdinalIgnoreCase), "OCR text recognition");
await UpscaleChecks.CheckConversionAsync(root, pinCache);
Console.WriteLine("ALL CHECKS PASSED");

sealed class CachedDownloads(string root, bool corrupt = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(corrupt ? [0] : File.ReadAllBytes(Path.Combine(root, Path.GetFileName(request.RequestUri!.AbsolutePath)))),
        });
}
