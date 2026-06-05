using System;
using System.Collections.Generic;

namespace Gateway
{
    public class SensorInfo
    {
        public string SensorId { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Zona { get; set; } = string.Empty;
        public List<string> TiposDados { get; set; } = new();
        public DateTime LastSync { get; set; }
    }
}
