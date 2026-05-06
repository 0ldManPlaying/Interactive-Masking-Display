using System.Windows.Data;
using System.Windows.Markup;

namespace InteractiveMask.Display;

/// <summary>
/// XAML markup extension that binds to <c>Strings.Instance.Current.{Key}</c>
/// and stays live: the binding source is the observable singleton, so a
/// language switch via <c>Strings.Instance.Apply()</c> refreshes every bound
/// element without rebuilding the visual tree.
///
/// Usage: <c>Text="{local:Loc PageTitleNvr}"</c>
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public LocExtension() { }
    public LocExtension(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"Current.{Key}")
        {
            Source = Strings.Instance,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
