#nullable enable
using GDK;

namespace InteractiveMask.Gdk;

/// <summary>
/// Refcounted access to <c>g2_main_app_initialize</c> / <c>g2_main_app_finalize</c>.
/// The GDK requires exactly one initialize/finalize pair per process; using a refcount
/// lets independent components (Display, future Setup wizard, tests) acquire it
/// without coordinating with each other.
/// </summary>
public static class GdkLifetime
{
    private static readonly object _lock = new();
    private static int _refCount;
    private static G2VERBOSE_CALLBACK? _verboseHandler;

    public static IDisposable Acquire(string clientProductInfo, G2LANGUAGE.ID language = G2LANGUAGE.ID.ENGLISH)
    {
        lock (_lock)
        {
            if (_refCount == 0)
            {
                g2main.app_initialize(language);
                g2main.set_client_product_info(clientProductInfo);
            }
            _refCount++;
        }
        return new Releaser();
    }

    /// <summary>
    /// Route GDK verbose messages to a managed handler. Call after <see cref="Acquire"/>.
    /// The handler is held by a static field so the GC cannot collect the delegate while
    /// native code holds a function pointer to it.
    /// </summary>
    public static void SetVerboseSink(Action<G2MAIN_VERBOSE.LEVEL, string>? sink, G2MAIN_VERBOSE.LEVEL level = G2MAIN_VERBOSE.LEVEL.INFO)
    {
        if (sink is null)
        {
            g2main.verbose_callback_revoke();
            _verboseHandler = null;
            return;
        }
        _verboseHandler = (lvl, msg) => sink(lvl, msg ?? string.Empty);
        g2main.verbose_set_level(level);
        g2main.verbose_callback_invoke(_verboseHandler);
    }

    private static void Release()
    {
        lock (_lock)
        {
            if (_refCount == 0) return;
            _refCount--;
            if (_refCount == 0)
            {
                g2main.verbose_callback_revoke();
                _verboseHandler = null;
                g2main.app_finalize();
            }
        }
    }

    private sealed class Releaser : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Release();
        }
    }
}
