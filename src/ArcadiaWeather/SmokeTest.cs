using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using ArcadiaWeather.Presentation;

namespace ArcadiaWeather;

internal static class SmokeTest
{
    internal static async Task RunUiAsync(AppController controller, MainWindow window, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        bool success = true;
        void Check(string name, bool passed) { checks.Add(new { Test = name, Passed = passed }); success &= passed; }
        try
        {
            await controller.StartAsync();
            await Task.Delay(700);
            Render(window, Path.Combine(output, "dashboard.png"));
            foreach (var scene in new[] { Scene.Day, Scene.Evening, Scene.Rain, Scene.Cloudy, Scene.Night })
            {
                controller.SelectScene(scene);
                await Task.Delay(65);
            }
            await Task.Delay(700);
            Check("Rapid scene changes settle on the final scene", window.SceneTitle.Text == SceneInfo.ShortName(Scene.Night)
                && window.HeroImage.Opacity == 1 && window.HeroPreviousImage.Source is null);
            Check("Exactly one scene card stays selected", window.SceneCards.Children.OfType<SceneCardButton>().Count(c => c.IsSelected) == 1);
            controller.SelectScene(null);
            Check("Return to automatic updates the mode", window.ModeBadge.Text == "Automatic");
            window.Tabs.SelectedIndex = 1;
            await Task.Delay(450);
            window.LocationNameInput.Text = "Unsaved location draft";
            window.Tabs.SelectedIndex = 0;
            await Task.Delay(80);
            window.Tabs.SelectedIndex = 1;
            await Task.Delay(500);
            Check("Settings draft survives tab navigation", window.LocationNameInput.Text == "Unsaved location draft");
            Check("Page transition settles at full opacity", window.SettingsPage.Opacity == 1);
            Render(window, Path.Combine(output, "settings.png"));
            window.Width = 960; window.Height = 720;
            await Task.Delay(400);
            var save = window.SaveButton.TransformToAncestor(window).TransformBounds(new Rect(window.SaveButton.RenderSize));
            Check("Save remains visible at the minimum window size", save.Bottom < window.ActualHeight && save.Right < window.ActualWidth && save.Top > 0);
            Render(window, Path.Combine(output, "settings-small.png"));
            window.Tabs.SelectedIndex = 2;
            await Task.Delay(500);
            var guide = (ScrollViewer)((TabItem)window.Tabs.Items[2]).Content;
            Check("Guide can scroll at the minimum window size", guide.ScrollableHeight > 0);
            guide.ScrollToEnd(); await Task.Delay(100);
            Render(window, Path.Combine(output, "guide-small.png"));
            window.Tabs.SelectedIndex = 0;
            await Task.Delay(500);
            Motion.SetEnabled(window, false);
            controller.SelectScene(Scene.Day);
            Check("Reduced motion changes the scene immediately", window.HeroImage.Opacity == 1 && window.HeroPreviousImage.Source is null);
            window.Hide(); controller.SelectScene(Scene.Evening);
            Check("Hidden window skips scene animation", window.HeroPreviousImage.Source is null);
            window.Show(); await Task.Delay(200);
            Render(window, Path.Combine(output, "dashboard-small.png"));
        }
        catch (Exception ex) { success = false; checks.Add(new { Test = "Unexpected UI failure", Passed = false, Error = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Success = success, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = success ? 0 : 1;
        }
    }
    internal static async Task RunAsync(AppController controller, MainWindow window, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        bool success = true;
        try
        {
            await controller.StartAsync();
            checks.Add(new { Test = "Live weather", Passed = controller.Weather is not null, Error = controller.WeatherError });
            success &= controller.Weather is not null;
            await Task.Delay(700);
            Render(window, Path.Combine(output, "dashboard.png"));
            foreach (var scene in Enum.GetValues<Scene>())
            {
                controller.Player.Play(controller.Settings.VideoPath(scene), "", 0, true);
                controller.Player.SetPaused(false);
                await Task.Delay(3500);
                long time = controller.Player.PlaybackTime;
                string frame = Path.Combine(output, $"day{(int)scene}-decoded.png");
                bool snapshot = controller.Player.Snapshot(frame);
                await Task.Delay(300);
                bool passed = controller.Player.IsAttached && controller.Player.IsPlaying && time > 500 && snapshot && File.Exists(frame);
                checks.Add(new { Test = $"Day {(int)scene} HEVC playback behind desktop", Passed = passed, TimeMs = time, Attached = controller.Player.IsAttached, Frame = frame });
                success &= passed;
            }
            controller.Player.SeekNearEnd();
            await Task.Delay(4500);
            bool loop = controller.Player.IsPlaying && controller.Player.PlaybackTime is >= 0 and < 15000;
            checks.Add(new { Test = "Loop across video end", Passed = loop, TimeMs = controller.Player.PlaybackTime }); success &= loop;
            controller.Player.SetPaused(true); await Task.Delay(500);
            long pausedAt = controller.Player.PlaybackTime; await Task.Delay(800);
            bool pause = Math.Abs(controller.Player.PlaybackTime - pausedAt) < 150;
            controller.Player.SetPaused(false); await Task.Delay(1500);
            bool resume = controller.Player.PlaybackTime > pausedAt + 300;
            checks.Add(new { Test = "Pause and resume", Passed = pause && resume }); success &= pause && resume;
            controller.Player.SetPaused(true);
            controller.Player.Play(controller.Settings.VideoPath(Scene.Evening), "", 0, true);
            await Task.Delay(1500);
            long switchPausedAt = controller.Player.PlaybackTime; await Task.Delay(1000);
            bool switchPaused = !controller.Player.IsPlaying && Math.Abs(controller.Player.PlaybackTime - switchPausedAt) < 150;
            checks.Add(new { Test = "Scene switch preserves pause", Passed = switchPaused }); success &= switchPaused;
            controller.Player.SetPaused(false);
            controller.Player.Reattach();
            controller.Player.Play(controller.Settings.VideoPath(Scene.Day), "", 0, true); await Task.Delay(2500);
            bool reattach = controller.Player.IsAttached && controller.Player.IsPlaying && controller.Player.PlaybackTime > 500;
            checks.Add(new { Test = "Desktop reattachment", Passed = reattach }); success &= reattach;
            window.Tabs.SelectedIndex = 1; await Task.Delay(300); Render(window, Path.Combine(output, "settings.png"));
        }
        catch (Exception ex) { success = false; checks.Add(new { Test = "Unexpected failure", Passed = false, Error = ex.ToString() }); }
        finally
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Success = success, Checks = checks }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = success ? 0 : 1;
        }
    }
    private static void Render(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
