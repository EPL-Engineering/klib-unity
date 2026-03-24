using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace KLibU.Net
{
    /// <summary>
    /// Provides TCP client functionality for sending and receiving TcpMessage objects using a length-prefixed wire protocol.
    /// </summary>
    public class KTcpClient
    {
        private TcpClient _client;
        private NetworkStream _networkStream;
        private BinaryReader _reader;
        private BinaryWriter _writer;

        /// <summary>
        /// Sends a TcpMessage request to the specified remote endpoint and returns the response.
        /// Handles connection setup and teardown automatically.
        /// </summary>
        /// <param name="remoteEndPoint">The remote server endpoint to connect to.</param>
        /// <param name="request">The TcpMessage request to send.</param>
        /// <returns>The TcpMessage response from the server.</returns>
        public static TcpMessage SendRequest(IPEndPoint remoteEndPoint, TcpMessage request)
        {
            var client = new KTcpClient();
            try
            {
                client.Connect(remoteEndPoint);
                return client.SendRequest(request);
            }
            catch (Exception ex)
            {
                return new TcpMessage { Code = 500, Command = "Error", Payload = ex.Message };
            }
            finally
            {
                client.Close();
            }
        }

        /// <summary>
        /// Establishes a TCP connection to the specified endpoint.
        /// </summary>
        /// <param name="localEP">The remote endpoint to connect to.</param>
        private void Connect(IPEndPoint localEP)
        {
            _client = new TcpClient(localEP.Address.ToString(), localEP.Port);
        }

        /// <summary>
        /// Closes the TCP client and releases associated resources.
        /// </summary>
        private void Close()
        {
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
        }

        /// <summary>
        /// Starts a buffered send session by connecting to the specified endpoint and initializing stream readers/writers.
        /// </summary>
        /// <param name="localEP">The remote endpoint to connect to.</param>
        public void StartBufferedSend(IPEndPoint localEP)
        {
            _client = new TcpClient(localEP.Address.ToString(), localEP.Port);
            _networkStream = _client.GetStream();
            _writer = new BinaryWriter(_networkStream);
            _reader = new BinaryReader(_networkStream);
        }

        /// <summary>
        /// Ends the buffered send session and closes all associated streams and the client.
        /// </summary>
        public void EndBufferedSend()
        {
            _writer.Close();
            _reader.Close();
            _networkStream.Close();
            Close();
        }

        /// <summary>
        /// Sends a TcpMessage request and blocks until a TcpMessage response arrives.
        /// This is the single method all new client code should use.
        /// </summary>
        /// <param name="request">The TcpMessage request to send.</param>
        /// <returns>The TcpMessage response from the server.</returns>
        public TcpMessage SendRequest(TcpMessage request)
        {
            string responseJson = SendRequestReceiveResponse(request.Serialize());
            return TcpMessage.Deserialize(responseJson);
        }

        /// <summary>
        /// Writes a byte array to the network stream, prefixed with its length as a 32-bit integer.
        /// </summary>
        /// <param name="buffer">The byte array to send.</param>
        public void WriteBuffer(byte[] buffer)
        {
            _writer.Write(ProcessInt32(buffer.Length));
            _writer.Write(buffer);
            _writer.Flush();
        }

        /// <summary>
        /// Reads a buffered TcpMessage response from the network stream.
        /// </summary>
        /// <returns>The TcpMessage response received from the server.</returns>
        public TcpMessage ReadBufferedSendResponse()
        {
            int length = ProcessInt32(_reader.ReadInt32());
            byte[] responseBytes = _reader.ReadBytes(length);
            string responseJson = Encoding.UTF8.GetString(responseBytes);
            return TcpMessage.Deserialize(responseJson);
        }

        /// <summary>
        /// Low-level: sends a JSON string, receives a JSON string back.
        /// Sits directly on top of your existing length-prefix wire protocol.
        /// </summary>
        /// <param name="jsonRequest">The JSON string to send.</param>
        /// <returns>The JSON string response received from the server.</returns>
        private string SendRequestReceiveResponse(string jsonRequest)
        {
            using (NetworkStream networkStream = _client.GetStream())
            using (BinaryReader binaryReader = new BinaryReader(networkStream))
            using (BinaryWriter binaryWriter = new BinaryWriter(networkStream))
            {

                WriteStringAsByteArray(binaryWriter, jsonRequest);  // your existing method
                binaryWriter.Flush();

                int length = ProcessInt32(binaryReader.ReadInt32());  // your existing method
                byte[] responseBytes = binaryReader.ReadBytes(length);
                return Encoding.UTF8.GetString(responseBytes);
            }
        }

        /// <summary>
        /// Writes a UTF-8 encoded string to the network stream, prefixed with its length as a 32-bit integer.
        /// </summary>
        /// <param name="theWriter">The BinaryWriter to use.</param>
        /// <param name="message">The string message to write.</param>
        private void WriteStringAsByteArray(BinaryWriter theWriter, string message)
        {
            var byteArray = Encoding.UTF8.GetBytes(message);

            theWriter.Write(ProcessInt32(byteArray.Length));
            theWriter.Write(byteArray);
            theWriter.Flush();
        }

        /// <summary>
        /// Processes a 32-bit integer, optionally reversing byte order if needed.
        /// </summary>
        /// <param name="raw">The raw integer value.</param>
        /// <returns>The processed integer value.</returns>
        private int ProcessInt32(int raw)
        {
            int value = raw;

            //if (ReverseBytes)
            //{
            //    var bytes = BitConverter.GetBytes(value);
            //    Array.Reverse(bytes);
            //    value = BitConverter.ToInt32(bytes, 0);
            //}

            return value;
        }

    }
}