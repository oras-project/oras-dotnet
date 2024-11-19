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
            Referrers.ReferrerOperation.ReferrerAdd
        );

        var updatedReferrers = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(3, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
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
            Referrers.ReferrerOperation.ReferrerAdd
        );

        var updatedReferrers = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);
        Assert.Equal(3, updatedReferrers.Count); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
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
            Referrers.ReferrerOperation.ReferrerAdd
        );


        var exception = Assert.Throws<NoReferrerUpdateException>(() => Referrers.ApplyReferrerChanges(oldReferrers, referrerChange));
        Assert.Equal("no referrer update in this request", exception.Message);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ShouldNotKeepOldEmptyReferrers()
    {
        var emptyDesc1 = Descriptor.EmptyDescriptor();
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
            Referrers.ReferrerOperation.ReferrerAdd
        );
        
        var updatedReferrers = Referrers.ApplyReferrerChanges(oldReferrers, referrerChange);

        Assert.Single(updatedReferrers); 
        for (var i = 0; i < updatedReferrers.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(updatedReferrers[i], expectedReferrers[i]));
        }
    }
    
    [Fact]
    public void ApplyReferrerChanges_ThrowsWhenOldAndNewReferrersAreNull()
    {
        IList<Descriptor> oldReferrers = null;
        Referrers.ReferrerChange referrerChange = null;
        
        var exception = Assert.Throws<NoReferrerUpdateException>(() => Referrers.ApplyReferrerChanges(oldReferrers, referrerChange));
        Assert.Equal("referrerChange or oldReferrers is null in this request", exception.Message);
    }
    
    [Fact]
    public void ApplyReferrerChanges_ThrowsWhenOldAndNewReferrersAreEmpty()
    {
        var oldReferrers = new List<Descriptor>();
        var referrerChange = new Referrers.ReferrerChange(Descriptor.EmptyDescriptor(), Referrers.ReferrerOperation.ReferrerAdd);
        
        var exception = Assert.Throws<NoReferrerUpdateException>(() => Referrers.ApplyReferrerChanges(oldReferrers, referrerChange));
        Assert.Equal("no referrer update in this request", exception.Message);
    }

    [Fact]
    public void RemoveEmptyDescriptors_ShouldRemoveEmptyDescriptors()
    {   
        var randomDescriptor1 = RandomDescriptor();
        var randomDescriptor2 = RandomDescriptor();
        var randomDescriptor3 = RandomDescriptor();
        var randomDescriptor4 = RandomDescriptor();
        var descriptors = new List<Descriptor>
        {
            Descriptor.EmptyDescriptor(),
            randomDescriptor1,
            Descriptor.EmptyDescriptor(),
            randomDescriptor2,
            Descriptor.EmptyDescriptor(),
            Descriptor.EmptyDescriptor(),
            randomDescriptor3,
            randomDescriptor4,
        };
        
        var expectedDescriptors = new List<Descriptor>
        {
            randomDescriptor1,
            randomDescriptor2,
            randomDescriptor3,
            randomDescriptor4
        };
        Referrers.RemoveEmptyDescriptors(descriptors, 4);
        
        Assert.Equal(4, descriptors.Count);
        Assert.DoesNotContain(Descriptor.EmptyDescriptor(), descriptors); 
        for (var i = 0; i < descriptors.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(descriptors[i], expectedDescriptors[i]));
        }
    }
    
    [Fact]
    public void RemoveEmptyDescriptors_ShouldReturnAllNonEmptyDescriptors()
    {   
        var randomDescriptor1 = RandomDescriptor();
        var randomDescriptor2 = RandomDescriptor();
        var randomDescriptor3 = RandomDescriptor();
        var randomDescriptor4 = RandomDescriptor();
        var descriptors = new List<Descriptor>
        {
            randomDescriptor1,
            randomDescriptor2,
            randomDescriptor3,
            randomDescriptor4,
        };
        
        var expectedDescriptors = new List<Descriptor>
        {
            randomDescriptor1,
            randomDescriptor2,
            randomDescriptor3,
            randomDescriptor4
        };
        Referrers.RemoveEmptyDescriptors(descriptors, 4);
        Assert.Equal(4, descriptors.Count);
        for (var i = 0; i < descriptors.Count; ++i)
        {
            Assert.True(AreDescriptorsEqual(descriptors[i], expectedDescriptors[i]));
        }
    }
    
    [Fact]
    public void RemoveEmptyDescriptors_ShouldRemoveAllEmptyDescriptors()
    {   
        var descriptors = new List<Descriptor>
        {
            Descriptor.EmptyDescriptor(),
            Descriptor.EmptyDescriptor(),
            Descriptor.EmptyDescriptor(),
            Descriptor.EmptyDescriptor(),
        };

        Referrers.RemoveEmptyDescriptors(descriptors, 0);
        Assert.Empty(descriptors);
    }
}
