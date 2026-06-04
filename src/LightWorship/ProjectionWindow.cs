using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightWorship
{
    public class ProjectionWindow : Window
    {
        private readonly Grid _root;
        private Slide _current;

        public ProjectionWindow()
        {
            Title = "LightWorship Projection";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Black;
            _root = new Grid { Background = Brushes.Black };
            Content = _root;
            Width = 960;
            Height = 540;
        }

        public void ShowOnScreen(int screenIndex, bool fullscreen)
        {
            var screens = Screen.AllScreens;
            if (fullscreen && screens.Length > 0)
            {
                var index = Math.Max(0, Math.Min(screenIndex, screens.Length - 1));
                var area = screens[index].Bounds;
                Left = area.Left;
                Top = area.Top;
                Width = area.Width;
                Height = area.Height;
                WindowState = WindowState.Normal;
                Topmost = true;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                Width = 960;
                Height = 540;
                Topmost = false;
            }

            Show();
            Activate();
        }

        public void Render(Slide slide)
        {
            _current = slide ?? new Slide();
            _root.Children.Clear();
            _root.Background = Brushes.Black;

            if (_current.Kind == SlideKind.Black)
            {
                return;
            }

            if (!String.IsNullOrWhiteSpace(_current.GradientStartColor) && !String.IsNullOrWhiteSpace(_current.GradientEndColor))
            {
                try
                {
                    var start = (Color)ColorConverter.ConvertFromString(_current.GradientStartColor);
                    var end = (Color)ColorConverter.ConvertFromString(_current.GradientEndColor);
                    _root.Background = new LinearGradientBrush(start, end, 45.0);
                }
                catch { }
            }
            else if (!String.IsNullOrWhiteSpace(_current.BackgroundColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(_current.BackgroundColor);
                    _root.Background = new SolidColorBrush(color);
                }
                catch { }
            }

            if (_current.Kind == SlideKind.Video && File.Exists(_current.MediaPath))
            {
                AddVideo(_current.MediaPath);
            }
            else if (!String.IsNullOrWhiteSpace(_current.BackgroundPath) && File.Exists(_current.BackgroundPath))
            {
                _root.Background = new ImageBrush(new BitmapImage(new Uri(_current.BackgroundPath)))
                {
                    Stretch = ToStretch(_current.FitMode)
                };
            }
            else if (_current.Kind == SlideKind.Image && File.Exists(_current.MediaPath))
            {
                _root.Background = new ImageBrush(new BitmapImage(new Uri(_current.MediaPath)))
                {
                    Stretch = ToStretch(_current.FitMode)
                };
            }

            if (_current.Kind == SlideKind.Logo && !String.IsNullOrWhiteSpace(_current.MediaPath) && File.Exists(_current.MediaPath))
            {
                _root.Background = new ImageBrush(new BitmapImage(new Uri(_current.MediaPath))) { Stretch = Stretch.Uniform };
                return;
            }

            AddText();
        }

        public void ClearText()
        {
            if (_current == null)
            {
                return;
            }

            _current.Body = "";
            _current.Footer = "";
            Render(_current);
        }

        private void AddVideo(string path)
        {
            var media = new MediaElement
            {
                Source = new Uri(path),
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Stretch = ToStretch(_current.FitMode),
                Volume = 0
            };
            media.Loaded += (sender, args) => media.Play();
            media.MediaEnded += (sender, args) =>
            {
                media.Position = TimeSpan.Zero;
                media.Play();
            };
            _root.Children.Add(media);
        }

        private void AddText()
        {
            if (String.IsNullOrWhiteSpace(_current.Body) && String.IsNullOrWhiteSpace(_current.Title))
            {
                return;
            }

            var panel = new Grid { Margin = new Thickness(70, 55, 70, 55) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            if (!String.IsNullOrWhiteSpace(_current.Title))
            {
                var title = BuildText(_current.Title, Math.Max(28, _current.FontSize * 0.45), FontWeights.SemiBold);
                title.Margin = new Thickness(0, 0, 0, 22);
                Grid.SetRow(title, 0);
                panel.Children.Add(title);
            }

            if (!String.IsNullOrWhiteSpace(_current.Body))
            {
                var body = BuildText(_current.Body, _current.FontSize, FontWeights.SemiBold);
                body.TextAlignment = TextAlignment.Center;
                body.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(body, 1);
                panel.Children.Add(body);
            }

            if (!String.IsNullOrWhiteSpace(_current.Footer))
            {
                var footer = BuildText(_current.Footer, Math.Max(24, _current.FontSize * 0.34), FontWeights.Normal);
                footer.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                footer.Margin = new Thickness(0, 22, 0, 0);
                Grid.SetRow(footer, 2);
                panel.Children.Add(footer);
            }

            _root.Children.Add(panel);
        }

        private TextBlock BuildText(string text, double size, FontWeight weight)
        {
            var color = (Color)ColorConverter.ConvertFromString(String.IsNullOrWhiteSpace(_current.TextColor) ? "#FFFFFFFF" : _current.TextColor);
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontFamily = new FontFamily(String.IsNullOrWhiteSpace(_current.FontFamily) ? "Segoe UI" : _current.FontFamily),
                FontSize = size,
                FontWeight = weight,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.9
                }
            };
        }

        private static Stretch ToStretch(string fitMode)
        {
            switch ((fitMode ?? "Fill").Trim().ToLowerInvariant())
            {
                case "fit":
                    return Stretch.Uniform;
                case "stretch":
                    return Stretch.Fill;
                case "center":
                    return Stretch.None;
                default:
                    return Stretch.UniformToFill;
            }
        }
    }
}
