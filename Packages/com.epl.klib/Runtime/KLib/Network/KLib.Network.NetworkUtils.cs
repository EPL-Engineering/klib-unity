using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace KLib.Network
{
    /// <summary>
    /// Collection of utilities for TCP/UDP
    /// </summary>
    public static class NetworkUtils
    {
        public static IPEndPoint FindNextAvailableEndPoint()
        {
            int numPortsToTry = 1000;
            int port = 4950;

            string address = FindServerAddress();

            IPAddress ipAddress = null;
            if (address.Equals("localhost"))
            {
                ipAddress = IPAddress.Loopback;
            }
            else
            {
                ipAddress = IPAddress.Parse(address);
            }

            TcpListener listener = null; ;
            bool success = false;
            for (int k = 0; k < numPortsToTry; k++)
            {
                try
                {
                    listener = new TcpListener(ipAddress, port);
                    listener.Start();
                    success = true;
                }
                catch (Exception ex)
                {
                    port++;
                }

                if (success)
                {
                    listener?.Stop();
                    break;
                }
            }

            if (success)
            {
                return new IPEndPoint(ipAddress, port);
            }

            return null;
        }

        public static string FindServerAddress()
        {
            return FindServerAddress(true);
        }

        /// <summary>
        /// Finds IP address belonging to a LAN on which to run a TCP server.
        /// </summary>
        /// <remarks>
        /// Parses ARP table to find a valid LAN address (starting with 169.254 or 11.12.13). Optionally defaults to localhost if no NIC found.
        /// </remarks>
        /// <param name="canUseLocalhost">specifies whether a localhost connection is allowed. Defaults to true.</param>
        /// <returns>IP address of NIC attached to LAN</returns>
        public static string FindServerAddress(bool canUseLocalhost)
        {
            System.Diagnostics.Process p = null;
            string output = string.Empty;
            string address = string.Empty;

            try
            {
                // Executes "arp -a" in Windows command shell
                p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("arp", "-a")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });

                output = p.StandardOutput.ReadToEnd();
                p.Close();

                foreach (var line in output.Split(new char[] { '\n', '\r' }))
                {
                    // Parse out all the MAC / IP Address combinations
                    if (!string.IsNullOrEmpty(line))
                    {
                        var pieces = (from piece in line.Split(new char[] { ' ', '\t' })
                                      where !string.IsNullOrEmpty(piece)
                                      select piece).ToArray();

                        // auto-configured LAN
                        if (line.StartsWith("Interface:") && pieces[1].StartsWith("192.168"))
                        {
                            address = pieces[1];
                            return address;
                        }
                        // direct connection
                        else if (line.StartsWith("Interface:") && pieces[1].StartsWith("169.254"))
                        {
                            address = pieces[1];
                            return address;
                        }
                        // LAN configured using 11.12.13.xxx convention
                        else if (line.StartsWith("Interface:") && pieces[1].StartsWith("11.12.13"))
                        {
                            address = pieces[1];
                            return address;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving 'arp -a' results", ex);
            }
            finally
            {
                if (p != null)
                {
                    p.Close();
                }
            }
#if UNITY_EDITOR
            canUseLocalhost = true;
#endif
            if (string.IsNullOrEmpty(address) && canUseLocalhost)
            {
                address = "localhost";
            }

            return address;
        }
    }
}