using Xunit;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Testes unitários para as classes de constantes do protocolo.
/// </summary>
public class ConstantesTests
{
    [Fact]
    public void TiposMensagem_DeveTerTodosOsOitoTiposDefinidos()
    {
        // Verifica que todos os 8 tipos de mensagem estão definidos
        Assert.Equal("REGISTER", TiposMensagem.REGISTER);
        Assert.Equal("REGISTER_OK", TiposMensagem.REGISTER_OK);
        Assert.Equal("REGISTER_ERR", TiposMensagem.REGISTER_ERR);
        Assert.Equal("DATA", TiposMensagem.DATA);
        Assert.Equal("DATA_ACK", TiposMensagem.DATA_ACK);
        Assert.Equal("HEARTBEAT", TiposMensagem.HEARTBEAT);
        Assert.Equal("HEARTBEAT_ACK", TiposMensagem.HEARTBEAT_ACK);
        Assert.Equal("ERROR", TiposMensagem.ERROR);
    }

    [Fact]
    public void TiposMensagem_Validos_DeveConterTodosOsTipos()
    {
        // Verifica que TiposMensagem.Validos contém todos os 8 tipos
        Assert.Equal(8, TiposMensagem.Validos.Count);
        Assert.Contains(TiposMensagem.REGISTER, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.REGISTER_OK, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.REGISTER_ERR, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.DATA, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.DATA_ACK, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.HEARTBEAT, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.HEARTBEAT_ACK, TiposMensagem.Validos);
        Assert.Contains(TiposMensagem.ERROR, TiposMensagem.Validos);
    }

    [Theory]
    [InlineData("REGISTER", true)]
    [InlineData("DATA", true)]
    [InlineData("HEARTBEAT", true)]
    [InlineData("REGISTER_OK", false)]
    [InlineData("REGISTER_ERR", false)]
    [InlineData("DATA_ACK", false)]
    [InlineData("HEARTBEAT_ACK", false)]
    [InlineData("ERROR", false)]
    public void TiposMensagem_RequerSensorId_DeveRetornarValorCorreto(string tipo, bool esperado)
    {
        // Verifica que RequerSensorId retorna true apenas para REGISTER, DATA e HEARTBEAT
        Assert.Equal(esperado, TiposMensagem.RequerSensorId(tipo));
    }

    [Fact]
    public void PortosProtocolo_DeveDefinirGatewayPort()
    {
        // Verifica que GATEWAY_PORT = 5000
        Assert.Equal(5000, PortosProtocolo.GATEWAY_PORT);
    }

    [Fact]
    public void PortosProtocolo_DeveDefinirServerPort()
    {
        // Verifica que SERVER_PORT = 6000
        Assert.Equal(6000, PortosProtocolo.SERVER_PORT);
    }

    [Fact]
    public void CodigosErro_DeveTerTodosOsCodigosDefinidos()
    {
        // Verifica que todos os códigos de erro estão definidos
        Assert.Equal("SENSOR_NOT_FOUND", CodigosErro.SENSOR_NOT_FOUND);
        Assert.Equal("SENSOR_INACTIVE", CodigosErro.SENSOR_INACTIVE);
        Assert.Equal("SERVER_UNAVAILABLE", CodigosErro.SERVER_UNAVAILABLE);
        Assert.Equal("INVALID_FORMAT", CodigosErro.INVALID_FORMAT);
        Assert.Equal("INVALID_DATA_TYPE", CodigosErro.INVALID_DATA_TYPE);
    }
}
