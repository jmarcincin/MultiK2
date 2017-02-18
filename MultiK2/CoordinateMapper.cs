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
        private Matrix4x4? _depthToColor;
        private Matrix4x4? _colorToDepth;

        private SpatialCoordinateSystem _depthSystem;
        private SpatialCoordinateSystem _colorSystem;

        public Vector3 MapDepthSpacePointToColor(Vector3 depthSpacePoint)
        {
            if (!_depthToColor.HasValue)
            {
                throw new InvalidOperationException();
            }

            // TODO: throw an excepton if no mapping matrix was set 
            return Vector3.Transform(depthSpacePoint, _depthToColor.Value);
        }

        public Vector3 MapColorSpacePointToDepth(Vector3 colorSpacePoint)
        {
            if (!_colorToDepth.HasValue)
            {
                throw new InvalidOperationException();
            }

            // TODO: throw an excepton if no mapping matrix was set 
            return Vector3.Transform(colorSpacePoint, _colorToDepth.Value);
        }

        internal void UpdateFromDepth(SpatialCoordinateSystem depthSystem)
        {
            _depthSystem = depthSystem;
            if (_colorSystem != null)
            {
                _depthToColor = _depthSystem.TryGetTransformTo(_colorSystem);
                _colorToDepth = _colorSystem.TryGetTransformTo(_depthSystem);
            } 
        }

        internal void UpdateFromColor(SpatialCoordinateSystem colorSystem)
        {
            _colorSystem = colorSystem;
            if (_depthSystem != null)
            {
                _depthToColor = _depthSystem.TryGetTransformTo(_colorSystem);
                _colorToDepth = _colorSystem.TryGetTransformTo(_depthSystem);
            }
        }

    }    
}
