using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KLibU.Net
{
    /// <summary>
    /// Provides network discovery utilities for finding available endpoints and addresses.
    /// </summary>
    public static class Discovery
    {
        /// <summary>
        /// Finds the next available TCP endpoint on the best server address and an open port.
        /// </summary>
        /// <returns>
        /// An <see cref="IPEndPoint"/> representing the next available address and port,
        /// or <c>null</c> if no suitable address is found.
        /// </returns>
        public static IPEndPoint FindNextAvailableEndPoint()
        {
            var ipAddress = FindServerAddress();
            if (ipAddress == null)
            {
                return null;
            }

            int port = GetAvailablePort(ipAddress);

            return new IPEndPoint(ipAddress, port);
        }

        /// <summary>
        /// Finds the best available IP address for a TCP server.
        /// Priority: LAN (192.168.x.x) → Direct Ethernet (169.254.x.x) → localhost.
        /// Excludes organisation network (10.10.x.x).
        /// </summary>
        /// <param name="canUseLocalhost">If true, allows localhost as a fallback address.</param>
        /// <returns>
        /// The best available <see cref="IPAddress"/> for server use, or <c>null</c> if none found.
        /// </returns>
        public static IPAddress FindServerAddress(bool canUseLocalhost = true)
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(addr => addr.Address)
                .Where(addr => addr.AddressFamily == AddressFamily.InterNetwork)
                .ToList();

            return candidates.FirstOrDefault(a => a.GetAddressBytes()[0] == 192 && a.GetAddressBytes()[1] == 168)
                ?? candidates.FirstOrDefault(a => a.GetAddressBytes()[0] == 169 && a.GetAddressBytes()[1] == 254)
                ?? (canUseLocalhost ? IPAddress.Loopback : null);
        }

        /// <summary>
        /// Gets the appropriate discovery address for multicast or broadcast scenarios.
        /// </summary>
        /// <param name="multicast">If true, returns a fixed multicast address.</param>
        /// <param name="address">The base address to determine the broadcast address.</param>
        /// <returns>
        /// The discovery <see cref="IPAddress"/> for the given scenario, or <c>null</c> if not applicable.
        /// </returns>
        public static IPAddress GetDiscoveryAddress(bool multicast, IPAddress address)
        {
            if (multicast)
            {
                return IPAddress.Parse("234.5.6.7");
            }
            if (address.ToString().StartsWith("169.254"))
            {
                return IPAddress.Parse("169.254.255.255");
            }
            if (address.ToString().StartsWith("192.168"))
            {
                return IPAddress.Parse("192.168.1.255");
            }
            if (address.ToString().Equals("127.0.0.1") || address.ToString().Equals("localhost"))
            {
                return IPAddress.Parse("127.0.0.1");
            }

            return null;
        }

        /// <summary>
        /// Finds an available TCP port on the specified IP address.
        /// </summary>
        /// <param name="ipAddress">The IP address to check for an available port.</param>
        /// <returns>An available port number.</returns>
        private static int GetAvailablePort(IPAddress ipAddress)
        {
            TcpListener listener = new TcpListener(ipAddress, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
