#if __ANDROID__
using System;
using Microsoft.Azure.SpatialAnchors;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using Google.AR.Sceneform;
using Java.Util.Concurrent;

namespace ArcGISSpatialAnchor
{
    public partial class SpatialAnchorsManager
    {
        private UpdateListener? _updateListener;
        private Google.AR.Core.TrackingState lastTrackingState;
        private Google.AR.Core.TrackingFailureReason lastTrackingFailureReason;

        private void OnUpdate(FrameTime frameTime)
        {
            var frame = _nativeView.ArSceneView.ArFrame;
            if (frame.Camera.TrackingState != this.lastTrackingState
               || frame.Camera.TrackingFailureReason != this.lastTrackingFailureReason)
            {
                this.lastTrackingState = frame.Camera.TrackingState;
                this.lastTrackingFailureReason = frame.Camera.TrackingFailureReason;
                Debug.WriteLine($"Tracker state changed: {this.lastTrackingState}, {this.lastTrackingFailureReason}.");
            }

            Task.Run(() => this.cloudSession.ProcessFrame(frame));
        }

        private class UpdateListener : Java.Lang.Object, Google.AR.Sceneform.Scene.IOnUpdateListener
        {
            private readonly Action<Google.AR.Sceneform.FrameTime> _onUpdate;

            public UpdateListener(Action<Google.AR.Sceneform.FrameTime> onUpdate)
            {
                _onUpdate = onUpdate ?? throw new ArgumentNullException(nameof(onUpdate));
            }

            void Google.AR.Sceneform.Scene.IOnUpdateListener.OnUpdate(Google.AR.Sceneform.FrameTime p0) => _onUpdate(p0);
        }

       // Screen coordinates for native Android is in physical pixels, but XF works in DIPs so apply the factor on conversion
        private static Android.Views.IWindowManager s_windowManager;
        internal static float SystemPixelToDipsFactor
        {
            get
            {
                var displayMetrics = new Android.Util.DisplayMetrics();
                if (s_windowManager == null)
                {
                    var windowService = Android.App.Application.Context?.GetSystemService(Android.Content.Context.WindowService);
                    if (windowService != null)
                        s_windowManager = Android.Runtime.Extensions.JavaCast<Android.Views.IWindowManager>(windowService);
                }
                if (s_windowManager == null)
                    return 1f;
                s_windowManager.DefaultDisplay.GetMetrics(displayMetrics);
                return displayMetrics?.Density ?? 1f;
            }
        }

        internal static Android.Graphics.PointF ToNativePoint(Xamarin.Forms.Point xfPoint)
        {
            return new Android.Graphics.PointF((float)(xfPoint.X * SystemPixelToDipsFactor), (float)(xfPoint.Y * SystemPixelToDipsFactor));
        }

        public CloudSpatialAnchor CreateLocalAnchor(Xamarin.Forms.Point screenLocation)
        {
            var p = ToNativePoint(screenLocation);
            var hitResults = _nativeView?.ArSceneView?.ArFrame?.HitTest(p.X, p.Y);
            if (hitResults != null) 
            {
                foreach (var item in hitResults)
                {
                    if (item.Trackable is Google.AR.Core.Plane pl && pl.IsPoseInPolygon(item.HitPose) ||
                       item.Trackable is Google.AR.Core.Point pnt && pnt.GetOrientationMode() == Google.AR.Core.Point.OrientationMode.EstimatedSurfaceNormal)
                    {
                        return new CloudSpatialAnchor() { LocalAnchor = item.CreateAnchor() };
                    }
                }
            }
            return null;
        }
    }
}
#endif