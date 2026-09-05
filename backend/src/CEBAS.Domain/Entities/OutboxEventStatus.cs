namespace CEBAS.Domain.Entities;

public enum OutboxEventStatus
{
    Pending,
    Processing,
    Published,
    Failed
}
