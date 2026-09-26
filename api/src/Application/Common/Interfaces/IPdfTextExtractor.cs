namespace FinanceFoodTracker.Application.Common.Interfaces;

public interface IPdfTextExtractor
{
    string Extract(byte[] pdf);
}
