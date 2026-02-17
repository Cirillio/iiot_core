using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IIoT.WebApi.Core.Interfaces
{
    public interface IMonitoringClient
    {
        Task ReceiveMetrics(string json);
    }
}
