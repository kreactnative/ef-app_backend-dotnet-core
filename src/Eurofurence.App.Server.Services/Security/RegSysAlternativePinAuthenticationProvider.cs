﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Eurofurence.App.Common.Validation;
using Eurofurence.App.Domain.Model.Abstractions;
using Eurofurence.App.Domain.Model.Security;
using Eurofurence.App.Server.Services.Abstractions.Security;

namespace Eurofurence.App.Server.Services.Security
{
    public class RegSysAlternativePinAuthenticationProvider : IAuthenticationProvider, IRegSysAlternativePinAuthenticationProvider
    {
        private readonly IEntityRepository<RegSysAlternativePinRecord> _regSysAlternativePinRepository;

        public RegSysAlternativePinAuthenticationProvider(IEntityRepository<RegSysAlternativePinRecord> regSysAlternativePinRepository)
        {
            _regSysAlternativePinRepository = regSysAlternativePinRepository;
        }

        private string GeneratePin()
        {
            var r = new Random();
            return $"{r.Next(100000, 999999)}";
        }

        public async Task<RegSysAlternativePinResponse> RequestAlternativePinAsync(RegSysAlternativePinRequest request, string requesterUid)
        {
            int regNo = 0;

            if (!BadgeChecksum.TryParse(request.RegNoOnBadge, out regNo)) return null;

            var alternativePin =
                (await _regSysAlternativePinRepository.FindAllAsync(a => a.RegNo == regNo))
                .SingleOrDefault();

            bool existingRecord = true;

            if (alternativePin == null)
            {
                existingRecord = false;
                alternativePin = new RegSysAlternativePinRecord()
                {
                    RegNo = regNo,
                };
                alternativePin.NewId();
            }

            alternativePin.IssuedByUid = requesterUid;
            alternativePin.IssuedDateTimeUtc = DateTime.UtcNow;
            alternativePin.Pin = GeneratePin();
            alternativePin.NameOnBadge = request.NameOnBadge;
            alternativePin.IssueLog.Add(new RegSysAlternativePinRecord.IssueRecord()
            {
                RequestDateTimeUtc = DateTime.UtcNow,
                NameOnBadge = request.NameOnBadge,
                RequesterUid = requesterUid
            });

            alternativePin.Touch();

            if (existingRecord)
                await _regSysAlternativePinRepository.ReplaceOneAsync(alternativePin);
            else
                await _regSysAlternativePinRepository.InsertOneAsync(alternativePin);

            return new RegSysAlternativePinResponse()
            {
                NameOnBadge = alternativePin.NameOnBadge,
                Pin = alternativePin.Pin,
                RegNo = alternativePin.RegNo
            };
        }

        public async Task<RegSysAlternativePinRecord> GetAlternativePinAsync(int regNo)
        {
            return (await _regSysAlternativePinRepository.FindAllAsync(a => a.RegNo == regNo)).SingleOrDefault();
        }


        public async Task<AuthenticationResult> ValidateRegSysAuthenticationRequestAsync(RegSysAuthenticationRequest request)
        {
            var result = new AuthenticationResult
            {
                Source = GetType().Name,
                IsAuthenticated = false
            };

            var alternativePin =
                (await _regSysAlternativePinRepository.FindAllAsync(a => a.RegNo == request.RegNo))
                .SingleOrDefault();

            if (alternativePin != null && request.Password == alternativePin.Pin)
            {
                result.IsAuthenticated = true;
                result.RegNo = alternativePin.RegNo;
                result.Username = alternativePin.NameOnBadge;

                alternativePin.PinConsumptionDatesUtc.Add(DateTime.UtcNow);
                await _regSysAlternativePinRepository.ReplaceOneAsync(alternativePin);
            }

            return result;
        }
    }
}
