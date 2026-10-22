using System;
using VirtoCommerce.Punchout.Core.Cxml;
using VirtoCommerce.Punchout.Core.Cxml.Models;
using VirtoCommerce.Punchout.Data.Cxml.Services;
using Xunit;

namespace VirtoCommerce.Punchout.Tests;

[Trait("Category", "Unit")]
public class PunchoutOrderMapperTests
{
    private const string OrderRequestXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE cXML SYSTEM "http://xml.cxml.org/schemas/cXML/1.2.014/cXML.dtd">
        <cXML xml:lang="en-US" payloadID="1491833992.758494@xyz.com" timestamp="2017-04-10T09:20:23-05:00">
          <Header>
            <From><Credential domain="NetworkID"><Identity>Buyer</Identity></Credential></From>
            <To><Credential domain="NetworkID"><Identity>Virto</Identity></Credential></To>
            <Sender>
              <Credential domain="NetworkID">
                <Identity>Test101</Identity>
                <SharedSecret>P@ssW0rd</SharedSecret>
              </Credential>
              <UserAgent> Procurement 1.0</UserAgent>
            </Sender>
          </Header>
          <Request>
            <OrderRequest>
              <OrderRequestHeader orderID="758494" orderDate="2026-10-10T09:20:30-05:00" type="new">
                <Total><Money currency="USD">5199.99</Money></Total>
                <ShipTo>
                  <Address isoCountryCode="US" addressID="66362">
                    <Name xml:lang="en">Customer</Name>
                    <PostalAddress name="default">
                      <DeliverTo>John Smith</DeliverTo>
                      <Street>12345 West Street</Street>
                      <City>Clearwater</City>
                      <State>FL</State>
                      <PostalCode>12345</PostalCode>
                      <Country isoCountryCode="US">United States</Country>
                    </PostalAddress>
                    <Email name="default">jsmith@customer.com</Email>
                  </Address>
                </ShipTo>
                <BillTo>
                  <Address isoCountryCode="US" addressID="6">
                    <Name xml:lang="en">Customer</Name>
                    <PostalAddress name="default">
                      <DeliverTo>Smith John</DeliverTo>
                      <Street>54321 East Street</Street>
                      <City>Milwaukee</City>
                      <State>WI</State>
                      <PostalCode>54321</PostalCode>
                      <Country isoCountryCode="US">United States</Country>
                    </PostalAddress>
                    <Email name="default">jsmith@customer.com</Email>
                  </Address>
                </BillTo>
                <Shipping>
                  <Money currency="USD">0.0</Money>
                  <Description xml:lang="en-US"/>
                </Shipping>
                <PaymentTerm payInNumberOfDays="8"/>
                <Contact role="endUser">
                  <Name xml:lang="en">John smith</Name>
                  <Email name="default">jsmith@customer.com</Email>
                </Contact>
              </OrderRequestHeader>
              <ItemOut quantity="10" lineNumber="1">
                <ItemID>
                  <SupplierPartID>5484027</SupplierPartID>
                  <SupplierPartAuxiliaryID>5137391</SupplierPartAuxiliaryID>
                </ItemID>
                <ItemDetail>
                  <UnitPrice><Money currency="USD">6.78</Money></UnitPrice>
                  <Description xml:lang="en">BEEF, CHUCK TERES MAJOR CHOICE 114F BONELESS RAW REF</Description>
                  <UnitOfMeasure>LB</UnitOfMeasure>
                  <Classification domain="UNSPSC">unknown</Classification>
                </ItemDetail>
                <Distribution>
                  <Accounting name="70006 - Raw Food - Dietary and Dining Services">
                    <Segment type="BU" id="70006" description="BU"/>
                    <Segment type="Account" id="530000" description="Account"/>
                    <Segment type="Dept" id="101" description="Dept"/>
                  </Accounting>
                  <Charge><Money currency="USD">67.8</Money></Charge>
                </Distribution>
              </ItemOut>
            </OrderRequest>
          </Request>
        </cXML>
        """;

    [Fact]
    public void MapRequest_OrderRequest_MapsAllFields()
    {
        var document = new CxmlSerializer().Deserialize<CxmlDocument>(OrderRequestXml);
        var mapper = new PunchoutOrderMapper(new CxmlResponseFactory());

        Assert.True(new CxmlPunchoutOrderRequestHandler(mapper, orderProcessor: null).CanHandle(document));

        var request = mapper.MapRequest(document);

        Assert.Equal("1491833992.758494@xyz.com", request.PayloadId);
        Assert.Equal("Test101", request.Sender);
        Assert.Equal("P@ssW0rd", request.SharedSecret);
        Assert.Equal("Virto", request.To);

        Assert.Equal("758494", request.OrderId);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 9, 20, 30, TimeSpan.FromHours(-5)), request.OrderDate);
        Assert.Equal(CxmlConstants.OrderRequestType.New, request.OrderType);
        Assert.Equal("USD", request.Currency);
        Assert.Equal(5199.99m, request.Total);
        Assert.Equal(0m, request.ShippingAmount);
        Assert.Null(request.ShippingDescription);
        Assert.Equal(8, request.PaymentTermDays);

        Assert.Equal("John smith", request.User.Name);
        Assert.Equal("jsmith@customer.com", request.User.Email);

        Assert.Equal("66362", request.ShipTo.AddressId);
        Assert.Equal("Customer", request.ShipTo.Name);
        Assert.Equal("John Smith", request.ShipTo.DeliverTo);
        Assert.Equal("12345 West Street", request.ShipTo.Line1);
        Assert.Null(request.ShipTo.Line2);
        Assert.Equal("Clearwater", request.ShipTo.City);
        Assert.Equal("FL", request.ShipTo.RegionName);
        Assert.Equal("12345", request.ShipTo.PostalCode);
        Assert.Equal("US", request.ShipTo.CountryCode);
        Assert.Equal("United States", request.ShipTo.CountryName);
        Assert.Equal("jsmith@customer.com", request.ShipTo.Email);
        Assert.Equal("Milwaukee", request.BillTo.City);

        var item = Assert.Single(request.Items);
        Assert.Equal(1, item.LineNumber);
        Assert.Equal(10m, item.Quantity);
        Assert.Equal("5484027", item.SupplierPartId);
        Assert.Equal("5137391", item.SupplierPartAuxiliaryId);
        Assert.Equal(6.78m, item.UnitPrice);
        Assert.Equal("USD", item.Currency);
        Assert.Equal("LB", item.UnitOfMeasure);
        Assert.Equal("unknown", item.Classifications["UNSPSC"]);

        var distribution = Assert.Single(item.Distributions);
        Assert.Equal("70006 - Raw Food - Dietary and Dining Services", distribution.AccountingName);
        Assert.Equal(67.8m, distribution.ChargeAmount);
        Assert.Equal(3, distribution.Segments.Count);
        Assert.Equal("Account", distribution.Segments[1].Type);
        Assert.Equal("530000", distribution.Segments[1].Id);
    }
}
