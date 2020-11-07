using UnityEngine;

namespace Cdm.XR.Extensions
{
    [RequireComponent(typeof(ARDensePointCloud))]
    public class ARDensePointCloudVisualizer : MonoBehaviour
    {
        protected ARDensePointCloud pointCloud { get; private set; }
        
        protected virtual void Awake()
        {
            pointCloud = GetComponent<ARDensePointCloud>();
        }
        
        protected virtual void OnEnable()
        {
            pointCloud.pointCloudUpdated += OnPointCloudUpdated;
        }
        
        protected virtual void OnDisable()
        {
            pointCloud.pointCloudUpdated -= OnPointCloudUpdated;
        }

        protected virtual void OnPointCloudUpdated(PointCloudUpdatedEventArgs e)
        {
            // Intentionally empty.
        }
    }
}