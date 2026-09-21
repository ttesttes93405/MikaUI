

using System;

namespace MikaUI
{
    public class ControlTokenDisposedException : ObjectDisposedException
    {
        readonly UIControlToken token;

        public string TokenName => token.Name;
        public Guid TokenID => token.TokenID;
        public Guid ElementID => token.ElementID;

        public ControlTokenDisposedException(UIControlToken token) : base($"Token {token.Name} ({token.TokenID}) has already been disposed.")
        {
            this.token = token;
        }
    }
}
