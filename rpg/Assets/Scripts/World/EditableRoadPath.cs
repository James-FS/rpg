using System;
using System.Collections.Generic;
using UnityEngine;

namespace FogHarbor.World
{
    /// <summary>Authoring data for one editable road ribbon. Meshes are baked in the editor.</summary>
    public sealed class EditableRoadPath : MonoBehaviour
    {
        [Serializable]
        public sealed class RoadPoint
        {
            public Vector3 position;
            [Min(0.05f)] public float width = 3f;

            public RoadPoint(Vector3 position, float width)
            {
                this.position = position;
                this.width = width;
            }
        }

        [SerializeField] private List<RoadPoint> points = new List<RoadPoint>();
        [SerializeField, Min(0.1f)] private float sampleSpacing = 0.4f;
        [SerializeField, Min(0f)] private float shoulderWidth = 0.09f;
        [SerializeField] private float surfaceHeight = 0.02f;
        [SerializeField] private float edgeHeight = 0.003f;
        [SerializeField, Min(0.1f)] private float textureRepeatMeters = 4f;

        public List<RoadPoint> Points => points;
        public float SampleSpacing => sampleSpacing;
        public float ShoulderWidth => shoulderWidth;
        public float SurfaceHeight => surfaceHeight;
        public float EdgeHeight => edgeHeight;
        public float TextureRepeatMeters => textureRepeatMeters;
    }
}
