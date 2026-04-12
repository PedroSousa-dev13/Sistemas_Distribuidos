using System;
using System.Collections.Generic;

namespace Gateway
{
    public class SensorInfo
    {
        public string SensorId { get; set; }
        public string Estado { get; set; }
        public string Zona { get; set; }
        public List<string> TiposDados { get; set; }
        public DateTime LastSync { get; set; }
    }
}