using Mapsui.Extensions;
using Mapsui.Manipulations;
using Mapsui.UI.Wpf.Extensions;
using Mapsui.Utilities;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MapsuiManipulation = Mapsui.Manipulations.Manipulation;

namespace Mapsui.UI.Wpf;

public partial class MapControl : Grid, IMapControl, IDisposable
{
    private readonly Rectangle _selectRectangle = CreateSelectRectangle();
    private ScreenPosition _pointerDownPosition;
    private bool _mouseDown;
    private ScreenPosition _previousMousePosition;
    private ScreenPosition _currentMousePosition;

    private readonly ManipulationTracker _manipulationTracker = new();

    public MapControl()
    {
        SharedConstructor();

        _invalidate = () =>
        {
            if (Dispatcher.CheckAccess()) InvalidateCanvas();
            else RunOnUIThread(InvalidateCanvas);
        };

        Children.Add(SkiaCanvas);

        SkiaCanvas.PaintSurface += SKElementOnPaintSurface;
        Loaded += MapControlLoaded;
        SizeChanged += MapControlSizeChanged;

        MouseLeftButtonDown += MapControlMouseLeftButtonDown;
        MouseLeftButtonUp += MapControlMouseLeftButtonUp;

        MouseMove += MapControlMouseMove;
        MouseLeave += MapControlMouseLeave;
        MouseWheel += MapControlMouseWheel;

        ManipulationInertiaStarting += OnManipulationInertiaStarting;
        ManipulationDelta += OnManipulationDelta;
        ManipulationCompleted += OnManipulationCompleted;

        TouchDown += MapControl_TouchDown;
        TouchUp += MapControlTouchUp;

        IsManipulationEnabled = true;

        SkiaCanvas.Visibility = Visibility.Visible;
        RefreshGraphics();
    }

    //private void FixMouseUpPosition(MouseButtonEventArgs e)
    //{
    //    var mousePosition = e.GetPosition(this).ToScreenPosition();

    //    if (_previousMousePosition != null)
    //    {
    //        if (IsInBoxZoomMode())
    //        {
    //            var previous = Map.Navigator.Viewport.ScreenToWorld(_previousMousePosition.X, _previousMousePosition.Y);
    //            var current = Map.Navigator.Viewport.ScreenToWorld(mousePosition.X, mousePosition.Y);
    //            ZoomToBox(previous, current);
    //        }
    //        else if (_pointerDownPosition != null && IsClick(mousePosition, _pointerDownPosition))
    //        {
    //            //HandleFeatureInfo(e);
    //            OnInfo(CreateMapInfoEventArgs(mousePosition, _pointerDownPosition, e.ClickCount));
    //        }
    //    }

    //    RefreshData();
    //    _mouseDown = false;

    //    double velocityX;
    //    double velocityY;

    //    (velocityX, velocityY) = _flingTracker.CalcVelocity(1, DateTime.Now.Ticks);

    //    if (Math.Abs(velocityX) > 200 || Math.Abs(velocityY) > 200)
    //    {
    //        // This was the last finger on screen, so this is a fling
    //        e.Handled = OnFlinged(velocityX, velocityY);
    //    }
    //    _flingTracker.RemoveId(1);

    //    _previousMousePosition = new MPoint();
    //    ReleaseMouseCapture();
    //}

    //private void FixMouseDownPosition(MouseButtonEventArgs e)
    //{
    //    var touchPosition = e.GetPosition(this).ToScreenPosition();
    //    _previousMousePosition = touchPosition;
    //    _pointerDownPosition = touchPosition;
    //    _mouseDown = true;
    //    _flingTracker.Clear();
    //    CaptureMouse();
    //}

    //private void HandleFeatureInfo(MouseButtonEventArgs e)
    //{
    //    if (FeatureInfo == null) return; // don't fetch if you the call back is not set.

    //    if (_pointerDownPosition == e.GetPosition(this).ToMapsui())
    //        foreach (var layer in Map.Layers)
    //        {
    //            // ReSharper disable once SuspiciousTypeConversion.Global
    //            (layer as IFeatureInfo)?.GetFeatureInfo(Map.Navigator.Viewport, _pointerDownPosition.X, _pointerDownPosition.Y,
    //                OnFeatureInfo);
    //        }
    //}

