using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Puts an object on a point so that its VISIBLE MODEL lands there,
    /// even if the mesh inside the model is shifted away from its pivot
    /// (common with imported models).
    /// </summary>
    public static class ItemPlacement
    {
        /// <param name="bottomOnPoint">true: model's bottom sits on the point (shelves, tables).
        /// false: model's center is at the point (holding in hand).</param>
        public static void SnapTo(Transform item, Transform point, bool bottomOnPoint)
        {
            item.SetParent(point, true); // keeps the real size
            item.localPosition = Vector3.zero;
            item.localRotation = Quaternion.identity;

            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            bool found = false;

            foreach (Renderer r in item.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

                Bounds lb = r.localBounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = lb.center + Vector3.Scale(lb.extents, new Vector3(x, y, z));
                    Vector3 local = point.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
                found = true;
            }

            if (!found) return;

            Vector3 center = (min + max) * 0.5f;
            Vector3 wanted = bottomOnPoint ? new Vector3(center.x, min.y, center.z) : center;
            item.position -= point.TransformVector(wanted);
        }
    }
}
