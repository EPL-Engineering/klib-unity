using UnityEngine;
using System.Collections;
using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KLib.Net
{
    /// <summary>
    /// 
    /// </summary>
    public class NetworkDiscoveryBeacon: MonoBehaviour
    {
        private bool _stopBroadcast = false;

        /// <summary>
        /// Called by TCP server to start discovery server.
        /// </summary>
        /// <param name="name">Name of TCP server (typically all caps). Client broadcasts this name when searching for server. </param>
        /// <param name="address">LAN address on which TCP server is listening</param>
        /// <param name="port">Port on which TCP server is listening</param>
        public void StartBroadcasting(string name, string address, int port, int discoveryPort = 10001, int intervalSeconds = 2)
        {
            var beacon = new ServerBeacon()
            {
                Name = name,
                Address = address,
                TcpPort = port,
                Version = 1
            };

            var broadcastMessage = KLib.FileIO.JSONSerializeToString(beacon);

            var broadcastAddress = Discovery.GetDiscoveryAddress(multicast: false, IPAddress.Parse(address));
            var broadcastEndPoint = new IPEndPoint(broadcastAddress, 10001);

            Debug.Log($"starting discovery beacon broadcasting {name} on {address}:{port} to {broadcastEndPoint.ToString()}");
            StartCoroutine(BeaconBroadcast(broadcastEndPoint, broadcastMessage));
        }

        IEnumerator BeaconBroadcast(IPEndPoint endpoint, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);

            while (!_stopBroadcast)
            {
                using (var udp = new UdpClient())
                {
                    udp.Send(bytes, bytes.Length, endpoint);
                    //Debug.Log($"Sent discovery beacon: {message}");
                }
                yield return new WaitForSeconds(1f);
            }
            Debug.Log("discovery beacon stopped");  
        }

        /// <summary>
        /// Called by TCP server to stop discovery server.
        /// </summary>
        public void StopBroadcast()
        {
            _stopBroadcast = true;
        }

        private void OnDestroy()
        {
            _stopBroadcast = true;
            Debug.Log("discovery beacon stopped");
        }

    }
}