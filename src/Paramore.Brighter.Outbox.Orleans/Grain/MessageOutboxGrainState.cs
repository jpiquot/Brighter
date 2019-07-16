using System;

namespace Paramore.Brighter.Outbox.Orleans.Grain
{
    [Serializable]
    public class MessageOutboxGrainState
    {
        public string Message { get; set; }

        public DateTime DateTime { get; set; }
    }
}
