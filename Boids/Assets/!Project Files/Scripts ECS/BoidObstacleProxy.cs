namespace BogdanCodreanu.ECS {
    using System;
    using Unity.Entities;
    using Unity.Transforms;
    
    public struct BoidObstacle : IComponentData { }

    public class BoidObstacleProxy : ComponentDataProxy<BoidObstacle> { }
}