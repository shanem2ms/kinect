using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectData
{
    public abstract class SceneNode
    {
        protected string name;
        public string Name => name;
        public abstract IEnumerable<SceneNode> Nodes { get; }

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
        public override IEnumerable<SceneNode> Nodes => Children;

        public Container(string name) :
            base(name)
        {

        }
        public List<SceneNode> Children = new List<SceneNode>();
    }
}
