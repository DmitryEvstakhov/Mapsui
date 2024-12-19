using Mapsui.Extensions;
using Mapsui.Nts.Extensions;
using Mapsui.Rendering.Skia.Extensions;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using SkiaSharp;

namespace Mapsui.Rendering.Skia;

public static class LineStringRenderer
{
    public static void Draw(SKCanvas canvas, Viewport viewport, VectorStyle? vectorStyle,
        IFeature feature, LineString lineString, float opacity, IRenderCache renderCache)
    {
        if (vectorStyle == null)
            return;
        
        SKPath ToPath((long featureId, MRect extent, double rotation, float lineWidth) valueTuple)
        {
            var result = lineString.ToSkiaPath(viewport, viewport.ToSkiaRect(), valueTuple.lineWidth);
            _ = result.Bounds;
            _ = result.TightBounds;
            return result;
        }

        var extent = viewport.ToExtent();
        var rotation = viewport.Rotation;
        var lineWidth = (float)(vectorStyle.Line?.Width ?? 1f);
        if (vectorStyle.Line.IsVisible())
        {
            using var paint = renderCache.GetOrCreatePaint((vectorStyle.Line, opacity), CreateSkPaint);
            using var path = renderCache.GetOrCreatePath((feature.Id, extent, rotation, lineWidth),ToPath);
            canvas.DrawPath(path, paint);
        }

        if (vectorStyle is ArrowVectorStyle arrowStyle)
        {
            if (lineString.NumPoints == 1)
                return;

            //рисуем стрелку в первой точке
            DrawArrow(arrowStyle, viewport, canvas, opacity, lineString.StartPoint.Coordinate, lineString.StartPoint.Coordinate);

            var distance = arrowStyle.Distance;
            //если задан параметр Distance, то рисуем стрелки в промежуточних точках с промежутком, равным Distance
            if (distance > 0.1 && lineString.Length > distance / 2)
            {
                if (lineString.Length < distance)
                    distance = lineString.Length / 2;

                var lenSum = 0d;
                for (var i = 0; i < lineString.Coordinates.Length - 1; i++)
                {
                    var c1 = lineString.Coordinates[i];
                    var c2 = lineString.Coordinates[i + 1];
                    var len = c1.Distance(c2);

                    //если длина текущего сегмента меньше Distance, то пробуем нарисовать в следующем сегменте
                    if (lenSum + len < distance)
                    {
                        lenSum += len;
                        continue;
                    }

                    //рисуем промежуточную стрелку
                    lenSum = distance - lenSum;
                    var p = GetCoordinateOnC1PlusLength(c1, c2, lenSum);
                    DrawArrow(arrowStyle, viewport, canvas, opacity, p, c2);

                    //если остаток длины сегмента больше Distance, то рисуем еще промежуточные стрелки
                    while (len - lenSum > distance)
                    {
                        p = GetCoordinateOnC1PlusLength(p, c2, distance);
                        DrawArrow(arrowStyle, viewport, canvas, opacity, p, c2);
                        lenSum += distance;
                    }

                    lenSum = len - lenSum;
                }
                //foreach (var segment in segments)
                //{
                //    //если длина текущего сегмента меньше Distance, то пробуем нарисовать в следующем сегменте
                //    if (lenSum + segment.Length < distance)
                //    {
                //        lenSum += segment.Length;
                //        continue;
                //    }

                //    //рисуем промежуточную стрелку
                //    lenSum = distance - lenSum;
                //    var p = GetCoordinateOnC1PlusLength(segment.StartPoint, segment.EndPoint, lenSum);
                //    DrawArrow(arrowStyle, viewport, canvas, opacity, p, segment.EndPoint);

                //    //если остаток длины сегмента больше Distance, то рисуем еще промежуточные стрелки
                //    while (segment.Length - lenSum > distance)
                //    {
                //        p = GetCoordinateOnC1PlusLength(p, segment.EndPoint, distance);
                //        DrawArrow(arrowStyle, viewport, canvas, opacity, p, segment.EndPoint);
                //        lenSum += distance;
                //    }

                //    lenSum = segment.Length - lenSum;
                //}
            }

            var lastLen = lineString.Coordinates[lineString.NumPoints - 2].Distance(lineString.EndPoint.Coordinate);
            //получаем точку лежащую на продолжении последнего отрезка линии
            var endPlus = GetCoordinateOnC1PlusLength(lineString.Coordinates[lineString.NumPoints - 2], lineString.EndPoint.Coordinate, lastLen + 10);
            //рисуем стрелку в последней точке
            DrawArrow(arrowStyle, viewport, canvas, opacity, lineString.EndPoint.Coordinate, endPlus);
        }
    }

