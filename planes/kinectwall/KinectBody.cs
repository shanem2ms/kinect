﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OpenTK;
using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using kd = KinectData;

namespace kinectwall
{
    public class KinectBody
    {
        KinectSensor kinectSensor;
        BodyFrameReader bodyFrameReader;
        // The face frame source
        HighDefinitionFaceFrameSource _faceSource = null;
        HighDefinitionFaceFrameReader _faceReader = null;
        private FaceAlignment currentFaceAlignment = null;
        private FaceModel currentFaceModel = null;
        private FaceModelBuilder faceModelBuilder = null;
        FaceMesh faceMesh = new FaceMesh();
        ulong CurrentTrackingId = 0;

        public bool IsRecording = false;

        public KinectBody()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            this.bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

            // Initialize the face source with the desired features
            _faceSource = new HighDefinitionFaceFrameSource(this.kinectSensor);
            _faceSource.TrackingIdLost += _faceSource_TrackingIdLost;
            _faceReader = _faceSource.OpenReader();
            _faceReader.FrameArrived += _faceReader_FrameArrived1;
            this.currentFaceAlignment = new FaceAlignment();
            this.currentFaceModel = new FaceModel();

            // open the sensor
            this.kinectSensor.Open();

            StartFaceCapture();
        }

        /// <summary>
        /// Start a face capture operation
        /// </summary>
        private void StartFaceCapture()
        {
            this.StopFaceCapture();

            this.faceModelBuilder = null;

            this.faceModelBuilder = this._faceSource.OpenModelBuilder(FaceModelBuilderAttributes.None);

            this.faceModelBuilder.BeginFaceDataCollection();

            this.faceModelBuilder.CollectionCompleted += this.HdFaceBuilder_CollectionCompleted;
        }

        /// <summary>
        /// This event fires when the face capture operation is completed
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void HdFaceBuilder_CollectionCompleted(object sender, FaceModelBuilderCollectionCompletedEventArgs e)
        {
            var modelData = e.ModelData;

            this.currentFaceModel = modelData.ProduceFaceModel();

            this.faceModelBuilder.Dispose();
            this.faceModelBuilder = null;

            App.WriteLine("Face Capture Complete");
        }
        /// <summary>
        /// Cancel the current face capture operation
        /// </summary>
        private void StopFaceCapture()
        {
            if (this.faceModelBuilder != null)
            {
                this.faceModelBuilder.Dispose();
                this.faceModelBuilder = null;
            }
        }


        private void _faceReader_FrameArrived1(object sender, HighDefinitionFaceFrameArrivedEventArgs e)
        {
            CheckOnBuilderStatus();
            using (var frame = e.FrameReference.AcquireFrame())
            {
                // We might miss the chance to acquire the frame; it will be null if it's missed.
                // Also ignore this frame if face tracking failed.
                if (frame == null || !frame.IsFaceTracked)
                {
                    return;
                }

                frame.GetAndRefreshFaceAlignmentResult(this.currentFaceAlignment);
                App.OnWriteMsg($"face {frame.TrackingId}");                
                faceMesh.Update(this.currentFaceModel, this.currentFaceAlignment);
            }
        }


        /// <summary>
        /// Check the face model builder status
        /// </summary>
        private void CheckOnBuilderStatus()
        {
            if (this.faceModelBuilder == null)
            {
                return;
            }

            string newStatus = string.Empty;

            var captureStatus = this.faceModelBuilder.CaptureStatus;
            newStatus += captureStatus.ToString();

            var collectionStatus = this.faceModelBuilder.CollectionStatus;

            newStatus += ", " + GetCollectionStatusText(collectionStatus);

            App.WriteLine(newStatus);
        }

        /// <summary>
        /// Gets the current collection status
        /// </summary>
        /// <param name="status">Status value</param>
        /// <returns>Status value as text</returns>
        private static string GetCollectionStatusText(FaceModelBuilderCollectionStatus status)
        {
            string res = string.Empty;

            if ((status & FaceModelBuilderCollectionStatus.FrontViewFramesNeeded) != 0)
            {
                res = "FrontViewFramesNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.LeftViewsNeeded) != 0)
            {
                res = "LeftViewsNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.RightViewsNeeded) != 0)
            {
                res = "RightViewsNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.TiltedUpViewsNeeded) != 0)
            {
                res = "TiltedUpViewsNeeded";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.Complete) != 0)
            {
                res = "Complete";
                return res;
            }

            if ((status & FaceModelBuilderCollectionStatus.MoreFramesNeeded) != 0)
            {
                res = "TiltedUpViewsNeeded";
                return res;
            }

            return res;
        }

        class FaceMesh
        {
            public uint[] indices = null;
            public Vector3[] pos;

            /// <summary>
            /// Sends the new deformed mesh to be drawn
            /// </summary>
            public void Update(FaceModel faceModel, FaceAlignment faceAlignment)
            {
                var vertices = faceModel.CalculateVerticesForAlignment(faceAlignment);
                if (this.indices == null)
                    this.indices = faceModel.TriangleIndices.ToArray();
                this.pos = vertices.Select(csp => new Vector3(csp.X, csp.Y, csp.Z)).ToArray();
            }
        }

        private void _faceSource_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            var lostTrackingID = e.TrackingId;

            if (this.CurrentTrackingId == lostTrackingID)
            {
                this.CurrentTrackingId = 0;
                if (this.faceModelBuilder != null)
                {
                    this.faceModelBuilder.Dispose();
                    this.faceModelBuilder = null;
                }

                this._faceSource.TrackingId = 0;
            }
        }

        long timeStamp;
        Body[] bodies = null;

        class TrackedBody
        {

        }
        Dictionary<ulong, TrackedBody> trackedBodies = new Dictionary<ulong, TrackedBody>();

        
        public kd.Frame CurrentFrame;
        private void BodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            kd.Frame frame = null;
            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    timeStamp = bodyFrame.RelativeTime.Ticks;
                    if (this.bodies == null)
                        this.bodies = new Body[bodyFrame.BodyCount];

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    for (int i = 0; i < bodies.Length; ++i)
                    {
                        Body b = bodies[i];
                        if (b.IsTracked)
                        {
                            //App.WriteLine($"b [{i}] {b.TrackingId}");
                            kd.Body newBody = new kd.Body(null);
                            newBody.top = kd.JointNode.MakeBodyDef();
                            Dictionary<kd.JointType, kd.Joint> jointDict = new Dictionary<kd.JointType, kd.Joint>();
                            newBody.joints = new Dictionary<kd.JointType, kd.Joint>();
                            foreach (var kv in b.Joints)
                            {
                                kd.JointType jt = (kd.JointType)kv.Key;
                                kd.Joint joint = new kd.Joint()
                                { Position = new Vector3(kv.Value.Position.X, kv.Value.Position.Y, kv.Value.Position.Z) };
                                joint.TrackingState = (kd.TrackingState)kv.Value.TrackingState;
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
                            newBody.joints = jointDict;
                            newBody.top.SetJoints(jointDict);
                            newBody.GetJointNodes();

                            if (frame == null)
                            {
                                frame = new kd.Frame();
                                frame.bodies = new Dictionary<ulong, kd.Body>();
                            }
                            frame.bodies.Add(b.TrackingId, newBody);
                        }
                    }

                    if (frame != null)
                    {
                        frame.timeStamp = e.FrameReference.RelativeTime.Ticks;
                        this._faceSource.TrackingId = this.CurrentTrackingId = frame.bodies.First().Key;
                    }

                    CurrentFrame = frame;
                }
            }

            if (frame != null && IsRecording)
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
