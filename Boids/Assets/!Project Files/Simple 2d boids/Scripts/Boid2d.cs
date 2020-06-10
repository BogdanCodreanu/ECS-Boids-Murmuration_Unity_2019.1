using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Boid2d : MonoBehaviour
{
    private Transform _transform;
    public new Transform transform => _transform != null ? _transform : (_transform = gameObject.transform);

    public Vector3 Position => transform.position;


    [SerializeField]
    private float speed = 5f;
    [SerializeField]
    private float turnSpeed = 5f;
    [SerializeField]
    private float neighborDistance = 5f;

    [SerializeField]
    private float limitRange = 20f;

    private Boid2dSystem system;

    private Vector3 wantedDirection;
    

    public void Init(Boid2dSystem boid2dSystem) {
        system = boid2dSystem;
    }

    private void Update() {
        CalculateDirection();

        transform.rotation = Quaternion.LookRotation(
            Vector3.RotateTowards(transform.forward, wantedDirection, turnSpeed * Time.deltaTime, 0f));

        transform.position += transform.up * speed * Time.deltaTime;
    }

    private IEnumerable<Boid2d> GetNeighbors() => 
        system.Spawns.Where(boid => boid != this && (Position - boid.Position).sqrMagnitude <= neighborDistance * neighborDistance);

    private void CalculateDirection() {
        wantedDirection = transform.up;
        wantedDirection += InfluenceMargin();
    }

    private Vector3 InfluenceMargin() {
        if (transform.position.magnitude >= limitRange) {
            return -Position;
        }
        return Vector3.zero;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.DrawWireSphere(Vector3.zero, limitRange);

    }

}
