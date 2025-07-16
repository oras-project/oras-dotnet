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
        var expected = desc.Digest.Replace(":", "-");
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
        var emptyDesc1 = ZeroDescriptor();
        Descriptor? emptyDesc2 = null;
        var newDescriptor = RandomDescriptor();

        var oldReferrers = new List<Descriptor>
        {
            emptyDesc1,
            emptyDesc2!,
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
        var referrerChange = new Referrers.ReferrerChange(ZeroDescriptor(), Referrers.ReferrerOperation.Add);

        var (updatedReferrers, updateRequired) = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Empty(updatedReferrers);
        Assert.False(updateRequired);
    }

    [Fact]
    public void IsReferrersFilterApplied_AppliedFiltersNull_ReturnsFalse()
    {
        string? appliedFilters = null;
        const string requestedFilter = "artifactType";
        var result = Referrers.IsReferrersFilterApplied(appliedFilters!, requestedFilter);
        Assert.False(result);
    }

    [Fact]
    public void IsReferrersFilterApplied_AppliedFiltersEmpty_ReturnsFalse()
    {
        const string appliedFilters = "";
        const string requestedFilter = "artifactType";
        var result = Referrers.IsReferrersFilterApplied(appliedFilters, requestedFilter);
        Assert.False(result);
    }

    [Fact]
    public void IsReferrersFilterApplied_RequestedFilterNull_ReturnsFalse()
    {
        const string appliedFilters = "artifactType,annotation";
        string? requestedFilter = null;
        var result = Referrers.IsReferrersFilterApplied(appliedFilters, requestedFilter!);
        Assert.False(result);
    }

    [Fact]
    public void IsReferrersFilterApplied_RequestedFilterEmpty_ReturnsFalse()
    {
        const string appliedFilters = "artifactType,annotation";
        const string requestedFilter = "";
        var result = Referrers.IsReferrersFilterApplied(appliedFilters, requestedFilter);
        Assert.False(result);
    }

    [Fact]
    public void IsReferrersFilterApplied_RequestedFilterMatches_ReturnsTrue()
    {
        const string appliedFilters = "artifactType,annotation";
        const string requestedFilter = "artifactType";

        var result = Referrers.IsReferrersFilterApplied(appliedFilters, requestedFilter);
        Assert.True(result);
    }

    [Fact]
    public void IsReferrersFilterApplied_SingleAppliedFiltersRequestedFilterMatches_ReturnsTrue()
    {
        const string appliedFilters = "filter1";
        const string requestedFilter = "filter1";
        var result = Referrers.IsReferrersFilterApplied(appliedFilters, requestedFilter);
        Assert.True(result);
    }

    [Fact]
    public void IsReferrersFilterApplied_RequestedFilterDoesNotMatch_ReturnsFalse()
    {
        const string appliedFilters = "filter1,filter2";
        const string requestedFilter = "filter3";
        var result = Referrers.IsReferrersFilterApplied(appliedFilters, requestedFilter);
        Assert.False(result);
    }

    [Fact]
    public void FilterReferrers_WithNullOrEmptyArtifactType_ShouldReturnAllReferrers()
    {
        var referrers = new List<Descriptor>
        {
            RandomDescriptor(),
            RandomDescriptor(),
            RandomDescriptor(),
        };
        string? artifactType = null;
        var result = Referrers.FilterReferrers(referrers, artifactType);
        Assert.Equal(3, result.Count);
        Assert.Equal(referrers, result);

        artifactType = "";
        result = Referrers.FilterReferrers(referrers, artifactType);
        Assert.Equal(3, result.Count);
        Assert.Equal(referrers, result);
    }

    [Fact]
    public void FilterReferrers_WithValidArtifactType_ShouldReturnMatchingReferrers()
    {
        var referrers = new List<Descriptor>
        {
            RandomDescriptor(artifactType:"doc/example"),
            RandomDescriptor(artifactType:"doc/abc"),
            RandomDescriptor(artifactType:"doc/example"),
            RandomDescriptor(artifactType:"abc/abc"),
        };
        const string artifactType = "doc/example";
        var result = Referrers.FilterReferrers(referrers, artifactType);

        Assert.Equal(2, result.Count);
        Assert.True(result.All(r => r.ArtifactType == artifactType));
    }

    [Fact]
    public void FilterReferrers_WithArtifactTypeThatDoesNotExist_ShouldReturnEmptyList()
    {
        var referrers = new List<Descriptor>
        {
            RandomDescriptor(artifactType:"doc/example"),
            RandomDescriptor(artifactType:"doc/abc"),
            RandomDescriptor(artifactType:"doc/example"),
            RandomDescriptor(artifactType:"abc/abc"),
        };
        const string artifactType = "NonExistentType";
        var result = Referrers.FilterReferrers(referrers, artifactType);
        Assert.Empty(result);
    }
}
