


using System.Globalization;
using Radzen.Blazor;

public class CurrencyDataGridColumn<TItem> : RadzenDataGridColumn<TItem> where TItem : notnull
{
    private readonly NumberFormatInfo _currencyFormat = new CultureInfo("en-US").NumberFormat;

    public CurrencyDataGridColumn()
    {
        this.FormatProvider = _currencyFormat;
        this.FormatString = "{0:C2}";
    }
}