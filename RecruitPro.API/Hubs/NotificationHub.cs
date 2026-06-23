using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RecruitPro.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
}
