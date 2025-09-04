using System.ServiceModel;

namespace Server
{
    [ServiceContract]
    public interface IWcfService
    {
        [OperationContract]
        string GetSecureData(string request);
    }
}