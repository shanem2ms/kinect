using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectData
{
    public class SceneNode
    {
        protected string name;
        public string Name => name;
        public virtual ObservableCollection<SceneNode> Nodes { get => null; }

        public bool IsSelected = false;

        public SceneNode this[string name]
        {
            get => Nodes.FirstOrDefault(sn => sn.name == name);
        }

        protected SceneNode(string _n)
        {
            name = _n;
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

    public class Container : SceneNode
    {
        public override ObservableCollection<SceneNode> Nodes => Children;

        public Container(string name) :
            base(name)
        {

        }
        public ObservableCollection<SceneNode> Children = new ObservableCollection<SceneNode>();
    }
}
