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
using System.Threading.Tasks;

namespace OrasProject.Oras;

internal static class AsyncInvocationExtensions
{
    /// <summary>
    /// Sequentially invokes an event that returns a <see cref="Task"/>.
    /// Each event listener is executed in sequence, and it's returning
    /// task awaited before executing the next one.
    /// </summary>
    /// <param name="eventDelegate"></param>
    /// <param name="args"></param>
    /// <typeparam name="TEventArgs"></typeparam>
    internal static async Task InvokeAsync<TEventArgs>(
        this Func<TEventArgs, Task>? eventDelegate, TEventArgs args)
    {
        if (eventDelegate == null) return;

        foreach (var handler in eventDelegate.GetInvocationList())
        {
            await ((Task?)handler.DynamicInvoke(args) ?? Task.CompletedTask);
        }
    }
}
