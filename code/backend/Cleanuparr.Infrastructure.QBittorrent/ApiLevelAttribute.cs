using System;

namespace QBittorrent.Client
{
    /// <summary>
    /// Annotates the minimal API Level that supports the annotated method.
    /// </summary>
    /// <seealso cref="System.Attribute" />
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ApiLevelAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiLevelAttribute"/> class.
        /// </summary>
        /// <param name="apiLevel">The minimal API Level that supports the annotated method.</param>
        public ApiLevelAttribute(ApiLevel apiLevel)
        {
            Level = apiLevel;
        }

        /// <summary>
        /// Gets the minimal API Level that supports the annotated method.
        /// </summary>
        public ApiLevel Level { get; }

        /// <summary>
        /// The maximum API version that supports the annotated method.
        /// </summary>
        public string MaxVersion { get; set; }

        /// <summary>
        /// The minimal API version that supports the annotated method.
        /// </summary>
        public string MinVersion { get; set; }
    }
}
