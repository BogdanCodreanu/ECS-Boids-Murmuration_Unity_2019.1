namespace BogdanCodreanu.ECS {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;


    public class ECSController : MonoBehaviour {


        private static ECSController instance;

        public static ECSController Instance {
            get {
                return instance ?? (instance = FindObjectOfType<ECSController>());
            }
        }

        public float CellSizeVaried { get; private set; }
        public Vector3 PositionNeighbourCubeOffset { get; private set; }


        [SerializeField]
        private ECSFlockSettings flockSettings;

        [SerializeField]
        private bool showDebug = true;
        [SerializeField]
        private Terrain worldTerrain;

        private ECSFlockSettings flockSettings2;
        public static ECSFlockSettings FlockParams => Instance.flockSettings2 ?? Instance.flockSettings;
        private float varySpeedOffset1, varySpeedOffset2, varySpeedOffset3;

        public static float TerrainY { get; private set; }

        private void Awake() {
            //var flockSettings2 = Instantiate(FlockParams);
            var flockSettings2 = FlockParams;
        }

        private void Start() {
            TerrainY = worldTerrain.GetPosition().y;
        }

        private float FromMinusOneOneToZeroOne(float f) {
            return f * 0.5f + 0.5f;
        }

        private void Update() {
            CellSizeVaried =
                FromMinusOneOneToZeroOne(Mathf.Sin(Time.time * flockSettings.varySizeSpeed)) *
                (flockSettings.CellSizeMax - flockSettings.CellSizeMin) +
                flockSettings.CellSizeMin;

            varySpeedOffset1 =
                FromMinusOneOneToZeroOne(Mathf.Cos(Time.time * flockSettings.movementSpeedRandom4)) +
                .1f;
            varySpeedOffset2 =
                FromMinusOneOneToZeroOne(Mathf.Sin(Time.time * flockSettings.movementSpeedRandom5)) +
                .1f;
            varySpeedOffset3 =
                FromMinusOneOneToZeroOne(Mathf.Cos(Time.time * flockSettings.movementSpeedRandom6)) +
                .1f;
            //varySpeedOffset1 = varySpeedOffset2 = varySpeedOffset3 = 1;


            PositionNeighbourCubeOffset = new Vector3(
                Mathf.Sin(Time.time * flockSettings.movementSpeedRandom1 * varySpeedOffset1),
                Mathf.Cos(Time.time * flockSettings.movementSpeedRandom2 * varySpeedOffset2),
                Mathf.Sin(Time.time * flockSettings.movementSpeedRandom3 * varySpeedOffset3))
                * CellSizeVaried * 0.5f;
        }

#if UNITY_EDITOR
        [SerializeField]
        private int spatialHashSteps;

        private void OnDrawGizmos() {
            Gizmos.color = Color.green;
            if (flockSettings == null || !showDebug)
                return;

            if (spatialHashSteps > 0) {
                Gizmos.color = new Color(1, 1, 0, .6f);
                for (int i = -spatialHashSteps; i <= spatialHashSteps; i++) {
                    for (int j = -spatialHashSteps; j <= spatialHashSteps; j++) {
                        for (int k = -spatialHashSteps; k <= spatialHashSteps; k++) {
                            Gizmos.DrawWireCube(
                                new Vector3(i, j, k) * CellSizeVaried +
                                transform.position, CellSizeVaried * Vector3.one);
                        }
                    }
                }
                return;
            }


            Gizmos.DrawWireCube(PositionNeighbourCubeOffset, CellSizeVaried * Vector3.one);
            Gizmos.DrawWireCube(PositionNeighbourCubeOffset, flockSettings.CellSizeMin * Vector3.one);
            Gizmos.DrawWireCube(PositionNeighbourCubeOffset, flockSettings.CellSizeMax * Vector3.one);

            Gizmos.color = new Color(0, 1, 0, flockSettings.BoundaryWeight);
            Gizmos.DrawWireSphere(Vector3.zero, flockSettings.SphereBoundarySize);

            if (worldTerrain) {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(
                    new Vector3(0, worldTerrain.GetPosition().y, 0)
                    + Vector3.up * flockSettings.distanceToAvoidTerrain * .5f,
                    Vector3.one * flockSettings.distanceToAvoidTerrain);
            }
        }
#endif
    }
}
