using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InteractiveMask.WebHost.Pages;

public class IndexModel : PageModel
{
    private readonly StateMirror _mirror;
    private readonly WebSettingsProvider _settings;

    public StateSnapshot InitialSnapshot { get; private set; } = new(false, 0, 0, new());
    public Translations T { get; private set; } = Translations.Nl;

    public IndexModel(StateMirror mirror, WebSettingsProvider settings)
    {
        _mirror = mirror;
        _settings = settings;
    }

    public void OnGet()
    {
        InitialSnapshot = _mirror.Snapshot();
        T = Translations.For(_settings.Current.Language);
    }
}
