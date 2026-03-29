using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Xunit;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Property-based tests para validação da classe Mensagem.
/// Feature: shared-protocol-phase0
/// </summary>
public class MensagemValidacaoPropertyTests
{
    private static readonly string ValidTimestamp = DateTime.UtcNow.ToString("o");
    private static readonly string ValidSensorId = "SENSOR_001";
    private static readonly Dictionary<string, object> EmptyPayload = new();

    // ─── Generators ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates null or empty strings (null, "", whitespace-only strings).
    /// </summary>
    private static Arbitrary<string?> NullOrEmptyStringArb()
    {
        var gen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            // whitespace strings of length 1..5
            Gen.Choose(1, 5).SelectMany(n =>
                Gen.Constant<string?>(new string(' ', n)))
        );
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates strings that are NOT in TiposMensagem.Validos.
    /// Uses FsCheck's built-in string generator and filters out valid ones.
    /// </summary>
    private static Arbitrary<string> InvalidTipoArb()
    {
        // Generate non-empty strings that are not valid tipos
        var gen = Arb.Default.NonEmptyString().Generator
            .Select(s => s.Get)
            .Where(s => !string.IsNullOrWhiteSpace(s) && !TiposMensagem.Validos.Contains(s));
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates strings that are clearly not valid ISO 8601 timestamps.
    /// Uses alphanumeric strings that won't parse as dates.
    /// </summary>
    private static Arbitrary<string> InvalidTimestampArb()
    {
        // Generate strings that are not parseable as ISO 8601
        // We use alphanumeric strings and filter out anything that could be a valid date
        var gen = Arb.Default.NonEmptyString().Generator
            .Select(s => s.Get)
            .Where(s =>
                !string.IsNullOrWhiteSpace(s) &&
                !DateTime.TryParseExact(s,
                    new[]
                    {
                        "yyyy-MM-ddTHH:mm:ssK",
                        "yyyy-MM-ddTHH:mm:ss.fK",
                        "yyyy-MM-ddTHH:mm:ss.ffK",
                        "yyyy-MM-ddTHH:mm:ss.fffK",
                        "yyyy-MM-ddTHH:mm:ss.ffffK",
                        "yyyy-MM-ddTHH:mm:ss.fffffK",
                        "yyyy-MM-ddTHH:mm:ss.ffffffK",
                        "yyyy-MM-ddTHH:mm:ss.fffffffK",
                        "o", "O"
                    },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out _));
        return Arb.From(gen);
    }

    // ─── Property 5: Null or Empty Tipo Rejection ────────────────────────────────

    // Feature: shared-protocol-phase0, Property 5: Null or Empty Tipo Rejection
    [Property(MaxTest = 100)]
    public Property NullOrEmptyTipo_SempreRejeitadoComArgumentException()
    {
        return Prop.ForAll(
            NullOrEmptyStringArb(),
            tipoNuloOuVazio =>
            {
                try
                {
                    new Mensagem(tipoNuloOuVazio!, ValidSensorId, EmptyPayload, ValidTimestamp);
                    return false; // should have thrown
                }
                catch (ArgumentException)
                {
                    return true;
                }
            });
    }

    // ─── Property 6: Invalid Tipo Rejection ─────────────────────────────────────

    // Feature: shared-protocol-phase0, Property 6: Invalid Tipo Rejection
    [Property(MaxTest = 100)]
    public Property TipoInvalido_SempreRejeitadoComArgumentException()
    {
        return Prop.ForAll(
            InvalidTipoArb(),
            tipoInvalido =>
            {
                try
                {
                    new Mensagem(tipoInvalido, ValidSensorId, EmptyPayload, ValidTimestamp);
                    return false; // should have thrown
                }
                catch (ArgumentException)
                {
                    return true;
                }
            });
    }

    // ─── Property 7: Conditional SensorId Validation ────────────────────────────

    // Feature: shared-protocol-phase0, Property 7: Conditional SensorId Validation
    [Property(MaxTest = 100)]
    public Property SensorIdVazio_TiposQueRequerem_SempreLancaArgumentException()
    {
        // For DATA, HEARTBEAT, REGISTER: null/empty sensorId must throw
        var tiposQueRequerem = new[] { TiposMensagem.DATA, TiposMensagem.HEARTBEAT, TiposMensagem.REGISTER };
        var tipoGen = Gen.Elements(tiposQueRequerem);
        var sensorIdVazioGen = Gen.OneOf(
            Gen.Constant(""),
            Gen.Constant<string>(null!)
        );

        return Prop.ForAll(
            Arb.From(tipoGen),
            Arb.From(sensorIdVazioGen),
            (tipo, sensorIdVazio) =>
            {
                try
                {
                    new Mensagem(tipo, sensorIdVazio, EmptyPayload, ValidTimestamp);
                    return false; // should have thrown
                }
                catch (ArgumentException)
                {
                    return true;
                }
            });
    }

    // Feature: shared-protocol-phase0, Property 7: Conditional SensorId Validation
    [Property(MaxTest = 100)]
    public Property SensorIdVazio_TiposQueNaoRequerem_NuncaLancaExcecao()
    {
        // For REGISTER_OK, REGISTER_ERR, DATA_ACK, HEARTBEAT_ACK, ERROR: empty sensorId is fine
        var tiposQueNaoRequerem = TiposMensagem.Validos
            .Where(t => !TiposMensagem.RequerSensorId(t))
            .ToArray();
        var tipoGen = Gen.Elements(tiposQueNaoRequerem);

        return Prop.ForAll(
            Arb.From(tipoGen),
            tipo =>
            {
                try
                {
                    var msg = new Mensagem(tipo, "", EmptyPayload, ValidTimestamp);
                    return msg.Tipo == tipo;
                }
                catch (ArgumentException)
                {
                    return false; // should NOT have thrown
                }
            });
    }

    // ─── Property 8: Invalid Timestamp Rejection ─────────────────────────────────

    // Feature: shared-protocol-phase0, Property 8: Invalid Timestamp Rejection
    [Property(MaxTest = 100)]
    public Property TimestampInvalido_SempreRejeitadoComArgumentException()
    {
        return Prop.ForAll(
            InvalidTimestampArb(),
            timestampInvalido =>
            {
                try
                {
                    new Mensagem(TiposMensagem.DATA_ACK, "", EmptyPayload, timestampInvalido);
                    return false; // should have thrown
                }
                catch (ArgumentException)
                {
                    return true;
                }
            });
    }
}
