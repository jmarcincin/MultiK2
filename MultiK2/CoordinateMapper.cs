using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Perception.Spatial;

namespace MultiK2
{
    public sealed class CoordinateMapper
    {
        private SpatialCoordinateSystem _depthSystem;
        private SpatialCoordinateSystem _colorSystem;
        
        internal Matrix4x4? DepthToColor { get; set; }
        internal Matrix4x4? ColorToDepth { get; set; }

        public Vector3 MapDepthSpacePointToColor(Vector3 depthSpacePoint)
        {
            if (!DepthToColor.HasValue)
            {
                throw new InvalidOperationException();
            }

            // TODO: throw an excepton if no mapping matrix was set 
            return Vector3.Transform(depthSpacePoint, DepthToColor.Value);
        }

        public Vector3 MapColorSpacePointToDepth(Vector3 colorSpacePoint)
        {
            if (!ColorToDepth.HasValue)
            {
                throw new InvalidOperationException();
            }

            // TODO: throw an excepton if no mapping matrix was set 
            return Vector3.Transform(colorSpacePoint, ColorToDepth.Value);
        }

        internal void UpdateFromDepthFrame(SpatialCoordinateSystem depthSystem)
        {
            _depthSystem = depthSystem;
            if (_colorSystem != null)
            {
                DepthToColor = _depthSystem.TryGetTransformTo(_colorSystem);
                ColorToDepth = _colorSystem.TryGetTransformTo(_depthSystem);
            } 
        }

        internal void UpdateFromColorFrame(SpatialCoordinateSystem colorSystem)
        {
            _colorSystem = colorSystem;
            if (_depthSystem != null)
            {
                DepthToColor = _depthSystem.TryGetTransformTo(_colorSystem);
                ColorToDepth = _colorSystem.TryGetTransformTo(_depthSystem);
            }
        }
    }    
}
