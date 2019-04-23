using System;

namespace Paramore.Brighter.Inbox.Orleans.Grain
{
    [Serializable]
    public class CommandInboxGrainState
    {
        public string Command { get; set; }

        public string ContextKey { get; set; }

        public string CommandType { get; set; }

        public DateTime SubmittedDateTime { get; set; }
    }
}
