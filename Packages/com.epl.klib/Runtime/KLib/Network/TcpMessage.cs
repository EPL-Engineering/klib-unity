using Newtonsoft.Json;

namespace KLib.Net
{
    /// <summary>
    /// Unified message envelope used for all TCP communication.
    /// Both requests (client→server) and responses (server→client) use this shape.
    /// </summary>
    public class TcpMessage
    {
        /// <summary>
        /// Status code. Follows HTTP conventions:
        ///   200 = OK
        ///   400 = Bad request (client error)
        ///   404 = Command not recognised
        ///   500 = Server error
        /// On outgoing requests from the client, Code can be left 0.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// The command name on a request (e.g. "GetHostName", "Ping").
        /// On a response, used optionally for a human-readable status message.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Always a JSON string. Use "{}" when there is no meaningful payload.
        /// Never null — the receiver can always safely deserialise this.
        /// </summary>
        public string Payload { get; set; } = "{}";

        // -------------------------------------------------------------------------
        // Serialisation
        // -------------------------------------------------------------------------

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static TcpMessage Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<TcpMessage>(json);
        }

        // -------------------------------------------------------------------------
        // Factory helpers — keep response construction concise at call sites
        // -------------------------------------------------------------------------

        /// <summary>Creates a 200-OK response with an optional JSON payload.</summary>
        public static TcpMessage Ok(string payload = "{}")
        {
            return new TcpMessage { Code = 200, Payload = payload };
        }

        /// <summary>Creates a 200-OK response by serialising any object as the payload.</summary>
        public static TcpMessage Ok(object payloadObject)
        {
            return new TcpMessage
            {
                Code = 200,
                Payload = JsonConvert.SerializeObject(payloadObject)
            };
        }

        /// <summary>Creates a 400 Bad Request response.</summary>
        public static TcpMessage BadRequest(string message = "Bad request")
        {
            return new TcpMessage { Code = 400, Command = message, Payload = "{}" };
        }

        /// <summary>Creates a 404 Not Found response (unrecognised command).</summary>
        public static TcpMessage NotFound(string command)
        {
            return new TcpMessage
            {
                Code = 404,
                Command = $"Unknown command: {command}",
                Payload = "{}"
            };
        }

        /// <summary>Creates a 500 Internal Server Error response.</summary>
        public static TcpMessage Error(string message = "Internal server error")
        {
            return new TcpMessage { Code = 500, Command = message, Payload = "{}" };
        }

        /// <summary>Creates a command-only request with no payload. The most concise way
        /// to send an instruction that requires no accompanying data.</summary>
        public static TcpMessage Request(string command, string payload = "{}")
        {
            return new TcpMessage { Command = command, Payload = payload };
        }

        public static TcpMessage Request(string command, object payloadObject)
        {
            var payload = JsonConvert.SerializeObject(payloadObject);
            return new TcpMessage { Command = command, Payload = payload };
        }

        // -------------------------------------------------------------------------
        // Convenience
        // -------------------------------------------------------------------------

        public bool IsOk => Code == 200;

        /// <summary>
        /// Deserialises the Payload JSON string into a typed object.
        /// Usage: var result = response.GetPayload&lt;MyType&gt;();
        /// </summary>
        public T GetPayload<T>()
        {
            return JsonConvert.DeserializeObject<T>(Payload);
        }
    }
}