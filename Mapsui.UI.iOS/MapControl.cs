using CoreFoundation;
using Mapsui.Logging;
using Mapsui.UI.iOS.Extensions;
using Mapsui.Utilities;
using SkiaSharp.Views.iOS;
using System.ComponentModel;

namespace Mapsui.UI.iOS;

[Register("MapControl"), DesignTimeVisible(true)]
public partial class MapControl : UIView, IMapControl
{
    private SKGLView? _glCanvas;
    private SKCanvasView? _canvas;
    private bool _init;
    private MPoint? _pointerDownPosition;
    private readonly TouchTracker _touchTracker = new();

    public static bool UseGPU { get; set; } = true;

    public MapControl(CGRect frame)
        : base(frame)
    {
        CommonInitialize();
        Initialize();
    }

    [Preserve]
    public MapControl(IntPtr handle) : base(handle) // used when initialized from storyboard
    {
        CommonInitialize();
        Initialize();
    }

    private void InitCanvas()
    {
        if (!_init)
        {
            _init = true;
            if (UseGPU)
            {
                _glCanvas?.Dispose();
                _glCanvas = [];
            }
            else
            {
                _canvas?.Dispose();
                _canvas = [];
            }
        }
    }

    private void Initialize()
    {
        InitCanvas();

        _invalidate = () =>
        {
            RunOnUIThread(() =>
            {
                SetNeedsDisplay();
                _glCanvas?.SetNeedsDisplay();
            });
        };

        BackgroundColor = UIColor.White;

        if (UseGPU)
        {
            _glCanvas!.TranslatesAutoresizingMaskIntoConstraints = false;
            _glCanvas.MultipleTouchEnabled = true;
            _glCanvas.PaintSurface += OnPaintSurface;
            AddSubview(_glCanvas);

            AddConstraints(
            [
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, _glCanvas,
                    NSLayoutAttribute.Leading, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, _glCanvas,
                    NSLayoutAttribute.Trailing, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Top, NSLayoutRelation.Equal, _glCanvas,
                    NSLayoutAttribute.Top, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, _glCanvas,
                    NSLayoutAttribute.Bottom, 1.0f, 0.0f)
            ]);
        }
        else
        {
            _canvas!.TranslatesAutoresizingMaskIntoConstraints = false;
            _canvas.MultipleTouchEnabled = true;
            _canvas.PaintSurface += OnPaintSurface;
            AddSubview(_canvas);

            AddConstraints(
            [
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Leading, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Trailing, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Top, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Top, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, _canvas,
                    NSLayoutAttribute.Bottom, 1.0f, 0.0f)
            ]);
        }

        ClipsToBounds = true;
        MultipleTouchEnabled = true;
        UserInteractionEnabled = true;

        var doubleTapGestureRecognizer = new UITapGestureRecognizer(OnDoubleTapped)
        {
            NumberOfTapsRequired = 2,
            CancelsTouchesInView = false,
        };
        AddGestureRecognizer(doubleTapGestureRecognizer);

        var tapGestureRecognizer = new UITapGestureRecognizer(OnSingleTapped)
        {
            NumberOfTapsRequired = 1,
            CancelsTouchesInView = false,
        };
        tapGestureRecognizer.RequireGestureRecognizerToFail(doubleTapGestureRecognizer);
        AddGestureRecognizer(tapGestureRecognizer);

        Map.Navigator.SetSize(ViewportWidth, ViewportHeight);
    }

    private void OnDoubleTapped(UITapGestureRecognizer gesture)
    {
        var position = GetScreenPosition(gesture.LocationInView(this));
        OnInfo(CreateMapInfoEventArgs(position, position, 2));
    }

    private void OnSingleTapped(UITapGestureRecognizer gesture)
    {
        var position = GetScreenPosition(gesture.LocationInView(this));
        OnInfo(CreateMapInfoEventArgs(position, position, 1));
    }

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs args)
    {
        if (PixelDensity <= 0)
            return;

        var canvas = args.Surface.Canvas;

        canvas.Scale(PixelDensity, PixelDensity);

        CommonDrawControl(canvas);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs args)
    {
        if (PixelDensity <= 0)
            return;

        var canvas = args.Surface.Canvas;

        canvas.Scale(PixelDensity, PixelDensity);

        CommonDrawControl(canvas);
    }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        base.TouchesBegan(touches, evt);

        if (evt?.AllTouches.Count >= 2)
            _touchTracker.Restart(GetLocations(evt, this).ToArray());

        if (touches.AnyObject is UITouch touch)
        {
            _pointerDownPosition = touch.LocationInView(this).ToMapsui();
            if (HandleWidgetPointerDown(_pointerDownPosition, true, 1, false))
            {
                return;
            }
        }
    }

    public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
        base.TouchesMoved(touches, evt);

        if (evt?.AllTouches.Count == 1)
        {
            if (touches.AnyObject is UITouch touch)
            {
                var position = touch.LocationInView(this).ToMapsui();
                if (HandleWidgetPointerMove(position, true, 0, false))
                    return;

                var previousPosition = touch.PreviousLocationInView(this).ToMapsui();
                Map.Navigator.Drag(position, previousPosition);
            }
        }
        else if (evt?.AllTouches.Count >= 2)
        {
            _touchTracker.Update(GetLocations(evt, this).ToArray());
            Map.Navigator.Pinch(_touchTracker.GetTouchManipulation());
        }
    }

    private static List<MPoint> GetLocations(UIEvent uiEvent, UIView uiView)    
        => uiEvent.AllTouches.Select(t => ((UITouch)t).LocationInView(uiView)).Select(p => new MPoint(p.X, p.Y)).ToList();
    

    public override void TouchesEnded(NSSet touches, UIEvent? e)
    {
        Refresh();

        if (touches.AnyObject is UITouch touch)
        {
            var position = touch.LocationInView(this).ToMapsui();
            if (HandleWidgetPointerUp(position, _pointerDownPosition, true, 1, false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Gets screen position in device independent units (or DIP or DP).
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private static MPoint GetScreenPosition(CGPoint point)
    {
        return new MPoint(point.X, point.Y);
    }

    private static void RunOnUIThread(Action action)
    {
        DispatchQueue.MainQueue.DispatchAsync(action);
    }

    public override CGRect Frame
    {
        get => base.Frame;
        set
        {
            InitCanvas();
            if (UseGPU)
            {
                _glCanvas!.Frame = value;
            }
            else
            {
                _canvas!.Frame = value;
            }

            base.Frame = value;
            SetViewportSize();
            OnPropertyChanged();
        }
    }

    public override void LayoutMarginsDidChange()
    {
        InitCanvas();
        if (_glCanvas == null || _canvas == null) return;

        base.LayoutMarginsDidChange();
        SetViewportSize();
    }

    public async void OpenBrowser(string url)
    {
        try
        {
            await UIApplication.SharedApplication.OpenUrlAsync(new NSUrl(url), new UIApplicationOpenUrlOptions());
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, ex.Message, ex);
        }
    }

    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected new virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            IosCommonDispose(disposing);
            base.Dispose(disposing);
        }
    }

    private void IosCommonDispose(bool disposing)
    {
        if (disposing)
        {
            _map?.Dispose();
            Unsubscribe();
            _glCanvas?.Dispose();
            _canvas?.Dispose();
        }

        CommonDispose(disposing);
    }

    private double ViewportWidth
    {
        get
        {
            InitCanvas();
            return UseGPU
                ? _glCanvas!.Frame.Width
                : _canvas!.Frame.Width;
        }
    }

    private double ViewportHeight
    {
        get
        {
            InitCanvas();
            return UseGPU
                ? _glCanvas!.Frame.Height
                : _canvas!.Frame.Height;
        }
    }

    private double GetPixelDensity()
    {
        InitCanvas();
        return UseGPU
            ? (double)_glCanvas!.ContentScaleFactor
            : (double)_canvas!.ContentScaleFactor;
    }
}
