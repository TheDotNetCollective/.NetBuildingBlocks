﻿using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace dotNetBlocks.System.IO.Tests.StreamBuffer
{
    static internal class StreamBufferTestHelper
    {

        /// <summary>
        /// Copies the specified number of bytes from one stream to another.
        /// </summary>
        /// <param name="sourceStream">The source stream.</param>
        /// <param name="targetStream">The target stream.</param>
        /// <param name="byteCount">The byte count.</param>
        /// <remarks>
        /// These are low performance purpose built helpers.
        /// </remarks>
        public static void CopyBytes(this Stream sourceStream, Stream targetStream, int byteCount)
        {
            var bufferBlock = new Span<byte>(new byte[byteCount]);
            sourceStream.ReadExactly(bufferBlock);
            targetStream.Write(bufferBlock);
        }

        /// <summary>
        /// Copies the block asynchronous.
        /// </summary>
        /// <param name="sourceStream">The source stream.</param>
        /// <param name="targetStream">The target stream.</param>
        /// <param name="byteCount">The byte count.</param>
        /// <remarks>
        /// These are low performance purpose built helpers.
        /// </remarks>
        public static async Task CopyBytesAsync(this Stream sourceStream, Stream targetStream, int byteCount, CancellationToken cancellationToken = default)
        {
            // Allocate a new memory chunk.
            var bufferBlock = new Memory<byte>(new byte[byteCount]);
            await sourceStream.ReadExactlyAsync(bufferBlock, cancellationToken);
            await targetStream.WriteAsync(bufferBlock, cancellationToken);
        }

        /// <summary>
        /// Reads the data and calculates the CRC.
        /// </summary>
        /// <param name="stream">The stream to readhalf.</param>
        /// <param name="totalBytes">The total bufferBlock to readhalf.</param>
        /// <param name="crc">The CRC.</param>
        public static async Task ReadAndCalculateCRCAsync(this Stream stream, Crc32 crc, int? readSize = default, CancellationToken cancellationToken = default)
        {
            if (readSize == default) // Read the whole stream.
            {
                await crc.AppendAsync(stream, cancellationToken);
                return;
            }
            using (var buffer = new MemoryStream())
            {
                await stream.CopyBytesAsync(buffer, readSize!.Value, cancellationToken);
                buffer.Position = 0;
                await crc.AppendAsync(buffer, cancellationToken);
            }
        }

        /// <summary>
        /// Reads the data and calculates the CRC.
        /// </summary>
        /// <param name="stream">The stream to readhalf.</param>
        /// <param name="totalBytes">The total bufferBlock to readhalf.</param>
        /// <param name="crc">The CRC.</param>
        public static void ReadAndCalculateCRC(this Stream stream, Crc32 crc, int? readSize = default)
        {
            if (readSize == default) // readhalf the whole stream. 
            {
                crc.Append(stream);
                return;
            }
            using (var buffer = new MemoryStream(readSize!.Value))
            {
                stream.CopyBytes(buffer, readSize!.Value);
                buffer.Position = 0;
                crc.Append(buffer);
            }
        }


        #region Execute with timeout helpers.

        /// <summary>
        /// Executes the action with a timeout
        /// </summary>
        /// 
        /// <param name="action">Action tp execute</param>
        /// <param name="timeout">timespan to timeout.</param>
        /// <returns>true if timed out otherwise false</returns>
        public static async Task<bool> ExecuteWithTimeoutAsync(this Action action, int timeoutmilliseconds, CancellationToken cancellation = default)
            => await ExecuteWithTimeoutAsync(action,  timeoutmilliseconds, cancellation);

        /// <summary>
        /// Executes the action with a timeout
        /// </summary>
        /// 
        /// <param name="action">Action tp execute</param>
        /// <param name="timeout">timespan to timeout.</param>
        /// <returns>true if timed out otherwise false</returns>
        public static async Task<bool> ExecuteWithTimeoutAsync(this Task actionTask, int timeoutmilliseconds, CancellationToken cancellation = default)
            => await ExecuteWithTimeoutAsync(default, default, actionTask,  timeoutmilliseconds, cancellation);


        /// <summary>
        /// Executes the with timeout asynchronous.
        /// </summary>
        /// <param name="asyncAction">The asynchronous action.</param>
        /// <param name="timeoutmilliseconds">The timeoutmilliseconds.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns></returns>
        public static async Task<bool> ExecuteWithTimeoutAsync(this Func<Task> asyncAction, int timeoutmilliseconds, CancellationToken cancellation = default)
                        => await ExecuteWithTimeoutAsync(default, asyncAction, default, timeoutmilliseconds:timeoutmilliseconds, cancellation);


        /// <summary>
        /// Executes the action with a timeout
        /// </summary>
        /// <param name="action">Action tp execute</param>
        /// <param name="asyncAction">The asynchronous action.</param>
        /// <param name="timeoutmilliseconds">The timeoutmilliseconds.</param>
        /// <param name="cancellation">The cancellation.</param>
        /// <returns>
        /// true if timed out otherwise false
        /// </returns>
        public static async Task<bool> ExecuteWithTimeoutAsync(Action? action = default, Func<Task>? asyncAction = default, Task? actionTask = default, int timeoutmilliseconds = 250, CancellationToken cancellation = default)
        {
            // Build a list of all the action tasks.
            List<Task> actionTasks = new List<Task>();
            if (actionTask != default)
                actionTasks.Add(actionTask);
            if (action != default) 
                actionTasks.Add( Task.Run(action, cancellation));
            if (asyncAction != default)
                actionTasks.Add(Task.Run(asyncAction, cancellation));

            // Create a timeout task.
            var timeoutTask = Task.Delay(timeoutmilliseconds, cancellation);

            // All actions must complete.
            var allActionsTasks = Task.WhenAll(actionTasks);

            await Task.WhenAny(timeoutTask, allActionsTasks);


            // Return true if the tasks are completed sucessfully
            return allActionsTasks.IsCompletedSuccessfully&&!timeoutTask.IsCompletedSuccessfully || cancellation.IsCancellationRequested;
        }

        #endregion

    }
}
