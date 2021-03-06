﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Eurofurence.App.Server.Services.Abstractions.Security;

namespace Eurofurence.App.Server.Services.Security
{
    public class ApiPrincipal : IApiPrincipal
    {
        private readonly ClaimsIdentity _identity;
        private readonly ClaimsPrincipal _principal;

        public ApiPrincipal(ClaimsPrincipal principal)
        {
            _principal = principal;
            _identity = (ClaimsIdentity) principal.Identity;
        }

        public string[] Roles =>
            _identity?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray() ?? new string[0];

        public List<KeyValuePair<string, string>> Claims =>
            _identity?.Claims.Select(c => new KeyValuePair<string, string>(c.Type, c.Value)).ToList();

        public bool IsAttendee => _principal?.IsInRole("Attendee") ?? false;
        public bool IsAuthenticated => _identity?.IsAuthenticated ?? false;
        public string Uid => _principal?.Identity.Name ?? "Anonymous";

        public string GivenName => _principal?.Claims.SingleOrDefault(c => c.Type == ClaimTypes.GivenName)?.Value ??
                              "Anonymous";
    }
}