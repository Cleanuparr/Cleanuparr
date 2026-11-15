using System;
using System.Collections.Generic;
using System.Text;

namespace QBittorrent.Client
{
    /// <summary>
    /// Annotates the method/property that is not longer supported in the API.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class DeprecatedAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeprecatedAttribute"/> class.
        /// </summary>
        /// <param name="fromVersion">The version from which the support for annotated method/property was dropped.</param>
        public DeprecatedAttribute(string fromVersion)
        {
            FromVersion = fromVersion;
        }

        /// <summary>
        ///  Gets the version from which the support for annotated method/property was dropped.
        /// </summary>
        public string FromVersion { get; }

        /// <summary>
        /// Describes the reason for deprecation and alternative API.
        /// </summary>
        public string Description { get; set; }
    }
}
