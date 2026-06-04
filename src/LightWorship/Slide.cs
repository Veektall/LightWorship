using System;

namespace LightWorship
{
    public enum SlideKind
    {
        Empty,
        Bible,
        Song,
        Text,
        Image,
        Video,
        Black,
        Logo
    }

    public class Slide
    {
        public SlideKind Kind { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Footer { get; set; }
        public string BackgroundPath { get; set; }
        public string BackgroundColor { get; set; }
        public string GradientStartColor { get; set; }
        public string GradientEndColor { get; set; }
        public string MediaPath { get; set; }
        public string TextColor { get; set; }
        public string FontFamily { get; set; }
        public string FitMode { get; set; }
        public double FontSize { get; set; }

        public Slide()
        {
            Kind = SlideKind.Empty;
            TextColor = "#FFFFFFFF";
            FontFamily = "Segoe UI";
            FitMode = "Fill";
            FontSize = 54;
            BackgroundColor = "";
            GradientStartColor = "";
            GradientEndColor = "";
        }

        public static Slide Black()
        {
            return new Slide { Kind = SlideKind.Black, Title = "Black Screen" };
        }

        public static Slide Text(string title, string body)
        {
            return new Slide { Kind = SlideKind.Text, Title = title, Body = body };
        }

        public override string ToString()
        {
            return String.IsNullOrWhiteSpace(Title) ? Kind.ToString() : Title;
        }
    }
}
