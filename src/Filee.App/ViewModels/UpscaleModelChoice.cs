using Filee.Core.Localization;
using Filee.Engines.Upscale;

namespace Filee.App.ViewModels;

/// <summary>A model's identity and a short, localized description for the model pickers.</summary>
public sealed class UpscaleModelChoice(UpscaleModel model, ILocalizer loc)
{
    public string Id => model.Id;
    public string Name => model.Name;
    public string License => model.License;
    public string Author => model.Author;
    public long TotalSize => model.TotalSize;
    public string Description
    {
        get
        {
            var key = $"upscale.model.{Id}.summary";
            var text = loc[key];
            return text == key ? model.Description : text;
        }
    }
}