    //private void OnFeatureInfo(IDictionary<string, IEnumerable<IFeature>> features)
    //{
    //    FeatureInfo?.Invoke(this, new FeatureInfoEventArgs { FeatureInfo = features });
    //}

    private static Rectangle CreateSelectRectangle()
    {
        return new Rectangle
        {
            Fill = new SolidColorBrush(Colors.Red),
            Stroke = new SolidColorBrush(Colors.Black),
            StrokeThickness = 3,
            RadiusX = 0.5,
            RadiusY = 0.5,
            StrokeDashArray = [3.0],
            Opacity = 0.3,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed
        };
    }

    private SKElement SkiaCanvas { get; } = CreateSkiaRenderElement();

    private static SKElement CreateSkiaRenderElement()
    {
        return new SKElement
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    [Obsolete("Use Info and ILayerFeatureInfo", true)]
    public event EventHandler<FeatureInfoEventArgs>? FeatureInfo; // todo: Remove and add sample for alternative

    internal void InvalidateCanvas()
    {
        SkiaCanvas.InvalidateVisual();
    }

    private void MapControlLoaded(object sender, RoutedEventArgs e)
    {
        SetViewportSize();

        Focusable = true;
    }

    private void MapControlMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouseWheelDelta = e.Delta;
        _currentMousePosition = e.GetPosition(this).ToScreenPosition();
        Map.Navigator.MouseWheelZoom(mouseWheelDelta, _currentMousePosition);
    }

