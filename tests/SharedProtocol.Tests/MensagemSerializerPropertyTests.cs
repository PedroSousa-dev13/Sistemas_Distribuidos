using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FsCheck;
using FsCheck.Xunit;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Property-based tests para MensagemSerializer.
/// Feature: shared-protocol-phase0
/// </summary>
public class MensagemSerializerPropertyTests
{
    // ─── Helpers ─────────────────────────────────────────────────────────────────

    // ─── Property 1: Serialization Round-Trip Preservation ───────────────────────

    // Feature: shared-protocol-phase0, Property 1: Serialization Round-Trip Preservation
    [Property(MaxTest = 100)]
    public Property RoundTrip_PreservaTodosOsCampos()
    {
        return Prop.ForAll(
            MensagemGenerators.ValidMensagem(),
            mensagem =>
            {
                try
                {
                    var json = MensagemSerializer.Serializar(mensagem);
                    var deserialized = MensagemSerializer.Deserializar(json);
                    var jsonAgain = MensagemSerializer.Serializar(deserialized);
                    // Igualdade do wire JSON evita comparação frágil List/JsonElement no payload.
                    return json == jsonAgain;
                }
                catch
                {
                    return false;
                }
            });
    }

    // ─── Property 2: Invalid JSON Rejection ──────────────────────────────────────

    // Feature: shared-protocol-phase0, Property 2: Invalid JSON Rejection
    [Property(MaxTest = 100)]
    public Property JsonInvalido_SempreLancaFormatExceptionOuArgumentException()
    {
        // Generate strings that are not valid JSON
        var invalidJsonGen = Arb.Default.NonEmptyString().Generator
            .Select(s => s.Get)
            .Where(s => !string.IsNullOrWhiteSpace(s) && !IsValidJson(s));

        return Prop.ForAll(
            Arb.From(invalidJsonGen),
            invalidJson =>
            {
                try
                {
                    MensagemSerializer.Deserializar(invalidJson);
                    return false; // should have thrown
                }
                catch (FormatException)
                {
                    return true;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            });
    }

    private static bool IsValidJson(string s)
    {
        try
        {
            JsonDocument.Parse(s);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─── Property 3: Concurrent Serialization Safety ───────────────────────────

    // Feature: shared-protocol-phase0, Property 3: Concurrent Serialization Safety
    [Property(MaxTest = 100)]
    public Property SerializacaoConcorrente_CadaThreadProduzJsonCorreto()
    {
        var msgArrayGen = Gen.ArrayOf(10, MensagemGenerators.ValidMensagem().Generator);
        return Prop.ForAll(
            Arb.From(msgArrayGen, _ => Enumerable.Empty<Mensagem[]>()),
            mensagens =>
            {
                var jsons = new string[mensagens.Length];
                Parallel.For(0, mensagens.Length, i => { jsons[i] = MensagemSerializer.Serializar(mensagens[i]); });

                for (var i = 0; i < mensagens.Length; i++)
                {
                    var d = MensagemSerializer.Deserializar(jsons[i]);
                    if (d.Tipo != mensagens[i].Tipo || d.SensorId != mensagens[i].SensorId)
                        return false;
                    if (MensagemSerializer.Serializar(d) != jsons[i])
                        return false;
                }

                return true;
            });
    }

    // ─── Property 4: Concurrent Deserialization Safety ─────────────────────────

    // Feature: shared-protocol-phase0, Property 4: Concurrent Deserialization Safety
    [Property(MaxTest = 100)]
    public Property DeserializacaoConcorrente_CadaThreadProduzMensagemCorreta()
    {
        var jsonArrayGen =
            from arr in Gen.ArrayOf(10, MensagemGenerators.ValidMensagem().Generator)
            select arr.Select(MensagemSerializer.Serializar).ToArray();

        return Prop.ForAll(
            Arb.From(jsonArrayGen, _ => Enumerable.Empty<string[]>()),
            jsons =>
            {
                var mensagens = new Mensagem[jsons.Length];
                Parallel.For(0, jsons.Length, i => { mensagens[i] = MensagemSerializer.Deserializar(jsons[i]); });

                for (var i = 0; i < jsons.Length; i++)
                {
                    if (MensagemSerializer.Serializar(mensagens[i]) != jsons[i])
                        return false;
                }

                return true;
            });
    }

    // ─── Property 9: Unknown Message Type Graceful Handling ───────────────────────

    // Feature: shared-protocol-phase0, Property 9: Unknown Message Type Graceful Handling
    [Property(MaxTest = 100)]
    public Property TipoDesconhecido_NaoCrash_LancaExcecaoControlada()
    {
        var unknownTipoGen = Gen.Choose(0, int.MaxValue).Select(i => "UNKNOWN_" + i);

        return Prop.ForAll(
            Arb.From(unknownTipoGen),
            tipoInvalido =>
            {
                var json =
                    "{\"tipo\":\"" + tipoInvalido +
                    "\",\"sensor_id\":\"SENSOR_001\",\"payload\":{},\"timestamp\":\"2024-01-15T10:30:00.0000000Z\"}";
                try
                {
                    MensagemSerializer.Deserializar(json);
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
                catch (FormatException)
                {
                    return true;
                }
            });
    }
}
