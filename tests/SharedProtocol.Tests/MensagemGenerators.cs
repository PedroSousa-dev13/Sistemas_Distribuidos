using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Geradores FsCheck para objetos Mensagem válidos.
/// </summary>
public static class MensagemGenerators
{
    private static readonly string[] SensorIds = { "SENSOR_001", "SENSOR_002", "SENSOR_999" };

    private static readonly Mensagem[] AllValidSamples = BuildAllValidSamples();

    private static Dictionary<string, object> PayloadForTipo(string tipo)
    {
        return tipo switch
        {
            TiposMensagem.REGISTER => new Dictionary<string, object>
            {
                ["tipos_dados"] = new List<string> { "temperatura", "humidade" }
            },
            TiposMensagem.DATA => new Dictionary<string, object>
            {
                ["tipo_dado"] = "temperatura",
                ["valor"] = 23.5
            },
            TiposMensagem.REGISTER_ERR or TiposMensagem.ERROR => new Dictionary<string, object>
            {
                ["error_code"] = CodigosErro.SENSOR_NOT_FOUND,
                ["description"] = "Erro de teste"
            },
            _ => new Dictionary<string, object>()
        };
    }

    private static Mensagem[] BuildAllValidSamples()
    {
        var ts = DateTime.UtcNow.ToString("o");
        return (
            from tipo in TiposMensagem.Validos
            from sensorId in SensorIds
            select new Mensagem(tipo, sensorId, PayloadForTipo(tipo), ts)
        ).ToArray();
    }

    /// <summary>
    /// Todas as combinações (tipo × sensor_id) usadas pelo gerador — útil para testes de distribuição.
    /// </summary>
    public static IReadOnlyList<Mensagem> AllValidSamplesList => AllValidSamples;

    /// <summary>
    /// Gera objetos Mensagem válidos para todos os tipos de mensagem.
    /// Usa <see cref="Gen.Elements"/> sobre um conjunto finito para evitar null do shrink em tipos referência.
    /// </summary>
    public static Arbitrary<Mensagem> ValidMensagem()
    {
        return Arb.From(Gen.Elements(AllValidSamples), _ => Enumerable.Empty<Mensagem>());
    }
}
