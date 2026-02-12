using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KLib.Network
{
    /// <summary>
    /// Runs a UDP multicast server on 234.5.6.7 to allow clients to discover the address of a TCP server.
    /// </summary>
    public class NetworkDiscoveryServer : MonoBehaviour
    {
        Thread _readThread = null;

        UdpClient _udp = null;
        string _name = "";
        string _address = "";
        int _port = -1;
        bool _multicast = true;

        void Start() { }

        /// <summary>
        /// Indicates whether UDP server is still running.
        /// </summary>
        public bool IsReceiving { get { return _readThread.IsAlive; } }

        /// <summary>
        /// Called by TCP server to start discovery server.
        /// </summary>
        /// <param name="name">Name of TCP server (typically all caps). Client broadcasts this name when searching for server. </param>
        /// <param name="address">LAN address on which TCP server is listening</param>
        /// <param name="port">Port on which TCP server is listening</param>
        public void StartReceiving(string name, string address, int port, bool multicast = true)
        {
            _name = name;
            _address = address;
            _port = port;
            _multicast = multicast;

            // create thread for reading UDP messages
            _readThread = new Thread(new ThreadStart(ReceiveData));
            _readThread.IsBackground = true;

            _readThread.Start();
        }

        /// <summary>
        /// Called by TCP server to stop discovery server.
        /// </summary>
        public void StopReceiving()
        {
            Debug.Log("Stopping multicast discovery thread");
            StopThread();
        }

        // Stop reading UDP messages
        private void StopThread()
        {
            if (_readThread == null)
            {
                return;
            }

            _readThread.Abort();
            if (_udp != null)
            {
                _udp.Close();
                Debug.Log("Multicast discovery server closed");
            }

            //if (_readThread.IsAlive)
            //{
            //    _readThread.Abort();
            //    _udp.Close();
            //    Debug.Log("Multicast thread aborted");
            //}
        }

        private void OnDestroy()
        {
            //StopThread();
        }

        private void ReceiveData()
        {
            Debug.Log("Discovery server listening on: " + _address);

            IPAddress localAddress;
            if (_address.Equals("localhost"))
            {
                localAddress = IPAddress.Loopback;
            }
            else
            {
                localAddress = IPAddress.Parse(_address);
            }

            var ipLocal = new IPEndPoint(localAddress, 10000);

            var address = IPAddress.Parse(NetworkUtils.GetDiscoveryAddress(_multicast, _address));
            var ipEndPoint = new IPEndPoint(address, 10000);

            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(ipLocal);
            _udp.Client.ReceiveTimeout = 500;

            if (_multicast)
            {
                _udp.JoinMulticastGroup(address, localAddress);
            }

            var anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    // receive bytes
                    var bytes = _udp.Receive(ref anyIP);
                    var response = Encoding.Default.GetString(bytes);
                    //Debug.Log("Multicast received: " + response);

                    if (response.Equals(_name))
                    {
                        bytes = Encoding.UTF8.GetBytes(_port.ToString());
                        _udp.Send(bytes, bytes.Length, anyIP);
                    }
                }
                catch (Exception ex)
                {
                    //Debug.Log(ex.Message);
                }
            }
        }
    }
}