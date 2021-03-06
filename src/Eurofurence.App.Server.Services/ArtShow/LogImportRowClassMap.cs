﻿using CsvHelper.Configuration;

namespace Eurofurence.App.Server.Services.ArtShow
{
    internal sealed class LogImportRowClassMap : CsvClassMap<LogImportRow>
    {
        public LogImportRowClassMap()
        {
            Map(m => m.RegNo).Index(0);
            Map(m => m.ASIDNO).Index(1);
            Map(m => m.ArtistName).Index(2);
            Map(m => m.ArtPieceTitle).Index(3);
            Map(m => m.Status).Index(4);
        }
    }
}