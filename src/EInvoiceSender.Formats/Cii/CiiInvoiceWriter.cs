using System.Globalization;
using System.Xml;
using EInvoiceSender.Application.Abstractions;
using EInvoiceSender.Domain.Calculation;
using EInvoiceSender.Domain.Model;
using EInvoiceSender.Domain.Money;
using EInvoiceSender.Domain.Values;
using EInvoiceSender.Formats.Xml;

namespace EInvoiceSender.Formats.Cii;

/// <summary>
/// Erzeugt eine Rechnungs-XML im Format UN/CEFACT Cross Industry Invoice,
/// Profil EN 16931.
///
/// Die Reihenfolge der Elemente ist durch das XSD fest vorgegeben und darf
/// nicht veraendert werden – jede Abweichung fuehrt zu einer ungueltigen Datei.
/// Die Reihenfolge ist unten je Block als Kommentar vermerkt und wird durch die
/// Gegenpruefung mit dem CEN-Schematron abgesichert
/// (build/validate-golden-masters.sh).
///
/// Der Writer rechnet nichts. Er schreibt ausschliesslich die uebergebenen,
/// bereits geprueften Summen – so kann die Datei nicht von dem abweichen,
/// was der Benutzer in der Kontrollansicht bestaetigt hat.
/// </summary>
public sealed class CiiInvoiceWriter : IInvoiceXmlWriter
{
    /// <inheritdoc />
    public string ProfileId => CiiConstants.ProfileEn16931;

    /// <inheritdoc />
    public string FormatDescription => CiiConstants.FormatDescription;

    /// <inheritdoc />
    public byte[] Write(Invoice invoice, InvoiceTotals totals)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(totals);

