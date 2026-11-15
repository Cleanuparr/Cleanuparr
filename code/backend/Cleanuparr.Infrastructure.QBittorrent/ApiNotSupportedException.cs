using System;

namespace QBittorrent.Client
{
    /// <summary>
    /// This exception is thrown on attempts to use functions not supported in current API version.
    /// </summary>
    /// <seealso cref="ApiLevel"/>
    /// <seealso cref="ApiLevelAttribute"/>
    [Serializable]
    public class ApiNotSupportedException : NotSupportedException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ApiNotSupportedException"/>.
        /// </summary>
        /// <param name="requiredApiLevel">The minimal required API level.</param>
        public ApiNotSupportedException(ApiLevel requiredApiLevel) 
            : this(requiredApiLevel, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ApiNotSupportedException"/>.
        /// </summary>
        /// <param name="requiredApiLevel">The minimal required API level.</param>
        /// <param name="requiredApiVersion">The minimal required API version.</param>
        public ApiNotSupportedException(ApiLevel requiredApiLevel, Version requiredApiVersion)
            : this("The API version being used does not support this function.", requiredApiLevel, requiredApiVersion)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ApiNotSupportedException"/>.
        /// </summary>
        /// <param name="requiredApiLevel">The minimal required API level.</param>
        /// <param name="requiredApiVersion">The minimal required API version.</param>
        /// <param name="maxApiVersion">The maximal supported API version.</param>
        public ApiNotSupportedException(ApiLevel requiredApiLevel, Version requiredApiVersion, Version maxApiVersion)
            : this(requiredApiLevel, requiredApiVersion)
        {
            MaxApiVersion = maxApiVersion;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ApiNotSupportedException"/>.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="requiredApiLevel">The minimal required API level.</param>
        public ApiNotSupportedException(string message, ApiLevel requiredApiLevel)
            : this(message, requiredApiLevel, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ApiNotSupportedException"/>.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="requiredApiLevel">The minimal required API level.</param>
        /// <param name="requiredApiVersion">The minimal required API version.</param>
        public ApiNotSupportedException(string message, ApiLevel requiredApiLevel, Version requiredApiVersion)
            : base(message)
        {
            RequiredApiLevel = requiredApiLevel;
            RequiredApiVersion = requiredApiVersion;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ApiNotSupportedException"/>.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="requiredApiLevel">The minimal required API level.</param>
        /// <param name="requiredApiVersion">The minimal required API version.</param>
        /// <param name="maxApiVersion">The maximum required API version.</param>
        public ApiNotSupportedException(string message, ApiLevel requiredApiLevel, Version requiredApiVersion, Version maxApiVersion)
            : this(message, requiredApiLevel, requiredApiVersion)
        {
            MaxApiVersion = maxApiVersion;
        }

        /// <summary>
        /// The minimal required API level.
        /// </summary>
        public ApiLevel RequiredApiLevel { get; }

        /// <summary>
        /// The minimal required API version.
        /// </summary>
        public Version RequiredApiVersion { get; }

        /// <summary>
        /// The maximal supported API version.
        /// </summary>
        public Version MaxApiVersion { get; }
    }
}
