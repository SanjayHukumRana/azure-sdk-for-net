﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Azure.Storage.Queues.Models
{
    /// <summary>
    /// Specifies options for listing queues with the
    /// <see cref="QueueServiceClient.GetQueuesAsync"/> operation.
    /// </summary>
    public struct GetQueuesOptions : IEquatable<GetQueuesOptions>
    {
        /// <summary>
        /// Gets or sets a string that filters the results to return only
        /// queues whose name begins with the specified prefix.
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// Gets or sets a flag specifing that the queue's metadata should
        /// be included.
        /// </summary>
        public bool IncludeMetadata { get; set; }

        /// <summary>
        /// Convert the details into a <see cref="ListQueuesIncludeType"/> value.
        /// </summary>
        /// <returns>A <see cref="ListQueuesIncludeType"/> value.</returns>
        internal IEnumerable<ListQueuesIncludeType> AsIncludeTypes() =>
            IncludeMetadata ?
                new ListQueuesIncludeType[] { ListQueuesIncludeType.Metadata } :
                Array.Empty<ListQueuesIncludeType>();

        /// <summary>
        /// Check if two GetQueuesOptions instances are equal.
        /// </summary>
        /// <param name="obj">The instance to compare to.</param>
        /// <returns>True if they're equal, false otherwise.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object obj) =>
            obj is GetQueuesOptions other && Equals(other);

        /// <summary>
        /// Get a hash code for the <see cref="GetQueuesOptions"/>.
        /// </summary>
        /// <returns>Hash code for the <see cref="GetQueuesOptions"/>.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode() =>
            IncludeMetadata.GetHashCode() ^
            (Prefix?.GetHashCode() ?? 0);

        /// <summary>
        /// Check if two <see cref="GetQueuesOptions"/> instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>True if they're equal, false otherwise.</returns>
        public static bool operator ==(GetQueuesOptions left, GetQueuesOptions right) =>
            left.Equals(right);

        /// <summary>
        /// Check if two <see cref="GetQueuesOptions"/> instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>True if they're not equal, false otherwise.</returns>
        public static bool operator !=(GetQueuesOptions left, GetQueuesOptions right) =>
            !(left == right);

        /// <summary>
        /// Check if two <see cref="GetQueuesOptions"/> instances are equal.
        /// </summary>
        /// <param name="other">The instance to compare to.</param>
        /// <returns>True if they're equal, false otherwise.</returns>
        public bool Equals(GetQueuesOptions other) =>
            IncludeMetadata == other.IncludeMetadata &&
            Prefix == other.Prefix;
    }
}
