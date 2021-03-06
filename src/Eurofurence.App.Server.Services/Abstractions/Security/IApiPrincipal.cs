﻿using System.Collections.Generic;

namespace Eurofurence.App.Server.Services.Abstractions.Security
{
    public interface IApiPrincipal
    {
        string[] Roles { get; }
        List<KeyValuePair<string, string>> Claims { get; }
        bool IsAttendee { get; }
        bool IsAuthenticated { get; }
        string Uid { get; }
        string GivenName { get; }
    }
}