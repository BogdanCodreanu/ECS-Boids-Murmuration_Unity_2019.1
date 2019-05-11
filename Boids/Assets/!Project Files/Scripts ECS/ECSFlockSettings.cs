namespace BogdanCodreanu.ECS {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    [CreateAssetMenu(menuName = "ECS/Flock Parameters")]
    public class ECSFlockSettings : ScriptableObject {
        public float MoveSpeed = 90;
        public float CellSizeMin = 8;
        public float CellSizeMax = 24;
        public float varySizeSpeed = 3f;

        public float movementSpeedRandom1, movementSpeedRandom2, movementSpeedRandom3,
            movementSpeedRandom4, movementSpeedRandom5, movementSpeedRandom6;

        public float SeparationWeight = 1;
        public float AlignmentWeight = 1;
        public float CohesionWeight = 1;
        public float WalkToFlockCenterWeight = 1;
        public float maintainYWeight = 1;
        public float yLength = 5;
        public float perlinNoiseScale = 0.01f;


        public float SphereBoundarySize = 50;
        public float BoundaryWeight = 1f;

        public float goToTargetsWeight = 1;

        public float avoidDistanceObstacles = 100;
        public float avoidObstaclesWeight = 1;
        public float avoidXZwhileHeightBiggerThan = 100;
        public float avoidXZwhileHeightBiggerFade = 20;
        public float obstacleKillRadius = 1;

        public float distanceToAvoidTerrain = 20;
        public float avoidTerrainWeight = 10;
    }
}