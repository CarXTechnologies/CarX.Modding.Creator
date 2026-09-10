using System;
using System.Linq;
using Plugins.CarX.Modding.Creator.Runtime;
using UnityEngine;
namespace Plugins.CarX.Modding.Creator.Editor
{
    public static class RigidbodyExporter
    {
        public static RigidbodyInstance Collect(Rigidbody body, Func<Transform, bool> excluded)
        {
            var parentLod = body.GetComponentInParent<LODGroup>();
            if (parentLod && parentLod.GetComponentInParent<Rigidbody>() != body)
                throw new InvalidOperationException($"Rigidbody '{body.name}' cannot split an enclosing LODGroup. Put the Rigidbody on the LODGroup root.");
            if (body.GetComponents<Joint>().Length > 0)
                throw new InvalidOperationException($"Rigidbody '{body.name}': Joint components are not supported by the mod format.");
            if (body.collisionDetectionMode != CollisionDetectionMode.Discrete)
                Debug.LogWarning($"Rigidbody '{body.name}': the client uses Unity Physics discrete collision detection; PhysX CCD modes are not exported.", body);
            var rotation = Quaternion.Inverse(body.rotation);
            var result = new RigidbodyInstance
            {
                name = body.name, transform = new LToWorld(body.position, body.rotation, Vector3.one),
                mass = body.mass, linearDamping = body.linearDamping, angularDamping = body.angularDamping,
                isKinematic = body.isKinematic, useGravity = body.useGravity, detectCollisions = body.detectCollisions,
                constraints = (int)body.constraints,
                automaticCenterOfMass = body.automaticCenterOfMass, automaticInertiaTensor = body.automaticInertiaTensor,
                centerOfMass = rotation * (body.transform.TransformPoint(body.centerOfMass) - body.position),
                inertiaTensor = body.inertiaTensor, inertiaRotation = body.inertiaTensorRotation,
                linearVelocity = body.linearVelocity, angularVelocity = body.angularVelocity
            };
            foreach (var collider in body.GetComponentsInChildren<Collider>(false))
            {
                if (!collider.enabled || collider.attachedRigidbody != body || excluded(collider.transform)) continue;
                var t = collider.transform;
                var shape = new RigidbodyColliderInstance
                {
                    transform = new LToWorld(rotation * (t.position - body.position), rotation * t.rotation, t.lossyScale),
                    trigger = collider.isTrigger
                };
                // TRS cannot preserve a sheared primitive. Reject instead of silently moving its collision shape.
                var actual = t.localToWorldMatrix;
                var represented = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);
                for (int column = 0; column < 3; column++)
                    if ((actual.GetColumn(column) - represented.GetColumn(column)).magnitude > 0.001f)
                        throw new InvalidOperationException($"Rigidbody '{body.name}': collider '{t.name}' has a sheared transform. Apply scale in the model first.");
                switch (collider)
                {
                    case BoxCollider box: shape.primitive = new PrimitiveColliderInstance { type = PrimitiveColliderType.Box, center = box.center, size = box.size }; break;
                    case SphereCollider sphere: shape.primitive = new PrimitiveColliderInstance { type = PrimitiveColliderType.Sphere, center = sphere.center, radius = sphere.radius }; break;
                    case CapsuleCollider capsule: shape.primitive = new PrimitiveColliderInstance { type = PrimitiveColliderType.Capsule, center = capsule.center, radius = capsule.radius, height = capsule.height, direction = capsule.direction }; break;
                    case MeshCollider mesh:
                        if (!mesh.sharedMesh) throw new InvalidOperationException($"Rigidbody '{body.name}': MeshCollider has no mesh.");
                        if (!mesh.convex && !body.isKinematic) throw new InvalidOperationException($"Rigidbody '{body.name}': a dynamic MeshCollider must be Convex.");
                        shape.mesh = true; shape.convex = mesh.convex;
                        shape.vertices = mesh.sharedMesh.vertices; shape.triangles = mesh.sharedMesh.triangles;
                        break;
                    default: throw new InvalidOperationException($"Rigidbody '{body.name}': {collider.GetType().Name} is not supported.");
                }
                if (collider.sharedMaterial)
                {
                    var material = collider.sharedMaterial;
                    if (material.frictionCombine == PhysicsMaterialCombine.Multiply || material.bounceCombine == PhysicsMaterialCombine.Multiply)
                        Debug.LogWarning($"Rigidbody '{body.name}': Multiply material combine uses the Unity Physics geometric mean approximation.", collider);
                    shape.friction = material.dynamicFriction; shape.restitution = material.bounciness;
                    shape.frictionCombine = (int)material.frictionCombine; shape.bounceCombine = (int)material.bounceCombine;
                }
                result.colliders.Add(shape);
            }
            if (result.colliders.Count == 0)
                throw new InvalidOperationException($"Rigidbody '{body.name}' needs at least one enabled supported collider.");
            return result;
        }
    }
}
