using System;

namespace Mapsui.Styles;
public class ArrowVectorStyle : VectorStyle
{
    /// <summary>
    /// Tilt angle for the arrow branches (degree)
    /// </summary>
    public double TiltAngle { get; set; } = 45;

    /// <summary>
    /// Length of the arrow branches (px)
    /// </summary>
    public double BranchesLength { get; set; } = 10;

    /// <summary>
    /// Offset to position of the arrow branches
    /// </summary>
    public double BranchesOffset { get; set; } = 1;

    /// <summary>
    /// Distance between near arrows
    /// </summary>
    public double Distance { get; set; } = 0;

    /// <summary>
    /// Compute the arrow head position
    /// </summary>
    public MPoint GetArrowHeadPosition(MPoint startPoint, MPoint endPoint)
    {
        return new MPoint(startPoint.X + (endPoint.X - startPoint.X) * BranchesOffset,
            startPoint.Y + (endPoint.Y - startPoint.Y) * BranchesOffset);
    }

    /// <summary>
    /// Compute the arrow branches endpoints
    /// </summary>
    public MPoint[] GetArrowEndPoints(MPoint startPoint, MPoint endPoint)
    {
        var radiansAngle = Utilities.Algorithms.DegreesToRadians(TiltAngle);
        var arrowHeadPosition = GetArrowHeadPosition(startPoint, endPoint);

        var tilt = Math.Abs(startPoint.X - endPoint.X) < 0.000001
            ? double.PositiveInfinity
            : (startPoint.Y - endPoint.Y) / (startPoint.X - endPoint.X);

        var arrowOrientation = (startPoint.X - endPoint.X) /
                               Math.Abs(startPoint.X - endPoint.X);

        var firstEndpoint =
            new MPoint(arrowHeadPosition.X + arrowOrientation * BranchesLength * Math.Cos(Math.Atan(tilt) - radiansAngle),
                arrowHeadPosition.Y + arrowOrientation * BranchesLength * Math.Sin(Math.Atan(tilt) - radiansAngle));
        var secondEndpoint =
            new MPoint(arrowHeadPosition.X + arrowOrientation * BranchesLength * Math.Cos(Math.Atan(tilt) + radiansAngle),
                arrowHeadPosition.Y + arrowOrientation * BranchesLength * Math.Sin(Math.Atan(tilt) + radiansAngle));

        return new[] { firstEndpoint, secondEndpoint };
    }
}
