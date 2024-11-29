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
    internal enum ReferrersState
    {
        ReferrersUnknown = 0,
        ReferrersSupported = 1,
        ReferrersNotSupported = 2
    }
    
    internal record ReferrerChange(Descriptor Referrer, ReferrerOperation ReferrerOperation);

    internal enum ReferrerOperation
    {
        ReferrerAdd,
        ReferrerDelete,
    }
    
    internal static string BuildReferrersTag(Descriptor descriptor)
    {
        var validatedDigest = Digest.Validate(descriptor.Digest);
        return validatedDigest.Substring(0, validatedDigest.IndexOf(':')) + "-" + validatedDigest.Substring(validatedDigest.IndexOf(':') + 1);
    }
    
    /// <summary>
    /// ApplyReferrerChanges applies the specified referrer change (either add or delete) to the existing list of referrers. 
    /// It updates the list based on the operation defined in the provided `referrerChange`. 
    /// If the referrer to be added or deleted already exists in the list, it is handled accordingly.
    /// </summary>
    /// <param name="oldReferrers"></param>
    /// <param name="referrerChange"></param>
    /// <returns>The updated referrers list, updateRequired</returns>
    internal static (IList<Descriptor>, bool) ApplyReferrerChanges(IList<Descriptor> oldReferrers, ReferrerChange referrerChange)
    {
        // updatedReferrers is a list to store the updated referrers
        var updatedReferrers = new List<Descriptor>();
        // referrerIndexMap is a Dictionary to store referrer as the key
        // and index(int) in the updatedReferrers list as the value
        var referrerIndexMap = new Dictionary<BasicDescriptor, int>();
        
        var updateRequired = false;
        foreach (var oldReferrer in oldReferrers)
        {
            if (Descriptor.IsEmptyOrNull(oldReferrer))
            {
                // Skip any empty or null referrers
                updateRequired = true;
                continue;
            }
            var basicDesc = oldReferrer.BasicDescriptor;
            if (referrerIndexMap.ContainsKey(basicDesc))
            {
                // Skip any duplicate referrers
                updateRequired = true;
                continue;
            }
            // Update the updatedReferrers list
            // Add referrer index in the referrerIndexMap
            
            // delete
            // ......
            if (referrerChange.ReferrerOperation == ReferrerOperation.ReferrerDelete)
            {
                var toBeDeletedBasicDesc = referrerChange.Referrer.BasicDescriptor;
                if (basicDesc == toBeDeletedBasicDesc)
                {
                    updateRequired = true;
                    continue;
                }
            }
            updatedReferrers.Add(oldReferrer);
            referrerIndexMap[basicDesc] = updatedReferrers.Count - 1;
        }
        
        // old => 1, 1
        // new => nil
        if (!Descriptor.IsEmptyOrNull(referrerChange.Referrer))
        {
            var basicDesc = referrerChange.Referrer.BasicDescriptor;
            switch (referrerChange.ReferrerOperation)
            {
                case ReferrerOperation.ReferrerAdd:
                    if (!referrerIndexMap.ContainsKey(basicDesc))
                    {
                        // Add the new referrer only when it has not already existed in the referrerIndexMap
                        updatedReferrers.Add(referrerChange.Referrer);
                        referrerIndexMap[basicDesc] = updatedReferrers.Count - 1;
                    }
                    
                    break;
                case ReferrerOperation.ReferrerDelete:
                    if (referrerIndexMap.TryGetValue(basicDesc, out var index))
                    {
                        // Delete the referrer only when it existed in the referrerIndexMap
                        // updatedReferrers.Remove(basicDesc);
                        
                        updatedReferrers[index] = Descriptor.EmptyDescriptor();
                        referrerIndexMap.Remove(basicDesc);
                    }
                    break;
                default:
                    break;
            }
        }
        
        // Skip unnecessary update
        if (!updateRequired && referrerIndexMap.Count == oldReferrers.Count)
        {
            // Check for any new referrers in the referrerIndexMap that are not present in the oldReferrers list
            foreach (var oldReferrer in oldReferrers)
            {
                var basicDesc = oldReferrer.BasicDescriptor;
                if (!referrerIndexMap.ContainsKey(basicDesc))
                {
                    updateRequired = true;
                    break;
                }
            }

            if (!updateRequired)
            {
                return (updatedReferrers, false);
            }
        }

        RemoveEmptyDescriptors(updatedReferrers, referrerIndexMap.Count);
        return (updatedReferrers, true);
    }

    /// <summary>
    /// RemoveEmptyDescriptors removes any empty or null descriptors from the provided list of descriptors,
    /// ensuring that only non-empty descriptors remain in the list.
    /// It optimizes the list by shifting valid descriptors forward and trimming the remaining elements at the end.
    /// The list is truncated to only contain non-empty descriptors up to the specified count.
    /// </summary>
    /// <param name="descriptors"></param>
    /// <param name="numNonEmptyDescriptors"></param>
    internal static void RemoveEmptyDescriptors(List<Descriptor> descriptors, int numNonEmptyDescriptors)
    {
        var lastEmptyIndex = 0;
        for (var i = 0; i < descriptors.Count; ++i)
        {
            if (Descriptor.IsEmptyOrNull(descriptors[i])) continue;
            if (i > lastEmptyIndex)
            {
                // Move the descriptor at index i to lastEmptyIndex
                descriptors[lastEmptyIndex] = descriptors[i];
            }
            ++lastEmptyIndex;
            if (lastEmptyIndex == numNonEmptyDescriptors)
            {
                // Break the loop when lastEmptyIndex reaches the number of Non-Empty descriptors
                break;
            }
        }
        descriptors.RemoveRange(lastEmptyIndex, descriptors.Count - lastEmptyIndex);
    }
}
