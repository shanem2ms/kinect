using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using OpenTK;
using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using kd = BodyData;

namespace kinectwall
{
    public class KinectBody
    {
        KinectSensor kinectSensor;
        BodyFrameReader bodyFrameReader;

        public event EventHandler<TrackedBody> OnNewTrackedBody;

        public bool IsRecording = false;

        public KinectBody()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            this.bodyFrameReader.FrameArrived += BodyFrameReader_FrameArrived;

            // open the sensor
            this.kinectSensor.Open();            
        }


        class FaceTrack
        {
            // The face frame source
            HighDefinitionFaceFrameSource _faceSource = null;
            HighDefinitionFaceFrameReader _faceReader = null;
            private FaceAlignment currentFaceAlignment = null;
            private FaceModel currentFaceModel = null;
            private FaceModelBuilder faceModelBuilder = null;
            TrackedFace faceMesh = new TrackedFace();
            ulong trackingId = 0;

            public long TimeStamp => faceMesh.timestamp;

            public FaceTrack(KinectSensor kinectSensor, ulong trackId)
            {
                trackingId = trackId;
                // Initialize the face source with the desired features
                _faceSource = new HighDefinitionFaceFrameSource(kinectSensor);
                _faceSource.TrackingIdLost += _faceSource_TrackingIdLost;
                _faceSource.TrackingId = trackId;
                _faceReader = _faceSource.OpenReader();
                _faceReader.FrameArrived += _faceReader_FrameArrived1;
                this.currentFaceAlignment = new FaceAlignment();
                this.currentFaceModel = new FaceModel();

                StartFaceCapture();
            }


            public kd.FaceMesh MakeMesh()
            {
                return new kd.FaceMesh()
                {
                    indices = faceMesh.indices,
                    pos = faceMesh.pos
                };
            }
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
                //CheckOnBuilderStatus();
                using (var frame = e.FrameReference.AcquireFrame())
                {
                    // We might miss the chance to acquire the frame; it will be null if it's missed.
                    // Also ignore this frame if face tracking failed.
                    if (frame == null || !frame.IsFaceTracked)
                    {
                        return;
                    }
                    frame.GetAndRefreshFaceAlignmentResult(this.currentFaceAlignment);
                    faceMesh.Update(this.currentFaceModel, this.currentFaceAlignment, frame.RelativeTime.Ticks);
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

            class TrackedFace
            {
                public uint[] indices = null;
                public Vector3[] pos;
                public long timestamp;

                /// <summary>
                /// Sends the new deformed mesh to be drawn
                /// </summary>
                public void Update(FaceModel faceModel, FaceAlignment faceAlignment, long timest)
                {
                    this.timestamp = timest;
                    var vertices = faceModel.CalculateVerticesForAlignment(faceAlignment);
                    if (this.indices == null)
                        this.indices = faceModel.TriangleIndices.ToArray();
                    this.pos = vertices.Select(csp => new Vector3(csp.X, csp.Y, csp.Z)).ToArray();
                }
            }

            private void _faceSource_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
            {
                var lostTrackingID = e.TrackingId;
                App.WriteLine("Face Lost");
                if (this.trackingId == lostTrackingID)
                {
                    if (this.faceModelBuilder != null)
                    {
                        this.faceModelBuilder.Dispose();
                        this.faceModelBuilder = null;
                    }
                }
            }
        }
        /// <summary>

        long timeStamp;
        Body[] bodies = null;

        public class TrackedBody
        {
            FaceTrack faceTrack = null;
            ulong trackingId;
            public List<Tuple<long, kd.Body>> bodyFrames = new List<Tuple<long, kd.Body>>();
            public kd.JointLimits[] JLimits = kd.JointLimits.Build();

            public TrackedBody(ulong id)
            {
                this.trackingId = id;
                App.WriteLine($"New tracked body {id}");
            }

            public void AddBodyFrame(Tuple<long, kd.Body> bodyFrame)
            {
                if (faceTrack != null)
                {
                    long msDelay = (bodyFrame.Item1 - faceTrack.TimeStamp) /
                        TimeSpan.FromMilliseconds(1).Ticks;
                    if (msDelay < 50)
                        bodyFrame.Item2.face = faceTrack.MakeMesh();
                }
                bodyFrames.Add(bodyFrame);
            }

            public void InitFaceTrack(KinectSensor sensor)
            {
                this.faceTrack = new FaceTrack(sensor, this.trackingId);
            }

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
                            TrackedBody tb;
                            if (!trackedBodies.TryGetValue(b.TrackingId, out tb))
                            {
                                tb = new TrackedBody(b.TrackingId);
                                tb.InitFaceTrack(this.kinectSensor);
                                trackedBodies.Add(b.TrackingId, tb);
                                OnNewTrackedBody?.Invoke(this, tb);
                            }
                            //App.WriteLine($"b [{i}] {b.TrackingId}");
                            kd.Body newBody = new kd.Body("live", null);
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

                            newBody.top.OnSceneNode<BodyData.JointNode>((jn) =>
                            {
                                Quaternion q = jn.LocalTransform.rot;
                                tb.JLimits[(int)jn.JType].ApplyQuaternion(q);
                            });

                            if (frame == null)
                            {
                                frame = new kd.Frame();
                                frame.bodies = new Dictionary<ulong, kd.Body>();
                            }
                            tb.AddBodyFrame(new Tuple<long, kd.Body>(timeStamp, newBody));
                            frame.bodies.Add(b.TrackingId, newBody);
                        }
                    }

                    if (frame != null)
                    {
                        frame.timeStamp = e.FrameReference.RelativeTime.Ticks;
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
