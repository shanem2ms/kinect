using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graph
{
    public abstract class SceneNode
    {
        protected string name;
        public string Name => name;
        public abstract IEnumerable<SceneNode> Nodes { get; }

        protected SceneNode(string _n)
        {
            name = _n;
        }
    }
}
