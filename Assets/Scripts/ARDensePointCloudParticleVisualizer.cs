using UnityEngine;

namespace Cdm.XR.Extensions
{
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public class ARDensePointCloudParticleVisualizer : ARDensePointCloudVisualizer
    {
        public Gradient confidenceGradient;
        public bool useConfidenceColor;

        private ParticleSystem _particleSystem;
        private ParticleSystem.Particle[] _particles;

        protected override void Awake()
        {
            base.Awake();
            
            _particleSystem = GetComponent<ParticleSystem>();

            var pointCloudManager = FindObjectOfType<ARDensePointCloudManager>();
            if (pointCloudManager != null)
            {
                _particles = new ParticleSystem.Particle[pointCloudManager.maxPoints];
            }
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();
            
            if (_particleSystem != null)
                _particleSystem.Play();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (_particleSystem != null)
                _particleSystem.Stop();
        }

        protected override void OnPointCloudUpdated(PointCloudUpdatedEventArgs e)
        {
            base.OnPointCloudUpdated(e);

            var size = _particleSystem.main.startSize.constant;
            var alpha = _particleSystem.main.startColor.color.a;

            for (var i = e.startIndex; i < e.startIndex + e.count; ++i)
            {
                _particles[i].position = e.pointCloud.points[i];
                _particles[i].startSize = size;

                var color =  useConfidenceColor
                    ? confidenceGradient.Evaluate(e.pointCloud.confidences[i])
                    : (Color) e.pointCloud.colors[i];
                color.a = alpha;
                _particles[i].startColor = color;

                //_particles[i].remainingLifetime = 1f;
            }

            _particleSystem.SetParticles(_particles, e.pointCloud.count);
        }
    }
}