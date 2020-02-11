using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BulletSharp;
using BulletSharp.Math;
using OpenTK;

namespace kinectwall
{

    static class Utils
    {
        public static Matrix FromMat4(Matrix4 m4)
        {
            return new Matrix(
                m4.M11, m4.M12, m4.M13, m4.M14,
                m4.M21, m4.M22, m4.M23, m4.M24,
                m4.M31, m4.M32, m4.M33, m4.M34,
                m4.M41, m4.M42, m4.M43, m4.M44);
        }

        public static Matrix4 FromMat(Matrix m)
        {
            return new Matrix4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);
        }

        public static BulletSharp.Math.Vector3 FromVector3(OpenTK.Vector3 v3)
        {
            return new BulletSharp.Math.Vector3(v3.X, v3.Y, v3.Z);
        }

    }
    class SimObjectMesh
    {
        ConvexTriangleMeshShape shape;
        RigidBody body;
        Matrix4 worldMatrix;

        public SimObjectMesh(Matrix4 initialPos, float mass, TriangleMesh tm)
        {
            worldMatrix = initialPos;
            shape = new ConvexTriangleMeshShape(tm);
            BulletSharp.Math.Vector3 inertia;
            shape.CalculateLocalInertia(mass, out inertia);
            RigidBodyConstructionInfo constructInfo =
                new RigidBodyConstructionInfo(mass, new DefaultMotionState(
                    Utils.FromMat4(worldMatrix)), shape, inertia);
            body = new RigidBody(constructInfo);
            
        }

        public void Refresh()
        {
            worldMatrix = Utils.FromMat(body.WorldTransform);
        }
        public Matrix4 WorldMatrix => worldMatrix;

        public RigidBody Body => body;
        public object objectInfo;
    }

    class BulletSimulation
    {
        DefaultCollisionConfiguration colConfiguration = new DefaultCollisionConfiguration();
        CollisionDispatcher colDispatcher;
        DbvtBroadphase broadphase;
        DiscreteDynamicsWorld colWorld;
        SequentialImpulseConstraintSolver solver;

        List<SimObjectMesh> bodies = new List<SimObjectMesh>();

        public BulletSimulation()
        {
            colDispatcher = new CollisionDispatcher(colConfiguration);
            broadphase = new DbvtBroadphase();
            solver = new SequentialImpulseConstraintSolver();
            colWorld = new DiscreteDynamicsWorld(colDispatcher, broadphase, solver, colConfiguration);
        }


        public void Init()
        {
            foreach (var body in bodies)
            {
                colWorld.AddCollisionObject(body.Body);
            }
        }

        public void AddObj(SimObjectMesh obj)
        {
            bodies.Add(obj);
            colWorld.AddCollisionObject(obj.Body);
        }

        public void Step()
        {
            var simulationTimestep = 1f / 60f;
            colWorld.StepSimulation(simulationTimestep);
            foreach (var body in bodies)
            {
                body.Refresh();
            }
        }
    }
}
