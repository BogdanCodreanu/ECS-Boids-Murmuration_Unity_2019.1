namespace BogdanCodreanu.ECS {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    public class Player : MonoBehaviour {
        [SerializeField]
        private Vector3 initialSpawningPoint;
        [SerializeField]
        private float cameraDistanceFromPlayer;
        [SerializeField]
        private float rotateCameraSpeed = 3f;

        public new Transform transform { get; private set; }
        private Transform cameraTransform;

        private Vector2 movementInput;
        private float upwardsInputFly;
        private Vector2 mouseInput;

        /// <summary>
        /// Result of added mouse input.
        /// </summary>
        private Vector2 currentMouseResult;

        public float CurrentSpeed { get; private set; }
        [SerializeField]
        private float upBreakAccel, downAccel, minUpSpeed;

        [SerializeField]
        private AnimationCurve maxDownSpeedByAngle;

        [SerializeField]
        private float turnSpeed = 2f, minTurnSpeedScalar = .5f;

        [SerializeField]
        private float changeSpeedSpeed = 20f;

        [SerializeField]
        private float cameraYLimits = 88f;

        public bool ControlPlayer { get; private set; } = false;

        private Vector3 cameraFollowPoint;
        [SerializeField]
        private float cameraMovementSpeed = 100, cameraMovementSpeedShift = 300;
        private EntityManager entityManager;

        [SerializeField]
        private float projectileLifetime = 5f;
        [SerializeField]
        private GameObject projectilePrefab;

        private bool working = false;

        [SerializeField]
        private Transform mainMenuCameraLook;
        [SerializeField]
        private GameObject terrainObject;

        private void Awake() {
            entityManager = World.Active.EntityManager;
            transform = gameObject.transform;
            cameraTransform = Camera.main.transform;
            ResetForDiving();
        }

        public void Disable() {
            ResetForCameraControl();
            working = false;
            cameraTransform.position = mainMenuCameraLook.position;
            cameraTransform.rotation = mainMenuCameraLook.rotation;
            terrainObject.SetActive(false);
        }

        public void Enable() {
            ResetForDiving();
            working = true;
            terrainObject.SetActive(true);
        }

        public void ResetForDiving() {
            transform.position = initialSpawningPoint;
            transform.rotation = Quaternion.identity;
            currentMouseResult = new Vector2(0, -90);
            CurrentSpeed = 0;
            ControlPlayer = true;
        }

        public void ResetForCameraControl() {
            cameraFollowPoint = transform.position;
            ShootObstacle(true);
            transform.position = new Vector3(0, 4000, 0);
            ControlPlayer = false;
        }

        private void Update() {
            if (!working)
                return;

            GetInput();


            if (Input.GetKeyDown(KeyCode.R) && ControlPlayer) {
                ResetForDiving();
            }
            if (ControlPlayer) {
                MovePlayer();
            } else {
                MoveCameraPoint();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) {
                ShootObstacle();
            }
        }

        private void GetInput() {
            movementInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
            mouseInput = new Vector2(
                Input.GetAxisRaw("Mouse X"),
                Input.GetAxisRaw("Mouse Y"));
            upwardsInputFly = -Input.GetAxisRaw("Upwards");
            currentMouseResult += mouseInput * rotateCameraSpeed;
        }

        private void LateUpdate() {
            if (!working)
                return;

            RotateCameraByMouse();
        }

        private void RotateCameraByMouse() {
            Vector3 dir = new Vector3(0, 0, -cameraDistanceFromPlayer);
            if (currentMouseResult.y > cameraYLimits) {
                currentMouseResult.y = cameraYLimits;
            } else if (currentMouseResult.y < -cameraYLimits) {
                currentMouseResult.y = -cameraYLimits;
            }

            Quaternion rotation = Quaternion.Euler(-currentMouseResult.y, currentMouseResult.x, 0);
            if (ControlPlayer) {
                cameraTransform.position = transform.position + rotation * dir;
                cameraTransform.LookAt(transform.position);
            } else {
                cameraTransform.position = cameraFollowPoint + rotation * dir;
                cameraTransform.LookAt(cameraFollowPoint);
            }

        }

        private float CalculateSpeedForAngle(float downwardValue) {
            float desiredSpeed;

            // control speed by direction
            if (downwardValue > 0) { // if pointing down
                desiredSpeed = CurrentSpeed + downwardValue * downAccel;
                desiredSpeed = Mathf.Min(desiredSpeed,
                    maxDownSpeedByAngle.Evaluate(downwardValue));
            } else { // if pointing up
                desiredSpeed = CurrentSpeed + downwardValue * upBreakAccel;
                desiredSpeed = Mathf.Max(desiredSpeed, minUpSpeed);
                if (movementInput.y < 0) {
                    desiredSpeed = 0;
                }
            }
            return desiredSpeed;
        }
        private void MovePlayer() {
            float downwardValue = (.5f - (Vector3.Angle(transform.forward, Vector3.down) / 180f)) * 2;
            transform.forward = Vector3.Lerp(transform.forward, movementInput.y < 0 ? Vector3.up : cameraTransform.forward,
                movementInput.y * Time.deltaTime * turnSpeed *
                (downwardValue > 0 ?
                    Mathf.Max(1 - (CurrentSpeed / maxDownSpeedByAngle.Evaluate(1)), minTurnSpeedScalar)
                : 1f));

            float desiredSpeed = CalculateSpeedForAngle(downwardValue);

            CurrentSpeed = Mathf.Lerp(CurrentSpeed, desiredSpeed, changeSpeedSpeed * Time.deltaTime);

            transform.position += transform.forward * CurrentSpeed * Time.deltaTime;
        }

        private void MoveCameraPoint() {
            float usedMovementSpeed = Input.GetKey(KeyCode.LeftShift) ?
                cameraMovementSpeedShift : cameraMovementSpeed;

            cameraFollowPoint +=
                (cameraTransform.forward * movementInput.y + cameraTransform.right * movementInput.x +
                cameraTransform.up * upwardsInputFly).normalized
                * Time.unscaledDeltaTime * usedMovementSpeed;
        }

        private void ShootObstacle(bool usePlayerForward = false) {
            NativeArray<Entity> entitySpawns = new NativeArray<Entity>(1, Allocator.Temp);

            float downwardValue = (.5f - 
                (Vector3.Angle(usePlayerForward ? transform.forward : cameraTransform.forward,
                Vector3.down) / 180f)) * 2;
            float speed = CalculateSpeedForAngle(downwardValue);


            entityManager.Instantiate(projectilePrefab, entitySpawns);
            Entity spawnedEntity = entitySpawns[0];


            entityManager.SetComponentData(spawnedEntity, new SpawnProjectile {
                initialDirection = cameraTransform.forward,
                speed = speed,
            });
            entityManager.SetComponentData(spawnedEntity, new Lifetime {
                value = projectileLifetime
            });
            if (ControlPlayer)
                cameraFollowPoint = transform.position;
            entityManager.SetComponentData(spawnedEntity, new LocalToWorld {
                Value = float4x4.TRS(usePlayerForward ? transform.position : cameraFollowPoint,
                quaternion.LookRotation(
                    usePlayerForward ? transform.forward : cameraTransform.forward,
                    usePlayerForward ? transform.up : cameraTransform.up),
                Vector3.one * 4)
            });

            entitySpawns.Dispose();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            if (transform == null) {
                transform = gameObject.transform;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(initialSpawningPoint, 1f);
            Gizmos.DrawWireSphere(transform.position, cameraDistanceFromPlayer);
            Gizmos.color = Color.blue;
        }

        private void OnDrawGizmos() {
            if (transform == null) {
                transform = gameObject.transform;
            }
            Gizmos.color = new Color(1, 0, 0, 1f);
            Gizmos.DrawWireSphere(transform.position, ECS.ECSController.FlockParams.avoidDistanceObstacles);
            Gizmos.DrawWireSphere(transform.position, ECS.ECSController.FlockParams.obstacleKillRadius);
        }
#endif
    }
}
