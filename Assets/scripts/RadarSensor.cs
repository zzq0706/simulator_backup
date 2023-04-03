using System.Linq;
using System.Collections;
using System.Collections.Generic;
// using Simulator.Bridge;
using Simulator.Bridge.Data;
// using Simulator.Utilities;
using UnityEngine;
using UnityEngine.AI;
// using Simulator.Sensors.UI;

namespace Simulator.Sensors
{
    public class RadarSensor : MonoBehaviour
    {
        [Range(1.0f, 100f)]
        public float Frequency = 13.4f;
        public LayerMask RadarBlockers;

        private List<RadarMesh> radars = new List<RadarMesh>();
        // private WireframeBoxes WireframeBoxes;

        private uint seqId;
        private float nextPublish;
        
        private Dictionary<Collider, DetectedRadarObject> Detected = new Dictionary<Collider, DetectedRadarObject>();
        private Dictionary<Collider, Box> Visualized = new Dictionary<Collider, Box>();
        
        
        struct Box
        {
            public Vector3 Size;
            public Color Color;
        }

        private void Awake()
        {
            radars.AddRange(GetComponentsInChildren<RadarMesh>());
            foreach (var radar in radars)
            {
                radar.Init();
            }
        }

        private void Start()
        {
        	Debug.Log("111111111111111111111");
            foreach (var radar in radars)
            {
            	Debug.Log("222222222222222");
                radar.SetCallbacks(WhileInRange, OnExitRange);
            }
            nextPublish = Time.time + 1.0f / Frequency;
        }

        private void Update()
        {

            if (Time.time < nextPublish)
            {
                return;
            }
            Debug.Log("3333333333333");
            nextPublish = Time.time + 1.0f / Frequency;
            
			// publish ros data
			if (Detected.Count == 0)
			{
				Debug.Log("The dictionary is empty");
			}
			KeyValuePair<Collider, DetectedRadarObject> firstElement = Detected.First();

			Debug.Log("Value: " + firstElement.Value.Position);
			// Debug.Log("456");
			
        }

        void WhileInRange(Collider other, RadarMesh radar)
        {
        	Debug.Log("666666666666666666");
            // if (other.isTrigger)
                // return;

            if (!other.enabled)
                return;
            
            if (CheckBlocked(other))
            {
                if (Detected.ContainsKey(other))
                    Detected.Remove(other);
                if (Visualized.ContainsKey(other))
                    Visualized.Remove(other);
                return;
            }

            if (Detected.ContainsKey(other)) // update existing data
            {
                Detected[other].SensorPosition = transform.position;
                Detected[other].SensorAim = transform.forward;
                Detected[other].SensorRight = transform.right;
                Detected[other].SensorVelocity = GetSensorVelocity();
                Detected[other].SensorAngle = GetSensorAngle(other);
                Detected[other].Position = other.bounds.center;
                Detected[other].Velocity = GetObjectVelocity(other);
                Detected[other].RelativePosition = other.bounds.center - transform.position;
                Detected[other].RelativeVelocity = GetSensorVelocity() - GetObjectVelocity(other);
                Detected[other].ColliderSize = other.bounds.size;
                Detected[other].State = GetAgentState(other);
                Detected[other].NewDetection = false;
            }
            else
            {
                Box box = GetVisualizationBox(other);
                if (box.Size != Vector3.zero) // Empty box returned if tag is not right
                {
                    // Visualized.Add(other, box);
                    Detected.Add(other, new DetectedRadarObject()
                    {
                        Id = other.gameObject.GetInstanceID(),
                        SensorPosition = transform.position,
                        SensorAim = transform.forward,
                        SensorRight = transform.right,
                        SensorVelocity = GetSensorVelocity(),
                        SensorAngle = GetSensorAngle(other),
                        Position = other.bounds.center,
                        Velocity = GetObjectVelocity(other),
                        RelativePosition = other.bounds.center - transform.position,
                        RelativeVelocity = GetSensorVelocity() - GetObjectVelocity(other),
                        ColliderSize = other.bounds.size,
                        State = GetAgentState(other),
                        NewDetection = true,
                });
                }
            }
        }

        void OnExitRange(Collider other, RadarMesh radar)
        {
            OnExitRange(other);
        }

        void OnExitRange(Collider other)
        {
            if (Detected.ContainsKey(other))
                Detected.Remove(other);

            if (Visualized.ContainsKey(other))
                Visualized.Remove(other);
        }

        private bool CheckBlocked(Collider col)
        {
            bool isBlocked = false;
            var orig = transform.position;
            var end = col.bounds.center;
            var dir = end - orig;
            var dist = (end - orig).magnitude;
            if (Physics.Raycast(orig, dir, out RaycastHit hit, dist, RadarBlockers)) // ignore if blocked
            {
                if (hit.collider != col)
                    isBlocked = true;
            }
            return isBlocked;
        }

        private Box GetVisualizationBox(Collider other)
        {
            var bbox = new Box();
            Vector3 size = Vector3.zero;

            if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
                bbox.Color = Color.green;
            else
            {
                return bbox;
            }

            if (other is BoxCollider)
            {
                var box = other as BoxCollider;
                bbox.Size = box.size;
                size.x = box.size.z;
                size.y = box.size.x;
                size.z = box.size.y;
            }
            else if (other is CapsuleCollider)
            {
                var capsule = other as CapsuleCollider;
                bbox.Size = new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2);
                size.x = capsule.radius * 2;
                size.y = capsule.radius * 2;
                size.z = capsule.height;
            }

            return bbox;
        }

        private Vector3 GetSensorVelocity()
        {
            return gameObject.GetComponentInParent<Rigidbody>() == null ? Vector3.zero : gameObject.GetComponentInParent<Rigidbody>().velocity;
        }

        private double GetSensorAngle(Collider col)
        {
            // angle is orientation of the obstacle in degrees as seen by radar, counterclockwise is positive
            double angle = -Vector3.SignedAngle(transform.forward, col.transform.forward, transform.up);
            if (angle > 90)
                angle -= 180;
            else if (angle < -90)
                angle += 180;
            return angle;
        }

        private Vector3 GetObjectVelocity(Collider col)
        {
            Vector3 velocity = Vector3.zero;
            // var npc = col.GetComponent<NPCController>();
            // if (npc != null)
                // velocity = npc.simpleVelocity;
            var ped = col.GetComponent<NavMeshAgent>();
            if (ped != null)
                velocity = ped.desiredVelocity;
            var rb = col.attachedRigidbody;
            if (rb != null)
                velocity = rb.velocity;
            return velocity;
        }

        private int GetAgentState(Collider col)
        {
            int state = 1;
            // if (col.GetComponent<NPCController>()?.currentSpeed > 1f)
                // state = 0;
            return state;
        }
        
        public void OnVisualize()
        {

            foreach (var radar in radars)
            {
                Graphics.DrawMesh(radar.GetComponent<MeshFilter>().sharedMesh, transform.localToWorldMatrix, radar.RadarMeshRenderer.sharedMaterial, LayerMask.NameToLayer("Sensor"));
            }
        }

    }
}



