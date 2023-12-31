﻿using Mapsui.Rendering.Skia.Extensions;
using Mapsui.Widgets;
using Mapsui.Widgets.PerformanceWidget;
using SkiaSharp;

namespace Mapsui.Rendering.Skia.SkiaWidgets;

public class PerformanceWidgetRenderer : ISkiaWidgetRenderer
{
    private readonly string[] _textHeader = { "Last", "Mean", "Frames", "Min", "Max", "Count", "Dropped" };
    private readonly string[] _text = new string[7];

    public void Draw(SKCanvas canvas, Viewport viewport, IWidget widget, float layerOpacity)
    {
        var performanceWidget = (PerformanceWidget)widget;
        var textSize = performanceWidget.TextSize;

        using var textPaint = new SKPaint { Color = performanceWidget.TextColor.ToSkia(), TextSize = textSize, };
        using var backgroundPaint = new SKPaint { Color = performanceWidget.BackColor.ToSkia().WithAlpha((byte)(255.0f * performanceWidget.Opacity)), Style = SKPaintStyle.Fill, };

        var widthHeader = 0f;

        for (var i = 0; i < _textHeader.Length; i++)
            widthHeader = System.Math.Max(widthHeader, textPaint.MeasureText(_textHeader[5]));

        var width = widthHeader + 20 + textPaint.MeasureText("0000.000") + 4;
        var height = _textHeader.Length * (performanceWidget.TextSize + 2) - 2 + 4;

        performanceWidget.UpdateEnvelope(width, height, viewport.Width, viewport.Height);

        _text[0] = performanceWidget.Performance.LastDrawingTime.ToString("0.000 ms");
        _text[1] = performanceWidget.Performance.Mean.ToString("0.000 ms");
        _text[2] = performanceWidget.Performance.FPS.ToString("0 fps");
        _text[3] = performanceWidget.Performance.Min.ToString("0.000 ms");
        _text[4] = performanceWidget.Performance.Max.ToString("0.000 ms");
        _text[5] = performanceWidget.Performance.Count.ToString("0");
        _text[6] = performanceWidget.Performance.Dropped.ToString("0");

        var rect = performanceWidget.Envelope?.ToSkia() ?? canvas.DeviceClipBounds;

        canvas.DrawRect(rect, backgroundPaint);

        for (var i = 0; i < _textHeader.Length; i++)
        {
            canvas.DrawText(_textHeader[i], rect.Left + 2, rect.Top + 2 * i + textSize * (i + 1), textPaint);
            canvas.DrawText(_text[i], rect.Right - 2 - textPaint.MeasureText(_text[i]), rect.Top + (2 + textSize) * (i + 1), textPaint);
        }
    }
}
