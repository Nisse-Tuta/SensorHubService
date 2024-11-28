using Microsoft.AspNetCore.SignalR;
using RaspSensorService.DTO;
using System.Runtime.CompilerServices;

namespace RaspSensorService;

public class RaspSensorHub : Hub
{
    
    public async Task SendMessage(ListVaulesDTO sensVals)
    {
        await Clients.All.SendAsync("ReceiveSensorEvents", sensVals);
    }
}
