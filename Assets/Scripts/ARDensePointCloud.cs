using System;
using Unity.Collections;
using UnityEngine;

namespace Cdm.XR.Extensions
{
    public class ARDensePointCloud : MonoBehaviour, IDisposable
    {
        private NativeArray<Vector3> _points;
        private NativeArray<Vector3> _normals;
        private NativeArray<Color32> _colors;
        private NativeArray<float> _confidences;
        
        private bool _isUpdating;
        private int _updateStartIndex;

        public NativeArray<Vector3> points => _points.GetSubArray(0, count);
        public NativeArray<Vector3> normals => _normals.GetSubArray(0, count);
        public NativeArray<Color32> colors => _colors.GetSubArray(0, count);
        public NativeArray<float> confidences => _confidences.GetSubArray(0, count);

        public int capacity { get; private set; } = 0;
        public int count { get; private set; } = 0;
        public bool isFull => count == capacity;

        private void OnDestroy()
        {
            Dispose();
        }

        public void Create(int capacity)
        {
            this.capacity = capacity;
            
            _points = new NativeArray<Vector3>(capacity, Allocator.Persistent);
            _normals = new NativeArray<Vector3>(capacity, Allocator.Persistent);
            _colors = new NativeArray<Color32>(capacity, Allocator.Persistent);
            _confidences = new NativeArray<float>(capacity, Allocator.Persistent);;
            count = 0;
        }

        public void CopyFrom(ARDensePointCloud target)
        {
            Create(target.capacity);

            BeginUpdate();
            target._points.CopyTo(_points);
            target._normals.CopyTo(_normals);
            target._colors.CopyTo(_colors);
            target._confidences.CopyTo(_confidences);
            count = target.count;
            EndUpdate();
        }

        public void Reset()
        {
            count = 0;
        }

        public void Dispose()
        {
            _points.Dispose();
            _normals.Dispose();
            _colors.Dispose();
            _confidences.Dispose();
        }
        
        public void Add(Vector3 position, Vector3 normal, Color32 color, float confidence)
        {
            if (!_isUpdating)
                throw new InvalidOperationException("Call BeginUpdate() before adding points");
            
            if (count < capacity)
            {
                _points[count] = position;
                _normals[count] = normal;
                _colors[count] = color;
                _confidences[count] = confidence;
                count++;
            }
        }

        public void BeginUpdate()
        {
            _updateStartIndex = count;
            _isUpdating = true;
        }

        public void EndUpdate()
        {
            _isUpdating = false;
            OnPointCloudUpdated(
                new PointCloudUpdatedEventArgs(this, _updateStartIndex, count - _updateStartIndex));
        }
        
        private void OnPointCloudUpdated(PointCloudUpdatedEventArgs e)
        {
            pointCloudUpdated?.Invoke(e);
        }

        public event Action<PointCloudUpdatedEventArgs> pointCloudUpdated;
    }
    
    public struct PointCloudUpdatedEventArgs
    {
        public ARDensePointCloud pointCloud { get; }
        public int startIndex { get; }
        public int count { get; }

        public PointCloudUpdatedEventArgs(ARDensePointCloud pointCloud, int startIndex, int count)
        {
            this.pointCloud = pointCloud;
            this.startIndex = startIndex;
            this.count = count;
        }
    }
}