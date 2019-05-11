namespace BogdanCodreanu.ECS {
    using System;
    using Unity.Entities;
    using Unity.Transforms;
    
    public struct BoidTarget : IComponentData { }

    public class BoidTargetProxy : ComponentDataProxy<BoidTarget> { }
}