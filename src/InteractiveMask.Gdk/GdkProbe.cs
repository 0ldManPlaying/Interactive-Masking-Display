using GDK;

namespace InteractiveMask.Gdk;

/// <summary>
/// Smoke test entry. The presence of this class confirms that the GDK C# bindings compile
/// inside the InteractiveMask.Gdk project against $(GdkSamplerPath).
/// </summary>
public static class GdkProbe
{
    public static string DllName => G2PLATFORM.DLL_name;

    public static int InitializeAndShutdown()
    {
        g2main.app_initialize(G2LANGUAGE.ID.ENGLISH);
        var lang = g2main.get_language();
        g2main.app_finalize();
        return (int)lang;
    }
}
