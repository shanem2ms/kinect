using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;

namespace Scene
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SceneNode
    {
        public class RenderData
        {
            public OpenTK.Matrix4 viewProj;
            public bool isPick;
            public List<PickItem> pickObjects;
            public int pickIdx;
            public GLObjects.Program ActiveProgram;
            public GLObjects.VertexArray ActiveVA;
            public int passIdx;
        }

        [JsonProperty]
        protected string name;

        public string Name => name;

        public virtual ObservableCollection<SceneNode> Nodes { get => null; }

        public virtual SceneNode Parent { get => null; }

        public virtual OpenTK.Matrix4 WorldMatrix => OpenTK.Matrix4.Identity;

        public virtual bool ShouldSerialize { get; } = false;

        public virtual void ApplyOverrideTransform() { }
        public virtual void SetOverrideWsTransform(OpenTK.Matrix4? transform) { }

        public bool IsSelected = false;

        protected bool isInit = false;


        protected virtual void OnInit() 
        { 
        }

        public SceneNode this[string name]
        {
            get => Nodes.FirstOrDefault(sn => sn.name == name);
        }

        protected SceneNode()
        { }

        protected SceneNode(string _n)
        {
            name = _n;
        }

        public void Render(RenderData renderData)
        {
            if (!isInit)
            { OnInit(); isInit = true; }
            OnRender(renderData);
            if (Nodes != null)
            {
                foreach (SceneNode child in Nodes)
                {
                    child.Render(renderData);
                }
            }
        }

        virtual protected void OnRender(RenderData renderData)
        { 
        }

        public void GetAllObjects<T>(List<T> objlist) where T : class
        {
            if (this is T)
                objlist.Add(this as T);
            if (Nodes == null)
                return;
            foreach (SceneNode node in Nodes)
            {
                node.GetAllObjects(objlist);
            }
        }

        public void OnSceneNode<T>(Action<T> callback) where T : SceneNode
        {
            if (this is T) callback(this as T);
            if (this.Nodes != null)
            {
                foreach (SceneNode sn in Nodes)
                {
                    sn.OnSceneNode<T>(callback);
                }
            }
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Container : SceneNode
    {
        public override ObservableCollection<SceneNode> Nodes => Children;

        public Container(string name) :
            base(name)
        {

        }
        [JsonProperty]
        public ObservableCollection<SceneNode> Children = new ObservableCollection<SceneNode>();

    }

    public enum TransformTool
    {
        None = 0,
        X = 1,
        Y = 2,
        Z = 4,
        Move = 8,
        Scale = 16,
        Rotate = 32
    }

    public class PickItem
    {
        public PickItem(SceneNode sn)
        {
            node = sn;
        }

        public PickItem(TransformTool t)
        {
            tool = t;            
        }
        public SceneNode node = null;
        public TransformTool tool = TransformTool.None;
    }
}
