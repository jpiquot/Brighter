using System;

namespace Paramore.Brighter.Inbox.Orleans.Grain
{
    [Serializable]
    public class CommandAndContext
    {
        public string Command { get; set; }

        public string ContextKey { get; set; }
    }
}
