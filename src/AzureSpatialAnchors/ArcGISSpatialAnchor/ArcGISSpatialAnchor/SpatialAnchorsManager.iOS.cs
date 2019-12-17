#if __IOS__
using ARKit;
using Microsoft.Azure.SpatialAnchors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ArcGISSpatialAnchor
{
    public partial class SpatialAnchorsManager
    {
        public CloudSpatialAnchor CreateLocalAnchor(Xamarin.Forms.Point screenLocation)
        {
            var hitResults = _nativeView.ARSCNView?.HitTest(ToNativePoint(screenLocation), ARHitTestResultType.FeaturePoint);
            var hit = hitResults?.FirstOrDefault();
            if (hit != null)
            {
                return new CloudSpatialAnchor() { LocalAnchor = new ARAnchor(hit.WorldTransform) };
            }
            return null;
        }

        private void NativeView_ARSCNViewWillRenderScene(object sender, Esri.ArcGISRuntime.ARToolkit.ARSCNViewRenderSceneEventArgs e)
        {
            using (ARFrame frame = _nativeView?.ARSCNView?.Session?.CurrentFrame)
            {
                if (frame == null)
                {
                    return;
                }

                if (cloudSession == null)
                {
                    return;
                }

                cloudSession.ProcessFrame(frame);

                //if (currentlyPlacingAnchor && enoughDataForSaving && localAnchor != null)
                //{
                //    CreateCloudAnchor();
                //}
            }
        }

        private void NativeView_ARSCNViewInterruptionEnded(object sender, EventArgs e) => cloudSession.Reset();

        internal static CoreGraphics.CGPoint ToNativePoint(Xamarin.Forms.Point xfPoint) => new CoreGraphics.CGPoint((nfloat)xfPoint.X, (nfloat)xfPoint.Y);
    }
}
#endif