﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TrackOne
{
    /// <summary>
    /// A handler interface for the receive operation. Use any implementation of this interface to specify
    /// user action when using <see cref="PartitionReceiver.SetReceiveHandler(IPartitionReceiveHandler, bool)"/>.
    /// </summary>
    internal interface IPartitionReceiveHandler
    {
        /// <summary>
        /// Gets or sets the maximum batch size.
        /// </summary>
        int MaxBatchSize { get; set; }

        /// <summary>
        /// Users should implement this method to specify the action to be performed on the received events.
        /// </summary>
        /// <seealso cref="PartitionReceiver.ReceiveAsync(int)"/>
        /// <param name="events">The list of fetched events from the corresponding PartitionReceiver.</param>
        Task ProcessEventsAsync(IEnumerable<EventData> events);

        /// <summary>
        /// Implement in order to handle exceptions that are thrown during receipt of events.
        /// </summary>
        /// <param name="error">The <see cref="Exception"/> to be processed</param>
        /// <returns>An asynchronour operation</returns>
        Task ProcessErrorAsync(Exception error);
    }
}
