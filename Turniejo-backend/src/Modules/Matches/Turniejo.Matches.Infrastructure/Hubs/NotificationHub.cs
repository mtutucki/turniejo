using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Turniejo.Matches.Infrastructure.Hubs;

// Powiadomienia są osobiste — hub tylko dla zalogowanych, bez grup: serwer wysyła przez Clients.Users(...)
// (identyfikator użytkownika z SubUserIdProvider), klient niczego nie subskrybuje sam.
[Authorize]
public sealed class NotificationHub : Hub;
