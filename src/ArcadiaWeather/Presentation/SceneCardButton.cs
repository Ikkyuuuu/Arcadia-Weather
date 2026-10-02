using System.Windows;
using System.Windows.Controls;

namespace ArcadiaWeather.Presentation;

public sealed class SceneCardButton : Button
{
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(SceneCardButton), new PropertyMetadata(false));
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
}
