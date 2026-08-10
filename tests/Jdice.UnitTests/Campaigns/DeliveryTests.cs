using Jdice.Domain.Campaigns;

namespace Jdice.UnitTests.Campaigns;

/// <summary>
/// A partir daqui um erro não é um dado errado no banco: é um e-mail enviado
/// para uma pessoa real, que não tem desfazer. As transições de estado são a
/// única barreira contra isso, então merecem cobertura minuciosa.
/// </summary>
public class DeliveryTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private static Delivery Criar() =>
        Delivery.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "ana@empresa.com", Agora);

    [Fact]
    public void Entrega_nasce_pendente_sem_tentativas()
    {
        var delivery = Criar();

        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(0, delivery.Attempts);
    }

    [Fact]
    public void Primeiro_worker_assume_a_entrega()
    {
        var delivery = Criar();

        Assert.True(delivery.TryStart());
        Assert.Equal(DeliveryStatus.Sending, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
    }

    [Fact]
    public void Segundo_worker_nao_assume_a_mesma_entrega()
    {
        var delivery = Criar();
        delivery.TryStart();

        // Com vários workers e redelivery de fila, a mesma entrega chega duas
        // vezes. A segunda precisa desistir, ou a pessoa recebe duplicado.
        Assert.False(delivery.TryStart());
        Assert.Equal(1, delivery.Attempts);
    }

    [Fact]
    public void Entrega_ja_enviada_nao_e_reprocessada()
    {
        var delivery = Criar();
        delivery.TryStart();
        delivery.MarkSent(Agora);

        Assert.False(delivery.TryStart());
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
    }

    [Fact]
    public void Envio_bem_sucedido_limpa_erro_anterior()
    {
        var delivery = Criar();

        delivery.TryStart();
        delivery.MarkFailed("timeout no SMTP", Agora);
        delivery.TryStart();
        delivery.MarkSent(Agora);

        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        Assert.Equal(Agora, delivery.SentAt);
        Assert.Empty(delivery.Error);
    }

    [Fact]
    public void Falha_com_tentativa_sobrando_volta_para_pendente()
    {
        var delivery = Criar();

        delivery.TryStart();
        delivery.MarkFailed("conexão recusada", Agora);

        // Volta para a fila: uma indisponibilidade momentânea do SMTP não
        // deve custar a entrega.
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal("conexão recusada", delivery.Error);
    }

    [Fact]
    public void Falha_no_limite_de_tentativas_desiste_de_vez()
    {
        var delivery = Criar();

        for (var i = 0; i < Delivery.MaximumAttempts; i++)
        {
            Assert.True(delivery.TryStart());
            delivery.MarkFailed("endereço inexistente", Agora);
        }

        // Insistir eternamente num endereço que não existe só queima recurso
        // e prejudica a reputação do remetente.
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(Delivery.MaximumAttempts, delivery.Attempts);
        Assert.False(delivery.TryStart());
    }

    [Fact]
    public void Cada_falha_entra_no_historico_de_tentativas()
    {
        var delivery = Criar();

        delivery.TryStart();
        delivery.MarkFailed("timeout", Agora);
        delivery.TryStart();
        delivery.MarkFailed("recusa 550", Agora.AddMinutes(1));

        // O histórico mostra a sequência: 1ª foi timeout, 2ª foi recusa — mais
        // do que só o último erro.
        Assert.Equal(2, delivery.AttemptLog.Count);
        Assert.Equal(1, delivery.AttemptLog[0].Numero);
        Assert.Equal("timeout", delivery.AttemptLog[0].Erro);
        Assert.Equal(2, delivery.AttemptLog[1].Numero);
        Assert.Equal(Agora.AddMinutes(1), delivery.AttemptLog[1].Quando);
    }

    [Fact]
    public void Entrega_pulada_nao_e_processada()
    {
        var delivery = Criar();

        delivery.Skip("destinatário descadastrado");

        Assert.Equal(DeliveryStatus.Skipped, delivery.Status);
        Assert.False(delivery.TryStart());
    }

    [Fact]
    public void Erro_muito_longo_e_encurtado()
    {
        var delivery = Criar();
        delivery.TryStart();

        delivery.MarkFailed(new string('x', 900), Agora);

        // Relatório de SMTP pode vir enorme; a coluna guarda o essencial.
        Assert.Equal(500, delivery.Error.Length);
    }

    [Fact]
    public void Email_e_guardado_na_entrega_e_nao_apenas_referenciado()
    {
        var delivery = Criar();

        // Se a pessoa trocar de e-mail depois, o histórico precisa continuar
        // mostrando para onde a mensagem realmente foi.
        Assert.Equal("ana@empresa.com", delivery.Email);
    }
}
