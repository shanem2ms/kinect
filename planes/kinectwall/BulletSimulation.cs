using BulletSharp;
using BulletSharp.Math;
using OpenTK;
using System;
using System.Collections.Generic;

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
        
        public static OpenTK.Vector3 FromBVector3(BulletSharp.Math.Vector3 v3)
        {
            return new OpenTK.Vector3(v3.X, v3.Y, v3.Z);
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
            body.SetDamping(0.3f, 0.3f);
        }

        public void AfterWorldAdd()
        {
            //System.Diagnostics.Debug.WriteLine($"{body.BroadphaseProxy.CollisionFilterGroup} .. {body.BroadphaseProxy.CollisionFilterMask}");
            if (CollisionGroup >= 0)
            {
                body.BroadphaseProxy.CollisionFilterGroup = CollisionGroup;
                body.BroadphaseProxy.CollisionFilterMask = (int)CollisionFilterGroups.StaticFilter;
                //body.Gravity = new BulletSharp.Math.Vector3(0);
            }
        }

        public void Refresh()
        {
            worldMatrix = Utils.FromMat(body.WorldTransform);
        }

        public void SetTransform(Matrix4 t)
        {            
            body.WorldTransform =
                Utils.FromMat4(worldMatrix);
        }

        public int CollisionGroup { get; set; } = -1;
        public Matrix4 WorldMatrix => worldMatrix;
        public RigidBody Body => body;
        public object objectInfo;

        public override string ToString()
        {
            return objectInfo != null ? objectInfo.ToString() :
                body.ToString();
        }
    }

    class Constraint
    {
        Generic6DofConstraint dof;
        Point2PointConstraint p2p;

        SimObjectMesh pinnedBody;
        public TypedConstraint C => p2p;
        public Constraint(SimObjectMesh mesh1, OpenTK.Vector3 m1pivot,
            SimObjectMesh mesh2, OpenTK.Vector3 m2pivot)
        {
            //dof = new Generic6DofConstraint(mesh1.Body, mesh2.Body, 
            p2p = new Point2PointConstraint(mesh1.Body, mesh2.Body, Utils.FromVector3(m1pivot), Utils.FromVector3(m2pivot));
            
            //p2p.BreakingImpulseThreshold = 10.0f;
        }

        public Constraint(SimObjectMesh mesh1, OpenTK.Vector3 m1pivot)
        {
            pinnedBody = mesh1;
            pinnedBody.Body.ActivationState = ActivationState.DisableDeactivation;
            p2p = new Point2PointConstraint(mesh1.Body, Utils.FromVector3(m1pivot));
            p2p.Setting.ImpulseClamp = 30.0f;
            p2p.Setting.Tau = 0.001f;
        }

        public void UpdateWsPos(OpenTK.Vector3 wspos)
        {
            //OpenTK.Vector3 lPos = OpenTK.Vector3.TransformPosition(wspos, pinnedBody.WorldMatrix.Inverted());
            p2p.PivotInB = Utils.FromVector3(wspos);
        }

        public bool Enabled { get => p2p.IsEnabled; set { p2p.IsEnabled = value; } }
    }

    class BulletSimulation
    {
        DefaultCollisionConfiguration colConfiguration = new DefaultCollisionConfiguration();
        CollisionDispatcher colDispatcher;
        DbvtBroadphase broadphase;
        DiscreteDynamicsWorld colWorld;
        ConstraintSolver solver;

        DebugDrawModes debugDraw;
        public DebugDrawModes DebugDraw { get => debugDraw; set
            {
                debugDraw = value;

            } }

        List<SimObjectMesh> bodies = new List<SimObjectMesh>();
        List<Constraint> constraints = new List<Constraint>();
        
        public delegate void DebugDrawLineDel(ref Matrix4 viewProj, OpenTK.Vector3 from, OpenTK.Vector3 to, OpenTK.Vector3 color);
        public DebugDrawLineDel DebugDrawLine = null;
        Matrix4 viewProjDbg;

        public BulletSimulation()
        {
            colDispatcher = new CollisionDispatcher(colConfiguration);
            broadphase = new DbvtBroadphase();
            solver = new NncgConstraintSolver();
            colWorld = new DiscreteDynamicsWorld(colDispatcher, broadphase, solver, colConfiguration);
            colWorld.DebugDrawer = new DbgRenderer(this);            
        }

        public void DrawLine(ref BulletSharp.Math.Vector3 from, ref BulletSharp.Math.Vector3 to, ref BulletSharp.Math.Vector3 color)
        {
            if (DebugDrawLine != null)
                DebugDrawLine(ref viewProjDbg, Utils.FromBVector3(from), Utils.FromBVector3(to), Utils.FromBVector3(color));
        }

        public void Init()
        {
        }

        public void AddObj(SimObjectMesh obj)
        {
            bodies.Add(obj);
            colWorld.AddCollisionObject(obj.Body);
            obj.AfterWorldAdd();
        }

        public void AddConst(Constraint constraint)
        {
            constraints.Add(constraint);
            colWorld.AddConstraint(constraint.C, true);
        }


        public void DrawDebug(Matrix4 viewProj)
        {
            viewProjDbg = viewProj;
            colWorld.DebugDrawWorld();
        }
        public void Step()
        {
            var simulationTimestep = 1f / 60f;
            colWorld.StepSimulation(simulationTimestep, 10);
            foreach (var body in bodies)
            {
                body.Refresh();
            }
        }        
    }

    class DbgRenderer : DebugDraw
    {

        BulletSimulation pthis;
        public DbgRenderer(BulletSimulation bs)
        {
            pthis = bs;
        }
        public override DebugDrawModes DebugMode { get => pthis.DebugDraw; set => pthis.DebugDraw = value; }

        public override void Draw3DText(ref BulletSharp.Math.Vector3 location, string textString)
        {
        }

        public override void DrawLine(ref BulletSharp.Math.Vector3 from, ref BulletSharp.Math.Vector3 to, ref BulletSharp.Math.Vector3 color)
        {
            pthis.DrawLine(ref from, ref to, ref color);
        }

        public override void ReportErrorWarning(string warningString)
        {
            System.Diagnostics.Debug.WriteLine(warningString);
        }
    }
}
