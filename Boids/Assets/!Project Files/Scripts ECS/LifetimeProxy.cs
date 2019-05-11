namespace BogdanCodreanu.ECS {
    using System;
    using Unity.Entities;
    using UnityEngine;
    using Unity.Mathematics;

    public struct Lifetime : IComponentData {
        public float value;
    }

    public class LifetimeProxy : ComponentDataProxy<Lifetime> { }
}
