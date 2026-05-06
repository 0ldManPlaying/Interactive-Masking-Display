using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InteractiveMask.WebHost.Pages;

public class LoginModel : PageModel
{
    private readonly WebSettingsProvider _settings;

    public LoginModel(WebSettingsProvider settings)
    {
        _settings = settings;
    }

    public string Language { get; private set; } = "nl";
    public Translations T { get; private set; } = Translations.Nl;
    public string ReturnUrl { get; private set; } = "/";

    public void OnGet(string? returnUrl)
    {
        var current = _settings.Current;
        Language = current.Language;
        T = Translations.For(current.Language);
        ReturnUrl = string.IsNullOrEmpty(returnUrl) || !returnUrl.StartsWith('/') ? "/" : returnUrl;
    }
}
