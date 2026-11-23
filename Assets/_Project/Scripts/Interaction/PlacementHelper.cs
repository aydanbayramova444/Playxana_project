using UnityEngine;

namespace Game.Interaction
{
    /// <summary>
    /// Puts an object on a point so that its VISIBLE MODEL lands there,
    /// even if the 3D model inside the prefab is shifted away from the prefab's pivot.
    /// (Ported from the Super Mario project.)
    /// </summary>
    public static class PlacementHelper
    {
        /// <param name="sitOnTop">true: model's bottom touches the point (shelves, tables).
        /// false: model's center is at the point (holding in hand).</param>
        public static void SnapTo(GameObject obj, Transform point, bool sitOnTop)
        {
            // "true" keeps the object's real size in the world
            obj.transform.SetParent(point, true);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            // Measure the model's box as seen from the point
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            bool found = false;

            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            {
                // Only measure real 3D models, skip sprites/particles
                if (r is SpriteRenderer || r is ParticleSystemRenderer) continue;

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
            Vector3 wanted = sitOnTop ? new Vector3(center.x, min.y, center.z) : center;

            // Move the whole object by the gap between the model and the point
            obj.transform.position -= point.TransformVector(wanted);
        }
    }
}
