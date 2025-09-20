using System;

namespace MikaUISystem
{
    public sealed record Logger
    {
        public Action<object> Log { get; init; }
        public Action<object> LogWarning { get; init; }
        public Action<object> LogError { get; init; }
    }
}