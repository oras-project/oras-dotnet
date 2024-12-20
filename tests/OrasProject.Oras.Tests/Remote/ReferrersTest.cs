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

using OrasProject.Oras.Exceptions;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry.Remote;
using static OrasProject.Oras.Tests.Remote.Util.Util;
using static OrasProject.Oras.Tests.Remote.Util.RandomDataGenerator;
using Xunit;

namespace OrasProject.Oras.Tests.Remote;

public class ReferrersTest
{
    [Fact]
    public void BuildReferrersTag_ShouldReturnReferrersTagSuccessfully()
    {
        var desc = RandomDescriptor();
        var index = desc.Digest.IndexOf(':');
        var expected = desc.Digest.Substring(0, index) + "-" + desc.Digest.Substring(index + 1);
        Assert.Equal(expected, Referrers.BuildReferrersTag(desc));
    }
    
    [Fact]
    public void BuildReferrersTag_ShouldThrowInvalidDigestException()
    {
        var desc = RandomDescriptor();
        desc.Digest = "sha123321";
        Assert.Throws<InvalidDigestException>(() => Referrers.BuildReferrersTag(desc));
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldAddNewReferrers()
    {
        var oldDescriptor1 = RandomDescriptor();
        var oldDescriptor2 = RandomDescriptor();
        var newDescriptor = RandomDescriptor();
        var oldReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
        };

        var expectedReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
            newDescriptor,
        };
        var referrerChange = new Referrers.ReferrerChange(
            newDescriptor, 
            Referrers.ReferrerOperation.Add
        );

        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(3, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
        Assert.True(updateRequired);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldDeleteReferrers()
    {
        var oldDescriptor1 = RandomDescriptor();
        var oldDescriptor2 = RandomDescriptor();
        var oldDescriptor3 = RandomDescriptor();

        var oldReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
            oldDescriptor3
        };

        var expectedReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor3
        };
        var referrerChange = new Referrers.ReferrerChange(
            oldDescriptor2, 
            Referrers.ReferrerOperation.Delete
        );

        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(2, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
        Assert.True(updateRequired);
    }
    
    
    [Fact]
    public void ApplyReferrerChanges_ShouldDeleteReferrersWithDuplicates()
    {
        var oldDescriptor1 = RandomDescriptor();
        var oldDescriptor2 = RandomDescriptor();
        var oldDescriptor3 = RandomDescriptor();

        var oldReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
            oldDescriptor3,
            oldDescriptor2,
            oldDescriptor2,
            oldDescriptor3,
        };

        var expectedReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2
        };
        var referrerChange = new Referrers.ReferrerChange(
            oldDescriptor3, 
            Referrers.ReferrerOperation.Delete
        );

        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(2, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
        Assert.True(updateRequired);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldNotDeleteReferrersWhenNoUpdateRequired()
    {
        var oldDescriptor1 = RandomDescriptor();
        var oldDescriptor2 = RandomDescriptor();
        var oldDescriptor3 = RandomDescriptor();

        var oldReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
        };

        var expectedReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
        };
        var referrerChange = new Referrers.ReferrerChange(
            oldDescriptor3, 
            Referrers.ReferrerOperation.Delete
        );

        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(2, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
        Assert.False(updateRequired);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldDiscardDuplicateReferrers()
    {
        var oldDescriptor1 = RandomDescriptor();
        var oldDescriptor2 = RandomDescriptor();
        var newDescriptor1 = RandomDescriptor();

        var oldReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
            oldDescriptor2,
            oldDescriptor1,
        };

        var expectedReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
            newDescriptor1,
        };
        var referrerChange = new Referrers.ReferrerChange(
            newDescriptor1,
            Referrers.ReferrerOperation.Add
        );

        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(3, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
        Assert.True(updateRequired);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldNotAddNewDuplicateReferrers()
    {
        var oldDescriptor1 = RandomDescriptor();
        var oldDescriptor2 = RandomDescriptor();
        var oldReferrers = new List<Descriptor>
        {
            oldDescriptor1,
            oldDescriptor2,
        };
        var referrerChange = new Referrers.ReferrerChange(
            oldDescriptor1,
            Referrers.ReferrerOperation.Add
        );
        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(2, updatedReferrers.Count); 
        Assert.False(updateRequired);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldNotKeepOldEmptyReferrers()
    {
        var emptyDesc1 = Descriptor.ZeroDescriptor();
        Descriptor? emptyDesc2 = null;
        var newDescriptor = RandomDescriptor();

        var oldReferrers = new List<Descriptor?>
        {
            emptyDesc1,
            emptyDesc2,
        };        
        var expectedReferrers = new List<Descriptor>
        {
            newDescriptor,
        };
        var referrerChange = new Referrers.ReferrerChange(
            newDescriptor,
            Referrers.ReferrerOperation.Add
        );
        
        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Single(updatedReferrers); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
        Assert.True(updateRequired);
    }
    
    [Fact]
    public void ApplyReferrerChanges_NoUpdateWhenOldAndNewReferrersAreEmpty()
    {
        var oldReferrers = new List<Descriptor>();
        var referrerChange = new Referrers.ReferrerChange(Descriptor.ZeroDescriptor(), Referrers.ReferrerOperation.Add);
        
        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Empty(updatedReferrers); 
        Assert.False(updateRequired);
    }
}
