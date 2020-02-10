using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OpenTK;
using Microsoft.Kinect;
using kd = KinectData;

namespace kinectwall
{
    public class KinectBody
    {
        KinectSensor kinectSensor;
        BodyFrameReader bodyFrameReader;
        public KinectBody()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // open the sensor
            this.kinectSensor.Open();

            this.bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;
        }

        long timeStamp;
        Body[] bodies = null;
        
        public kd.Frame CurrentFrame;
        private void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            kd.Frame frame = null;
            bool hasBody = false;
            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    timeStamp = bodyFrame.RelativeTime.Ticks;
                    if (this.bodies == null)
                        this.bodies = new Body[bodyFrame.BodyCount];
                    if (frame == null)
                    {
                        frame = new kd.Frame();
                        frame.bodies = new kd.Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    frame.timeStamp = e.FrameReference.RelativeTime.Ticks;
                    for (int i = 0; i < bodies.Length; ++i)
                    {
                        Body b = bodies[i];
                        if (b.IsTracked)
                        {
                            hasBody = true;
                            System.Diagnostics.Debug.WriteLine($"b [{i}] {b.TrackingId}");
                            frame.bodies[i] = new kd.Body(null);
                            frame.bodies[i].top = kd.JointNode.MakeBodyDef();
                            Dictionary<kd.JointType, kd.Joint> jointDict = new Dictionary<kd.JointType, kd.Joint>();
                            frame.bodies[i].joints = new Dictionary<kd.JointType, kd.Joint>();
                            foreach (var kv in b.Joints)
                            {
                                kd.JointType jt = (kd.JointType)kv.Key;
                                kd.Joint joint = new kd.Joint()
                                { Position = new Vector3(kv.Value.Position.X, kv.Value.Position.Y, kv.Value.Position.Z) };
                                jointDict.Add(jt, joint);
                            }
                            foreach (var kv in b.JointOrientations)
                            {
                                kd.JointType jt = (kd.JointType)kv.Key;
                                jointDict[jt].Orientation = new OpenTK.Vector4(kv.Value.Orientation.X,
                                    kv.Value.Orientation.Y,
                                    kv.Value.Orientation.Z,
                                    kv.Value.Orientation.W);
                            }
                            frame.bodies[i].joints = jointDict;
                            frame.bodies[i].top.SetJoints(jointDict);
                            frame.bodies[i].GetJointNodes();
                        }
                    }

                    CurrentFrame = frame;
                }
            }

            if (hasBody && App.Recording)
                WriteFrame();
        }

        FileStream fs;
        void WriteFrame()
        {
            if (fs == null) fs = new FileStream("body.out", FileMode.Create, FileAccess.Write);
            byte[] bytes = BitConverter.GetBytes(timeStamp);
            fs.Write(bytes, 0, bytes.Length);
            bytes = BitConverter.GetBytes(bodies.Count());
            fs.Write(bytes, 0, bytes.Length);
            foreach (Body body in bodies)
            {
                bytes = BitConverter.GetBytes(body.IsTracked);
                fs.Write(bytes, 0, bytes.Length);

                if (body.IsTracked)
                {
                    bytes = BitConverter.GetBytes(body.Lean.X);
                    fs.Write(bytes, 0, bytes.Length);
                    bytes = BitConverter.GetBytes(body.Lean.Y);
                    fs.Write(bytes, 0, bytes.Length);

                    IReadOnlyDictionary<JointType, Joint> joints = body.Joints;
                    foreach (var joint in body.Joints)
                    {                        
                        bytes = BitConverter.GetBytes((int)joint.Key);
                        fs.Write(bytes, 0, bytes.Length);

                        bytes = BitConverter.GetBytes((int)joint.Value.TrackingState);
                        fs.Write(bytes, 0, bytes.Length);
                        bytes = BitConverter.GetBytes(joint.Value.Position.X);
                        fs.Write(bytes, 0, bytes.Length);
                        bytes = BitConverter.GetBytes(joint.Value.Position.Y);
                        fs.Write(bytes, 0, bytes.Length);
                        bytes = BitConverter.GetBytes(joint.Value.Position.Z);
                        fs.Write(bytes, 0, bytes.Length);

                        var jointOrient = body.JointOrientations[joint.Key];

                        bytes = BitConverter.GetBytes(jointOrient.Orientation.X);
                        fs.Write(bytes, 0, bytes.Length);
                        bytes = BitConverter.GetBytes(jointOrient.Orientation.Y);
                        fs.Write(bytes, 0, bytes.Length);
                        bytes = BitConverter.GetBytes(jointOrient.Orientation.Z);
                        fs.Write(bytes, 0, bytes.Length);
                        bytes = BitConverter.GetBytes(jointOrient.Orientation.W);
                        fs.Write(bytes, 0, bytes.Length);
                    }
                }
            }
        }
    }
}