        using var stream = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(stream, SecureXml.CreateWriterSettings()))
        {
            WriteDocument(writer, invoice, totals);
        }

        return stream.ToArray();
    }

    private static void WriteDocument(XmlWriter w, Invoice invoice, InvoiceTotals totals)
    {
        w.WriteStartDocument();
        w.WriteStartElement(CiiConstants.PrefixRsm, CiiConstants.RootElement, CiiConstants.NsRsm);
        w.WriteAttributeString("xmlns", CiiConstants.PrefixRam, null, CiiConstants.NsRam);
        w.WriteAttributeString("xmlns", CiiConstants.PrefixQdt, null, CiiConstants.NsQdt);
        w.WriteAttributeString("xmlns", CiiConstants.PrefixUdt, null, CiiConstants.NsUdt);

        WriteExchangedDocumentContext(w);
        WriteExchangedDocument(w, invoice);

        w.WriteStartElement(CiiConstants.PrefixRsm, "SupplyChainTradeTransaction", CiiConstants.NsRsm);

        for (int i = 0; i < invoice.Lines.Count; i++)
        {
            WriteLine(w, invoice.Lines[i], totals.LineNetAmounts[i]);
        }

        WriteHeaderTradeAgreement(w, invoice);
        WriteHeaderTradeDelivery(w, invoice);
        WriteHeaderTradeSettlement(w, invoice, totals);

        w.WriteEndElement(); // SupplyChainTradeTransaction
        w.WriteEndElement(); // CrossIndustryInvoice
        w.WriteEndDocument();
    }

    private static void WriteExchangedDocumentContext(XmlWriter w)
    {
        w.WriteStartElement(CiiConstants.PrefixRsm, "ExchangedDocumentContext", CiiConstants.NsRsm);
        Ram(w, "GuidelineSpecifiedDocumentContextParameter", () =>
            RamText(w, "ID", CiiConstants.ProfileEn16931));
        w.WriteEndElement();
    }

    private static void WriteExchangedDocument(XmlWriter w, Invoice invoice)
    {
        w.WriteStartElement(CiiConstants.PrefixRsm, "ExchangedDocument", CiiConstants.NsRsm);

        // Reihenfolge: ID, Name, TypeCode, IssueDateTime, ..., IncludedNote
        RamText(w, "ID", invoice.InvoiceNumber);
        RamText(w, "TypeCode", ((int)invoice.TypeCode).ToString(CultureInfo.InvariantCulture));
        Ram(w, "IssueDateTime", () => WriteDateTimeString(w, invoice.IssueDate));

        if (!string.IsNullOrWhiteSpace(invoice.Note))
        {
            Ram(w, "IncludedNote", () => RamText(w, "Content", invoice.Note));
        }

        w.WriteEndElement();
    }

    private static void WriteLine(XmlWriter w, InvoiceLine line, decimal lineNetAmount)
    {
        Ram(w, "IncludedSupplyChainTradeLineItem", () =>
        {
            Ram(w, "AssociatedDocumentLineDocument", () =>
                RamText(w, "LineID", line.Number.ToString(CultureInfo.InvariantCulture)));

            // SpecifiedTradeProduct: ..., Name, Description, ...
            Ram(w, "SpecifiedTradeProduct", () =>
            {
                RamText(w, "Name", line.Name);
                if (!string.IsNullOrWhiteSpace(line.Description))
                {
                    RamText(w, "Description", line.Description);
                }
            });

            Ram(w, "SpecifiedLineTradeAgreement", () =>
                Ram(w, "NetPriceProductTradePrice", () =>
                {
                    // Reihenfolge: ChargeAmount, BasisQuantity
                    RamText(w, "ChargeAmount", Amounts.ToXmlString(line.NetUnitPrice));
                    if (line.PriceBaseQuantity != 1m && line.PriceBaseQuantity > 0m)
                    {
                        WriteQuantity(w, "BasisQuantity", line.PriceBaseQuantity, line.Unit);
                    }
                }));

            Ram(w, "SpecifiedLineTradeDelivery", () =>
                WriteQuantity(w, "BilledQuantity", line.Quantity, line.Unit));

            Ram(w, "SpecifiedLineTradeSettlement", () =>
            {
                // Reihenfolge: ApplicableTradeTax, BillingSpecifiedPeriod,
                // SpecifiedTradeAllowanceCharge, ...,
                // SpecifiedTradeSettlementLineMonetarySummation
                Ram(w, "ApplicableTradeTax", () =>
                {
                    RamText(w, "TypeCode", CiiConstants.TaxTypeVat);
                    RamText(w, "CategoryCode", line.VatCategory.ToCode());
                    RamText(w, "RateApplicablePercent", Amounts.RateToXmlString(line.VatRate));
                });

                if (line.ServicePeriodStart is not null || line.ServicePeriodEnd is not null)
                {
                    Ram(w, "BillingSpecifiedPeriod", () =>
                    {
                        if (line.ServicePeriodStart is { } start)
                        {
                            Ram(w, "StartDateTime", () => WriteDateTimeString(w, start));
                        }

                        if (line.ServicePeriodEnd is { } end)
                        {
                            Ram(w, "EndDateTime", () => WriteDateTimeString(w, end));
                        }
                    });
                }

                if (line.AllowanceAmount != 0m)
                {
                    WriteLineAllowanceCharge(w, isCharge: false, line.AllowanceAmount, line.AllowanceReason);
                }

                if (line.ChargeAmount != 0m)
                {
                    WriteLineAllowanceCharge(w, isCharge: true, line.ChargeAmount, line.ChargeReason);
                }

                Ram(w, "SpecifiedTradeSettlementLineMonetarySummation", () =>
                    RamText(w, "LineTotalAmount", Amounts.ToXmlString(lineNetAmount)));
            });
        });
    }

    private static void WriteLineAllowanceCharge(XmlWriter w, bool isCharge, decimal amount, string? reason)
    {
        // Reihenfolge: ChargeIndicator, ..., ActualAmount, ReasonCode, Reason
        Ram(w, "SpecifiedTradeAllowanceCharge", () =>
        {
            Ram(w, "ChargeIndicator", () =>
                w.WriteElementString(
                    CiiConstants.PrefixUdt, "Indicator", CiiConstants.NsUdt,
                    isCharge ? "true" : "false"));

            RamText(w, "ActualAmount", Amounts.ToXmlString(amount));

            if (!string.IsNullOrWhiteSpace(reason))
            {
                RamText(w, "Reason", reason);
            }
        });
    }

    private static void WriteHeaderTradeAgreement(XmlWriter w, Invoice invoice)
    {
        // Reihenfolge: BuyerReference, SellerTradeParty, BuyerTradeParty, ...,
        // BuyerOrderReferencedDocument, ContractReferencedDocument
        Ram(w, "ApplicableHeaderTradeAgreement", () =>
        {
            if (!string.IsNullOrWhiteSpace(invoice.BuyerReference))
            {
                RamText(w, "BuyerReference", invoice.BuyerReference);
            }

            WriteSeller(w, invoice.Seller);
            WriteBuyer(w, invoice.Buyer);

            if (!string.IsNullOrWhiteSpace(invoice.OrderReference))
            {
                Ram(w, "BuyerOrderReferencedDocument", () =>
                    RamText(w, "IssuerAssignedID", invoice.OrderReference));
            }

            if (!string.IsNullOrWhiteSpace(invoice.ContractReference))
            {
                Ram(w, "ContractReferencedDocument", () =>
                    RamText(w, "IssuerAssignedID", invoice.ContractReference));
            }
        });
    }

    private static void WriteSeller(XmlWriter w, SellerParty seller)
    {
        // TradePartyType-Reihenfolge: ID, GlobalID, Name, Description,
        // SpecifiedLegalOrganization, DefinedTradeContact, PostalTradeAddress,
        // URIUniversalCommunication, SpecifiedTaxRegistration
        Ram(w, "SellerTradeParty", () =>
        {
            RamText(w, "Name", seller.Name);

            if (!string.IsNullOrWhiteSpace(seller.LegalRegistrationId)
                || !string.IsNullOrWhiteSpace(seller.TradingName))
            {
                Ram(w, "SpecifiedLegalOrganization", () =>
                {
                    if (!string.IsNullOrWhiteSpace(seller.LegalRegistrationId))
                    {
                        RamText(w, "ID", seller.LegalRegistrationId);
                    }

                    if (!string.IsNullOrWhiteSpace(seller.TradingName))
                    {
                        RamText(w, "TradingBusinessName", seller.TradingName);
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(seller.ContactName)
                || !string.IsNullOrWhiteSpace(seller.ContactPhone)
                || !string.IsNullOrWhiteSpace(seller.Email))
            {
                Ram(w, "DefinedTradeContact", () =>
                {
                    if (!string.IsNullOrWhiteSpace(seller.ContactName))
                    {
                        RamText(w, "PersonName", seller.ContactName);
                    }

                    if (!string.IsNullOrWhiteSpace(seller.ContactPhone))
                    {
                        Ram(w, "TelephoneUniversalCommunication", () =>
                            RamText(w, "CompleteNumber", seller.ContactPhone));
                    }

                    if (!string.IsNullOrWhiteSpace(seller.Email))
                    {
                        Ram(w, "EmailURIUniversalCommunication", () =>
                            RamText(w, "URIID", seller.Email));
                    }
                });
            }

            WriteAddress(w, seller.Address);

            if (!string.IsNullOrWhiteSpace(seller.Email))
            {
                // BT-34: elektronische Adresse des Verkaeufers
                Ram(w, "URIUniversalCommunication", () =>
                    RamTextWithAttribute(w, "URIID", seller.Email,
                        "schemeID", CiiConstants.ElectronicAddressSchemeEmail));
            }

            if (!string.IsNullOrWhiteSpace(seller.VatId))
            {
                Ram(w, "SpecifiedTaxRegistration", () =>
                    RamTextWithAttribute(w, "ID", seller.VatId, "schemeID", CiiConstants.TaxSchemeVatId));
            }

            if (!string.IsNullOrWhiteSpace(seller.TaxNumber))
            {
                Ram(w, "SpecifiedTaxRegistration", () =>
                    RamTextWithAttribute(w, "ID", seller.TaxNumber, "schemeID", CiiConstants.TaxSchemeTaxNumber));
            }
        });
    }

    private static void WriteBuyer(XmlWriter w, BuyerParty buyer)
    {
        Ram(w, "BuyerTradeParty", () =>
        {
            RamText(w, "Name", buyer.Name);

            if (!string.IsNullOrWhiteSpace(buyer.ContactName) || !string.IsNullOrWhiteSpace(buyer.Email))
            {
                Ram(w, "DefinedTradeContact", () =>
                {
                    if (!string.IsNullOrWhiteSpace(buyer.ContactName))
                    {
                        RamText(w, "PersonName", buyer.ContactName);
                    }

                    if (!string.IsNullOrWhiteSpace(buyer.Email))
                    {
                        Ram(w, "EmailURIUniversalCommunication", () =>
                            RamText(w, "URIID", buyer.Email));
                    }
                });
            }

            WriteAddress(w, buyer.Address);

            string? electronicAddress = buyer.ElectronicAddress ?? buyer.Email;
            if (!string.IsNullOrWhiteSpace(electronicAddress))
            {
                string scheme = buyer.ElectronicAddressScheme ?? CiiConstants.ElectronicAddressSchemeEmail;
                Ram(w, "URIUniversalCommunication", () =>
                    RamTextWithAttribute(w, "URIID", electronicAddress, "schemeID", scheme));
            }

            if (!string.IsNullOrWhiteSpace(buyer.VatId))
            {
                Ram(w, "SpecifiedTaxRegistration", () =>
                    RamTextWithAttribute(w, "ID", buyer.VatId, "schemeID", CiiConstants.TaxSchemeVatId));
            }
        });
    }

    private static void WriteAddress(XmlWriter w, PostalAddress address)
    {
        // Reihenfolge: PostcodeCode, LineOne, LineTwo, LineThree, CityName,
        // CountryID, CountrySubDivisionName
        Ram(w, "PostalTradeAddress", () =>
        {
            if (!string.IsNullOrWhiteSpace(address.PostalCode))
            {
                RamText(w, "PostcodeCode", address.PostalCode);
            }

            if (!string.IsNullOrWhiteSpace(address.Street))
            {
                RamText(w, "LineOne", address.Street);
            }

            if (!string.IsNullOrWhiteSpace(address.AdditionalLine))
            {
                RamText(w, "LineTwo", address.AdditionalLine);
            }

            if (!string.IsNullOrWhiteSpace(address.City))
            {
                RamText(w, "CityName", address.City);
            }

            RamText(w, "CountryID", address.Country.Value);

            if (!string.IsNullOrWhiteSpace(address.CountrySubdivision))
            {
                RamText(w, "CountrySubDivisionName", address.CountrySubdivision);
            }
        });
    }

    private static void WriteHeaderTradeDelivery(XmlWriter w, Invoice invoice)
    {
        Ram(w, "ApplicableHeaderTradeDelivery", () =>
        {
            if (invoice.DeliveryDate is { } delivery)
            {
                Ram(w, "ActualDeliverySupplyChainEvent", () =>
                    Ram(w, "OccurrenceDateTime", () => WriteDateTimeString(w, delivery)));
            }
        });
    }

    private static void WriteHeaderTradeSettlement(XmlWriter w, Invoice invoice, InvoiceTotals totals)
    {
        // Reihenfolge: PaymentReference, InvoiceCurrencyCode,
        // SpecifiedTradeSettlementPaymentMeans, ApplicableTradeTax,
        // BillingSpecifiedPeriod, SpecifiedTradeAllowanceCharge,
        // SpecifiedTradePaymentTerms,
        // SpecifiedTradeSettlementHeaderMonetarySummation
        Ram(w, "ApplicableHeaderTradeSettlement", () =>
        {
            if (invoice.Payment?.Reference is { Length: > 0 } reference)
            {
                RamText(w, "PaymentReference", reference);
            }

            RamText(w, "InvoiceCurrencyCode", invoice.Currency.Value);

            if (invoice.Payment is { } payment)
            {
                WritePaymentMeans(w, payment);
            }

            foreach (VatBreakdownEntry entry in totals.VatBreakdown)
            {
                WriteHeaderTradeTax(w, invoice, entry);
            }

            if (invoice.BillingPeriodStart is not null || invoice.BillingPeriodEnd is not null)
            {
                Ram(w, "BillingSpecifiedPeriod", () =>
                {
                    if (invoice.BillingPeriodStart is { } start)
                    {
                        Ram(w, "StartDateTime", () => WriteDateTimeString(w, start));
                    }

                    if (invoice.BillingPeriodEnd is { } end)
                    {
                        Ram(w, "EndDateTime", () => WriteDateTimeString(w, end));
                    }
                });
            }

            foreach (DocumentAllowanceCharge item in invoice.AllowancesAndCharges)
            {
                WriteDocumentAllowanceCharge(w, item);
            }

            if (invoice.DueDate is not null || !string.IsNullOrWhiteSpace(invoice.Payment?.Terms))
            {
                Ram(w, "SpecifiedTradePaymentTerms", () =>
                {
                    if (!string.IsNullOrWhiteSpace(invoice.Payment?.Terms))
                    {
                        RamText(w, "Description", invoice.Payment.Terms);
                    }

                    if (invoice.DueDate is { } dueDate)
                    {
                        Ram(w, "DueDateDateTime", () => WriteDateTimeString(w, dueDate));
                    }
                });
            }

            WriteMonetarySummation(w, invoice, totals);
        });
    }

    private static void WritePaymentMeans(XmlWriter w, PaymentDetails payment)
    {
        // Reihenfolge: TypeCode, Information, ...,
        // PayeePartyCreditorFinancialAccount,
        // PayeeSpecifiedCreditorFinancialInstitution
        Ram(w, "SpecifiedTradeSettlementPaymentMeans", () =>
        {
            RamText(w, "TypeCode", ((int)payment.MeansCode).ToString(CultureInfo.InvariantCulture));

            if (payment.BankAccount is { } account)
            {
                Ram(w, "PayeePartyCreditorFinancialAccount", () =>
                {
                    RamText(w, "IBANID", account.Iban.Value);
                    if (!string.IsNullOrWhiteSpace(account.AccountHolder))
                    {
                        RamText(w, "AccountName", account.AccountHolder);
                    }
                });

                if (!string.IsNullOrWhiteSpace(account.Bic))
                {
                    Ram(w, "PayeeSpecifiedCreditorFinancialInstitution", () =>
                        RamText(w, "BICID", account.Bic));
                }
            }
        });
    }

    private static void WriteHeaderTradeTax(XmlWriter w, Invoice invoice, VatBreakdownEntry entry)
    {
        // Reihenfolge: CalculatedAmount, TypeCode, ExemptionReason, BasisAmount,
        // CategoryCode, ExemptionReasonCode, ..., RateApplicablePercent
        Ram(w, "ApplicableTradeTax", () =>
        {
            RamText(w, "CalculatedAmount", Amounts.ToXmlString(entry.TaxAmount));
            RamText(w, "TypeCode", CiiConstants.TaxTypeVat);

            VatExemptionReason? exemption = invoice.ExemptionReasons
                .FirstOrDefault(r => r.Category == entry.Category);

            if (exemption is not null && !string.IsNullOrWhiteSpace(exemption.Reason))
            {
                RamText(w, "ExemptionReason", exemption.Reason);
            }

            RamText(w, "BasisAmount", Amounts.ToXmlString(entry.TaxableAmount));
            RamText(w, "CategoryCode", entry.Category.ToCode());

            if (exemption is not null && !string.IsNullOrWhiteSpace(exemption.ReasonCode))
            {
                RamText(w, "ExemptionReasonCode", exemption.ReasonCode);
            }

            RamText(w, "RateApplicablePercent", Amounts.RateToXmlString(entry.Rate));
        });
    }

    private static void WriteDocumentAllowanceCharge(XmlWriter w, DocumentAllowanceCharge item)
    {
        // Reihenfolge: ChargeIndicator, ..., ActualAmount, ReasonCode, Reason,
        // CategoryTradeTax
        Ram(w, "SpecifiedTradeAllowanceCharge", () =>
        {
            Ram(w, "ChargeIndicator", () =>
                w.WriteElementString(
                    CiiConstants.PrefixUdt, "Indicator", CiiConstants.NsUdt,
                    item.IsCharge ? "true" : "false"));

            RamText(w, "ActualAmount", Amounts.ToXmlString(item.Amount));

            if (!string.IsNullOrWhiteSpace(item.ReasonCode))
            {
                RamText(w, "ReasonCode", item.ReasonCode);
            }

            RamText(w, "Reason", item.Reason);

            Ram(w, "CategoryTradeTax", () =>
            {
                RamText(w, "TypeCode", CiiConstants.TaxTypeVat);
                RamText(w, "CategoryCode", item.VatCategory.ToCode());
                RamText(w, "RateApplicablePercent", Amounts.RateToXmlString(item.VatRate));
            });
        });
    }

    private static void WriteMonetarySummation(XmlWriter w, Invoice invoice, InvoiceTotals totals)
    {
        // Reihenfolge streng nach XSD:
        // LineTotalAmount, ChargeTotalAmount, AllowanceTotalAmount,
        // TaxBasisTotalAmount, TaxTotalAmount, RoundingAmount, GrandTotalAmount,
        // TotalPrepaidAmount, DuePayableAmount
        Ram(w, "SpecifiedTradeSettlementHeaderMonetarySummation", () =>
        {
            RamText(w, "LineTotalAmount", Amounts.ToXmlString(totals.LineTotal));
            RamText(w, "ChargeTotalAmount", Amounts.ToXmlString(totals.ChargeTotal));
            RamText(w, "AllowanceTotalAmount", Amounts.ToXmlString(totals.AllowanceTotal));
            RamText(w, "TaxBasisTotalAmount", Amounts.ToXmlString(totals.TaxBasisTotal));

            // BR-53: Der Gesamtsteuerbetrag traegt zwingend die Waehrung.
            RamTextWithAttribute(
                w, "TaxTotalAmount", Amounts.ToXmlString(totals.TaxTotal),
                "currencyID", invoice.Currency.Value);

            if (totals.RoundingAmount != 0m)
            {
                RamText(w, "RoundingAmount", Amounts.ToXmlString(totals.RoundingAmount));
            }

            RamText(w, "GrandTotalAmount", Amounts.ToXmlString(totals.GrandTotal));
            RamText(w, "TotalPrepaidAmount", Amounts.ToXmlString(totals.PaidAmount));
            RamText(w, "DuePayableAmount", Amounts.ToXmlString(totals.DuePayableAmount));
        });
    }

    // --- Schreibhilfen ------------------------------------------------------

    /// <summary>Schreibt ein ram-Element mit verschachteltem Inhalt.</summary>
    private static void Ram(XmlWriter w, string localName, Action writeContent)
    {
        w.WriteStartElement(CiiConstants.PrefixRam, localName, CiiConstants.NsRam);
        writeContent();
        w.WriteEndElement();
    }

    /// <summary>Schreibt ein ram-Element mit Textinhalt.</summary>
    private static void RamText(XmlWriter w, string localName, string? value)
        => w.WriteElementString(CiiConstants.PrefixRam, localName, CiiConstants.NsRam, value ?? string.Empty);

    /// <summary>Schreibt ein ram-Element mit Textinhalt und einem Attribut.</summary>
    private static void RamTextWithAttribute(
        XmlWriter w, string localName, string? value, string attributeName, string attributeValue)
    {
        w.WriteStartElement(CiiConstants.PrefixRam, localName, CiiConstants.NsRam);
        w.WriteAttributeString(attributeName, attributeValue);
        w.WriteString(value ?? string.Empty);
        w.WriteEndElement();
    }

    /// <summary>
    /// Schreibt eine Mengenangabe mit Einheitencode.
    /// Mengen duerfen mehr als zwei Nachkommastellen haben; es wird die
    /// kuerzeste verlustfreie Schreibweise verwendet.
    /// </summary>
    private static void WriteQuantity(XmlWriter w, string localName, decimal quantity, UnitCode unit)
    {
        w.WriteStartElement(CiiConstants.PrefixRam, localName, CiiConstants.NsRam);
        w.WriteAttributeString("unitCode", unit.Value);
        w.WriteString(quantity.ToString("0.####", CultureInfo.InvariantCulture));
        w.WriteEndElement();
    }

    /// <summary>
    /// Schreibt ein Datum im Format 102 (<c>JJJJMMTT</c>).
    /// Die Norm laesst hier keinen Zeitanteil und keine Zeitzone zu.
    /// </summary>
    private static void WriteDateTimeString(XmlWriter w, DateOnly date)
    {
        w.WriteStartElement(CiiConstants.PrefixUdt, "DateTimeString", CiiConstants.NsUdt);
        w.WriteAttributeString("format", CiiConstants.DateFormatCode);
        w.WriteString(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        w.WriteEndElement();
    }
}
