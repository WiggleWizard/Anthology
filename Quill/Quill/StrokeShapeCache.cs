// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Prowl.Vector;

[assembly: InternalsVisibleTo("Quill.Tests")]

namespace Prowl.Quill
{
    /// <summary>
    /// Stroke meshes for <see cref="StrokeShape"/>s, one per shape, stroke style and size bucket.
    /// </summary>
    /// <remarks>
    /// Buckets are geometric, so an entry built at a bucket's centre scale serves every request within
    /// sqrt(<see cref="BucketStep"/>) of it (about +-14%). Replaying at a different scale must not scale the
    /// one pixel anti-aliasing fringe along with the stroke, so each vertex is stored in two parts: a core
    /// position that scales with the shape, and a fringe offset that does not.
    ///
    /// The split is exact. With the meshing decisions held fixed (see the topology fringe in
    /// <see cref="PolylineMesher"/>) every vertex position is linear in the fringe width, so meshing the
    /// stroke at the real fringe and at half of it gives both parts. The mesher is homogeneous in scale, so
    /// a mesh of the shape at scale c*k with fringe f equals k times the mesh at scale c with fringe f/k,
    /// which is k*core + fringe. Only the decisions (arc segment counts, which joins overlap) stay those of
    /// the bucket centre, and they are valid meshes at any scale in the bucket.
    /// </remarks>
    internal sealed class StrokeShapeCache
    {
        /// <summary>Ratio between neighbouring size buckets.</summary>
        internal const float BucketStep = 1.3f;

        /// <summary>Unit-space stroke widths are rounded to this before keying.</summary>
        internal const float WidthQuantum = 1f / 4096f;

        // Above this the least recently used half is dropped. Icons come in a handful of sizes, so a UI
        // needs far fewer entries than this.
        private const int MaxEntries = 1024;

        private static readonly float LogBucketStep = MathF.Log(BucketStep);
        private static readonly Color32 BuildColor = new Color32(255, 255, 255, 255);

        internal readonly struct Key : IEquatable<Key>
        {
            public readonly StrokeShape Shape;
            public readonly int WidthKey;
            public readonly int Bucket;
            public readonly JointStyle Joint;
            public readonly EndCapStyle StartCap;
            public readonly EndCapStyle EndCap;
            public readonly float MiterLimit;
            public readonly float RoundingMinDistance;
            public readonly float FringeWidth;

            public Key(StrokeShape shape, int widthKey, int bucket, JointStyle joint, EndCapStyle startCap, EndCapStyle endCap,
                float miterLimit, float roundingMinDistance, float fringeWidth)
            {
                Shape = shape;
                WidthKey = widthKey;
                Bucket = bucket;
                Joint = joint;
                StartCap = startCap;
                EndCap = endCap;
                MiterLimit = miterLimit;
                RoundingMinDistance = roundingMinDistance;
                FringeWidth = fringeWidth;
            }

            public bool Equals(Key other)
            {
                return ReferenceEquals(Shape, other.Shape)
                    && WidthKey == other.WidthKey
                    && Bucket == other.Bucket
                    && Joint == other.Joint
                    && StartCap == other.StartCap
                    && EndCap == other.EndCap
                    && MiterLimit == other.MiterLimit
                    && RoundingMinDistance == other.RoundingMinDistance
                    && FringeWidth == other.FringeWidth;
            }

            public override bool Equals(object? obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                int styles = (int)Joint | ((int)StartCap << 4) | ((int)EndCap << 8);
                return HashCode.Combine(RuntimeHelpers.GetHashCode(Shape), WidthKey, Bucket, styles, MiterLimit, RoundingMinDistance, FringeWidth);
            }
        }

        internal sealed class Entry
        {
            /// <summary>Vertex positions without the fringe, at <see cref="CentreScale"/>, relative to the shape origin.</summary>
            public Float2[] Core = Array.Empty<Float2>();
            /// <summary>Per-vertex fringe offset in physical pixels, the same at every scale.</summary>
            public Float2[] Fringe = Array.Empty<Float2>();
            /// <summary>Per-vertex coverage (uv.x) at the centre scale.</summary>
            public float[] Coverage = Array.Empty<float>();
            public uint[] Indices = Array.Empty<uint>();

            /// <summary>Physical pixels per shape unit the entry was built at.</summary>
            public float CentreScale;
            /// <summary>Stroke width in physical pixels at the centre scale.</summary>
            public float CentreThickness;
            /// <summary>Built with the mesher's thin-line path, where coverage is proportional to the width.</summary>
            public bool Thin;
            /// <summary>False when the two probe meshes disagreed, in which case the stroke is meshed directly.</summary>
            public bool Usable;

            public long LastUse;
        }

        private readonly Dictionary<Key, Entry> _entries = new Dictionary<Key, Entry>();
        private long _useClock;

        // Build scratch.
        private readonly List<Float2> _points = new List<Float2>();
        private readonly List<Float2> _fullPositions = new List<Float2>();
        private readonly List<float> _fullCoverage = new List<float>();
        private readonly List<uint> _fullIndices = new List<uint>();
        private readonly List<Float2> _core = new List<Float2>();
        private readonly List<Float2> _fringe = new List<Float2>();
        private readonly List<float> _coverage = new List<float>();
        private readonly List<uint> _indices = new List<uint>();
        private readonly List<Entry> _evictScratch = new List<Entry>();
        private readonly List<Key> _evictKeys = new List<Key>();

        public int Count => _entries.Count;

