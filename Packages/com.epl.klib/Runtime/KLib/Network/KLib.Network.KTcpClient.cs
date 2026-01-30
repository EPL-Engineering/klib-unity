using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace KLib.Network
{
    public class KTcpClient
    {
        private TcpClient _socket = null;

        public int SendTimeout { get; set; }
        public int ReceiveTimeout { get; set; }
        public string LastError { get { return _lastError; } }

        private string _lastError;

        private bool _bigEndian = false;

        private NetworkStream _theStream;
        private BinaryReader _theReader;
        private BinaryWriter _theWriter;

        #region STATIC METHODS
        public static int SendMessage(IPEndPoint ipEndPoint, string message)
        {
            int result = -1;

            try
            {
                var client = new KTcpClient();
                client.ConnectTCPServer(ipEndPoint.Address.ToString(), ipEndPoint.Port);
                result = client.WriteStringAsByteArray(message);
                client.CloseTCPServer();
            }
            catch (Exception ex)
            {
            }

            return result;

        }
        #endregion

        public void Connect(string hostName, int port)
        {
            _socket = new TcpClient(hostName, port);
        }

        public void ConnectAsync(string hostName, int port)
        {
            _socket = new TcpClient();
            var result = _socket.BeginConnect(hostName, port, null, null);

            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

            if (!success)
            {
                throw new Exception("failed to connect");
            }

            _socket.EndConnect(result);
        }

        public void Close()
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }

        public void ConnectTCPServer(string hostName, int port)
        {
            _socket = new TcpClient(hostName, port);
            _theStream = _socket.GetStream();
            _theReader = new BinaryReader(_theStream);
            _theWriter = new BinaryWriter(_theStream);
        }

        public void CloseTCPServer()
        {
            _theStream.Dispose();

            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
        }

        public int WriteBinary(string s)
        {
            var byteArray = Encoding.UTF8.GetBytes(s);
            int nbytes = byteArray.Length;

            _theWriter.Write(nbytes);
            _theWriter.Write(byteArray);
            _theWriter.Flush();

            return _theReader.ReadInt32();
        }

        public int WriteBinary(byte[] data)
        {
            int nbytes = data.Length;

            _theWriter.Write(nbytes);
            _theWriter.Write(data);
            _theWriter.Flush();

            return _theReader.ReadInt32();
        }

        public int WriteStringAsByteArray(string s)
        {
            var byteArray = Encoding.UTF8.GetBytes(s);
            int nbytes = byteArray.Length;

            if (_bigEndian)
            {
                var bytes = BitConverter.GetBytes(nbytes);
                Array.Reverse(bytes);
                nbytes = BitConverter.ToInt32(bytes, 0);
            }

            _theWriter.Write(nbytes);
            _theWriter.Write(byteArray);
            _theWriter.Flush();

            return _theReader.ReadInt32();
        }

        public string WriteStringAndBytes(string s, string data)
        {
            string result = null;

            using (NetworkStream theStream = _socket.GetStream())
            using (BinaryReader theReader = new BinaryReader(theStream))
            using (BinaryWriter theWriter = new BinaryWriter(theStream))
            using (StreamWriter textWriter = new StreamWriter(theStream))
            {
                textWriter.WriteLine(s);
                textWriter.Flush();

                var byteArray = System.Text.Encoding.UTF8.GetBytes(data);
                int nbytes = byteArray.Length;

                var bytes = BitConverter.GetBytes(nbytes);
                Array.Reverse(bytes);
                nbytes = BitConverter.ToInt32(bytes, 0);

                theWriter.Write(nbytes);
                theWriter.Write(byteArray);
                theWriter.Flush();

                result = theReader.ReadString();
            }

            return result;
        }

        public string WriteStringAndBytes(string s, byte[] data)
        {
            string result = null;

            using (NetworkStream theStream = _socket.GetStream())
            using (BinaryReader theReader = new BinaryReader(theStream))
            using (BinaryWriter theWriter = new BinaryWriter(theStream))
            using (StreamWriter textWriter = new StreamWriter(theStream))
            {
                textWriter.WriteLine(s);
                textWriter.Flush();

                int nbytes = data.Length;

                var bytes = BitConverter.GetBytes(nbytes);
                Array.Reverse(bytes);
                nbytes = BitConverter.ToInt32(bytes, 0);

                theWriter.Write(nbytes);
                theWriter.Write(data);
                theWriter.Flush();

                result = theReader.ReadString();
            }

            return result;
        }

        public string WriteStringToOutputStream(string s)
        {
            string result = "";

            using (NetworkStream theStream = _socket.GetStream())
            using (StreamWriter theWriter = new StreamWriter(theStream))
            using (BinaryReader theReader = new BinaryReader(theStream))
            {
                theWriter.WriteLine(s);
                theWriter.Flush();
                result = theReader.ReadString();
            }

            return result;
        }

        public string ReadStringFromInputStream()
        {
            string result = null;

            using (NetworkStream theStream = _socket.GetStream())
            using (BinaryReader theReader = new BinaryReader(theStream))
            {
                result = theReader.ReadString();
            }

            return result;
        }

        public int ReadIntFromInputStream()
        {
            int result = -1;

            using (NetworkStream theStream = _socket.GetStream())
            using (BinaryReader theReader = new BinaryReader(theStream))
            {
                result = theReader.ReadInt32();
            }

            return result;
        }

        public void StartBufferedSend()
        {
            _theStream = _socket.GetStream();
            _theWriter = new BinaryWriter(_theStream);
            _theReader = new BinaryReader(_theStream);
        }

        public void EndBufferedSend()
        {
            _theWriter.Close();
            _theReader.Close();
            _theStream.Close();
        }

        public int SendBuffer(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            _theWriter.Write(ProcessInt32(bytes.Length));
            _theWriter.Write(bytes);
            _theWriter.Flush();
            return _theReader.ReadInt32();
        }

        public int SendBuffer(byte[] buffer)
        {
            _theWriter.Write(ProcessInt32(buffer.Length));
            _theWriter.Write(buffer);
            _theWriter.Flush();
            return _theReader.ReadInt32();
        }

        private int ProcessInt32(int raw)
        {
            int num = raw;
            if (_bigEndian)
            {
                byte[] bytes = BitConverter.GetBytes(num);
                Array.Reverse(bytes);
                num = BitConverter.ToInt32(bytes, 0);
            }

            return num;
        }


    }
}