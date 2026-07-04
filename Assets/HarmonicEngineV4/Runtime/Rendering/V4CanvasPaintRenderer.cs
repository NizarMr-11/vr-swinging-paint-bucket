using HarmonicEngineV4.Simulation;
using UnityEngine;

namespace HarmonicEngineV4.Rendering
{
    /// <summary>
    /// Displays the baked canvas paint grid (spec section 7) on a quad above the canvas
    /// plane. The pipeline blits the grid buffer into a RenderTexture every frame; this
    /// component just owns the display quad + material.
    /// </summary>
    public sealed class V4CanvasPaintRenderer : MonoBehaviour
    {
        public V4PipelineRoot pipeline;

        private GameObject _quad;
        private Material _material;

        private void Start()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }
        }

        private void Update()
        {
            if (_quad == null && pipeline != null && pipeline.Initialized && pipeline.CanvasTexture != null)
            {
                CreateQuad();
            }
        }

        private void CreateQuad()
        {
            V4Canvas canvas = pipeline.canvas;
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "V4CanvasPaintQuad";
            Object.Destroy(_quad.GetComponent<Collider>());
            _quad.transform.SetParent(transform, false);
            _quad.transform.position = new Vector3(
                canvas.transform.position.x,
                canvas.PlaneY + 0.001f,
                canvas.transform.position.z);
            _quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _quad.transform.localScale = new Vector3(canvas.width, canvas.depth, 1f);

            Shader shader = Shader.Find("Unlit/Texture");
            _material = new Material(shader);
            _material.mainTexture = pipeline.CanvasTexture;
            _quad.GetComponent<MeshRenderer>().sharedMaterial = _material;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
