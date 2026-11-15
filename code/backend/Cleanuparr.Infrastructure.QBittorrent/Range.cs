using System;

namespace QBittorrent.Client
{
    /// <summary>
    /// Torrent piece range.
    /// </summary>
    /// <seealso cref="System.IFormattable" />
    public struct Range : IFormattable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Range"/> struct.
        /// </summary>
        /// <param name="startIndex">The start index.</param>
        /// <param name="endIndex">The end index.</param>
        public Range(long startIndex, long endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        /// <summary>
        /// Gets the start index.
        /// </summary>
        /// <value>
        /// The start index.
        /// </value>
        public long StartIndex { get; }

        /// <summary>
        /// Gets the end index.
        /// </summary>
        /// <value>
        /// The end index.
        /// </value>
        public long EndIndex { get; }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"[{StartIndex}; {EndIndex}]";
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="formatProvider">The format provider.</param>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            var formatString = $"[{{0:{format}}}; {{1:{format}}}]";
            return string.Format(formatProvider, formatString, StartIndex, EndIndex);
        }
    }
}
