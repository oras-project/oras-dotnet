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
using System.Linq;
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
        // updatedReferrersSet is a HashSet to store unique referrers
        var updatedReferrersSet = new HashSet<BasicDescriptor>();
        
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
            if (updatedReferrersSet.Contains(basicDesc))
            {
                // Skip any duplicate referrers
                updateRequired = true;
                continue;
            }
            // Update the updatedReferrers list
            // Add referrer index in the updatedReferrersSet
            if (referrerChange.ReferrerOperation == ReferrerOperation.ReferrerDelete && Descriptor.Equals(basicDesc, referrerChange.Referrer.BasicDescriptor))
            {
                updateRequired = true;
                continue;
            }
            updatedReferrers.Add(oldReferrer);
            updatedReferrersSet.Add(basicDesc);
        }
        
        if (!Descriptor.IsEmptyOrNull(referrerChange.Referrer))
        {
            var basicDesc = referrerChange.Referrer.BasicDescriptor;
            if (referrerChange.ReferrerOperation == ReferrerOperation.ReferrerAdd)
            {
                if (!updatedReferrersSet.Contains(basicDesc))
                {
                    // Add the new referrer only when it has not already existed in the updatedReferrersSet
                    updatedReferrers.Add(referrerChange.Referrer);
                    updatedReferrersSet.Add(basicDesc);
                }
            }
        }
        
        // Skip unnecessary update
        if (!updateRequired && updatedReferrersSet.Count == oldReferrers.Count)
        {
            // Check for any new referrers in the updatedReferrersSet that are not present in the oldReferrers list
            foreach (var oldReferrer in oldReferrers)
            {
                var basicDesc = oldReferrer.BasicDescriptor;
                if (!updatedReferrersSet.Contains(basicDesc))
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
        return (updatedReferrers, true);
    }
}
