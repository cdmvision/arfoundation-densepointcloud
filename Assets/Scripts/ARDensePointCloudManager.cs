using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Cdm.XR.Extensions
{
    public class ARDensePointCloudManager : MonoBehaviour
    {
        private static readonly List<ARDensePointCloud> _pointClouds = new List<ARDensePointCloud>();
        
        public ARDensePointCloud pointCloudPrefab;

        // Maximum number of points we store in a point cloud.
        public int maxPoints = 3000000;
        public int maxPointsPerFrame = 500;

        [SerializeField, Range(0f, 1f)] 
        private float _minConfidence = 0.5f;

        public float minConfidence
        {
            get => _minConfidence;
            set => _minConfidence = Mathf.Clamp01(value);
        }

        [SerializeField, Tooltip("Max rotation angle in degrees.")]
        private float _cameraRotationThreshold = 2;

        [SerializeField, Tooltip("Max translation in meters")]
        private float _cameraTranslationThreshold = 0.02f;

        // Camera's threshold values for detecting when the camera moves so that we can accumulate the points.
        private float cameraRotationThreshold => Mathf.Cos(_cameraRotationThreshold * Mathf.Deg2Rad);
        private float cameraTranslationThreshold => Mathf.Pow(_cameraTranslationThreshold, 2); // (meter-squared)

        private XRSessionSubsystem _sessionSubsystem;
        private XROcclusionSubsystem _occlusionSubsystem;
        private XRCameraSubsystem _cameraSubsystem;

        public ARDensePointCloud pointCloud { get; private set; }

        private Camera _mainCamera;
        private Pose _lastCameraPose;

        private Vector2Int[] _samplingGrid;
        private int _depthWidth;
        private int _depthHeight;

        private static readonly List<ARDensePointCloud> _pointCloudsAdded = new List<ARDensePointCloud>();
        private static readonly List<ARDensePointCloud> _pointCloudsUpdated = new List<ARDensePointCloud>();
        private static readonly List<ARDensePointCloud> _pointCloudsRemoved = new List<ARDensePointCloud>();

        private void Start()
        {
            pointCloud = Instantiate(pointCloudPrefab);
            pointCloud.Create(maxPoints);
            _pointClouds.Add(pointCloud);
            OnPointCloudAdded();

            _mainCamera = Camera.main;

#if !UNITY_EDITOR
            StartCoroutine(InitializeSubsystems());
#endif
        }
        
        public void DestroyAllPointClouds()
        {
            foreach (var pc in _pointClouds)
            {
                if (pc != null)
                {
                    _pointCloudsRemoved.Add(pc);
                    Destroy(pc.gameObject);
                }
            }
            
            _pointClouds.Clear();
            OnPointCloudsChanged();
        }
        
        private unsafe void Update()
        {
            if (_sessionSubsystem == null || _cameraSubsystem == null || _occlusionSubsystem == null)
                return;

            if (_sessionSubsystem.trackingState != TrackingState.Tracking)
                return;

            if (!ShouldAccumulatePoints())
                return;

            XRCpuImage cameraImage = default;
            XRCpuImage depthImage = default;
            XRCpuImage depthConfidenceImage = default;

            try
            {
                if (!_cameraSubsystem.TryAcquireLatestCpuImage(out cameraImage))
                    throw new InvalidOperationException("Cannot acquire camera image");

                if (!_occlusionSubsystem.TryAcquireEnvironmentDepthCpuImage(out depthImage))
                    throw new InvalidOperationException("Cannot acquire depth image");

                if (!_occlusionSubsystem.TryAcquireEnvironmentDepthConfidenceCpuImage(out depthConfidenceImage))
                    throw new InvalidOperationException("Cannot acquire depth confidence map image");

                // Debug.Log($"Screen size: {Screen.width}x{Screen.height}");
                // Debug.Log($"Depth image size: {depthImage.width}x{depthImage.height}");

                var conversionParams = new XRCpuImage.ConversionParams()
                {
                    inputRect = new RectInt(0, 0, cameraImage.width, cameraImage.height),
                    outputDimensions = new Vector2Int(depthImage.width, depthImage.height),
                    outputFormat = TextureFormat.RGBA32,
                    transformation = XRCpuImage.Transformation.None
                };

                var cameraImageSize = cameraImage.GetConvertedDataSize(conversionParams);
                var cameraImageBuffer = new NativeArray<Color32>(cameraImageSize, Allocator.Temp);
                cameraImage.Convert(conversionParams, new IntPtr(cameraImageBuffer.GetUnsafePtr()),
                    cameraImageBuffer.Length);

                var depthValues = depthImage.GetPlane(0).data.Reinterpret<float>();
                var depthConfidenceValues = depthConfidenceImage.GetPlane(0).data;

                if (_samplingGrid == null || _depthWidth != depthImage.width || _depthHeight != depthImage.height)
                {
                    CreateSamplingGrid(depthImage.width, depthImage.height);
                    _depthWidth = depthImage.width;
                    _depthHeight = depthImage.height;
                }

                pointCloud.BeginUpdate();
                foreach (var c in _samplingGrid)
                {
                    var i = c.x + c.y * depthImage.width;

                    var npx = c.x / (float) depthImage.width;
                    var npy = 1f - (c.y / (float) depthImage.height);

                    var color = cameraImageBuffer[i];
                    var depth = depthValues[i];
                    var depthConfidence = ClampConfidence01(depthConfidenceValues[i]);

                    var worldPoint =
                        _mainCamera.ScreenToWorldPoint(new Vector3(Screen.width * npx, Screen.height * npy, depth));

                    if (!pointCloud.isFull)
                    {
                        if (depthConfidence >= minConfidence)
                        {
                            var normal = (_mainCamera.transform.position - worldPoint).normalized;
                            pointCloud.Add(worldPoint, normal, color, depthConfidence);
                        }
                            
                    }
                    else
                    {
                        Debug.LogWarning("Max point cloud size has been reached.");
                    }
                }

                pointCloud.EndUpdate();

                OnPointCloudUpdated();
            }
            finally
            {
                _lastCameraPose = new Pose(_mainCamera.transform.position, _mainCamera.transform.rotation);

                cameraImage.Dispose();
                depthImage.Dispose();
                depthConfidenceImage.Dispose();
            }
        }

        private static float ClampConfidence01(byte confidence)
        {
            switch (confidence)
            {
                case 0: return 0; // Low
                case 1: return 0.5f; // Medium
                case 2: return 1f; // High
                default: return 0f;
            }
        }

        private void CreateSamplingGrid(int width, int height)
        {
            var gridArea = width * height;
            var spacing = Mathf.Sqrt(gridArea / (float) maxPointsPerFrame);
            var deltaX = Mathf.RoundToInt(width / spacing);
            var deltaY = Mathf.RoundToInt(height / spacing);

            _samplingGrid = new Vector2Int[deltaX * deltaY];
            var i = 0;
            for (var y = 0; y < deltaY; y++)
            {
                var alternatingOffsetX = (y % 2) * spacing / 2f;

                for (var x = 0; x < deltaX; x++)
                {
                    var point = new Vector2Int
                    (
                        Mathf.FloorToInt(alternatingOffsetX + (x + 0.5f) * spacing),
                        Mathf.FloorToInt((y + 0.5f) * spacing)
                    );
                    _samplingGrid[i++] = point;
                }
            }
        }

        private bool ShouldAccumulatePoints()
        {
            var cameraTransform = _mainCamera.transform;
            return pointCloud.count == 0 ||
                   Vector3.Dot(_lastCameraPose.forward, cameraTransform.forward) <= cameraRotationThreshold ||
                   (_lastCameraPose.position - cameraTransform.position).sqrMagnitude >= cameraTranslationThreshold;
        }

        private IEnumerator InitializeSubsystems()
        {
            // Try get AR session subsystem and make sure AR is supported by the device.
            if (TryGetFirstSubsystem(out _sessionSubsystem))
            {
                var availabilityPromise = _sessionSubsystem.GetAvailabilityAsync();
                yield return availabilityPromise;
                var availability = availabilityPromise.result;
                if (!availability.IsSupported())
                {
                    Debug.LogError($"The current device is not AR capable (but may require a software update).");
                    yield break;
                }
            }
            else
            {
                Debug.LogError($"{nameof(XRSessionSubsystem)} not found.");
                yield break;
            }

            if (!TryGetFirstSubsystem(out _cameraSubsystem))
            {
                Debug.LogError($"{nameof(XRCameraSubsystem)} not found.");
                yield break;
            }

            if (!TryGetFirstSubsystem(out _occlusionSubsystem))
            {
                Debug.LogError($"{nameof(XROcclusionSubsystem)} not found.");
                yield break;
            }

            _occlusionSubsystem.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
            _occlusionSubsystem.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.PreferEnvironmentOcclusion;
        }

        private static bool TryGetFirstSubsystem<T>(out T subsystem) where T : ISubsystem
        {
            var subsystems = new List<T>();
            SubsystemManager.GetInstances(subsystems);

            if (subsystems.Any())
            {
                subsystem = subsystems.First();
                return true;
            }

            subsystem = default(T);
            return false;
        }

        public static IEnumerable<ARDensePointCloud> GetAllPointClouds()
        {
            return _pointClouds;
        }

        private void OnPointCloudAdded()
        {
            Debug.Assert(pointCloud != null);
            _pointCloudsAdded.Add(pointCloud);
            OnPointCloudsChanged();
        }

        private void OnPointCloudUpdated()
        {
            Debug.Assert(pointCloud != null);
            _pointCloudsUpdated.Add(pointCloud);
            OnPointCloudsChanged();
        }

        private void OnPointCloudRemoved()
        {
            if (pointCloud != null)
            {
                _pointCloudsRemoved.Add(pointCloud);
                OnPointCloudsChanged();
            }
        }

        private static void OnPointCloudsChanged()
        {
            pointCloudsChanged?.Invoke(
                new ARDensePointCloudsChangedEventArgs(_pointCloudsAdded, _pointCloudsUpdated, _pointCloudsRemoved));

            _pointCloudsAdded.Clear();
            _pointCloudsUpdated.Clear();
            _pointCloudsRemoved.Clear();
        }

        public static event Action<ARDensePointCloudsChangedEventArgs> pointCloudsChanged;
    }
}