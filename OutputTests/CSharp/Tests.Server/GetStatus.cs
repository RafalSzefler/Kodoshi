using Kodoshi.Core;
using TestProjectBase.Models.v1;
using TestProjectBase.Server.v1;

namespace Tests.Server;

internal sealed class GetStatusService : IGetStatus
{
    public ValueTask<Response<VoidType>> HandleAsync(Request<VoidType> instance, CancellationToken ct)
    {
        Console.WriteLine(Convert.ToBase64String(instance.CorrelationId.AsSpan()));
        var resp = Response.CreateOk(VoidType.Instance);
        return new ValueTask<Response<VoidType>>(resp);
    }
}