    private void MapControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, ActualWidth, ActualHeight) };
        SetViewportSize();
    }

    private void MapControlMouseLeave(object sender, MouseEventArgs e)
    {
        _previousMousePosition = new ScreenPosition();
        ReleaseMouseCapture();
    }

    private void RunOnUIThread(Action action)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void MapControlMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        //if (System.Configuration.ConfigurationManager.AppSettings.Get("MapMoveMouseButton") != "Middle")
        //{
        //    FixMouseDownPosition(e);
        //}

        var position = e.GetPosition(this).ToScreenPosition();
        _manipulationTracker.Restart([position]);

        if (OnMapPointerPressed([position]))
            return;

        CaptureMouse();

        //_pointerDownPosition = e.GetPosition(this).ToMapsui();

        //if (HandleWidgetPointerDown(_pointerDownPosition, true, e.ClickCount, GetShiftPressed()))
        //    return;

        //_previousMousePosition = _pointerDownPosition;
        //_mouseDown = true;
        //_flingTracker.Clear();
        //CaptureMouse();
    }

    private static bool IsInBoxZoomMode()
    {
        return Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
    }

    private void MapControlMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this).ToScreenPosition();
        OnMapPointerReleased([position]);
        ReleaseMouseCapture();

        //if (System.Configuration.ConfigurationManager.AppSettings.Get("MapMoveMouseButton") != "Middle")
        //{
        //    FixMouseUpPosition(e);
        //}

        //var mousePosition = e.GetPosition(this).ToMapsui();

        //if (HandleWidgetPointerUp(mousePosition, _pointerDownPosition, true, e.ClickCount, GetShiftPressed()))
        //{
        //    _mouseDown = false;

        //    return;
        //}

        //if (_previousMousePosition != null)
        //{
        //    if (IsInBoxZoomMode())
        //    {
        //        var previous = Map.Navigator.Viewport.ScreenToWorld(_previousMousePosition.X, _previousMousePosition.Y);
        //        var current = Map.Navigator.Viewport.ScreenToWorld(mousePosition.X, mousePosition.Y);
        //        ZoomToBox(previous, current);
        //    }
        //    else if (_pointerDownPosition != null && IsClick(mousePosition, _pointerDownPosition))
        //    {
        //        OnInfo(CreateMapInfoEventArgs(mousePosition, _pointerDownPosition, e.ClickCount));
        //    }
        //}

        //RefreshData();
        //_mouseDown = false;

        //double velocityX;
        //double velocityY;

        //(velocityX, velocityY) = _flingTracker.CalcVelocity(1, DateTime.Now.Ticks);

        //if (Math.Abs(velocityX) > 200 || Math.Abs(velocityY) > 200)
        //{
        //    // This was the last finger on screen, so this is a fling
        //    e.Handled = OnFlinged(velocityX, velocityY);
        //}
        //_flingTracker.RemoveId(1);

        //_previousMousePosition = new MPoint();
        //ReleaseMouseCapture();
    }

    private void MapControl_TouchDown(object? sender, TouchEventArgs e)
    {
        var position = e.GetTouchPoint(this).Position.ToScreenPosition();
        if (OnMapPointerPressed([position]))
            return;



        //todo
        //if (System.Configuration.ConfigurationManager.AppSettings.Get("MapMoveMouseButton") == "Middle")
        //{
        //    if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
        //    {
        //        FixMouseDownPosition(e);
        //    }
        //}
    }

    private void MapControlTouchUp(object? sender, TouchEventArgs e)
    {
        var position = e.GetTouchPoint(this).Position.ToScreenPosition();
        if (OnMapPointerReleased([position]))
            return;


        //todo
        //if (System.Configuration.ConfigurationManager.AppSettings.Get("MapMoveMouseButton") == "Middle")
        //{
        //    if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
        //    {
        //        FixMouseUpPosition(e);
        //    }
        //}
    }

    public void OpenInBrowser(string url)
    {
        Catch.TaskRun(() =>
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                // The default for this has changed in .net core, you have to explicitly set if to true for it to work.
                UseShellExecute = true
            });
        });
    }

    private void MapControlMouseMove(object sender, MouseEventArgs e)
    {
        var isHovering = IsHovering(e);
        var position = e.GetPosition(this).ToScreenPosition();

        if (OnMapPointerMoved([position], isHovering))
            return;

        if (!isHovering)
            _manipulationTracker.Manipulate([position], Map.Navigator.Manipulate);
    }

    private double ViewportWidth => ActualWidth;
    private double ViewportHeight => ActualHeight;

    private static void OnManipulationInertiaStarting(object? sender, ManipulationInertiaStartingEventArgs e)
    {
        e.TranslationBehavior.DesiredDeceleration = 25 * 96.0 / (1000.0 * 1000.0);
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        Map.Navigator.Manipulate(ToManipulation(e));
    }

    private static MapsuiManipulation ToManipulation(ManipulationDeltaEventArgs e)
    {
        var translation = e.DeltaManipulation.Translation;

        var previousCenter = e.ManipulationOrigin.ToScreenPosition();
        var center = previousCenter.Offset(translation.X, translation.Y);
        var scaleFactor = GetScaleFactor(e.DeltaManipulation.Scale);
        var rotationChange = e.DeltaManipulation.Rotation;

        return new MapsuiManipulation(center, previousCenter, scaleFactor, rotationChange, e.CumulativeManipulation.Rotation);
    }

    private static double GetScaleFactor(Vector scale)
    {
        var deltaScale = (scale.X + scale.Y) / 2;
        if (Math.Abs(deltaScale) < Constants.Epsilon)
            return 1; // If there is no scaling the deltaScale will be 0.0 in Windows Phone (while it is 1.0 in wpf)
        if (!(Math.Abs(deltaScale - 1d) > Constants.Epsilon)) return 1;
        return deltaScale;
    }

    private void OnManipulationCompleted(object? sender, ManipulationCompletedEventArgs e) => Refresh();

    private void SKElementOnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        if (PixelDensity <= 0)
            return;
        var canvas = args.Surface.Canvas;
        canvas.Scale(PixelDensity, PixelDensity);
        CommonDrawControl(canvas);
    }

    private double GetPixelDensity()
    {
        var presentationSource = PresentationSource.FromVisual(this)
            ?? throw new Exception("PresentationSource is null");
        var compositionTarget = presentationSource.CompositionTarget
            ?? throw new Exception("CompositionTarget is null");

        var matrix = compositionTarget.TransformToDevice;

        var dpiX = matrix.M11;
        var dpiY = matrix.M22;

        if (dpiX != dpiY) throw new ArgumentException();

        return dpiX;
    }

    protected virtual void Dispose(bool disposing)
    {
        CommonDispose(disposing);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private static bool GetShiftPressed()
    {
        return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
    }

    private static bool IsHovering(MouseEventArgs e)
    {
        return e.LeftButton != MouseButtonState.Pressed;
    }
}
