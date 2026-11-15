using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace QBittorrent.Client
{
    /// <summary>
    /// Represents the API version.
    /// </summary>
    /// <seealso cref="System.IEquatable{ApiVersion}" />
    /// <seealso cref="System.IComparable{ApiVersion}" />
    public readonly struct ApiVersion : IEquatable<ApiVersion>, IComparable<ApiVersion>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiVersion"/> struct.
        /// </summary>
        /// <param name="major">The major part of the version.</param>
        /// <param name="minor">The minor part of the version.</param>
        /// <param name="release">The release part of the version.</param>
        public ApiVersion(byte major, byte minor = 0, byte release = 0) : this()
        {
            Major = major;
            Minor = minor;
            Release = release;
            _reserved = 0;
        }

        /// <summary>
        /// The major part of the version.
        /// </summary>
        public readonly byte Major;

        /// <summary>
        /// The minor part of the version.
        /// </summary>
        public readonly byte Minor;

        /// <summary>
        /// The release part of the version.
        /// </summary>
        public readonly byte Release;

        // This field is not currently used but is reserved for the future.
        private readonly byte _reserved;

        /// <summary>
        /// Parses the specified version string.
        /// </summary>
        /// <param name="versionString">The version string in format major[.minor[.release]]</param>
        /// <returns></returns>
        public static ApiVersion Parse(string versionString)
        {
            if (versionString == null)
                throw new ArgumentNullException(nameof(versionString));

            var match = Regex.Match(versionString,
                @"^(?<major>\d{1,3})(\.(?<minor>\d{1,3})(\.(?<release>\d{1,3}))?)?$",
                RegexOptions.ExplicitCapture);
            if (!match.Success)
                throw new FormatException("The version string must be in format major[.minor[.release]]");

            byte major = byte.Parse(match.Groups["major"].Value);
            byte minor = !string.IsNullOrEmpty(match.Groups["minor"].Value)
                ? byte.Parse(match.Groups["minor"].Value)
                : default;
            byte release = !string.IsNullOrEmpty(match.Groups["release"].Value)
                ? byte.Parse(match.Groups["release"].Value)
                : default;
            return new ApiVersion(major, minor, release);
        }

        /// <inheritdoc />
        public override string ToString() => $"{Major}.{Minor}.{Release}";

        /// <inheritdoc />
        public bool Equals(ApiVersion other)
        {
            return Major == other.Major && Minor == other.Minor && Release == other.Release;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ApiVersion version && Equals(version);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (Major << 24) | (Minor << 16) | (Release << 8);
        }

        /// <inheritdoc />
        public int CompareTo(ApiVersion other)
        {
            var comparer = Comparer<byte>.Default;
            var majorComparison = comparer.Compare(Major, other.Major);
            if (majorComparison != 0)
                return majorComparison;

            var minorComparison = comparer.Compare(Minor, other.Minor);
            if (minorComparison != 0)
                return minorComparison;

            return comparer.Compare(Release, other.Release);
        }

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(ApiVersion left, ApiVersion right) => left.Equals(right);

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(ApiVersion left, ApiVersion right) => !left.Equals(right);

        /// <summary>
        /// Implements the operator &lt;.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator <(ApiVersion left, ApiVersion right) => left.CompareTo(right) < 0;

        /// <summary>
        /// Implements the operator &gt;.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator >(ApiVersion left, ApiVersion right) => left.CompareTo(right) > 0;

        /// <summary>
        /// Implements the operator &lt;=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator <=(ApiVersion left, ApiVersion right) => left.CompareTo(right) <= 0;

        /// <summary>
        /// Implements the operator &gt;=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator >=(ApiVersion left, ApiVersion right) => left.CompareTo(right) >= 0;

        /// <summary>
        /// Performs an implicit conversion from <see cref="ApiVersion"/> to <see cref="Version"/>.
        /// </summary>
        /// <param name="apiVersion">The API version.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator Version(ApiVersion apiVersion)
        {
            return new Version(apiVersion.Major, apiVersion.Minor, apiVersion.Release, apiVersion._reserved);
        }
    }
}
