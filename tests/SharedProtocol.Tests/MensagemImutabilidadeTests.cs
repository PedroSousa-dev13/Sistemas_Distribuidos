using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SharedProtocol;
using Xunit;

namespace SharedProtocol.Tests;

public class MensagemImutabilidadeTests
{
    [Fact]
    public void MesmaInstancia_MultiplasThreadsLeitura_ConcluiSemErro()
    {
        var msg = Mensagem.CriarHeartbeat("SENSOR_001");
        Parallel.For(0, 500, _i =>
        {
            Assert.Equal(TiposMensagem.HEARTBEAT, msg.Tipo);
            Assert.Equal("SENSOR_001", msg.SensorId);
            Assert.False(string.IsNullOrEmpty(msg.Timestamp));
            Assert.NotNull(msg.Payload);
        });
    }

    [Fact]
    public void PropriedadesPublicas_UsamInit_Externo()
    {
        var props = typeof(Mensagem).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p is { Name: "Tipo" or "SensorId" or "Payload" or "Timestamp", CanWrite: true });

        foreach (var p in props)
        {
            var set = p.GetSetMethod(nonPublic: true);
            Assert.NotNull(set);
            var mods = set.ReturnParameter.GetRequiredCustomModifiers();
            Assert.Contains(typeof(IsExternalInit), mods);
        }
    }
}
