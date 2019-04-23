using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paramore.Brighter.Inbox.Orleans.Grain
{
    [Serializable]
    public class CommandAndContext
    {
        public string Command { get; set; }
        public string ContextKey { get; set; }
    }
}
