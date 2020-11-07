using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Vector3 = UnityEngine.Vector3;

namespace Cdm.XR.Extensions
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class ARDensePointCloudMeshVisualizer : ARDensePointCloudVisualizer
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 position;
            public Color32 color;

            public Vertex(Vector3 position, Color32 color)
            {
                this.position = position;
                this.color = color;
            }
        }
        
        private NativeArray<int> _indices;
        private Mesh _mesh;

        private VertexAttributeDescriptor[] _vertexAttributeDescriptors;
        protected override void Awake()
        {
            base.Awake();
            
            _mesh = new Mesh();
            _mesh.name = "Point Cloud";
            
            _vertexAttributeDescriptors = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4)
            };
            
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }

        protected virtual void OnDestroy()
        {
            _indices.Dispose();
            Destroy(_mesh);
        }

        private Vector3 _boundsMin = Vector3.one * float.MaxValue;
        private Vector3 _boundsMax = Vector3.one * float.MinValue;

        protected override void OnPointCloudUpdated(PointCloudUpdatedEventArgs e)
        {
            base.OnPointCloudUpdated(e);

            if (!_indices.IsCreated)
                InitializeIndices();
            
            var vertices = new NativeArray<Vertex>(e.count, Allocator.Temp);
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vertex(e.pointCloud.points[e.startIndex + i], e.pointCloud.colors[e.startIndex + i]);

                _boundsMin = Vector3.Min(_boundsMin, vertices[i].position);
                _boundsMax = Vector3.Max(_boundsMax, vertices[i].position);
            }
            
            _mesh.SetVertexBufferParams(pointCloud.count, _vertexAttributeDescriptors);
            _mesh.SetVertexBufferData(vertices, 0, e.startIndex, e.count, 0);
            
            _mesh.SetIndexBufferParams(pointCloud.count, IndexFormat.UInt32);
            _mesh.SetIndexBufferData(_indices, e.startIndex, e.startIndex, e.count);
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, pointCloud.count, MeshTopology.Points));
            
            var bounds = new Bounds();
            bounds.SetMinMax(_boundsMin, _boundsMax);
            _mesh.bounds = bounds;
        }

        private void InitializeIndices()
        {
            _indices = new NativeArray<int>(pointCloud.capacity, Allocator.Persistent);
            for (var i = 0; i < _indices.Length; i++)
            {
                _indices[i] = i;
            }
        }
    }
}