    private static SKPaint CreateSkPaint((Pen? pen, float opacity) valueTuple)
    {
        var pen = valueTuple.pen;
        var opacity = valueTuple.opacity;

        float lineWidth = 1;
        var lineColor = new Color();

        var strokeCap = PenStrokeCap.Butt;
        var strokeJoin = StrokeJoin.Miter;
        var strokeMiterLimit = 4f;
        var strokeStyle = PenStyle.Solid;
        float[]? dashArray = null;
        float dashOffset = 0;

        if (pen != null)
        {
            lineWidth = (float)pen.Width;
            lineColor = pen.Color;
            strokeCap = pen.PenStrokeCap;
            strokeJoin = pen.StrokeJoin;
            strokeMiterLimit = pen.StrokeMiterLimit;
            strokeStyle = pen.PenStyle;
            dashArray = pen.DashArray;
            dashOffset = pen.DashOffset;
        }

        var paint = new SKPaint { IsAntialias = true };
        paint.IsStroke = true;
        paint.StrokeWidth = lineWidth;
        paint.Color = lineColor.ToSkia(opacity);
        paint.StrokeCap = strokeCap.ToSkia();
        paint.StrokeJoin = strokeJoin.ToSkia();
        paint.StrokeMiter = strokeMiterLimit;
        paint.PathEffect = strokeStyle != PenStyle.Solid
            ? strokeStyle.ToSkia(lineWidth, dashArray, dashOffset)
            : null;
        return paint;
    }

    private static void DrawArrow(ArrowVectorStyle arrowStyle, Viewport viewport, SKCanvas canvas, float opacity,
        Coordinate c1, Coordinate c2)
    {
        var arrowHeadPosition = arrowStyle.GetArrowHeadPosition(c1.ToMPoint(), c2.ToMPoint());
        var arrowHeadScreenPosition = viewport.WorldToScreen(arrowHeadPosition);
        var arrowBranchesEndPoints = arrowStyle.GetArrowEndPoints(c1.ToMPoint(), c2.ToMPoint());
        var deltaFirstEndpoint = new Point(arrowBranchesEndPoints[0].X - arrowHeadPosition.X, arrowBranchesEndPoints[0].Y - arrowHeadPosition.Y);
        var deltaSecondEndpoint = new Point(arrowBranchesEndPoints[1].X - arrowHeadPosition.X, arrowBranchesEndPoints[1].Y - arrowHeadPosition.Y);

        DrawArrow(canvas, arrowStyle, arrowHeadScreenPosition.ToCoordinate(), deltaFirstEndpoint.Coordinate, deltaSecondEndpoint.Coordinate, opacity);
    }

    private static void DrawArrow(SKCanvas canvas, IStyle style, Coordinate destination, Coordinate firstEndpoint, Coordinate secondEndpoint, float opacity)
    {
        var vectorStyle = style is VectorStyle ? (VectorStyle)style : new VectorStyle();
        canvas.Save();
        canvas.Translate((float)destination.X, (float)destination.Y);

        var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo((float)firstEndpoint.X, -1 * (float)firstEndpoint.Y);
        path.MoveTo(0, 0);
        path.LineTo((float)secondEndpoint.X, -1 * (float)secondEndpoint.Y);

        if (vectorStyle.Line != null)
        {
            var linePaint = CreateLinePaint(vectorStyle.Line, opacity);
            if (linePaint.Color.Alpha != 0)
            {
                canvas.DrawPath(path, linePaint);
            }
        }

        canvas.Restore();
    }

    private static SKPaint CreateLinePaint(Pen line, float opacity)
    {
        return new SKPaint
        {
            Color = line.Color.ToSkia(opacity),
            StrokeWidth = (float)line.Width,
            StrokeCap = line.PenStrokeCap.ToSkia(),
            PathEffect = line.PenStyle.ToSkia((float)line.Width),
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
    }

    private static Coordinate GetCoordinateOnC1PlusLength(Coordinate c1, Coordinate c2, double length)
    {
        var len = c1.Distance(c2);

        var x = c1.X + length * (c2.X - c1.X) / len;
        var y = c1.Y + length * (c2.Y - c1.Y) / len;

        return new Coordinate(x, y);
    }
}
