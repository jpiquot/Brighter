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
        public string Command { get; set; }
        public string ContextKey { get; set; }
        public string CommandType { get; set; }
        public DateTime SubmittedDateTime { get; set; }
    }
}
