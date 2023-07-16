

using Kodoshi.Core;
using TestProjectBase.Client;
using TestProjectBase.Models.v1;

var client = new RPCClientBuilder()
    .SetApiUri(new Uri("http://localhost:5000/rpc"))
    .Build();


var rng = new Random();
var arr = new byte[12];
rng.NextBytes(arr);
var content = Convert.ToBase64String(arr);
Console.WriteLine($"Array: {content}");
var req = new Request<VoidType>(VoidType.Instance, ReadOnlyArray.Move(arr));
var resp = await client.Call_v1_GetStatus(req, default);
Console.WriteLine(resp.GetType());