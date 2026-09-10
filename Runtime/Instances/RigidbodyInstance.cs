using System;
using System.Collections.Generic;
using UnityEngine;
namespace Plugins.CarX.Modding.Creator.Runtime
{
    [Serializable]
    public sealed class RigidbodyInstance
    {
        public string name;
        public LToWorld transform;
        public float mass, linearDamping, angularDamping;
        public bool isKinematic, useGravity, detectCollisions;
        public int constraints;
        public bool automaticCenterOfMass, automaticInertiaTensor;
        public Vector3 centerOfMass, inertiaTensor, linearVelocity, angularVelocity;
        public Quaternion inertiaRotation;
        public List<RigidbodyColliderInstance> colliders = new();
    }
    [Serializable]
    public sealed class RigidbodyColliderInstance
    {
        public LToWorld transform;
        public bool mesh, convex, trigger;
        public PrimitiveColliderInstance primitive;
        public Vector3[] vertices;
        public int[] triangles;
        public float friction = 0.6f, restitution;
        public int frictionCombine, bounceCombine;
    }
}