        /// <summary>Cached entries that could not be split and so mesh directly on every draw.</summary>
        public int UnusableCount
        {
            get
            {
                int n = 0;
                foreach (Entry e in _entries.Values)
                {
                    if (!e.Usable)
                        n++;
                }
                return n;
            }
        }

        public void Clear() => _entries.Clear();

        /// <summary>Bucket index for a scale in physical pixels per shape unit.</summary>
        public static int Bucket(float pixelScale) => (int)MathF.Round(MathF.Log(pixelScale) / LogBucketStep);

        /// <summary>Centre scale of a bucket.</summary>
        public static float BucketCentre(int bucket) => MathF.Pow(BucketStep, bucket);

        /// <summary>Returns the entry for <paramref name="key"/>, meshing it first when it is not cached.</summary>
        public Entry GetOrBuild(in Key key, out bool built)
        {
            if (_entries.TryGetValue(key, out Entry? entry))
            {
                entry.LastUse = ++_useClock;
                built = false;
                return entry;
            }

            if (_entries.Count >= MaxEntries)
                EvictOldestHalf();

            entry = Build(key);
            entry.LastUse = ++_useClock;
            _entries[key] = entry;
            built = true;
            return entry;
        }

        private Entry Build(in Key key)
        {
            float centre = BucketCentre(key.Bucket);
            float thickness = key.WidthKey * WidthQuantum * centre;
            float fringe = key.FringeWidth;
            float probeFringe = fringe * 0.5f;

            var entry = new Entry
            {
                CentreScale = centre,
                CentreThickness = thickness,
                Thin = fringe > 0f && thickness <= fringe,
                Usable = true,
            };

            _core.Clear();
            _fringe.Clear();
            _coverage.Clear();
            _indices.Clear();

            foreach (Float2[] contour in key.Shape.Contours)
            {
                _points.Clear();
                for (int i = 0; i < contour.Length; i++)
                    _points.Add(contour[i] * centre);

                // The mesh at the real fringe. Copied out, as the mesher reuses its output lists.
                PolylineMesher.Create(_points, thickness, fringe, BuildColor, key.Joint, key.MiterLimit, key.StartCap, key.EndCap,
                    key.RoundingMinDistance, out List<Vertex> verts, out List<uint> idxs, fringe);

                _fullPositions.Clear();
                _fullCoverage.Clear();
                _fullIndices.Clear();
                for (int i = 0; i < verts.Count; i++)
                {
                    _fullPositions.Add(new Float2(verts[i].x, verts[i].y));
                    _fullCoverage.Add(verts[i].u);
                }
                _fullIndices.AddRange(idxs);

                uint baseIndex = (uint)_core.Count;

                if (fringe <= 0f)
                {
                    // No anti-aliasing: there is no fringe, so the whole mesh scales.
                    for (int i = 0; i < _fullPositions.Count; i++)
                    {
                        _core.Add(_fullPositions[i]);
                        _fringe.Add(Float2.Zero);
                        _coverage.Add(_fullCoverage[i]);
                    }
                }
                else
                {
                    // The same stroke at half the fringe with the decisions of the real fringe: same
                    // topology, and each position moves linearly with the fringe width.
                    PolylineMesher.Create(_points, thickness, probeFringe, BuildColor, key.Joint, key.MiterLimit, key.StartCap, key.EndCap,
                        key.RoundingMinDistance, out List<Vertex> probeVerts, out List<uint> probeIdxs, fringe);

                    if (!SameTopology(probeVerts.Count, probeIdxs))
                    {
                        entry.Usable = false;
                        return entry;
                    }

                    float invDelta = 1f / (fringe - probeFringe);
                    for (int i = 0; i < _fullPositions.Count; i++)
                    {
                        Float2 full = _fullPositions[i];
                        Float2 probe = new Float2(probeVerts[i].x, probeVerts[i].y);
                        Float2 perUnitFringe = (full - probe) * invDelta;
                        Float2 fringeOffset = perUnitFringe * fringe;
                        _core.Add(full - fringeOffset);
                        _fringe.Add(fringeOffset);
                        _coverage.Add(_fullCoverage[i]);
                    }
                }

                for (int i = 0; i < _fullIndices.Count; i++)
                    _indices.Add(baseIndex + _fullIndices[i]);
            }

            entry.Core = _core.ToArray();
            entry.Fringe = _fringe.ToArray();
            entry.Coverage = _coverage.ToArray();
            entry.Indices = _indices.ToArray();
            return entry;
        }

        private bool SameTopology(int probeVertexCount, List<uint> probeIndices)
        {
            if (probeVertexCount != _fullPositions.Count)
                return false;
            if (probeIndices.Count != _fullIndices.Count)
                return false;
            for (int i = 0; i < probeIndices.Count; i++)
            {
                if (probeIndices[i] != _fullIndices[i])
                    return false;
            }
            return true;
        }

        private void EvictOldestHalf()
        {
            _evictScratch.Clear();
            foreach (Entry e in _entries.Values)
                _evictScratch.Add(e);
            _evictScratch.Sort((a, b) => a.LastUse.CompareTo(b.LastUse));
            long cutoff = _evictScratch[_evictScratch.Count / 2].LastUse;

            _evictKeys.Clear();
            foreach (KeyValuePair<Key, Entry> pair in _entries)
            {
                if (pair.Value.LastUse < cutoff)
                    _evictKeys.Add(pair.Key);
            }
            foreach (Key k in _evictKeys)
                _entries.Remove(k);

            _evictScratch.Clear();
            _evictKeys.Clear();
        }
    }
}
