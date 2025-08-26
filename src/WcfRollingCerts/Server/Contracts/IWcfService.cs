using System.ServiceModel;

namespace Server.Contracts;

[ServiceContract]
public interface IWcfService
{
    [OperationContract]
    string GetCurrentTime(string message);
}