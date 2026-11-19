using UnityEngine;

namespace MissionRacer.Tracks
{
    public class WaypointPath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;

        public int WaypointCount => waypoints != null ? waypoints.Length : 0;

        public void SetWaypoints(Transform[] points)
        {
            waypoints = points;
        }

        public Vector3 GetWaypointPosition(int index)
        {
            if (waypoints == null || waypoints.Length == 0) return transform.position;
            int safeIndex = (int)Mathf.Repeat(index, waypoints.Length);
            return waypoints[safeIndex].position;
        }

        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;

                Vector3 current = waypoints[i].position;
                Vector3 next = waypoints[(i + 1) % waypoints.Length].position;

                Gizmos.DrawSphere(current, 1.2f);
                Gizmos.DrawLine(current, next);
            }
        }
    }
}
