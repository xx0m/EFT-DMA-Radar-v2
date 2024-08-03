using SkiaSharp;

namespace eft_dma_radar
{
    internal static class SKPaints
    {
        #region Radar Paints
        public static readonly SKPaint PaintBase = new SKPaint() {
            Color = SKColors.WhiteSmoke,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint TextBase = new SKPaint()
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.WhiteSmoke,
            TextSize = 13,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint TextBaseOutline = new SKPaint()
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Black,
            StrokeWidth = 2,
            TextSize = 13,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint PaintMouseoverGroup = new SKPaint()
        {
            Color = SKColors.LawnGreen,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint TextMouseoverGroup = new SKPaint()
        {
            Color = SKColors.LawnGreen,
            IsStroke = false,
            TextSize = 12,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint PaintDeathMarker = new SKPaint()
        {
            Color = SKColors.Red,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        #endregion

        #region Loot Paints
        public static readonly SKPaint LootPaint = new SKPaint()
        {
            Color = SKColors.WhiteSmoke,
            StrokeWidth = 3,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint LootText = new SKPaint()
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.WhiteSmoke,
            TextSize = 13,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial"),
            FilterQuality = SKFilterQuality.High
        };

        public static readonly SKPaint DeathMarker = new SKPaint()
        {
            Color = SKColors.Black,
            StrokeWidth = 2,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        #endregion

        #region Aimview Paints
        public static readonly SKPaint PaintTransparentBacker = new SKPaint()
        {
            Color = SKColors.Black.WithAlpha(0xBE), // Transparent backer
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
        };
        public static readonly SKPaint PaintAimviewCrosshair = new SKPaint()
        {
            Color = SKColors.White,
            StrokeWidth = 1,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        #endregion

        #region Render/Misc Paints
        public static readonly SKPaint PaintBitmap = new SKPaint()
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        public static readonly SKPaint TextRadarStatus = new SKPaint()
        {
            Color = SKColors.Red,
            IsStroke = false,
            TextSize = 48,
            TextEncoding = SKTextEncoding.Utf8,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
            TextAlign = SKTextAlign.Center
        };
        public static readonly SKPaint PaintGrenades = new SKPaint()
        {
            Color = SKColors.OrangeRed,
            StrokeWidth = 3,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        public static readonly SKPaint PaintExfilOpen = new SKPaint()
        {
            Color = SKColors.LimeGreen,
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        public static readonly SKPaint PaintExfilPending = new SKPaint()
        {
            Color = SKColors.Yellow,
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        public static readonly SKPaint PaintExfilClosed = new SKPaint()
        {
            Color = SKColors.Red,
            StrokeWidth = 1,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High,
        };
        #endregion
    }

    public class PaintColor {
        public Colors Color { get; set; }
        public string Name { get; set; }

        public struct Colors {
            public byte A { get; set; }
            public byte R { get; set; }
            public byte G { get; set; }
            public byte B { get; set; }
        }
    }
}
