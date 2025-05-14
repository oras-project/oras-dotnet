// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrasProject.Oras;

/// <summary>
/// Provides extension methods for invoking asynchronous event delegates in a safe and consistent manner.
/// </summary>
internal static class AsyncInvocationExtensions
{

    /// <summary>
    ///  Asynchronously invokes all handlers from an event.
    ///  All handlers are executed in parallel. If <paramref name="eventDelegate"/> is null,
    /// a collection containing a completed task is returned.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
    /// <param name="eventDelegate">The event delegate to invoke.</param>
    /// <param name="args">The arguments to pass to the event handlers.</param>
    /// <returns>An enumerable of tasks representing the asynchronous event handler invocations.</returns>
    internal static IEnumerable<Task> InvokeAsync<TEventArgs>(
       this Func<TEventArgs, Task>? eventDelegate, TEventArgs args)
    {
        if (eventDelegate == null)
        {
            return [Task.CompletedTask];
        }
        return eventDelegate.GetInvocationList()
            .Select(d => (Task?)d.DynamicInvoke(args) ?? Task.CompletedTask);
    }
}
