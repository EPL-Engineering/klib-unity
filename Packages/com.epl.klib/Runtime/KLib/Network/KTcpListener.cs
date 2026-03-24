using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;

namespace KLibU.Net
{
    /// <summary>
    /// Provides TCP server functionality for accepting client connections and exchanging TcpMessage objects.
    /// </summary>
    public class KTcpListener
    {
        private TcpListener _listener = null;
        private TcpClient _client = null;

        private NetworkStream _network = null;
        private BinaryReader _theReader;
        private BinaryWriter _theWriter;

        private IPEndPoint _ipEndPoint;

        /// <summary>
        /// Starts listening for incoming TCP connections on the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The IP endpoint to listen on.</param>
        /// <returns>True if the listener started successfully.</returns>
        public bool StartListener(IPEndPoint endpoint)
        {
            _listener = new TcpListener(endpoint);
            _listener.Start();

            _ipEndPoint = (IPEndPoint)_listener.LocalEndpoint;
            return true;
        }

        /// <summary>
        /// Stops the TCP listener and releases associated resources.
        /// </summary>
        public void CloseListener()
        {
            if (_listener != null)
            {
                _listener.Stop();
                _listener = null;
            }
        }

        /// <summary>
        /// Checks if there are pending client connection requests.
        /// </summary>
        /// <returns>True if a client is waiting to connect; otherwise, false.</returns>
        public bool Pending()
        {
            return _listener.Pending();
        }

        /// <summary>
        /// Accepts a pending TCP client connection and initializes the network stream and readers/writers.
        /// </summary>
        public void AcceptTcpClient()
        {
            _client = _listener.AcceptTcpClient();
            _network = _client.GetStream();
            _theReader = new BinaryReader(_network);
            _theWriter = new BinaryWriter(_network);
        }

        /// <summary>
        /// Closes the current TCP client connection and disposes the network stream.
        /// </summary>
        public void CloseTcpClient()
        {
            _network.Dispose();
        }

        /// <summary>
        /// Reads the next incoming message from the stream and deserializes it as a TcpMessage.
        /// </summary>
        /// <returns>The deserialized TcpMessage received from the client.</returns>
        public TcpMessage ReadRequest()
        {
            string json = ReadString();  // your existing method
            return TcpMessage.Deserialize(json);
        }

        /// <summary>
        /// Serializes a TcpMessage and writes it back to the client.
        /// </summary>
        /// <param name="response">The TcpMessage to send to the client.</param>
        public void WriteResponse(TcpMessage response)
        {
            WriteStringAsByteArray(response.Serialize());  // your existing method
        }

        /// <summary>
        /// Reads a UTF-8 encoded string from the network stream.
        /// The string is prefixed with its length as a 32-bit integer.
        /// </summary>
        /// <returns>The string read from the stream.</returns>
        public string ReadString()
        {
            int nbytes = _theReader.ReadInt32();
            var byteArray = _theReader.ReadBytes(nbytes);

            return System.Text.Encoding.UTF8.GetString(byteArray, 0, byteArray.Length);
        }

        /// <summary>
        /// Writes a UTF-8 encoded string to the network stream, prefixed with its length as a 32-bit integer.
        /// </summary>
        /// <param name="s">The string to write.</param>
        public void WriteStringAsByteArray(string s)
        {
            var byteArray = System.Text.Encoding.UTF8.GetBytes(s);
            int nbytes = byteArray.Length;

            _theWriter.Write(nbytes);
            _theWriter.Write(byteArray);
            _theWriter.Flush();
        }

        /// <summary>
        /// Reads a byte array from the network stream, prefixed with its length as a 32-bit integer.
        /// Sends an acknowledgement (int value 1) after reading.
        /// </summary>
        /// <returns>The byte array read from the stream.</returns>
        public byte[] ReadBytes()
        {
            byte[] result = null;

            int nbytes = _theReader.ReadInt32();
            result = _theReader.ReadBytes(nbytes);

            _theWriter.Write((int)1);
            _theWriter.Flush();

            return result;
        }

    }
}