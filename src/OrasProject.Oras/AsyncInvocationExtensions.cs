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
using System.Linq;
using System.Threading.Tasks;

namespace OrasProject.Oras;

internal static class AsyncInvocationExtensions
{
    /// <summary>
    /// Asynchronously invokes all handlers from an event that returns a <see cref="Task"/>.
    /// <remarks>All handlers are executed in parallel</remarks>
    /// </summary>
    /// <param name="eventDelegate"></param>
    /// <param name="args"></param>
    /// <typeparam name="TEventArgs"></typeparam>
    internal static Task InvokeAsync<TEventArgs>(
        this Func<TEventArgs, Task>? eventDelegate, TEventArgs args)
    {
        if (eventDelegate == null) return Task.CompletedTask;

        var tasks = eventDelegate.GetInvocationList()
            .Select(d => (Task?)d.DynamicInvoke(args) ?? Task.CompletedTask);

        return Task.WhenAll(tasks);
    }
}
