namespace IPS_PROBACKOFFICE.Models;

public sealed class BrokerageFeeRow
{
    public BrokerageFeeRow(
        int instCode,
        string institutionName,
        decimal? df1mFee,
        decimal? ndf1mFee,
        decimal? ndMarFee,
        decimal? basisMarFee,
        string? currencyCode,
        string? remarks)
    {
        InstCode = instCode;
        InstitutionName = institutionName;
        Df1mFee = df1mFee;
        Ndf1mFee = ndf1mFee;
        NdMarFee = ndMarFee;
        BasisMarFee = basisMarFee;
        CurrencyCode = currencyCode;
        Remarks = remarks;
    }

    public int InstCode { get; }
    public string InstitutionName { get; }
    public decimal? Df1mFee { get; set; }
    public decimal? Ndf1mFee { get; set; }
    public decimal? NdMarFee { get; set; }
    public decimal? BasisMarFee { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Remarks { get; set; }
}
