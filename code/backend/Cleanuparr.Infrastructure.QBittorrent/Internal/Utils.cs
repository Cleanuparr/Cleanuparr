using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace QBittorrent.Client.Internal
{
    internal static class Utils
    {
        private static readonly char[] PortSeparator = {':'};

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HashIsValid(string hash)
        {
            return hash.Length == 40 &&
                   hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidateHash(string hash)
        {
            if (hash == null)
                throw new ArgumentNullException(nameof(hash));

            if (!HashIsValid(hash))
                throw new ArgumentException("The parameter must be a hexadecimal representation of SHA-1 hash.",
                    nameof(hash));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidateHashes(ref IEnumerable<string> hashes)
        {
            if (hashes == null)
                throw new ArgumentNullException(nameof(hashes));

            var list = new List<string>();
            foreach (var hash in hashes)
            {
                ValidateHash(hash);
                list.Add(hash);
            }

            hashes = list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidatePeers(ref IEnumerable<IPEndPoint> peers)
        {
            if (peers == null)
                throw new ArgumentNullException(nameof(peers));

            var list = new List<IPEndPoint>();
            foreach (var peer in peers)
            {
                ValidatePeer(peer);
                list.Add(peer);
            }

            peers = list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidatePeer(IPEndPoint peer)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));
            if (peer.Port == 0)
                throw new ArgumentException("Port number must be greater than zero.", nameof(peer));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidatePeers(ref IEnumerable<string> peers)
        {
            if (peers == null)
                throw new ArgumentNullException(nameof(peers));

            var list = new List<string>();
            foreach (var peer in peers)
            {
                ValidatePeer(peer);
                list.Add(peer);
            }

            peers = list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidatePeer(string peer)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            _ = ParseIpEndpoint(peer);
        }

        internal static IPEndPoint ParseIpEndpoint(string ipEndpointString)
        {
            if (ipEndpointString == null)
                throw new ArgumentNullException(nameof(ipEndpointString));

            try
            {
                var (ipString, portString) = IsIPv6(out int separatorPos) ? SplitIPv6(separatorPos) : SplitIPv4();
                var ip = IPAddress.Parse(ipString);
                var port = ushort.Parse(portString, CultureInfo.InvariantCulture);
                if (port == 0)
                    throw GetFormatException();
                return new IPEndPoint(ip, port);

                bool IsIPv6(out int portSeparatorPos)
                {
                    if (ipEndpointString.StartsWith("[", StringComparison.OrdinalIgnoreCase))
                    {
                        var pos = ipEndpointString.IndexOf("]", StringComparison.OrdinalIgnoreCase);
                        if (pos < 0)
                            throw GetFormatException();
                        portSeparatorPos = pos + 1;
                        return true;
                    }

                    portSeparatorPos = -1;
                    return false;
                }
            }
            catch (Exception ex)
            {
                throw GetFormatException(ex);
            }

            (string ipString, string portString) SplitIPv6(int portSeparatorPos) =>
            (
                ipEndpointString.Substring(0, portSeparatorPos),
                ipEndpointString.Substring(portSeparatorPos + 1)
            );

            (string ipString, string portString) SplitIPv4()
            {
                var parts = ipEndpointString.Split(PortSeparator);
                if (parts.Length != 2)
                    throw GetFormatException();

                return (parts[0], parts[1]);
            }

            FormatException GetFormatException(Exception inner = null) =>
                new FormatException($"'{ipEndpointString}' is not a valid IP endpoint.", inner);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static int ValidateAndCountTags(ref IEnumerable<string> tags)
        {
            if (tags == null)
                throw new ArgumentNullException(nameof(tags));

            var list = new List<string>();
            foreach (var tag in tags)
            {
                ValidateTag(tag);
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    list.Add(tag);
                }
            }

            tags = list;
            return list.Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ContractAnnotation("null => halt")]
        internal static void ValidateTag(string tag)
        {
            if (tag == null)
                throw new ArgumentNullException(nameof(tag));

            if (tag.Contains(","))
                throw new ArgumentException("The tag cannot contain comma.", nameof(tag));
        }
    }
}
