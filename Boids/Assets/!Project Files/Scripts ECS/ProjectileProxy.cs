namespace BogdanCodreanu.ECS {
    using System;
    using Unity.Entities;
    using UnityEngine;
    using Unity.Mathematics;

    public struct SpawnProjectile : IComponentData {
        public float3 initialDirection;
        public float speed;
    }

    public class ProjectileProxy : ComponentDataProxy<SpawnProjectile> { }
}
