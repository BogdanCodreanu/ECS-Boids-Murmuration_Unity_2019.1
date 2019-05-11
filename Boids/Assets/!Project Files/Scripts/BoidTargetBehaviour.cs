namespace BogdanCodreanu.ECS {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    public class BoidTargetBehaviour : MonoBehaviour {
        [SerializeField]
        private float xLength = 10, yLength = 10, zLength = 10;
        [SerializeField]
        private float xSpeed = 2, ySpeed = 2, zSpeed = 2;
        [SerializeField]
        private float timeSpeed = 1;

        private new Transform transform;

        private Vector3 initialPosition;

        private void Awake() {
            transform = gameObject.transform;
            initialPosition = transform.position;
        }


        private void Update() {
            transform.position = initialPosition + CalculatePosition(Time.time);
        }

        private Vector3 CalculatePosition(float atTime) {
            return new Vector3(
                Mathf.Sin(atTime * timeSpeed * xSpeed) * xLength,
                Mathf.Sin((atTime * timeSpeed) * ySpeed) * yLength,
                Mathf.Cos(atTime * timeSpeed * zSpeed) * zLength
                );
        }

#if UNITY_EDITOR
        [SerializeField, Min(.05f)]
        private float debugSeconds = 15, debugStep = 0.5f, debugSize = 2f;
        private void OnDrawGizmos() {
            if (!transform)
                transform = gameObject.transform;

            Gizmos.color = new Color(1, 1, 0, 1f);
            Gizmos.DrawSphere(transform.position, debugSize);
        }
        private void OnDrawGizmosSelected() {
            if (!transform)
                transform = gameObject.transform;
            if (!Application.isPlaying)
                initialPosition = transform.position;

            Gizmos.color = new Color(1, 1, 0, .6f);
            if (debugStep < .05f)
                return;
            for (float f = 0; f < debugSeconds; f += debugStep) {
                Gizmos.DrawSphere(initialPosition + CalculatePosition(f), debugSize);
            }      
        }
#endif
    }
}
