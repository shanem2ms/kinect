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

        public SceneNode this[string name]
        {
            get => Nodes.FirstOrDefault(sn => sn.name == name);
        }

        protected SceneNode(string _n)
        {
            name = _n;
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
