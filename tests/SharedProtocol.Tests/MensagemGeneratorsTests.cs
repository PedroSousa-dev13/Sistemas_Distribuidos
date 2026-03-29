using System.Collections.Generic;
using System.Linq;
using FsCheck;
using SharedProtocol;
using Xunit;

namespace SharedProtocol.Tests;

public class MensagemGeneratorsTests
{
    [Fact]
    public void ValidMensagem_TodasAsInstancias_TipoEstaEmValidos()
    {
        var arb = MensagemGenerators.ValidMensagem();
        for (var i = 0; i < 200; i++)
        {
            var m = arb.Generator.Sample(30, 1).First();
            Assert.Contains(m.Tipo, TiposMensagem.Validos);
        }
    }

    [Fact]
    public void ValidMensagem_RegraSensorId_Respeitada()
    {
        var arb = MensagemGenerators.ValidMensagem();
        for (var i = 0; i < 200; i++)
        {
            var m = arb.Generator.Sample(30, 1).First();
            if (TiposMensagem.RequerSensorId(m.Tipo))
                Assert.False(string.IsNullOrWhiteSpace(m.SensorId));
        }
    }

    [Fact]
    public void AllValidSamplesList_CobreTodosOsTiposDeMensagem()
    {
        var tipos = MensagemGenerators.AllValidSamplesList.Select(m => m.Tipo).ToHashSet();
        foreach (var tipo in TiposMensagem.Validos)
            Assert.Contains(tipo, tipos);
    }

    [Fact]
    public void ValidMensagem_AmostragemAleatoria_EventualmenteCobreTodosOsTipos()
    {
        var arb = MensagemGenerators.ValidMensagem();
        var vistos = new HashSet<string>();
        for (var i = 0; i < 10_000; i++)
            vistos.Add(arb.Generator.Sample(30, 1).First().Tipo);

        foreach (var tipo in TiposMensagem.Validos)
            Assert.Contains(tipo, vistos);
    }

    [Theory]
    [MemberData(nameof(TiposComPayloadEsperado))]
    public void ValidMensagem_PayloadContemChavesEsperadas(string tipo, string[] chavesEsperadas)
    {
        var amostras = MensagemGenerators.AllValidSamplesList.Where(m => m.Tipo == tipo).ToList();
        Assert.NotEmpty(amostras);
        foreach (var m in amostras)
            foreach (var k in chavesEsperadas)
                Assert.True(m.Payload.ContainsKey(k), $"Tipo {tipo} deve ter chave '{k}'");
    }

    public static IEnumerable<object[]> TiposComPayloadEsperado()
    {
        yield return new object[] { TiposMensagem.REGISTER, new[] { "tipos_dados" } };
        yield return new object[] { TiposMensagem.DATA, new[] { "tipo_dado", "valor" } };
        yield return new object[] { TiposMensagem.REGISTER_ERR, new[] { "error_code", "description" } };
        yield return new object[] { TiposMensagem.ERROR, new[] { "error_code", "description" } };
    }
}
