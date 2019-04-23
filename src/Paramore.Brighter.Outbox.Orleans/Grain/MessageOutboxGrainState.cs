using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paramore.Brighter.Outbox.Orleans.Grain
{
    [Serializable]
    public class MessageOutboxGrainState
    {
        public string Message { get; set; }
        public DateTime DateTime { get; set; }
    }
}
