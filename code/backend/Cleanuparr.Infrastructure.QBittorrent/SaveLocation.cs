using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using QBittorrent.Client.Converters;

namespace QBittorrent.Client
{
    /// <summary>
    /// The save location for a monitored folder.
    /// </summary>
    [JsonConverter(typeof(SaveLocationConverter))]
    public readonly struct SaveLocation : IEquatable<SaveLocation>
    {
        /// <summary>
        /// Creates a new instance for the standard folder.
        /// </summary>
        /// <param name="standardFolder"></param>
        public SaveLocation(StandardSaveLocation standardFolder) : this()
        {
            StandardFolder = standardFolder;
        }

        /// <summary>
        /// Creates a new instance for the custom folder.
        /// </summary>
        /// <param name="customFolder"></param>
        public SaveLocation(string customFolder) : this()
        {
            if (string.IsNullOrWhiteSpace(customFolder))
                throw new ArgumentException("The custom folder cannot be null or empty.");

            CustomFolder = customFolder;
        }

        /// <summary>
        /// Gets the custom folder. This property is <see langword="null"/> if <see cref="CustomFolder"/> is not <see langword="null"/>.
        /// </summary>
        public StandardSaveLocation? StandardFolder { get; }

        /// <summary>
        /// Gets the custom folder. This property is <see langword="null"/> if <see cref="StandardFolder"/> is not <see langword="null"/>.
        /// </summary>
        public string CustomFolder { get; }

        /// <inheritdoc />
        public bool Equals(SaveLocation other)
        {
            return StandardFolder == other.StandardFolder && string.Equals(CustomFolder, other.CustomFolder);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SaveLocation location && Equals(location);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (StandardFolder.GetHashCode() * 397) ^ (CustomFolder != null ? CustomFolder.GetHashCode() : 0);
            }
        }

        /// <summary>
        /// Determines whether two specified save locations are equal.
        /// </summary>
        public static bool operator ==(SaveLocation left, SaveLocation right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified save locations are different.
        /// </summary>
        public static bool operator !=(SaveLocation left, SaveLocation right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString()
        {
            if (StandardFolder != null)
                return $"[{StandardFolder}]";

            if (CustomFolder != null)
                return CustomFolder;

            return base.ToString();
        }
    }
}
