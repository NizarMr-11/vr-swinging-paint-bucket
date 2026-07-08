using System.Collections.Generic;
using HarmonicEngineV4.Networking.Transport;

namespace HarmonicEngineV4.Networking
{
    /// <summary>Shares paired loopback transports for host/client settings with the same session id.</summary>
    internal static class V4NetworkLoopbackPair
    {
        private sealed class SessionPair
        {
            public V4LoopbackTransport Host;
            public V4LoopbackTransport Client;
        }

        private static readonly Dictionary<ulong, SessionPair> Sessions = new Dictionary<ulong, SessionPair>();

        public static IV4NetworkTransport Resolve(V4NetworkRole role, ulong sessionId)
        {
            if (!Sessions.TryGetValue(sessionId, out SessionPair pair))
            {
                pair = new SessionPair
                {
                    Host = new V4LoopbackTransport(),
                    Client = new V4LoopbackTransport()
                };
                pair.Host.PairWith(pair.Client);
                Sessions[sessionId] = pair;
            }

            return role == V4NetworkRole.Host ? pair.Host : pair.Client;
        }

        public static void Reset(ulong sessionId)
        {
            Sessions.Remove(sessionId);
        }

        public static void ResetAll()
        {
            Sessions.Clear();
        }
    }
}
