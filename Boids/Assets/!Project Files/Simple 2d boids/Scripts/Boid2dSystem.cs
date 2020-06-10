
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Boid2dSystem : MonoBehaviour
{
    [SerializeField]
    private int numberOfBoids = 100;
    [SerializeField]
    private float spawnRange = 40;

    [SerializeField]
    private Boid2d boidPrefab;

    private List<Boid2d> spawns = new List<Boid2d>();
    public ICollection<Boid2d> Spawns => spawns;

    private void Awake() {
        foreach (var i in Enumerable.Range(0, numberOfBoids)) {
            var spawn = Instantiate(boidPrefab.gameObject, Random.insideUnitCircle * spawnRange, Quaternion.identity).GetComponent<Boid2d>();
            spawns.Add(spawn);
            spawn.Init(this);
        }
    }

    private void OnDrawGizmosSelected() {
        Gizmos.DrawWireSphere(Vector3.zero, spawnRange);
    }
}
