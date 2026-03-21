using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommandsAPI.Domain.Shared;
using FluentAssertions;

namespace ECK1.CommandsAPI.Tests;

public class AggregateRootTests
{
    [Fact]
    public void Sample_FromHistory_AppliesCorrectOverloads_ForAllSampleEvents()
    {
        // Arrange
        var sampleId = Guid.NewGuid();
        var initialAddress = new Address("Street 1", "City 1", "Country 1");
        var changedAddress = new Address("Street 2", "City 2", "Country 2");
        var attachment = new SampleAttachment("invoice-v1.pdf", "https://cdn/files/invoice-v1.pdf");
        var version = 1;
        List<ISampleEvent> history =
        [
            new SampleCreatedEvent(sampleId, "Name-1", "Description-1", initialAddress) { Version = version++ },
            new SampleNameChangedEvent(sampleId, "Name-2") { Version = version++ },
            new SampleDescriptionChangedEvent(sampleId, "Description-2") { Version = version++ },
            new SampleAddressChangedEvent(sampleId, changedAddress) { Version = version++ },
            new SampleAttachmentAddedEvent(sampleId, attachment) { Version = version++ },
            new SampleAttachmentUpdatedEvent(sampleId, attachment.Id, "invoice-v2.pdf", "https://cdn/files/invoice-v2.pdf") { Version = version++ },
            new SampleAttachmentRemovedEvent(sampleId, attachment.Id) { Version = version++ },
            new SampleRebuiltEvent(sampleId, "Ignored", "Ignored", changedAddress, []) {  Version = version++ },
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

        var createdCustomer = new Sample2Customer("old@example.com", "B2C");

        var createdAddress = new Address("Old street", "Old city", "US");


        var changedAddress = new Address("New street", "New city", "DE");


        var initialLineItems = new List<Sample2LineItem>
        {
            new("SKU-1", 1, new Sample2Money (10m, "USD"))
        };

        var addedLineItem = new Sample2LineItem("SKU-2", 2, new Sample2Money(25m, "USD"));

        var version = 1;
        List<ISample2Event> history =
        [
            new Sample2CreatedEvent(sample2Id, createdCustomer, createdAddress, initialLineItems, ["alpha"], Sample2Status.Draft),
            new Sample2CustomerEmailChangedEvent(sample2Id, "new@example.com") { Version = version++ },
            new Sample2ShippingAddressChangedEvent(sample2Id, changedAddress) { Version = version++ },
            new Sample2LineItemAddedEvent(sample2Id, addedLineItem) { Version = version++ },
            new Sample2LineItemRemovedEvent(sample2Id, initialLineItems[0].ItemId) { Version = version++ },
            new Sample2StatusChangedEvent(sample2Id, Sample2Status.Paid, "Payment received") { Version = version++ },
            new Sample2TagAddedEvent(sample2Id, "beta" ) { Version = version++ },
            new Sample2TagRemovedEvent(sample2Id, "alpha") { Version = version++ },
            new Sample2RebuiltEvent(sample2Id, createdCustomer, changedAddress, [], [], Sample2Status.Cancelled) { Version = version++ },
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
        sample2.LineItems.Single().ItemId.Should().Be(addedLineItem.ItemId);

        sample2.Status.Should().Be(Sample2Status.Paid);
        sample2.Tags.Should().ContainSingle(tag => tag == "beta");
        sample2.Tags.Should().NotContain(tag => tag == "alpha");
        sample2.Version.Should().Be(history.Count);
    }
}
