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

using System.Collections.Generic;
using OrasProject.Oras.Content;
using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Registry.Remote;

public class Referrers
{
    internal enum ReferrerState
    {
        ReferrerUnknown,
        ReferrerSupported,
        ReferrerNotSupported
    }
    
    internal record ReferrerChange(Descriptor Referrer, ReferrerOperation ReferrerOperation);

    internal enum ReferrerOperation
    {
        ReferrerAdd,
        ReferrerDelete,
    }
    
    internal static string BuildReferrersTag(Descriptor descriptor)
    {
        return Digest.GetAlgorithm(descriptor.Digest) + "-" + Digest.GetRef(descriptor.Digest);
    }
    
    /// <summary>
    /// ApplyReferrerChanges applies the specified referrer change (either add or delete) to the existing list of referrers. 
    /// It updates the list based on the operation defined in the provided `referrerChange`. 
    /// If the referrer to be added or deleted already exists in the list, it is handled accordingly.
    /// </summary>
    /// <param name="oldReferrers"></param>
    /// <param name="referrerChange"></param>
    /// <returns>The updated referrers list</returns>
    internal static IList<Descriptor> ApplyReferrerChanges(IList<Descriptor> oldReferrers, ReferrerChange referrerChange)
    {
        if (oldReferrers == null || referrerChange == null)
        {
            throw new NoReferrerUpdateException("referrerChange or oldReferrers is null in this request");
        }
        var updatedReferrers = new List<Descriptor>();
        var referrerToIndex = new Dictionary<BasicDescriptor, int>();
        
        var updateRequired = false;
        foreach (var oldReferrer in oldReferrers)
        {
            if (Descriptor.IsEmptyOrNull(oldReferrer))
            {
                updateRequired = true;
                continue;
            }
            var basicDesc = oldReferrer.BasicDescriptor;
            if (referrerToIndex.ContainsKey(basicDesc))
            {
                updateRequired = true;
                continue;
            }
            updatedReferrers.Add(oldReferrer);
            referrerToIndex[basicDesc] = updatedReferrers.Count - 1;
        }


        if (!Descriptor.IsEmptyOrNull(referrerChange.Referrer))
        {
            var basicDesc = referrerChange.Referrer.BasicDescriptor;
            switch (referrerChange.ReferrerOperation)
            {
                case ReferrerOperation.ReferrerAdd:
                    if (!referrerToIndex.ContainsKey(basicDesc))
                    {
                        updatedReferrers.Add(referrerChange.Referrer);
                        referrerToIndex[basicDesc] = updatedReferrers.Count - 1;
                    }

                    break;

                case ReferrerOperation.ReferrerDelete:
                    if (referrerToIndex.TryGetValue(basicDesc, out var index))
                    {
                        updatedReferrers[index] = Descriptor.EmptyDescriptor();
                        referrerToIndex.Remove(basicDesc);
                    }

                    break;
                default:
                    break;
            }
        }
        
        if (!updateRequired && referrerToIndex.Count == oldReferrers.Count)
        {
            foreach (var oldReferrer in oldReferrers)
            {
                var basicDesc = oldReferrer.BasicDescriptor;
                if (!referrerToIndex.ContainsKey(basicDesc)) updateRequired = true;
            }

            if (!updateRequired) throw new NoReferrerUpdateException("no referrer update in this request");
        }

        RemoveEmptyDescriptors(updatedReferrers, referrerToIndex.Count);
        return updatedReferrers;
    }

    /// <summary>
    /// RemoveEmptyDescriptors removes any empty or null descriptors from the provided list of referrers, ensuring that only non-empty 
    /// descriptors remain in the list. It optimizes the list by shifting valid descriptors forward and trimming 
    /// the remaining elements at the end. The list is truncated to only contain non-empty descriptors up to the specified count.
    /// </summary>
    /// <param name="updatedReferrers"></param>
    /// <param name="numNonEmptyReferrers"></param>
    internal static void RemoveEmptyDescriptors(List<Descriptor> updatedReferrers, int numNonEmptyReferrers)
    {
        var lastEmptyIndex = 0;
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            if (Descriptor.IsEmptyOrNull(updatedReferrers[i])) continue;
            
            if (i > lastEmptyIndex) updatedReferrers[lastEmptyIndex] = updatedReferrers[i];
            ++lastEmptyIndex;
            if (lastEmptyIndex == numNonEmptyReferrers) break;
        }
        updatedReferrers.RemoveRange(lastEmptyIndex, updatedReferrers.Count - lastEmptyIndex);
    }
}
