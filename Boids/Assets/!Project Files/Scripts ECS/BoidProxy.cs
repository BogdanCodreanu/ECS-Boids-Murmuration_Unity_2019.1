namespace BogdanCodreanu.ECS {
    using System;
    using Unity.Entities;
    using Unity.Transforms;

    [Serializable]
    [WriteGroup(typeof(LocalToWorld))]
    public struct Boid : ISharedComponentData {
        public int test;
    }

    public class BoidProxy : SharedComponentDataProxy<Boid> { }
}