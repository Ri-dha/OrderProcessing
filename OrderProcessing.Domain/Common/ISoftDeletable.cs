namespace OrderProcessing.Domain.Common;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    void MarkAsDeleted();
}