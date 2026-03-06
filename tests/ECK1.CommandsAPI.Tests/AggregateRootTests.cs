using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Samples;
using FluentAssertions;

namespace ECK1.CommandsAPI.Tests;

public class AggregateRootTests
{
    [Fact]
    public void Sample_FromHistory_AppliesCorrectOverloads_ForAllSampleEvents()
    {
        // Arrange
        var sampleId = Guid.NewGuid();
        var initialAddress = new SampleAddress("Street 1", "City 1", "Country 1");
        var changedAddress = new SampleAddress("Street 2", "City 2", "Country 2");
        var attachmentId = Guid.NewGuid();
        var attachment = new SampleAttachment(attachmentId, "invoice-v1.pdf", "https://cdn/files/invoice-v1.pdf");

        List<ISampleEvent> history =
        [
            new SampleCreatedEvent(sampleId, "Name-1", "Description-1", initialAddress),
            new SampleNameChangedEvent(sampleId, "Name-2"),
            new SampleDescriptionChangedEvent(sampleId, "Description-2"),
            new SampleAddressChangedEvent(sampleId, changedAddress),
            new SampleAttachmentAddedEvent(sampleId, attachment),
            new SampleAttachmentUpdatedEvent(sampleId, attachmentId, "invoice-v2.pdf", "https://cdn/files/invoice-v2.pdf"),
            new SampleAttachmentRemovedEvent(sampleId, attachmentId),
            new SampleRebuiltEvent(sampleId, "Ignored", "Ignored", changedAddress, []),
        ];

        // Act
        var sample = AggregateRoot.FromHistory<Sample>(history, sampleId);

        // Assert
        sample.Id.Should().Be(sampleId);
        sample.Name.Should().Be("Name-2");
        sample.Description.Should().Be("Description-2");
        sample.Address.Should().NotBeNull();
        sample.Address.Street.Should().Be("Street 2");
        sample.Address.City.Should().Be("City 2");
        sample.Address.Country.Should().Be("Country 2");
        sample.Attachments.Should().BeEmpty();
        sample.Version.Should().Be(history.Count);
    }

    [Fact]
    public void Sample2_FromHistory_AppliesCorrectOverloads_ForAllSample2Events()
    {
        // Arrange
        var sample2Id = Guid.NewGuid();

        var createdCustomer = new Sample2Customer
        {
            CustomerId = Guid.NewGuid(),
            Email = "old@example.com",
            Segment = "B2C",
        };

        var createdAddress = new Sample2Address
        {
            Id = Guid.NewGuid(),
            Street = "Old street",
            City = "Old city",
            Country = "US",
        };

        var changedAddress = new Sample2Address
        {
            Id = Guid.NewGuid(),
            Street = "New street",
            City = "New city",
            Country = "DE",
        };

        var initialLineItemId = Guid.NewGuid();
        var addedLineItemId = Guid.NewGuid();

        var initialLineItems = new List<Sample2LineItem>
        {
            new()
            {
                ItemId = initialLineItemId,
                Sku = "SKU-1",
                Quantity = 1,
                UnitPrice = new Sample2Money { Amount = 10m, Currency = "USD" },
            },
        };

        var addedLineItem = new Sample2LineItem
        {
            ItemId = addedLineItemId,
            Sku = "SKU-2",
            Quantity = 2,
            UnitPrice = new Sample2Money { Amount = 25m, Currency = "USD" },
        };

        List<ISample2Event> history =
        [
            new Sample2CreatedEvent(sample2Id, createdCustomer, createdAddress, initialLineItems, ["alpha"], Sample2Status.Draft),
            new Sample2CustomerEmailChangedEvent(sample2Id, "new@example.com"),
            new Sample2ShippingAddressChangedEvent(sample2Id, changedAddress),
            new Sample2LineItemAddedEvent(sample2Id, addedLineItem),
            new Sample2LineItemRemovedEvent(sample2Id, initialLineItemId),
            new Sample2StatusChangedEvent(sample2Id, Sample2Status.Paid, "Payment received"),
            new Sample2TagAddedEvent(sample2Id, "beta"),
            new Sample2TagRemovedEvent(sample2Id, "alpha"),
            new Sample2RebuiltEvent(sample2Id, createdCustomer, changedAddress, [], [], Sample2Status.Cancelled),
        ];

        // Act
        var sample2 = AggregateRoot.FromHistory<Sample2>(history, sample2Id);

        // Assert
        sample2.Id.Should().Be(sample2Id);
        sample2.Customer.Email.Should().Be("new@example.com");
        sample2.ShippingAddress.Street.Should().Be("New street");
        sample2.ShippingAddress.City.Should().Be("New city");
        sample2.ShippingAddress.Country.Should().Be("DE");

        sample2.LineItems.Should().HaveCount(1);
        sample2.LineItems.Single().ItemId.Should().Be(addedLineItemId);

        sample2.Status.Should().Be(Sample2Status.Paid);
        sample2.Tags.Should().ContainSingle(tag => tag == "beta");
        sample2.Tags.Should().NotContain(tag => tag == "alpha");
        sample2.Version.Should().Be(history.Count);
    }
}
